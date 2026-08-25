import {
	combineRgb,
	type CompanionPresetAction,
	type CompanionPresetDefinitions,
	type CompanionPresetSection,
	type CompanionSimplePresetDefinition,
} from '@companion-module/base'
import type { ModuleSchema } from './main.js'
import type ModuleInstance from './main.js'

const WHITE = combineRgb(255, 255, 255)
const BLACK = combineRgb(0, 0, 0)
const RED = combineRgb(190, 0, 0)
const PINK = combineRgb(210, 0, 70)
const GREEN = combineRgb(0, 145, 65)

function action(
	actionId: keyof ModuleSchema['actions'],
	options: Record<string, never> | { volume: number } = {},
): CompanionPresetAction<ModuleSchema['actions']> {
	return { actionId, options } as CompanionPresetAction<ModuleSchema['actions']>
}

export function UpdatePresets(self: ModuleInstance): void {
	const structure: CompanionPresetSection<ModuleSchema>[] = [
		{
			id: 'transport',
			name: 'Transport',
			definitions: [
				{
					id: 'transport_controls',
					type: 'simple',
					name: 'Transport controls',
					presets: ['previous', 'play_pause', 'next'],
				},
			],
		},
		{
			id: 'volume',
			name: 'Volume',
			definitions: [
				{
					id: 'volume_controls',
					type: 'simple',
					name: 'YouTube Music volume controls',
					presets: ['volume_down', 'volume_up', 'mute', 'volume_encoder'],
				},
			],
		},
		{
			id: 'track',
			name: 'Track and Like',
			definitions: [
				{
					id: 'track_controls',
					type: 'simple',
					name: 'Track display and Like controls',
					presets: ['like', 'track_display'],
				},
			],
		},
	]

	const presets: CompanionPresetDefinitions<ModuleSchema> = {
		previous: buttonPreset('Previous', '⏮\nPrevious', action('previous')),
		play_pause: {
			...buttonPreset('Play/Pause', '⏯\nPlay/Pause', action('play_pause')),
			feedbacks: [
				{ feedbackId: 'playing', options: {}, style: { bgcolor: GREEN, color: WHITE, text: '⏸\nPause' } },
				{ feedbackId: 'paused', options: {}, style: { text: '▶\nPlay' } },
			],
		},
		next: buttonPreset('Next', '⏭\nNext', action('next')),
		volume_up: buttonPreset('Volume Up', '🔊\nVol +', action('volume_up')),
		volume_down: buttonPreset('Volume Down', '🔉\nVol −', action('volume_down')),
		mute: {
			...buttonPreset('Mute toggle', '🔇\nMute', action('toggle_mute')),
			feedbacks: [{ feedbackId: 'muted', options: {}, style: { bgcolor: RED, color: WHITE, text: '🔇\nMuted' } }],
		},
		like: {
			...buttonPreset('Like toggle', '♡\nLike', action('toggle_like')),
			feedbacks: [{ feedbackId: 'liked', options: {}, style: { bgcolor: PINK, color: WHITE, text: '♥\nLiked' } }],
		},
		track_display: {
			type: 'simple',
			name: 'D200X clock / now playing',
			keywords: ['D200X', 'wide', 'clock', 'track', 'artist', 'album art', 'Now Playing'],
			style: {
				text: '$(this:clock)',
				size: 'auto',
				color: WHITE,
				bgcolor: BLACK,
				alignment: 'center:center',
			},
			steps: [{ down: [], up: [] }],
			feedbacks: [{ feedbackId: 'now_playing_display', options: {} }],
		},
		volume_encoder: {
			type: 'simple',
			name: 'Volume encoder (rotate volume, press mute)',
			keywords: ['D200X', 'encoder', 'rotary'],
			style: { text: '🔊 $(this:volume)%\nPress: mute', size: 'auto', color: WHITE, bgcolor: BLACK },
			steps: [
				{
					down: [action('toggle_mute')],
					up: [],
					rotate_left: [action('volume_down')],
					rotate_right: [action('volume_up')],
				},
			],
			feedbacks: [{ feedbackId: 'muted', options: {}, style: { bgcolor: RED, text: '🔇 Muted\nPress: unmute' } }],
		},
	}
	self.setPresetDefinitions(structure, presets)
}

function buttonPreset(
	name: string,
	text: string,
	presetAction: ReturnType<typeof action>,
): CompanionSimplePresetDefinition<ModuleSchema> {
	return {
		type: 'simple' as const,
		name,
		style: { text, size: 'auto' as const, color: WHITE, bgcolor: BLACK },
		steps: [{ down: [presetAction], up: [] }],
		feedbacks: [],
	}
}
