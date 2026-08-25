import type ModuleInstance from './main.js'

export type ActionsSchema = {
	play: { options: Record<string, never> }
	pause: { options: Record<string, never> }
	play_pause: { options: Record<string, never> }
	next: { options: Record<string, never> }
	previous: { options: Record<string, never> }
	volume_up: { options: Record<string, never> }
	volume_down: { options: Record<string, never> }
	set_volume: { options: { volume: number } }
	mute: { options: Record<string, never> }
	unmute: { options: Record<string, never> }
	toggle_mute: { options: Record<string, never> }
	like: { options: Record<string, never> }
	unlike: { options: Record<string, never> }
	toggle_like: { options: Record<string, never> }
}

export function UpdateActions(self: ModuleInstance): void {
	self.setActionDefinitions({
		play: { name: 'Play', options: [], callback: async () => self.executeCommand('play') },
		pause: { name: 'Pause', options: [], callback: async () => self.executeCommand('pause') },
		play_pause: { name: 'Play/Pause toggle', options: [], callback: async () => self.executeCommand('playPause') },
		next: { name: 'Next track', options: [], callback: async () => self.executeCommand('next') },
		previous: { name: 'Previous track', options: [], callback: async () => self.executeCommand('previous') },
		volume_up: {
			name: 'Increase YouTube Music volume',
			options: [],
			callback: async () => self.adjustVolume(1),
		},
		volume_down: {
			name: 'Decrease YouTube Music volume',
			options: [],
			callback: async () => self.adjustVolume(-1),
		},
		set_volume: {
			name: 'Set YouTube Music volume',
			options: [
				{
					type: 'number',
					id: 'volume',
					label: 'Volume (%)',
					default: 50,
					min: 0,
					max: 100,
					step: 1,
					clampValues: true,
				},
			],
			callback: async (event) => self.setVolume(event.options.volume),
		},
		mute: { name: 'Mute YouTube Music', options: [], callback: async () => self.executeCommand('mute') },
		unmute: { name: 'Unmute YouTube Music', options: [], callback: async () => self.executeCommand('unmute') },
		toggle_mute: { name: 'Toggle YouTube Music mute', options: [], callback: async () => self.toggleMute() },
		like: { name: 'Like current track', options: [], callback: async () => self.executeCommand('like') },
		unlike: { name: 'Unlike current track', options: [], callback: async () => self.executeCommand('unlike') },
		toggle_like: {
			name: 'Toggle Like for current track',
			options: [],
			callback: async () => self.executeCommand('toggleLike'),
		},
	})
}
