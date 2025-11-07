import { getApiUrl } from '@/config/env'
import type { Station } from '@/types'
import { ref } from 'vue'

export function useStations() {
    const stations = ref<Record<string, Station[]>>({})
    const isLoading = ref<Record<string, boolean>>({})
    const error = ref<Record<string, string | null>>({})

    const loadStations = async (country: string, areaId: string): Promise<void> => {
        isLoading.value[areaId] = true
        error.value[areaId] = null

        try {
            const url = getApiUrl(`/area/${country}/${areaId}/station`)
            const response = await fetch(url, {
                method: 'GET',
                headers: {
                    'Content-Type': 'application/json',
                },
            })

            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`)
            }

            const data: Station[] = await response.json()
            stations.value[areaId] = data
        } catch (err) {
            error.value[areaId] = err instanceof Error ? err.message : 'Failed to load stations'
            console.error(`Error loading stations for area ${areaId}:`, err)
        } finally {
            isLoading.value[areaId] = false
        }
    }

    const getStationsForArea = (areaId: string): Station[] => {
        return stations.value[areaId] || []
    }

    const isLoadingForArea = (areaId: string): boolean => {
        return isLoading.value[areaId] || false
    }

    const getErrorForArea = (areaId: string): string | null => {
        return error.value[areaId] || null
    }

    return {
        stations,
        isLoading,
        error,
        loadStations,
        getStationsForArea,
        isLoadingForArea,
        getErrorForArea
    }
}
