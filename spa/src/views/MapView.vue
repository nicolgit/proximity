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

    <!-- Welcome Popup -->
    <div v-if="showWelcomePopup" class="welcome-popup-overlay" @click="closeWelcomePopup">
      <div class="welcome-popup" @click.stop>
        <div class="welcome-header">
          <h2>🗺️ Welcome to Metro Proximity!</h2>
          <button @click="closeWelcomePopup" class="welcome-close-btn" type="button">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <line x1="18" y1="6" x2="6" y2="18"></line>
              <line x1="6" y1="6" x2="18" y2="18"></line>
            </svg>
          </button>
        </div>
        <div class="welcome-content">
          <p>Welcome to metro-proximity!</p>
          <p>a tool that helps you to understand how far you are from a metro/train/tram stop!</p>
          <p v-if="isAreasLoading">Loading available areas... ⏳</p>
          <p v-else-if="areasError">Error loading areas: {{ areasError }}</p>
          <p v-else-if="areas.length > 0">
            Following areas are currently available: 
            <span v-for="(area, index) in areas" :key="area.id">
              📍{{ area.name }}<span v-if="index < areas.length - 1">, </span>
            </span>
          </p>
          <p v-else>No areas available at the moment</p>
          
          <div class="welcome-actions">
            <button @click="closeWelcomePopup()" class="welcome-btn welcome-btn--secondary">
              not used yet! 😅
            </button>
            <button @click="closeWelcomePopup" class="welcome-btn welcome-btn--primary">
              🗺️ start explore Map!
            </button>
          </div>
        </div>
      </div>
    </div>

    <!-- Location button -->
    <button 
      @click="goToCurrentLocation"
      class="location-btn"
      :class="{ 'location-btn--active': currentLocation }"
      :disabled="isLocationLoading"
      title="go to your current location"
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
        :center="initialCenter"
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
          :icon="searchLocationIconSvg as any"
        >
          <l-popup>
            <div>{{ selectedLocationName }}</div>
          </l-popup>
        </l-marker>

        <!-- Marker for current location -->
        <l-marker
          v-if="currentLocation"
          :lat-lng="currentLocation"
          :icon="userLocationIconSvg as any"
        >
          <l-popup>
            <div class="user-location-popup">
              <strong>🟢you're here🟢</strong>
            </div>
          </l-popup>
        </l-marker>

        <!-- Area circles -->
        <l-circle
          v-for="area in areas"
          :key="area.id"
          :lat-lng="[area.latitude, area.longitude]"
          :radius="area.diameter / 2"
          :color="'#3388ff'"
          :fill-opacity="0"
          :weight="2"
          :dash-array="'5, 5'"
        >
          <l-popup>
            <div class="area-popup">
              <h3>📍 {{ area.name }}</h3>
              <p><strong>Location:</strong> {{ area.latitude.toFixed(4) }}, {{ area.longitude.toFixed(4) }}</p>
              <p><strong>Coverage:</strong> {{ area.diameter }}m diameter</p>
              
              <!-- Toggle Metro Stations Button -->
              <div class="station-toggle-section">
                <button 
                  @click="toggleStationsForArea(area.id)"
                  class="station-toggle-btn"
                  :class="{ 'station-toggle-btn--active': visibleStations.has(area.id) }"
                  :disabled="isLoadingForArea(area.id)"
                >
                  <div v-if="isLoadingForArea(area.id)" class="loading-spinner-small"></div>
                  <span v-else>
                    {{ visibleStations.has(area.id) ? '🚇 Hide Metro Stations' : '🚇 Show Metro Stations' }}
                  </span>
                </button>
                
                <!-- Station count info -->
                <div v-if="visibleStations.has(area.id)" class="station-count">
                  {{ getStationsForArea(area.id).length }} stations found
                </div>
                
                <!-- Error message -->
                <div v-if="getErrorForArea(area.id)" class="station-error">
                  Error: {{ getErrorForArea(area.id) }}
                </div>
              </div>
            </div>
          </l-popup>
        </l-circle>

        <!-- Isochrone circles (rendered before stations to be under them) -->
        <l-circle
          v-for="(circle, index) in isochroneCircles"
          :key="`isochrone-${index}`"
          :lat-lng="circle.center"
          :radius="circle.radius"
          :color="circle.color"
          :fill-color="circle.color"
          :fill-opacity="0.1"
          :weight="circle.timeMinutes === 30 ? 2 : 0"
          :opacity="circle.timeMinutes === 30 ? 0.6 : 0"
        >
          <l-popup>
            <div class="isochrone-popup">
              <h4>🚶‍♂️ {{ circle.timeMinutes }} minute walk</h4>
              <p><strong>Distance:</strong> ~{{ Math.round(circle.radius) }}m radius</p>
              <p><strong>From:</strong> {{ selectedStationForIsochrone?.name }}</p>
            </div>
          </l-popup>
        </l-circle>

        <!-- API Isochrone GeoJSON layers (rendered before stations to be under them) -->
        <l-geo-json
          v-for="(isochrone, index) in isochroneGeoJson"
          :key="`isochrone-geojson-${index}`"
          :geojson="isochrone.geojson"
          :options-style="getGeoJsonStyle(isochrone)"
        >
          <l-popup>
            <div class="isochrone-popup">
              <h4>🚶‍♂️ {{ isochrone.timeMinutes }} minute walk (API)</h4>
              <p><strong>From:</strong> {{ selectedStationForIsochrone?.name }}</p>
              <p><strong>Data source:</strong> Routing API</p>
            </div>
          </l-popup>
        </l-geo-json>

        <!-- Station markers -->
        <l-marker
          v-for="station in allVisibleStations"
          :key="station.id"
          :lat-lng="[station.latitude, station.longitude]"
          :icon="getStationIcon(station.type) as any"
          @click="onStationClick(station)"
        >
          <l-popup>
            <div class="station-popup">
              <h4>
                <span v-if="station.type === 'station'">🚇</span>
                <span v-else>🚊</span>
                <span 
                  v-if="station.wikipediaLink"
                  @click="openWikipediaLink(station.wikipediaLink)"
                  class="station-name-link"
                >
                  {{ station.name }}
                </span>
                <span v-else class="station-name">{{ station.name }}</span>
              </h4>
              <p><strong>Type:</strong> {{ station.type === 'station' ? 'Metro Station' : 'Tram Stop' }}</p>
              <p><strong>Location:</strong> {{ station.latitude.toFixed(4) }}, {{ station.longitude.toFixed(4) }}</p>
              <div class="station-actions">
                <button 
                  @click="onStationClick(station)"
                  class="isochrone-btn"
                  :class="{ 'isochrone-btn--active': selectedStationForIsochrone?.id === station.id }"
                >
                  🚶‍♂️ {{ selectedStationForIsochrone?.id === station.id ? 'Hide' : 'Show' }} Walking Distances
                </button>
              </div>
            </div>
          </l-popup>
        </l-marker>
      </l-map>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useAreas } from '@/composables/useAreas'
import { useGeolocation } from '@/composables/useGeolocation'
import { useLocationSearch } from '@/composables/useLocationSearch'
import { useStations } from '@/composables/useStations'
import type { SearchResult, Station } from '@/types'
import { searchLocationIconSvg, userLocationIconSvg, stationIconSvg, tramStopIconSvg } from '@/utils/mapIcons'
import { LCircle, LMap, LMarker, LPopup, LTileLayer, LGeoJson } from '@vue-leaflet/vue-leaflet'
import { onMounted, ref, computed } from 'vue'

// Map setup
const mapRef = ref<InstanceType<typeof LMap> | null>(null)
const zoom = ref(13)
const initialCenter = ref<[number, number]>([41.9028, 12.4964]) // Default to Rome
const selectedLocation = ref<[number, number] | null>(null)
const selectedLocationName = ref('')

// Map configuration
const tileLayerUrl = 'https://tiles.stadiamaps.com/tiles/alidade_smooth/{z}/{x}/{y}{r}.png'
const attribution = '© <a href="https://stadiamaps.com/">Stadia Maps</a>, © <a href="https://openmaptiles.org/">OpenMapTiles</a> © <a href="http://openstreetmap.org">OpenStreetMap</a> contributors'

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
  getLocation
} = useGeolocation()

const {
  areas,
  isLoading: isAreasLoading,
  error: areasError,
  load: loadAreas
} = useAreas()

const {
  loadStations,
  getStationsForArea,
  isLoadingForArea,
  getErrorForArea
} = useStations()

// Search UI state
const isSearchFocused = ref(false)
const highlightedIndex = ref(-1)

// Welcome popup state
const showWelcomePopup = ref(true)

// Stations state
const visibleStations = ref<Set<string>>(new Set())

// Isochrone state
const selectedStationForIsochrone = ref<Station | null>(null)
const isochroneCircles = ref<Array<{
  center: [number, number]
  radius: number
  color: string
  timeMinutes: number
}>>([])
const isochroneGeoJson = ref<Array<{
  geojson: any
  timeMinutes: number
  color: string
}>>([])

// Computed property for all visible stations across all areas
const allVisibleStations = computed(() => {
  const stations: Station[] = []
  for (const areaId of visibleStations.value) {
    stations.push(...getStationsForArea(areaId))
  }
  return stations
})

// Station toggle functionality
const toggleStationsForArea = async (areaId: string) => {
  if (visibleStations.value.has(areaId)) {
    // Hide stations
    visibleStations.value.delete(areaId)
  } else {
    // Show stations - first load them if not already loaded
    if (getStationsForArea(areaId).length === 0) {
      await loadStations(areaId)
    }
    visibleStations.value.add(areaId)
  }
}

// Helper function to get icon for station type
const getStationIcon = (type: 'station' | 'tram_stop') => {
  return type === 'station' ? stationIconSvg : tramStopIconSvg
}

// Helper function to open Wikipedia link
const openWikipediaLink = (url: string) => {
  window.open(url, '_blank')
}

// Helper function to get GeoJSON style options
const getGeoJsonStyle = (isochrone: any) => {
  return {
      color: isochrone.color,
      fillColor: isochrone.color,
      fillOpacity: 0.1,
      weight: isochrone.timeMinutes === 30 ? 2 : 0,
      opacity: isochrone.timeMinutes === 30 ? 0.6 : 0
  }
}

// Function to handle station click and show isochrone circles
const onStationClick = async (station: Station) => {
  // If clicking the same station, toggle off the isochrones
  if (selectedStationForIsochrone.value?.id === station.id) {
    selectedStationForIsochrone.value = null
    isochroneCircles.value = []
    isochroneGeoJson.value = []
  } else {
    // Show isochrones for the new station
    selectedStationForIsochrone.value = station
    await loadIsochronesForStation(station)
  }
}

// Function to load isochrones from API or fallback to calculated circles
const loadIsochronesForStation = async (station: Station) => {
  const timeIntervals = [5, 10, 15, 20, 30] // API time intervals
  const baseColor = station.type === 'station' ? '#22c55e' : '#eab308' // green for metro, yellow for tram
  
  // Clear existing isochrones
  isochroneCircles.value = []
  isochroneGeoJson.value = []
  
  // Find the area that contains this station
  const stationArea = areas.value.find(area => {
    // Check if station is within area bounds
    const stationLat = station.latitude
    const stationLng = station.longitude
    const areaLat = area.latitude
    const areaLng = area.longitude
    const radiusKm = (area.diameter / 2) / 1000 // Convert to km
    
    // Simple distance check (approximate)
    const distance = Math.sqrt(
      Math.pow(stationLat - areaLat, 2) + Math.pow(stationLng - areaLng, 2)
    ) * 111 // Rough conversion to km
    
    return distance <= radiusKm
  })
  
  if (!stationArea) {
    console.warn('No area found for station, using calculated circles')
    generateIsochroneCircles(station)
    return
  }
  
  const apiPromises = timeIntervals.map(async (time) => {
    try {
      const response = await fetch(
        `http://localhost:7071/api/area/${stationArea.id}/station/${station.id}/isochrone/${time}`
      )
      
      if (response.status === 400) {
        console.warn(`Isochrone not found for ${time} minutes, will use calculated circle`)
        return { time, success: false, data: null }
      }
      
      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`)
      }
      
      const geojson = await response.json()
      return { time, success: true, data: geojson }
    } catch (error) {
      console.error(`Error fetching isochrone for ${time} minutes:`, error)
      return { time, success: false, data: null }
    }
  })
  
  try {
    const results = await Promise.all(apiPromises)
    
    // Check if all API calls were successful
    const allSuccessful = results.every(result => result.success)
    
    if (allSuccessful) {
      // Use API isochrones - reverse order so largest renders first (bottom)
      const sortedResults = results.sort((a, b) => b.time - a.time) // 30, 20, 15, 10
      sortedResults.forEach(result => {
        if (result.success && result.data) {
          isochroneGeoJson.value.push({
            geojson: result.data,
            timeMinutes: result.time,
            color: baseColor
          })
        }
      })
      console.log('✅ Using API isochrones')
    } else {
      // Fallback to calculated circles
      console.warn('⚠️ Some API calls failed, falling back to calculated circles')
      generateIsochroneCircles(station)
    }
  } catch (error) {
    console.error('Error loading isochrones:', error)
    // Fallback to calculated circles
    generateIsochroneCircles(station)
  }
}

// Function to generate isochrone circles for walking distances
const generateIsochroneCircles = (station: Station) => {
  // Walking speeds: approximately 5 km/h = 83.33 m/min
  const walkingSpeedMPerMin = 83.33
  const timeIntervals = [30, 20, 15, 10, 5] // minutes - reversed order so largest renders first (bottom)
  
  // Determine color based on station type
  const baseColor = station.type === 'station' ? '#22c55e' : '#eab308' // green for metro, yellow for tram
  
  // Clear existing circles
  isochroneCircles.value = []
  
  // Generate circles for each time interval
  timeIntervals.forEach(minutes => {
    const radiusMeters = walkingSpeedMPerMin * minutes
    
    isochroneCircles.value.push({
      center: [station.latitude, station.longitude],
      radius: radiusMeters,
      color: baseColor,
      timeMinutes: minutes
    })
  })
}

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
      if (event.target && 'blur' in event.target && typeof event.target.blur === 'function') {
        event.target.blur()
      }
      break
  }
}

// Map event handlers
const onMapReady = () => {
  console.log('🗺️ Map is ready!', mapRef.value?.leafletObject)
}

// Location selection
const selectLocation = (result: SearchResult) => {
  const lat = parseFloat(result.lat)
  const lon = parseFloat(result.lon)
  
  selectedLocation.value = [lat, lon]
  selectedLocationName.value = result.display_name
  
  // Use map's setView method instead of reactive center
  if (mapRef.value?.leafletObject) {
    mapRef.value.leafletObject.setView([lat, lon], 15)
  }
  
  // Reset search state
  clearSearch()
  highlightedIndex.value = -1
  isSearchFocused.value = false
}

// Current location functionality
const goToCurrentLocation = async () => {
  console.log('🗺️ Getting current location...')
  console.log('🔄 Loading state:', isLocationLoading.value)
  
  try {
    await getLocation()
    
    console.log('📍 Current location after getLocation():', currentLocation.value)
    console.log('🗺️ Map ref available:', !!mapRef.value)
    console.log('🍃 Leaflet object available:', !!mapRef.value?.leafletObject)
    
    if (currentLocation.value) {
      // Handle different coordinate formats
      let lat, lng
      
      if (Array.isArray(currentLocation.value)) {
        [lat, lng] = currentLocation.value
      } else if (currentLocation.value.lat !== undefined && currentLocation.value.lng !== undefined) {
        lat = currentLocation.value.lat
        lng = currentLocation.value.lng
      } else {
        console.error('❌ Invalid coordinate format:', currentLocation.value)
        return
      }
      
      console.log(`🎯 Centering map on: ${lat}, ${lng}`)
      
      if (mapRef.value?.leafletObject) {
        mapRef.value.leafletObject.setView([lat, lng], 16)
        console.log('✅ Map centered successfully')
      } else {
        console.error('❌ Map reference not available')
        // Try again after a short delay
        setTimeout(() => {
          if (mapRef.value?.leafletObject) {
            mapRef.value.leafletObject.setView([lat, lng], 16)
            console.log('✅ Map centered successfully (delayed)')
          }
        }, 500)
      }
    } else {
      console.error('❌ Current location not available')
      console.error('🔍 Is loading:', isLocationLoading.value)
    }
  } catch (error) {
    console.error('❌ Error in goToCurrentLocation:', error)
  }
}

// Welcome popup functionality
const closeWelcomePopup = () => {
  showWelcomePopup.value = false
}

// Initialize on mount
onMounted(async () => {
  console.log('🚀 Component mounted')
  
  // Load areas first
  await loadAreas()
  
  // Try to get user's current location
  await getLocation()
  
  console.log('📍 Initial location check:', currentLocation.value)
  
  if (currentLocation.value) {
    const lat = currentLocation.value.lat
    const lng = currentLocation.value.lng
    console.log(`🎯 Setting initial center to: ${lat}, ${lng}`)
    
    initialCenter.value = [lat, lng]
    
    // Wait a bit for the map to be ready, then set view
    setTimeout(() => {
      if (mapRef.value?.leafletObject) {
        console.log('🗺️ Setting initial map view')
        mapRef.value.leafletObject.setView([lat, lng], 13)
      }
    }, 100)
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

.user-location-popup {
  text-align: center;
  font-size: 14px;
}

.area-popup {
  min-width: 200px;
}

.area-popup h3 {
  margin: 0 0 10px 0;
  color: #333;
  font-size: 16px;
}

.area-popup p {
  margin: 5px 0;
  font-size: 14px;
  color: #666;
}

.area-popup strong {
  color: #333;
}

/* Station Toggle Button Styles */
.station-toggle-section {
  margin-top: 15px;
  padding-top: 10px;
  border-top: 1px solid #eee;
}

.station-toggle-btn {
  background: #007bff;
  color: white;
  border: none;
  border-radius: 6px;
  padding: 8px 12px;
  font-size: 13px;
  cursor: pointer;
  transition: all 0.2s ease;
  display: flex;
  align-items: center;
  gap: 4px;
  min-height: 32px;
}

.station-toggle-btn:hover:not(:disabled) {
  background: #0056b3;
  transform: translateY(-1px);
}

.station-toggle-btn--active {
  background: #28a745;
}

.station-toggle-btn--active:hover:not(:disabled) {
  background: #1e7e34;
}

.station-toggle-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
  transform: none;
}

.loading-spinner-small {
  width: 12px;
  height: 12px;
  border: 2px solid rgba(255, 255, 255, 0.3);
  border-top: 2px solid white;
  border-radius: 50%;
  animation: spin 1s linear infinite;
}

.station-count {
  margin-top: 6px;
  font-size: 12px;
  color: #666;
  font-style: italic;
}

.station-error {
  margin-top: 6px;
  font-size: 12px;
  color: #dc3545;
  background: #f8d7da;
  padding: 4px 6px;
  border-radius: 3px;
  border: 1px solid #f5c6cb;
}

/* Station Popup Styles */
.station-popup {
  min-width: 200px;
}

.station-popup h4 {
  margin: 0 0 8px 0;
  color: #333;
  font-size: 15px;
  display: flex;
  align-items: center;
  gap: 6px;
}

.station-name-link {
  color: #007bff;
  cursor: pointer;
  text-decoration: underline;
  transition: color 0.2s ease;
}

.station-name-link:hover {
  color: #0056b3;
}

.station-name {
  color: #333;
}

.station-popup p {
  margin: 4px 0;
  font-size: 13px;
  color: #666;
}

.station-popup strong {
  color: #333;
}

.station-actions {
  margin-top: 12px;
  padding-top: 8px;
  border-top: 1px solid #eee;
}

.isochrone-btn {
  background: #007bff;
  color: white;
  border: none;
  border-radius: 6px;
  padding: 6px 10px;
  font-size: 12px;
  cursor: pointer;
  transition: all 0.2s ease;
  width: 100%;
}

.isochrone-btn:hover {
  background: #0056b3;
}

.isochrone-btn--active {
  background: #dc3545;
}

.isochrone-btn--active:hover {
  background: #c82333;
}

/* Isochrone Popup Styles */
.isochrone-popup {
  min-width: 180px;
}

.isochrone-popup h4 {
  margin: 0 0 8px 0;
  color: #333;
  font-size: 14px;
}

.isochrone-popup p {
  margin: 4px 0;
  font-size: 12px;
  color: #666;
}

.isochrone-popup strong {
  color: #333;
}

/* Welcome Popup Styles */
.welcome-popup-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-color: rgba(0, 0, 0, 0.5);
  backdrop-filter: blur(4px);
  z-index: 2000;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 20px;
  animation: fadeIn 0.3s ease-out;
}

.welcome-popup {
  background: white;
  border-radius: 16px;
  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.15);
  max-width: 500px;
  width: 100%;
  max-height: 90vh;
  overflow-y: auto;
  animation: slideUp 0.3s ease-out;
}

.welcome-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 24px 24px 16px;
  border-bottom: 1px solid #f0f0f0;
}

.welcome-header h2 {
  margin: 0;
  font-size: 24px;
  font-weight: 600;
  color: #333;
}

.welcome-close-btn {
  background: none;
  border: none;
  cursor: pointer;
  padding: 8px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #666;
  transition: all 0.2s ease;
}

.welcome-close-btn:hover {
  background-color: #f0f0f0;
  color: #333;
}

.welcome-content {
  padding: 24px;
}

.welcome-content p {
  margin: 0 0 20px;
  font-size: 16px;
  color: #666;
  line-height: 1.5;
}

.welcome-features {
  list-style: none;
  margin: 0 0 24px;
  padding: 0;
}

.welcome-features li {
  padding: 8px 0;
  font-size: 15px;
  color: #555;
  display: flex;
  align-items: center;
  gap: 8px;
}

.welcome-actions {
  display: flex;
  gap: 12px;
  flex-wrap: wrap;
}

.welcome-btn {
  flex: 1;
  min-width: 140px;
  padding: 12px 20px;
  border: none;
  border-radius: 8px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
}

.welcome-btn--primary {
  background-color: #007bff;
  color: white;
}

.welcome-btn--primary:hover {
  background-color: #0056b3;
  transform: translateY(-1px);
}

.welcome-btn--secondary {
  background-color: #f8f9fa;
  color: #666;
  border: 1px solid #e9ecef;
}

.welcome-btn--secondary:hover {
  background-color: #e9ecef;
  color: #333;
  transform: translateY(-1px);
}

@keyframes fadeIn {
  from {
    opacity: 0;
  }
  to {
    opacity: 1;
  }
}

@keyframes slideUp {
  from {
    opacity: 0;
    transform: translateY(20px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

@media (max-width: 480px) {
  .welcome-popup {
    margin: 10px;
    border-radius: 12px;
  }
  
  .welcome-header {
    padding: 20px 20px 12px;
  }
  
  .welcome-header h2 {
    font-size: 20px;
  }
  
  .welcome-content {
    padding: 20px;
  }
  
  .welcome-actions {
    flex-direction: column;
  }
  
  .welcome-btn {
    min-width: auto;
  }
}
</style>
