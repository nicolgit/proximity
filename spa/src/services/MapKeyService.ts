import { getApiUrl } from '@/config/env'

export interface MapKeyResponse {
    mapKey: string
}

export class MapKeyService {
    /**
     * Fetches the map key from the API
     */
    static async fetchMapKey(): Promise<string> {
        try {
            const url = getApiUrl('/map/key')
            const response = await fetch(url)
            
            if (!response.ok) {
                throw new Error(`Failed to fetch map key: ${response.status} ${response.statusText}`)
            }
            
            const data: MapKeyResponse = await response.json()
            
            if (!data.mapKey) {
                throw new Error('Map key not found in response')
            }
            
            return data.mapKey
        } catch (error) {
            console.error('Error fetching map key:', error)
            throw error
        }
    }
}