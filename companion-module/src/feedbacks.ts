import { combineRgb } from '@companion-module/base'
import { ArtworkCache, marqueeDisplayText } from './artwork.js'
import type ModuleInstance from './main.js'

type VolumeOperator = 'eq' | 'gt' | 'gte' | 'lt' | 'lte' | 'between'

export type FeedbacksSchema = {
	playing: { type: 'boolean'; options: Record<string, never> }
	paused: { type: 'boolean'; options: Record<string, never> }
	muted: { type: 'boolean'; options: Record<string, never> }
	liked: { type: 'boolean'; options: Record<string, never> }
	volume: { type: 'boolean'; options: { operator: VolumeOperator; value: number; value2: number } }
	now_playing_display: { type: 'advanced'; options: Record<string, never> }
}

export function UpdateFeedbacks(self: ModuleInstance): void {
	const activeStyle = { bgcolor: combineRgb(0, 170, 70), color: combineRgb(255, 255, 255) }
	const artwork = new ArtworkCache((message) => self.log('debug', message))
	self.setFeedbackDefinitions({
		playing: {
			name: 'Playback is active',
			type: 'boolean',
			defaultStyle: activeStyle,
			options: [],
			callback: () => self.state.isPlaying,
		},
		paused: {
			name: 'Playback is paused',
			type: 'boolean',
			defaultStyle: { bgcolor: combineRgb(230, 145, 0), color: combineRgb(0, 0, 0) },
			options: [],
			callback: () => self.state.playerState === 'paused',
		},
		muted: {
			name: 'YouTube Music is muted',
			type: 'boolean',
			defaultStyle: { bgcolor: combineRgb(190, 0, 0), color: combineRgb(255, 255, 255) },
			options: [],
			callback: () => self.state.isMuted,
		},
		liked: {
			name: 'Current track is liked',
			type: 'boolean',
			defaultStyle: { bgcolor: combineRgb(210, 0, 70), color: combineRgb(255, 255, 255) },
			options: [],
			callback: () => self.state.isLiked === true,
		},
		volume: {
			name: 'YouTube Music volume matches',
			type: 'boolean',
			defaultStyle: activeStyle,
			options: [
				{
					type: 'dropdown',
					id: 'operator',
					label: 'Condition',
					default: 'eq',
					choices: [
						{ id: 'eq', label: 'Equals' },
						{ id: 'gt', label: 'Greater than' },
						{ id: 'gte', label: 'Greater than or equal' },
						{ id: 'lt', label: 'Less than' },
						{ id: 'lte', label: 'Less than or equal' },
						{ id: 'between', label: 'Between (inclusive)' },
					],
				},
				{ type: 'number', id: 'value', label: 'Volume / minimum', default: 0, min: 0, max: 100, clampValues: true },
				{
					type: 'number',
					id: 'value2',
					label: 'Maximum (for between)',
					default: 100,
					min: 0,
					max: 100,
					clampValues: true,
				},
			],
			callback: (event) =>
				matchesVolume(self.state.volume, event.options.operator, event.options.value, event.options.value2),
		},
		now_playing_display: {
			name: 'D200X clock / now playing display',
			description: 'Replaces the idle clock with album artwork and track information while music is playing',
			type: 'advanced',
			options: [],
			callback: async (event) => {
				if (!self.state.isPlaying) return {}

				const title = marqueeDisplayText(self.state.trackTitle || 'YouTube Music', 28, self.displayScrollOffset)
				const artist = marqueeDisplayText(self.state.trackArtist, 28, self.displayScrollOffset)
				const like = self.state.isLiked === true ? '♥' : self.state.isLiked === false ? '♡' : ''
				const text = [title, `${artist}${artist && like ? '  ' : ''}${like}`].filter(Boolean).join('\n')
				const result = {
					text,
					size: '18' as const,
					color: combineRgb(255, 255, 255),
					bgcolor: combineRgb(0, 0, 0),
					alignment: 'right:center' as const,
					pngalignment: 'left:center' as const,
					png64: undefined as string | undefined,
				}

				if (self.state.artworkUrl && event.image) {
					const artSize = Math.max(48, Math.min(event.image.height - 16, Math.floor(event.image.width * 0.38)))
					result.png64 = await artwork.get(self.state.artworkUrl, artSize)
				}

				return result
			},
		},
	})
}

export function matchesVolume(volume: number, operator: VolumeOperator, value: number, value2: number): boolean {
	switch (operator) {
		case 'eq':
			return volume === value
		case 'gt':
			return volume > value
		case 'gte':
			return volume >= value
		case 'lt':
			return volume < value
		case 'lte':
			return volume <= value
		case 'between':
			return volume >= Math.min(value, value2) && volume <= Math.max(value, value2)
	}
}
