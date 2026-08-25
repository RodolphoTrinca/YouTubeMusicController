import assert from 'node:assert/strict'
import test from 'node:test'
import { MalformedResponseError, parseStateResponse } from '../dist/api/protocol.js'
import { applyVolumeDelta, clampVolume, fractionToPercent, percentToFraction } from '../dist/api/volume.js'

const validState = {
	isPlaying: true,
	isMuted: false,
	volume: 67.4,
	isLiked: true,
	track: { title: 'Track', artist: 'Artist', album: 'Album', artworkUrl: 'https://example.test/art.jpg' },
}

test('volume helpers clamp, round, and convert predictably', () => {
	assert.equal(clampVolume(-5), 0)
	assert.equal(clampVolume(101), 100)
	assert.equal(clampVolume(49.6), 50)
	assert.equal(fractionToPercent(0.425), 43)
	assert.equal(percentToFraction(25), 0.25)
	assert.equal(applyVolumeDelta(98, 5), 100)
	assert.equal(applyVolumeDelta(2, -5), 0)
})

test('state parsing normalizes playback, metadata, volume, mute, and like', () => {
	const state = parseStateResponse(validState)
	assert.equal(state.playerState, 'playing')
	assert.equal(state.isPlaying, true)
	assert.equal(state.trackTitle, 'Track')
	assert.equal(state.trackArtist, 'Artist')
	assert.equal(state.trackAlbum, 'Album')
	assert.equal(state.volume, 67)
	assert.equal(state.isMuted, false)
	assert.equal(state.isLiked, true)
})

test('state parsing supports no active track and unknown like state', () => {
	const state = parseStateResponse({ isPlaying: false, isMuted: false, volume: 0, isLiked: null, track: null })
	assert.equal(state.playerState, 'unknown')
	assert.equal(state.trackTitle, '')
	assert.equal(state.isLiked, null)
})

test('a stopped loaded track is paused', () => {
	const state = parseStateResponse({ ...validState, isPlaying: false })
	assert.equal(state.playerState, 'paused')
})

test('malformed state is rejected without crashing callers', () => {
	assert.throws(
		() => parseStateResponse({ isPlaying: true, isMuted: false, volume: 'loud', isLiked: false, track: null }),
		MalformedResponseError,
	)
	assert.throws(() => parseStateResponse(null), MalformedResponseError)
	assert.throws(() => parseStateResponse({ ...validState, isLiked: 'yes' }), MalformedResponseError)
})
