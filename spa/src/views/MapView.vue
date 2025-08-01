<template>
  <div class="map-view">
    <!-- Search Container -->
    <div class="search-container">
      <div class="search-input-wrapper">
        <input
          v-model="searchQuery"
          @input="() => debouncedSearch(250)"
          @keydown="onSearchKeydown"
          @focus="onSearchFocus"
          @blur="onSearchBlur"
          type="text"
          class="search-input"
          :class="{ 
            'search-input--active': isSearchFocused,
            'search-input--loading': isSearching,
            'search-input--has-results': searchResults.length > 0
          }"
          placeholder="Cerca una località..."
          autocomplete="off"
        />
        
        <!-- Search icon/loading spinner -->
        <div class="search-icon">
          <div v-if="isSearching" class="loading-spinner"></div>
          <svg v-else-if="!hasTyped" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <circle cx="11" cy="11" r="8"></circle>
            <path d="m21 21-4.35-4.35"></path>
          </svg>
          <button 
            v-else-if="searchQuery.length > 0"
            @click="clearSearch"
            class="clear-button"
            type="button"
          >
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <line x1="18" y1="6" x2="6" y2="18"></line>
              <line x1="6" y1="6" x2="18" y2="18"></line>
            </svg>
          </button>
        </div>
      </div>
      
      <!-- Search status messages -->
      <div v-if="hasTyped && searchQuery.length > 0 && searchQuery.length < 3 && !isSearching" class="search-hint">
        Digita almeno 3 caratteri per cercare
      </div>
      
      <!-- Search results -->
      <div v-if="searchResults.length > 0" class="search-results">
        <div
          v-for="(result, index) in searchResults"
          :key="result.place_id"
          @click="selectLocation(result)"
          @mouseenter="highlightResult(index)"
          class="search-result-item"
          :class="{ 'search-result-item--highlighted': highlightedIndex === index }"
        >
          <div class="result-name">{{ result.display_name }}</div>
        </div>
      </div>

      <!-- No results message -->
      <div v-if="hasTyped && searchQuery.length >= 3 && !isSearching && searchResults.length === 0 && !searchError" class="no-results">
        Nessun risultato trovato per "{{ searchQuery }}"
      </div>

      <!-- Search error -->
      <div v-if="searchError" class="search-error">
        {{ searchError }}
      </div>
    </div>

    <!-- Location button -->
    <button 
      @click="goToCurrentLocation"
      class="location-btn"
      :class="{ 'location-btn--active': currentLocation }"
      :disabled="isLocationLoading"
      title="Vai alla tua posizione"
    >
      <svg v-if="isLocationLoading" width="20" height="20" viewBox="0 0 24 24" class="loading-icon">
        <circle cx="12" cy="12" r="10" stroke="currentColor" stroke-width="2" fill="none" opacity="0.3"/>
        <path d="M12 2 A10 10 0 0 1 22 12" stroke="currentColor" stroke-width="2" fill="none">
          <animateTransform attributeName="transform" type="rotate" values="0 12 12;360 12 12" dur="1s" repeatCount="indefinite"/>
        </path>
      </svg>
      <svg v-else width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
        <path d="M12 2L12 6M12 18L12 22M22 12L18 12M6 12L2 12"/>
        <circle cx="12" cy="12" r="3"/>
      </svg>
    </button>

    <!-- Map Container -->
    <div class="map-container">
      <l-map
        ref="mapRef"
        v-model:zoom="zoom"
        :center="center"
        :use-global-leaflet="false"
        class="leaflet-map"
        @ready="onMapReady"
      >
        <l-tile-layer
          :url="tileLayerUrl"
          :attribution="attribution"
        />
        
        <!-- Marker for selected location -->
        <l-marker
          v-if="selectedLocation"
          :lat-lng="selectedLocation"
          :icon="searchLocationIconSvg"
        >
          <l-popup>
            <div>{{ selectedLocationName }}</div>
          </l-popup>
        </l-marker>

        <!-- Marker for current location -->
        <l-marker
          v-if="currentLocation"
          :lat-lng="currentLocation"
          :icon="userLocationIconSvg"
        >
          <l-popup>
            <div class="user-location-popup">
              <strong>🟢 La tua posizione</strong>
            </div>
          </l-popup>
        </l-marker>
      </l-map>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { LMap, LTileLayer, LMarker, LPopup } from '@vue-leaflet/vue-leaflet'
import type { LatLng } from 'leaflet'
import { useLocationSearch } from '@/composables/useLocationSearch'
import { useGeolocation } from '@/composables/useGeolocation'
import type { SearchResult } from '@/types'
import { userLocationIconSvg, searchLocationIconSvg } from '@/utils/mapIcons'

// Map setup
const mapRef = ref<InstanceType<typeof LMap> | null>(null)
const zoom = ref(13)
const center = ref<LatLng>([41.9028, 12.4964]) // Default to Rome
const selectedLocation = ref<LatLng | null>(null)
const selectedLocationName = ref('')

// Map configuration
const tileLayerUrl = 'https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png'
const attribution = '© <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'

// Use composables
const {
  searchQuery,
  searchResults,
  isSearching,
  searchError,
  hasTyped,
  performSearch,
  debouncedSearch,
  clearSearch
} = useLocationSearch()

const {
  coordinates: currentLocation,
  isLoading: isLocationLoading,
  error: locationError,
  getLocation
} = useGeolocation()

// Search UI state
const isSearchFocused = ref(false)
const highlightedIndex = ref(-1)

// Search event handlers
const onSearchFocus = () => {
  isSearchFocused.value = true
}

const onSearchBlur = () => {
  // Delay hiding to allow click on results
  setTimeout(() => {
    isSearchFocused.value = false
    highlightedIndex.value = -1
  }, 150)
}

const highlightResult = (index: number) => {
  highlightedIndex.value = index
}

// Keyboard navigation
const onSearchKeydown = (event: KeyboardEvent) => {
  if (searchResults.value.length === 0) return

  switch (event.key) {
    case 'ArrowDown':
      event.preventDefault()
      highlightedIndex.value = Math.min(
        highlightedIndex.value + 1,
        searchResults.value.length - 1
      )
      break
    
    case 'ArrowUp':
      event.preventDefault()
      highlightedIndex.value = Math.max(highlightedIndex.value - 1, -1)
      break
    
    case 'Enter':
      event.preventDefault()
      if (highlightedIndex.value >= 0 && highlightedIndex.value < searchResults.value.length) {
        selectLocation(searchResults.value[highlightedIndex.value])
      } else if (searchResults.value.length > 0) {
        selectLocation(searchResults.value[0])
      } else {
        performSearch()
      }
      break
    
    case 'Escape':
      clearSearch()
      event.target?.blur()
      break
  }
}

// Map event handlers
const onMapReady = () => {
  console.log('Map is ready!')
}

// Location selection
const selectLocation = (result: SearchResult) => {
  const lat = parseFloat(result.lat)
  const lon = parseFloat(result.lon)
  
  selectedLocation.value = [lat, lon] as LatLng
  selectedLocationName.value = result.display_name
  center.value = [lat, lon] as LatLng
  zoom.value = 15
  
  // Reset search state
  clearSearch()
  highlightedIndex.value = -1
  isSearchFocused.value = false
}

// Current location functionality
const goToCurrentLocation = async () => {
  await getLocation()
  
  if (currentLocation.value) {
    center.value = currentLocation.value
    zoom.value = 16
  }
}

// Initialize on mount
onMounted(async () => {
  // Try to get user's current location
  await getLocation()
  
  if (currentLocation.value) {
    center.value = currentLocation.value
  }
})
</script>

<style scoped>
.map-view {
  height: 100vh;
  width: 100vw;
  position: relative;
}

.search-container {
  position: absolute;
  top: 20px;
  left: 50%;
  transform: translateX(-50%);
  z-index: 1000;
  background: white;
  border-radius: 8px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  padding: 8px;
  min-width: 300px;
  max-width: 90vw;
  transition: box-shadow 0.2s ease;
}

.search-container:focus-within {
  box-shadow: 0 6px 20px rgba(0, 0, 0, 0.2);
}

.search-input-wrapper {
  position: relative;
  display: flex;
  align-items: center;
}

.search-input {
  width: 100%;
  padding: 12px 40px 12px 16px;
  border: 1px solid #ddd;
  border-radius: 6px;
  font-size: 16px;
  outline: none;
  transition: all 0.2s ease;
  background: white;
}

.search-input:focus {
  border-color: #007bff;
  box-shadow: 0 0 0 2px rgba(0, 123, 255, 0.25);
}

.search-input--active {
  border-color: #007bff;
}

.search-input--loading {
  padding-right: 45px;
}

.search-input--has-results {
  border-bottom-left-radius: 2px;
  border-bottom-right-radius: 2px;
}

.search-icon {
  position: absolute;
  right: 12px;
  top: 50%;
  transform: translateY(-50%);
  display: flex;
  align-items: center;
  justify-content: center;
  color: #666;
}

.loading-spinner {
  width: 16px;
  height: 16px;
  border: 2px solid #f3f3f3;
  border-top: 2px solid #007bff;
  border-radius: 50%;
  animation: spin 1s linear infinite;
}

@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}

.clear-button {
  background: none;
  border: none;
  cursor: pointer;
  padding: 2px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #666;
  transition: all 0.2s ease;
}

.clear-button:hover {
  background-color: #f0f0f0;
  color: #333;
}

.search-hint {
  padding: 8px 12px;
  color: #666;
  font-size: 13px;
  font-style: italic;
  border-top: 1px solid #f0f0f0;
  margin-top: 8px;
}

.no-results {
  padding: 12px;
  color: #666;
  font-size: 14px;
  text-align: center;
  border-top: 1px solid #f0f0f0;
  margin-top: 8px;
}

.search-error {
  padding: 8px 12px;
  color: #dc3545;
  background-color: #f8d7da;
  border-radius: 4px;
  margin-top: 8px;
  font-size: 14px;
  border: 1px solid #f5c6cb;
}

.search-results {
  max-height: 200px;
  overflow-y: auto;
  border-top: 1px solid #eee;
  margin-top: 8px;
  border-radius: 0 0 6px 6px;
}

.search-result-item {
  padding: 12px;
  cursor: pointer;
  border-bottom: 1px solid #f5f5f5;
  transition: all 0.2s ease;
  position: relative;
}

.search-result-item:hover,
.search-result-item--highlighted {
  background-color: #f8f9fa;
  border-left: 3px solid #007bff;
}

.search-result-item:last-child {
  border-bottom: none;
  border-radius: 0 0 6px 6px;
}

.search-result-item:first-child {
  border-top: none;
}

.result-name {
  font-size: 14px;
  color: #333;
  line-height: 1.4;
}

.location-btn {
  position: absolute;
  top: 20px;
  right: 20px;
  z-index: 1000;
  background: white;
  border: 2px solid #e5e7eb;
  border-radius: 50%;
  width: 50px;
  height: 50px;
  cursor: pointer;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
  transition: all 0.2s ease;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #6b7280;
}

.location-btn:hover:not(:disabled) {
  background-color: #f8f9fa;
  border-color: #22C55E;
  color: #22C55E;
  transform: scale(1.05);
}

.location-btn--active {
  border-color: #22C55E;
  color: #22C55E;
  background-color: #f0fdf4;
}

.location-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.loading-icon {
  color: #22C55E;
}

.leaflet-map {
  height: 100%;
  width: 100%;
}
</style>
