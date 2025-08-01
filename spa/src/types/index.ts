// Types for the application

export interface Location {
    lat: number
    lon: number
    name?: string
}

export interface SearchResult {
    place_id: string
    display_name: string
    lat: string
    lon: string
    type?: string
    importance?: number
}

export interface MapConfig {
    defaultZoom: number
    maxZoom: number
    minZoom: number
    defaultCenter: [number, number]
}

export interface NominatimSearchParams {
    q: string
    format: 'json' | 'xml'
    limit?: number
    addressdetails?: 0 | 1
    countrycodes?: string
    bounded?: 0 | 1
    viewbox?: string
}

export interface ApiResponse<T> {
    success: boolean
    data?: T
    error?: string
}
