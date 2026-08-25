const MAX_ARTWORK_BYTES = 5 * 1024 * 1024
const ARTWORK_TIMEOUT_MS = 5_000
const MAX_CACHE_ENTRIES = 8
const ALLOWED_ARTWORK_HOSTS = ['googleusercontent.com', 'ggpht.com', 'ytimg.com']

export function isAllowedArtworkUrl(value: string): boolean {
	try {
		const url = new URL(value)
		return (
			url.protocol === 'https:' &&
			ALLOWED_ARTWORK_HOSTS.some((host) => url.hostname === host || url.hostname.endsWith(`.${host}`))
		)
	} catch {
		return false
	}
}

export function truncateDisplayText(value: string, maximumLength: number): string {
	const text = value.trim()
	if (text.length <= maximumLength) return text
	return `${text.slice(0, Math.max(1, maximumLength - 1)).trimEnd()}…`
}

export function marqueeDisplayText(value: string, windowLength: number, offset: number): string {
	const text = value.trim()
	if (text.length <= windowLength) return text
	const loop = text + '   •   '
	const start = ((Math.floor(offset) % loop.length) + loop.length) % loop.length
	const repeated = loop.repeat(Math.ceil((start + windowLength) / loop.length) + 1)
	return repeated.slice(start, start + windowLength)
}

export class ArtworkCache {
	private readonly entries = new Map<string, Promise<string | undefined>>()

	constructor(private readonly onError: (message: string) => void) {}

	async get(url: string, size: number): Promise<string | undefined> {
		if (!isAllowedArtworkUrl(url)) return Promise.resolve(undefined)

		const cacheKey = `${url}|${size}`
		const existing = this.entries.get(cacheKey)
		if (existing) return existing

		const pending = this.load(url, size).catch((error: unknown) => {
			this.entries.delete(cacheKey)
			this.onError(error instanceof Error ? error.message : 'Unknown album artwork error')
			return undefined
		})
		this.entries.set(cacheKey, pending)
		this.trim()
		return pending
	}

	private async load(url: string, size: number): Promise<string> {
		const response = await fetch(url, { signal: AbortSignal.timeout(ARTWORK_TIMEOUT_MS) })
		if (!response.ok) throw new Error(`Album artwork request returned HTTP ${response.status}`)
		if (!response.headers.get('content-type')?.toLowerCase().startsWith('image/')) {
			throw new Error('Album artwork response was not an image')
		}

		const declaredLength = Number(response.headers.get('content-length') ?? 0)
		if (Number.isFinite(declaredLength) && declaredLength > MAX_ARTWORK_BYTES) {
			throw new Error('Album artwork response was too large')
		}

		const encoded = Buffer.from(await response.arrayBuffer())
		if (encoded.length > MAX_ARTWORK_BYTES) throw new Error('Album artwork response was too large')

		const { ImageTransformer } = await import('@julusian/image-rs')
		return ImageTransformer.fromEncodedImage(encoded)
			.scale(size * 4, size * 4, 'Fit')
			.toDataUrl('png')
	}

	private trim(): void {
		while (this.entries.size > MAX_CACHE_ENTRIES) {
			const oldestKey = this.entries.keys().next().value
			if (oldestKey === undefined) return
			this.entries.delete(oldestKey)
		}
	}
}
