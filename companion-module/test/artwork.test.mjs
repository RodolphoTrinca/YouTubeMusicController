import assert from 'node:assert/strict'
import test from 'node:test'
import { isAllowedArtworkUrl, marqueeDisplayText, truncateDisplayText } from '../dist/artwork.js'

test('artwork URL validation permits only HTTPS YouTube image hosts', () => {
	assert.equal(isAllowedArtworkUrl('https://lh3.googleusercontent.com/example'), true)
	assert.equal(isAllowedArtworkUrl('https://i.ytimg.com/vi/example/default.jpg'), true)
	assert.equal(isAllowedArtworkUrl('http://i.ytimg.com/vi/example/default.jpg'), false)
	assert.equal(isAllowedArtworkUrl('https://ytimg.com.example.test/image.jpg'), false)
	assert.equal(isAllowedArtworkUrl('file:///c:/secret.txt'), false)
})

test('wide display text is trimmed and truncated safely', () => {
	assert.equal(truncateDisplayText('  Track title  ', 20), 'Track title')
	assert.equal(truncateDisplayText('A very long track title', 12), 'A very long…')
})

test('wide display marquee preserves short text and scrolls long text', () => {
	assert.equal(marqueeDisplayText('  Artist  ', 10, 5), 'Artist')
	assert.equal(marqueeDisplayText('Long track title', 8, 0), 'Long tra')
	assert.equal(marqueeDisplayText('Long track title', 8, 5), 'track ti')
	assert.equal(marqueeDisplayText('Long track title', 8, 17), '  •   Lo')
})
