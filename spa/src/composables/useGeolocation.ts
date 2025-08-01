import type { LatLng } from 'leaflet'
import { onUnmounted, ref } from 'vue'

export function useGeolocation() {
    const coordinates = ref<LatLng | null>(null)
    const isLoading = ref(false)
    const error = ref<string | null>(null)

    const getCurrentPosition = (): Promise<GeolocationPosition> => {
        return new Promise((resolve, reject) => {
            if (!navigator.geolocation) {
                reject(new Error('Geolocation is not supported by this browser.'))
                return
            }

            navigator.geolocation.getCurrentPosition(
                (position) => resolve(position),
                (err) => reject(err),
                {
                    enableHighAccuracy: true,
                    timeout: 10000,
                    maximumAge: 60000 // 1 minute
                }
            )
        })
    }

    const getLocation = async () => {
        isLoading.value = true
        error.value = null

        try {
            const position = await getCurrentPosition()
            coordinates.value = [position.coords.latitude, position.coords.longitude] as unknown as LatLng
        } catch (err) {
            error.value = err instanceof Error ? err.message : 'Failed to get location'
            console.error('Geolocation error:', err)
        } finally {
            isLoading.value = false
        }
    }

    // Watch position for real-time updates
    let watchId: number | null = null

    const startWatching = () => {
        if (!navigator.geolocation) {
            error.value = 'Geolocation is not supported by this browser.'
            return
        }

        watchId = navigator.geolocation.watchPosition(
            (position) => {
                coordinates.value = [position.coords.latitude, position.coords.longitude] as unknown as LatLng
                error.value = null
            },
            (err) => {
                error.value = err.message
                console.error('Geolocation watch error:', err)
            },
            {
                enableHighAccuracy: true,
                timeout: 10000,
                maximumAge: 60000
            }
        )
    }

    const stopWatching = () => {
        if (watchId !== null) {
            navigator.geolocation.clearWatch(watchId)
            watchId = null
        }
    }

    // Cleanup on unmount
    onUnmounted(() => {
        stopWatching()
    })

    return {
        coordinates,
        isLoading,
        error,
        getLocation,
        startWatching,
        stopWatching
    }
}
