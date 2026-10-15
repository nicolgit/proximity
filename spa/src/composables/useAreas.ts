import { getApiUrl } from '@/config/env';
import type { Area } from '@/types';
import { ref, toValue, type MaybeRefOrGetter } from 'vue';

export function useAreas(country: MaybeRefOrGetter<string>, selectedArea?: string) {
    const areas = ref<Area[]>([]);
    const isLoading = ref(false);
    const error = ref<string | null>(null);
    let latestRequest = 0;

    const load = async (overrideSelectedArea?: string): Promise<void> => {
        const request = ++latestRequest;
        isLoading.value = true;
        error.value = null;

        try {
            let url: string;
            const areaToLoad = overrideSelectedArea ?? selectedArea;
            if (areaToLoad) {
                url = getApiUrl(`/area/${toValue(country)}/${areaToLoad}`);
            } else {
                url = getApiUrl(`/area/${toValue(country)}`);
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
                if (request !== latestRequest) return;
                areas.value = [data];
            } else {
                const data: Area[] = await response.json();
                if (request !== latestRequest) return;
                areas.value = data;
            }
        } catch (err) {
            if (request === latestRequest) {
                error.value = err instanceof Error ? err.message : 'Failed to load areas';
            }
            console.error('Error loading areas:', err);
        } finally {
            if (request === latestRequest) isLoading.value = false;
        }
    };

    return {
        areas,
        isLoading,
        error,
        load
    };
}
