import { LocationSearchService } from '@/services/LocationSearchService'
import type { SearchResult } from '@/types'
import { ref, type Ref, watch } from 'vue'

export function useLocationSearch() {
    const searchQuery = ref('')
    const searchResults: Ref<SearchResult[]> = ref([])
    const isSearching = ref(false)
    const searchError = ref<string | null>(null)
    const hasTyped = ref(false)

    let searchTimeout: ReturnType<typeof setTimeout> | null = null
    let currentSearchController: AbortController | null = null

    const performSearch = async (query?: string) => {
        const searchTerm = query || searchQuery.value

        // Cancel previous search if still running
        if (currentSearchController) {
            currentSearchController.abort()
        }

        if (searchTerm.length < 3) {
            searchResults.value = []
            isSearching.value = false
            hasTyped.value = searchTerm.length > 0
            return
        }

        // Create new abort controller for this search
        currentSearchController = new AbortController()

        isSearching.value = true
        searchError.value = null
        hasTyped.value = true

        try {
            const results = await LocationSearchService.search(searchTerm, {}, currentSearchController.signal)

            // Only update results if this search wasn't cancelled
            if (!currentSearchController.signal.aborted) {
                searchResults.value = results
            }
        } catch (error) {
            if (!currentSearchController.signal.aborted) {
                searchError.value = error instanceof Error ? error.message : 'Search failed'
                searchResults.value = []
            }
        } finally {
            if (!currentSearchController.signal.aborted) {
                isSearching.value = false
            }
        }
    }

    const debouncedSearch = (delay = 300) => {
        // Clear previous timeout
        if (searchTimeout) {
            clearTimeout(searchTimeout)
        }

        // Cancel previous search
        if (currentSearchController) {
            currentSearchController.abort()
        }

        const query = searchQuery.value.trim()

        if (query.length < 3) {
            searchResults.value = []
            isSearching.value = false
            hasTyped.value = query.length > 0
            return
        }

        // Show loading state immediately for better UX
        isSearching.value = true
        hasTyped.value = true

        searchTimeout = setTimeout(() => {
            performSearch(query)
        }, delay)
    }

    // Watch for immediate changes to provide instant feedback
    watch(searchQuery, (newQuery) => {
        const trimmedQuery = newQuery.trim()

        if (trimmedQuery.length === 0) {
            searchResults.value = []
            isSearching.value = false
            hasTyped.value = false
            searchError.value = null

            if (searchTimeout) {
                clearTimeout(searchTimeout)
            }
            if (currentSearchController) {
                currentSearchController.abort()
            }
        } else if (trimmedQuery.length < 3) {
            searchResults.value = []
            isSearching.value = false
            hasTyped.value = true
            searchError.value = null
        }
    })

    const clearSearch = () => {
        searchQuery.value = ''
        searchResults.value = []
        searchError.value = null
        hasTyped.value = false

        if (searchTimeout) {
            clearTimeout(searchTimeout)
        }

        if (currentSearchController) {
            currentSearchController.abort()
        }
    }

    return {
        searchQuery,
        searchResults,
        isSearching,
        searchError,
        hasTyped,
        performSearch,
        debouncedSearch,
        clearSearch
    }
}
