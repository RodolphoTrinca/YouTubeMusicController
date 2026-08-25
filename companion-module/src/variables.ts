import type ModuleInstance from './main.js'

export type VariablesSchema = {
	player_state: string
	is_playing: boolean
	track_title: string
	track_artist: string
	track_album: string
	artwork_url: string
	volume: number
	is_muted: boolean
	is_liked: boolean | string
	like_symbol: string
	clock: string
}

export function UpdateVariableDefinitions(self: ModuleInstance): void {
	self.setVariableDefinitions({
		player_state: { name: 'Player state' },
		is_playing: { name: 'Is playing' },
		track_title: { name: 'Track title' },
		track_artist: { name: 'Track artist' },
		track_album: { name: 'Track album' },
		artwork_url: { name: 'Track artwork URL' },
		volume: { name: 'YouTube Music volume (0–100)' },
		is_muted: { name: 'Is muted' },
		is_liked: { name: 'Is liked (true, false, or unknown)' },
		like_symbol: { name: 'Like symbol (heart, outline, or unknown)' },
		clock: { name: 'Current local time (HH:MM:SS)' },
	})
}

export function formatClock(date: Date): string {
	return [date.getHours(), date.getMinutes(), date.getSeconds()]
		.map((value) => String(value).padStart(2, '0'))
		.join(':')
}
