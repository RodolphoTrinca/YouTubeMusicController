export function clampVolume(value: number): number {
	if (!Number.isFinite(value)) return 0
	return Math.min(100, Math.max(0, Math.round(value)))
}

export function fractionToPercent(value: number): number {
	return clampVolume(value * 100)
}

export function percentToFraction(value: number): number {
	return clampVolume(value) / 100
}

export function applyVolumeDelta(current: number, delta: number): number {
	return clampVolume(current + delta)
}
