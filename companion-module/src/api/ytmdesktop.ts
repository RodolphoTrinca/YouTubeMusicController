import { EventEmitter } from 'node:events'
import { Buffer } from 'node:buffer'
import type { ClientStatus, YtmCommand, YtmControllerState } from '../types.js'
import { clampVolume } from './volume.js'
import { MalformedResponseError, parseStateResponse } from './protocol.js'

const REQUEST_TIMEOUT_MS = 6_000

const COMMAND_PATHS: Record<YtmCommand, string> = {
	play: '/api/player/play',
	pause: '/api/player/pause',
	playPause: '/api/player/toggle',
	next: '/api/player/next',
	previous: '/api/player/previous',
	volumeUp: '/api/volume/up',
	volumeDown: '/api/volume/down',
	mute: '/api/volume/mute',
	unmute: '/api/volume/unmute',
	toggleMute: '/api/volume/toggle-mute',
	like: '/api/player/like',
	unlike: '/api/player/unlike',
	toggleLike: '/api/player/toggle-like',
}

export interface YtmDesktopClientOptions {
	host: string
	port: number
	token: string
	reconnectIntervalMs: number
}

export class YtmDesktopApiError extends Error {
	constructor(
		message: string,
		readonly status?: number,
	) {
		super(message)
		this.name = 'YtmDesktopApiError'
	}
}

export class YtmDesktopClient extends EventEmitter {
	private socket?: WebSocket
	private reconnectTimer?: NodeJS.Timeout
	private lastState?: YtmControllerState
	private destroyed = false
	private opening = false

	constructor(private readonly options: YtmDesktopClientOptions) {
		super()
	}

	get state(): YtmControllerState | undefined {
		return this.lastState
	}

	private get baseUrl(): string {
		const host = this.options.host.includes(':') ? `[${this.options.host}]` : this.options.host
		return `http://${host}:${this.options.port}`
	}

	connect(): void {
		this.destroyed = false
		void this.openConnection()
	}

	destroy(): void {
		this.destroyed = true
		this.opening = false
		if (this.reconnectTimer) clearTimeout(this.reconnectTimer)
		this.reconnectTimer = undefined
		if (this.socket) {
			this.socket.onopen = null
			this.socket.onmessage = null
			this.socket.onerror = null
			this.socket.onclose = null
			this.socket.close()
		}
		this.socket = undefined
		this.removeAllListeners()
	}

	async refreshState(): Promise<void> {
		const rawState = await this.request('/api/player/status', { method: 'GET' })
		this.acceptState(rawState)
	}

	async sendCommand(command: YtmCommand): Promise<void> {
		await this.request(COMMAND_PATHS[command], { method: 'POST' })
	}

	async setVolume(volume: number): Promise<void> {
		const target = clampVolume(volume)
		await this.request(`/api/volume/${target}`, { method: 'PUT' })
	}

	private async openConnection(): Promise<void> {
		if (this.destroyed || this.opening) return
		this.opening = true
		this.emitStatus('connecting')
		try {
			await this.refreshState()
			if (this.destroyed) return

			const websocketUrl = this.baseUrl.replace(/^http/, 'ws') + '/api/player/events'
			const encodedToken = Buffer.from(this.options.token, 'utf8').toString('base64url')
			const socket = new WebSocket(websocketUrl, `ytmusic-controller.auth.${encodedToken}`)
			this.socket = socket
			socket.onopen = () => {
				this.opening = false
				this.emitStatus('connected')
			}
			socket.onmessage = (event) => {
				void this.acceptWebSocketMessage(event.data)
			}
			socket.onerror = () => {
				if (!this.destroyed) this.emitStatus('connection_failure', 'WebSocket connection failed')
			}
			socket.onclose = () => {
				if (this.socket === socket) this.socket = undefined
				this.opening = false
				if (this.destroyed) return
				this.emitStatus('disconnected')
				this.scheduleReconnect()
			}
		} catch (error) {
			this.opening = false
			if (this.destroyed) return
			if (error instanceof YtmDesktopApiError && (error.status === 401 || error.status === 403)) {
				this.emitStatus('authentication_failure', 'YtMusicController rejected the bearer token')
			} else {
				this.emitStatus('connection_failure', safeErrorMessage(error))
			}
			this.emitError(error)
			this.scheduleReconnect()
		}
	}

	private scheduleReconnect(): void {
		if (this.destroyed || this.reconnectTimer) return
		this.reconnectTimer = setTimeout(() => {
			this.reconnectTimer = undefined
			void this.openConnection()
		}, this.options.reconnectIntervalMs)
	}

	private async acceptWebSocketMessage(raw: unknown): Promise<void> {
		try {
			let text: string
			if (typeof raw === 'string') text = raw
			else if (raw instanceof ArrayBuffer) text = new TextDecoder().decode(raw)
			else if (raw instanceof Blob) text = await raw.text()
			else throw new MalformedResponseError('WebSocket state frame is not text')
			this.acceptState(JSON.parse(text) as unknown)
		} catch (error) {
			this.emitError(error)
		}
	}

	private acceptState(rawState: unknown): void {
		try {
			const parsed = parseStateResponse(rawState)
			this.lastState = parsed
			this.emit('state', parsed)
		} catch (error) {
			this.emitError(error)
		}
	}

	private async request(path: string, init: RequestInit): Promise<unknown> {
		const headers = new Headers(init.headers)
		headers.set('Accept', 'application/json')
		headers.set('Authorization', `Bearer ${this.options.token}`)

		let response: Response
		try {
			response = await fetch(`${this.baseUrl}${path}`, {
				...init,
				headers,
				signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
			})
		} catch (error) {
			throw new YtmDesktopApiError(safeErrorMessage(error))
		}

		let payload: unknown
		const text = await response.text()
		if (text) {
			try {
				payload = JSON.parse(text) as unknown
			} catch {
				throw new MalformedResponseError(`YtMusicController returned non-JSON data (${response.status})`)
			}
		}

		if (!response.ok) {
			const detail =
				typeof payload === 'object' && payload !== null && 'detail' in payload
					? String(payload.detail)
					: `request failed (${response.status})`
			throw new YtmDesktopApiError(`YtMusicController ${detail}`, response.status)
		}
		return payload
	}

	private emitStatus(status: ClientStatus, message?: string): void {
		this.emit('status', status, message)
	}

	private emitError(error: unknown): void {
		this.emit('client-error', error)
	}
}

function safeErrorMessage(error: unknown): string {
	return error instanceof Error ? error.message : 'Unknown error'
}
