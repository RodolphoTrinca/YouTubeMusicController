import { Regex, type SomeCompanionConfigField } from '@companion-module/base'

export interface ModuleConfig {
	[key: string]: string | number | boolean
	host: string
	port: number
	reconnectIntervalMs: number
}

export interface ModuleSecrets {
	[key: string]: string
	token: string
}

export const DEFAULT_CONFIG: ModuleConfig = {
	host: '127.0.0.1',
	port: 38472,
	reconnectIntervalMs: 3000,
}

export function normalizeConfig(config: Partial<ModuleConfig>): ModuleConfig {
	const host = typeof config.host === 'string' && isValidHost(config.host) ? config.host.trim() : DEFAULT_CONFIG.host
	const port =
		Number.isInteger(config.port) && Number(config.port) >= 1 && Number(config.port) <= 65535
			? Number(config.port)
			: DEFAULT_CONFIG.port
	const reconnectIntervalMs = Number.isFinite(config.reconnectIntervalMs)
		? Math.min(30_000, Math.max(1_000, Math.round(Number(config.reconnectIntervalMs))))
		: DEFAULT_CONFIG.reconnectIntervalMs
	return { host, port, reconnectIntervalMs }
}

export function isValidHost(host: string): boolean {
	const value = host.trim()
	return (
		value.length > 0 &&
		value.length <= 253 &&
		!value.includes('://') &&
		!/[\s/?#]/.test(value) &&
		/^[a-z0-9.:[\]-]+$/i.test(value)
	)
}

export function GetConfigFields(): SomeCompanionConfigField[] {
	return [
		{
			type: 'static-text',
			id: 'setup',
			label: 'YouTube Music Controller setup',
			width: 12,
			value:
				'Start YtMusicController, then copy the <strong>Active API URL</strong> and <strong>Bearer token</strong> from its Settings window. The port can change automatically if the preferred port is occupied.',
		},
		{
			type: 'textinput',
			id: 'host',
			label: 'Host',
			width: 8,
			default: DEFAULT_CONFIG.host,
			regex: Regex.HOSTNAME,
			tooltip: 'Keep 127.0.0.1 when Companion and YtMusicController run on the same computer.',
		},
		{
			type: 'number',
			id: 'port',
			label: 'Active API port',
			width: 4,
			min: 1,
			max: 65535,
			default: DEFAULT_CONFIG.port,
		},
		{
			type: 'secret-text',
			id: 'token',
			label: 'Bearer token',
			width: 12,
			minLength: 32,
			tooltip: 'Copy this from YtMusicController Settings. It is stored as a Companion secret and is never logged.',
		},
		{
			type: 'number',
			id: 'reconnectIntervalMs',
			label: 'Reconnect interval (ms)',
			width: 6,
			min: 1000,
			max: 30000,
			default: DEFAULT_CONFIG.reconnectIntervalMs,
		},
		{
			type: 'static-text',
			id: 'volume-help',
			label: 'Volume behavior',
			width: 12,
			value:
				'Volume Up/Down follows the control method selected in YtMusicController Settings. Player API mode uses its configured step; Keyboard mode shows YouTube Music’s native volume overlay.',
		},
	]
}
