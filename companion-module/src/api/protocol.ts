import type { YtmControllerState } from '../types.js'
import { clampVolume } from './volume.js'

export class MalformedResponseError extends Error {
	constructor(message: string) {
		super(message)
		this.name = 'MalformedResponseError'
	}
}

function isRecord(value: unknown): value is Record<string, unknown> {
	return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function optionalString(record: Record<string, unknown>, key: string): string {
	const value = record[key]
	return typeof value === 'string' ? value.slice(0, 1000) : ''
}

export function parseStateResponse(input: unknown): YtmControllerState {
	if (!isRecord(input)) throw new MalformedResponseError('Player state is not an object')
	if (typeof input.isPlaying !== 'boolean') throw new MalformedResponseError('isPlaying is invalid')
	if (typeof input.isMuted !== 'boolean') throw new MalformedResponseError('isMuted is invalid')
	if (typeof input.volume !== 'number' || !Number.isFinite(input.volume)) {
		throw new MalformedResponseError('volume is invalid')
	}
	if (input.isLiked !== null && typeof input.isLiked !== 'boolean') {
		throw new MalformedResponseError('isLiked is invalid')
	}

	const track = input.track === null || input.track === undefined ? {} : input.track
	if (!isRecord(track)) throw new MalformedResponseError('track is invalid')
	const trackTitle = optionalString(track, 'title')
	const trackArtist = optionalString(track, 'artist')
	const hasTrack = trackTitle.length > 0 || trackArtist.length > 0

	return {
		playerState: input.isPlaying ? 'playing' : hasTrack ? 'paused' : 'unknown',
		isPlaying: input.isPlaying,
		trackTitle,
		trackArtist,
		trackAlbum: optionalString(track, 'album'),
		artworkUrl: optionalString(track, 'artworkUrl'),
		volume: clampVolume(input.volume),
		isMuted: input.isMuted,
		isLiked: input.isLiked,
	}
}
