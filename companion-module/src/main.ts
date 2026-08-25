import { InstanceBase, InstanceStatus, type SomeCompanionConfigField } from '@companion-module/base'
import { UpdateActions, type ActionsSchema } from './actions.js'
import { YtmDesktopApiError, YtmDesktopClient } from './api/ytmdesktop.js'
import { DEFAULT_CONFIG, GetConfigFields, normalizeConfig, type ModuleConfig, type ModuleSecrets } from './config.js'
import { UpdateFeedbacks, type FeedbacksSchema } from './feedbacks.js'
import { UpdatePresets } from './presets.js'
import { EMPTY_STATE, type ClientStatus, type YtmCommand, type YtmControllerState } from './types.js'
import { UpgradeScripts } from './upgrades.js'
import { formatClock, UpdateVariableDefinitions, type VariablesSchema } from './variables.js'

export type ModuleSchema = {
	config: ModuleConfig
	secrets: ModuleSecrets
	actions: ActionsSchema
	feedbacks: FeedbacksSchema
	variables: VariablesSchema
}

export { UpgradeScripts }

export default class ModuleInstance extends InstanceBase<ModuleSchema> {
	displayScrollOffset = 0
	config: ModuleConfig = { ...DEFAULT_CONFIG }
	secrets: ModuleSecrets = { token: '' }
	state: YtmControllerState = { ...EMPTY_STATE }
	private client?: YtmDesktopClient
	private lastStatus?: ClientStatus
	private displayScrollTimer?: NodeJS.Timeout
	private displayTrackKey = ''
	private lastClockValue = ''

	constructor(internal: unknown) {
		super(internal)
	}

	async init(config: ModuleConfig, _isFirstInit: boolean, secrets: ModuleSecrets): Promise<void> {
		this.config = normalizeConfig(config)
		this.secrets = { token: secrets?.token ?? '' }
		this.installDefinitions()
		this.publishState(this.state)
		this.startDisplayMarquee()
		this.startClient()
	}

	async destroy(): Promise<void> {
		if (this.displayScrollTimer) clearInterval(this.displayScrollTimer)
		this.client?.destroy()
		this.client = undefined
		this.log('debug', 'YouTube Music Controller module destroyed')
	}

	async configUpdated(config: ModuleConfig, secrets: ModuleSecrets): Promise<void> {
		this.config = normalizeConfig(config)
		this.secrets = { token: secrets?.token ?? '' }
		this.client?.destroy()
		this.client = undefined
		this.startClient()
	}

	getConfigFields(): SomeCompanionConfigField[] {
		return GetConfigFields()
	}

	async executeCommand(command: YtmCommand): Promise<void> {
		if (!this.client) return this.actionUnavailable()
		try {
			await this.client.sendCommand(command)
		} catch (error) {
			this.handleActionError(error)
		}
	}

	async adjustVolume(delta: number): Promise<void> {
		return this.executeCommand(delta >= 0 ? 'volumeUp' : 'volumeDown')
	}

	async setVolume(volume: number): Promise<void> {
		if (!this.client) return this.actionUnavailable()
		try {
			await this.client.setVolume(volume)
		} catch (error) {
			this.handleActionError(error)
		}
	}

	async toggleMute(): Promise<void> {
		return this.executeCommand('toggleMute')
	}

	private installDefinitions(): void {
		UpdateActions(this)
		UpdateFeedbacks(this)
		UpdateVariableDefinitions(this)
		UpdatePresets(this)
	}

	private startClient(): void {
		const token = this.secrets.token?.trim()
		if (!token) {
			this.updateStatus(InstanceStatus.BadConfig, 'Bearer token is required')
			return
		}
		this.client = new YtmDesktopClient({
			host: this.config.host,
			port: this.config.port,
			token,
			reconnectIntervalMs: this.config.reconnectIntervalMs,
		})
		this.client.on('status', (status: ClientStatus, message?: string) => this.handleClientStatus(status, message))
		this.client.on('state', (state: YtmControllerState) => this.publishState(state))
		this.client.on('client-error', (error: unknown) => {
			const message = safeErrorMessage(error)
			if (error instanceof YtmDesktopApiError && error.status === 401) {
				this.updateStatus(InstanceStatus.AuthenticationFailure, 'YtMusicController rejected the token')
			}
			this.log(
				error instanceof Error && error.name === 'MalformedResponseError' ? 'warn' : 'debug',
				`YouTube Music Controller API error: ${message}`,
			)
		})
		this.client.connect()
	}

	private handleClientStatus(status: ClientStatus, message?: string): void {
		const changed = status !== this.lastStatus
		this.lastStatus = status
		switch (status) {
			case 'connected':
				this.updateStatus(InstanceStatus.Ok)
				if (changed) this.log('info', 'Connected to YouTube Music Controller')
				break
			case 'connecting':
				this.updateStatus(InstanceStatus.Connecting, 'Connecting to YouTube Music Controller')
				if (changed) this.log('debug', 'Attempting to connect to YouTube Music Controller')
				break
			case 'authentication_failure':
				this.updateStatus(InstanceStatus.AuthenticationFailure, 'YouTube Music Controller rejected the token')
				if (changed) this.log('error', 'YouTube Music Controller authentication failed')
				break
			case 'disconnected':
				this.updateStatus(InstanceStatus.Disconnected, 'Connection lost; reconnecting')
				if (changed) this.log('warn', 'YouTube Music Controller connection lost')
				break
			case 'connection_failure':
				this.updateStatus(InstanceStatus.ConnectionFailure, 'Cannot reach controller; reconnecting')
				if (changed) this.log('warn', `Cannot connect to YouTube Music Controller${message ? `: ${message}` : ''}`)
				break
		}
	}

	private publishState(state: YtmControllerState): void {
		this.state = state
		const clock = formatClock(new Date())
		this.lastClockValue = clock
		const trackKey = state.trackTitle + '\0' + state.trackArtist
		if (trackKey !== this.displayTrackKey) {
			this.displayTrackKey = trackKey
			this.displayScrollOffset = 0
		}
		this.setVariableValues({
			player_state: state.playerState,
			is_playing: state.isPlaying,
			track_title: state.trackTitle,
			track_artist: state.trackArtist,
			track_album: state.trackAlbum,
			artwork_url: state.artworkUrl,
			volume: state.volume,
			is_muted: state.isMuted,
			is_liked: state.isLiked === null ? 'unknown' : state.isLiked,
			like_symbol: state.isLiked === true ? '♥' : state.isLiked === false ? '♡' : '·',
			clock,
		})
		this.checkFeedbacks('playing', 'paused', 'muted', 'liked', 'volume', 'now_playing_display')
	}

	private startDisplayMarquee(): void {
		if (this.displayScrollTimer) clearInterval(this.displayScrollTimer)
		this.displayScrollTimer = setInterval(() => {
			if (!this.state.isPlaying) {
				const clock = formatClock(new Date())
				if (clock !== this.lastClockValue) {
					this.lastClockValue = clock
					this.setVariableValues({ clock })
				}
			}
			if (!this.state.isPlaying || (this.state.trackTitle.length <= 28 && this.state.trackArtist.length <= 28)) return
			this.displayScrollOffset += 1
			this.checkFeedbacks('now_playing_display')
		}, 700)
	}

	private actionUnavailable(reason = 'the client is not configured'): void {
		this.log('warn', 'YouTube Music action ignored because ' + reason)
	}

	private handleActionError(error: unknown): void {
		const message = safeErrorMessage(error)
		if (error instanceof YtmDesktopApiError && error.status === 401) {
			this.updateStatus(InstanceStatus.AuthenticationFailure, 'YouTube Music Controller rejected the token')
		}
		this.log('warn', `YouTube Music action failed: ${message}`)
	}
}

function safeErrorMessage(error: unknown): string {
	return error instanceof Error ? error.message : 'Unknown error'
}
