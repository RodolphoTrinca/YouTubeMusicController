export type PlayerStateName = 'unknown' | 'paused' | 'playing'

export interface YtmControllerState {
	playerState: PlayerStateName
	isPlaying: boolean
	trackTitle: string
	trackArtist: string
	trackAlbum: string
	artworkUrl: string
	volume: number
	isMuted: boolean
	isLiked: boolean | null
}

export const EMPTY_STATE: YtmControllerState = {
	playerState: 'unknown',
	isPlaying: false,
	trackTitle: '',
	trackArtist: '',
	trackAlbum: '',
	artworkUrl: '',
	volume: 0,
	isMuted: false,
	isLiked: null,
}

export type YtmCommand =
	| 'play'
	| 'pause'
	| 'playPause'
	| 'next'
	| 'previous'
	| 'volumeUp'
	| 'volumeDown'
	| 'mute'
	| 'unmute'
	| 'toggleMute'
	| 'like'
	| 'unlike'
	| 'toggleLike'

export type ClientStatus = 'connecting' | 'connected' | 'disconnected' | 'connection_failure' | 'authentication_failure'
