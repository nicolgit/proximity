import type { NominatimSearchParams, SearchResult } from '@/types'

// Service for handling location search using OpenStreetMap Nominatim API
export class LocationSearchService {
    private static readonly BASE_URL = 'https://nominatim.openstreetmap.org'
    private static readonly DEFAULT_PARAMS: Partial<NominatimSearchParams> = {
        format: 'json',
        limit: 5,
        addressdetails: 1
    }

    static async search(
        query: string,
        params?: Partial<NominatimSearchParams>,
        signal?: AbortSignal
    ): Promise<SearchResult[]> {
        if (query.length < 3) {
            return []
        }

        const searchParams = {
            ...this.DEFAULT_PARAMS,
            ...params,
            q: query
        }

        const urlParams = new URLSearchParams()
        Object.entries(searchParams).forEach(([key, value]) => {
            if (value !== undefined && value !== null) {
                urlParams.append(key, value.toString())
            }
        })

        try {
            const response = await fetch(
                `${this.BASE_URL}/search?${urlParams.toString()}`,
                { signal }
            )

            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`)
            }

            const results: SearchResult[] = await response.json()
            return results
        } catch (error) {
            if (error instanceof Error && error.name === 'AbortError') {
                // Request was cancelled, don't log as error
                throw error
            }
            console.error('Error searching location:', error)
            throw error
        }
    }

    static async reverseGeocode(lat: number, lon: number): Promise<SearchResult | null> {
        const urlParams = new URLSearchParams({
            format: 'json',
            lat: lat.toString(),
            lon: lon.toString(),
            addressdetails: '1'
        })

        try {
            const response = await fetch(`${this.BASE_URL}/reverse?${urlParams.toString()}`)

            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`)
            }

            const result: SearchResult = await response.json()
            return result
        } catch (error) {
            console.error('Error in reverse geocoding:', error)
            return null
        }
    }
}
