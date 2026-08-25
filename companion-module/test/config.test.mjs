import assert from 'node:assert/strict'
import test from 'node:test'
import { isValidHost, normalizeConfig } from '../dist/config.js'
import { formatClock } from '../dist/variables.js'

test('configuration defaults favor the controller localhost port', () => {
	const config = normalizeConfig({})
	assert.equal(config.host, '127.0.0.1')
	assert.equal(config.port, 38472)
	assert.equal(config.reconnectIntervalMs, 3000)
})

test('configuration clamps reconnect interval and validates port', () => {
	assert.equal(normalizeConfig({ reconnectIntervalMs: 20 }).reconnectIntervalMs, 1000)
	assert.equal(normalizeConfig({ reconnectIntervalMs: 999999 }).reconnectIntervalMs, 30000)
	assert.equal(normalizeConfig({ port: 0 }).port, 38472)
	assert.equal(normalizeConfig({ port: 45678 }).port, 45678)
})

test('host validation rejects schemes, paths, and whitespace', () => {
	assert.equal(isValidHost('127.0.0.1'), true)
	assert.equal(isValidHost('music-host.local'), true)
	assert.equal(isValidHost('http://127.0.0.1'), false)
	assert.equal(isValidHost('host/path'), false)
	assert.equal(isValidHost('bad host'), false)
})

test('clock formatting is stable and zero-padded', () => {
	assert.equal(formatClock(new Date(2026, 0, 2, 3, 4, 5)), '03:04:05')
})
