import { getApiUrl } from '@/config/env';
import type { Area } from '@/types';
import { ref } from 'vue';

export function useAreas(country: string, selectedArea?: string) {
    const areas = ref<Area[]>([]);
    const isLoading = ref(false);
    const error = ref<string | null>(null);

    const load = async (overrideSelectedArea?: string): Promise<void> => {
        isLoading.value = true;
        error.value = null;

        try {
            let url: string;
            const areaToLoad = overrideSelectedArea ?? selectedArea;
            if (areaToLoad) {
                url = getApiUrl(`/area/${country}/${areaToLoad}`);
            } else {
                url = getApiUrl('/area');
            }
            const response = await fetch(url, {
                method: 'GET',
                headers: {
                    'Content-Type': 'application/json',
                },
            });

            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            if (areaToLoad) {
                const data: Area = await response.json();
                areas.value = [data];
            } else {
                const data: Area[] = await response.json();
                areas.value = data;
            }
        } catch (err) {
            error.value = err instanceof Error ? err.message : 'Failed to load areas';
            console.error('Error loading areas:', err);
        } finally {
            isLoading.value = false;
        }
    };

    return {
        areas,
        isLoading,
        error,
        load
    };
}
