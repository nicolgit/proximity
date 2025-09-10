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
          placeholder="find a place..."
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
    <WelcomePopup
      v-if="showWelcomePopup"
      :areas="areas"
      :is-areas-loading="isAreasLoading"
      :areas-error="areasError"
      @close="closeWelcomePopup"
    />

    <!-- Area Proximity Level Selector -->
    <div class="proximity-level-toolbar">
      <div class="proximity-level-header">
        <span class="proximity-level-title">🏙️ Levels</span>
        <button 
          @click="toggleProximityToolbar"
          class="proximity-level-toggle"
          :class="{ 'proximity-level-toggle--active': showProximityToolbar }"
        >
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <polyline :points="showProximityToolbar ? '6 9 12 15 18 9' : '9 18 15 12 9 6'"></polyline>
          </svg>
        </button>
      </div>
      
      <div v-show="showProximityToolbar" class="proximity-level-selector">
        <div class="proximity-level-slider-container">
          <div class="proximity-level-slider-labels">
            <span v-for="level in proximityLevelOptions" :key="level" class="slider-label">
              {{ level }}m
            </span>
          </div>
          <input
            type="range"
            :min="0"
            :max="proximityLevelOptions.length - 1"
            :value="proximityLevelOptions.indexOf(pendingProximityLevel)"
            @input="selectProximityLevelByIndex(($event.target as HTMLInputElement).value)"
            class="proximity-level-slider"
          />
          <div class="proximity-level-current">
             <strong>{{ pendingProximityLevel }}min</strong> isochrones
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

    <!-- Area bounds indicator -->
    <div v-if="areaBoundsActive" class="area-bounds-indicator">
      <div class="bounds-icon">🔒</div>
      <div class="bounds-text">Map limited to {{ currentAreaName }}</div>
    </div>

    <!-- Map Container -->
    <div class="map-container">
      <l-map
        ref="mapRef"
        v-model:zoom="zoom"
        :center="initialCenter"
        :min-zoom="minZoom"
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
            <div class="area-popup" @click="closeAreaPopupOnOutsideClick($event)">
              <h3>📍 {{ area.name }}</h3>
              
              <!-- Use reusable AreaControls component -->
              <AreaControls
                :area-id="area.id"
                :show-stats="true"
                :station-visible="visibleStations.has(area.id)"
                :is-loading-stations="isLoadingForArea(area.id)"
                :station-count="getStationsForArea(area.id).length"
                :station-error="getErrorForArea(area.id) ?? undefined"
                :proximity-visible="visibleAreaIsochrones.has(area.id)"
                :is-loading-proximity="isLoadingAreaIsochroneForArea(area.id)"
                :proximity-count="areaIsochronesWithBorderIndex.filter(iso => iso.areaId === area.id).length"
                :proximity-error="getAreaIsochroneErrorForArea(area.id) ?? undefined"
                :selected-proximity-level="selectedProximityLevel"
                @toggle-stations="() => toggleStationsForArea(area.id)"
                @toggle-proximity="() => toggleAreaIsochronesForArea(area.id)"
              />
            </div>
          </l-popup>
        </l-circle>

        <!-- Area Proximity Isochrone GeoJSON layers (rendered first, on the bottom) -->
        <l-geo-json
          v-for="isochrone in areaIsochronesWithBorderIndex"
          :key="`area-isochrone-geojson-${isochrone.areaId}-${isochrone.timeMinutes}`"
          :geojson="isochrone.geojson"
          :options-style="getGeoJsonStyle(isochrone, isochrone.borderIndex)"
          @click="onIsochroneClick"
        >
          <l-popup>
            <div class="isochrone-popup" @click="closeAreaPopupOnOutsideClick($event)">
              <h4>🏙️ less than {{ isochrone.timeMinutes }} minutes walk</h4>
              from a train or tram stop
              
              <!-- Reverse geocoding info for clicked location -->
              <div v-if="isochroneClickLocation" class="reverse-geocoding-info">
                <hr style="margin: 10px 0; border: none; border-top: 1px solid #eee;">
                <div class="location-address">
                  <strong>📍 Address:</strong>
                  <div v-if="isLoadingIsochroneAddress" class="address-loading">Loading address...</div>
                  <div v-else-if="isochroneClickAddress" class="address-text">{{ isochroneClickAddress }}</div>
                  <div v-else class="address-error">Address not found</div>
                </div>
              </div>

              <!-- Area controls reused here -->
              <AreaControls
                :area-id="isochrone.areaId"
                :show-stats="false"
                :station-visible="visibleStations.has(isochrone.areaId)"
                :is-loading-stations="isLoadingForArea(isochrone.areaId)"
                :station-count="getStationsForArea(isochrone.areaId).length"
                :station-error="getErrorForArea(isochrone.areaId) ?? undefined"
                :proximity-visible="visibleAreaIsochrones.has(isochrone.areaId)"
                :is-loading-proximity="isLoadingAreaIsochroneForArea(isochrone.areaId)"
                :proximity-count="areaIsochronesWithBorderIndex.filter(iso => iso.areaId === isochrone.areaId).length"
                :proximity-error="getAreaIsochroneErrorForArea(isochrone.areaId) ?? undefined"
                :selected-proximity-level="selectedProximityLevel"
                @toggle-stations="() => toggleStationsForArea(isochrone.areaId)"
                @toggle-proximity="() => toggleAreaIsochronesForArea(isochrone.areaId)"
              />
            </div>
          </l-popup>
        </l-geo-json>

        <!-- Station Isochrone circles (rendered on top of area isochrones) -->
        <l-circle
          v-for="(circle, index) in filteredIsochroneCircles"
          :key="`isochrone-${index}`"
          :lat-lng="circle.center"
          :radius="circle.radius"
          :color="circle.color"
          :fill-color="circle.color"
          :fill-opacity="0.1"
          :weight="index === 0 ? 2 : 0"
          :opacity="index === 0 ? 0.6 : 0.3"
          @click="onCircleClick"
        >
          <l-popup>
            <div class="isochrone-popup" @click="closeAreaPopupOnOutsideClick($event)">
              <h4>🚶‍♂️ less than {{ circle.timeMinutes }} minutes walk</h4>
              <p><strong>Distance:</strong> ~{{ Math.round(circle.radius) }}m radius</p>
              <p><strong>From:</strong> {{ selectedStationForIsochrone?.name }}</p>
              
              <!-- Reverse geocoding info for clicked location on circle -->
              <div v-if="circleClickLocation" class="reverse-geocoding-info">
                <hr style="margin: 10px 0; border: none; border-top: 1px solid #eee;">
                <div class="location-address">
                  <strong>📍 Address:</strong>
                  <div v-if="isLoadingCircleAddress" class="address-loading">Loading address...</div>
                  <div v-else-if="circleClickAddress" class="address-text">{{ circleClickAddress }}</div>
                  <div v-else class="address-error">Address not found</div>
                </div>
              </div>
            </div>
          </l-popup>
        </l-circle>

        <!-- Station API Isochrone GeoJSON layers (rendered on top of area isochrones) -->
        <l-geo-json
          v-for="(isochrone, index) in filteredIsochroneGeoJson"
          :key="`isochrone-geojson-${index}`"
          :geojson="isochrone.geojson"
          :options-style="getGeoJsonStyle(isochrone, index)"
          @click="onStationIsochroneClick"
        >
          <l-popup>
            <div class="isochrone-popup" @click="closeAreaPopupOnOutsideClick($event)">
              <h4>🚶‍♂️ less than {{ isochrone.timeMinutes }} minutes walk</h4>
              <p>from <strong>{{ selectedStationForIsochrone?.name }}</strong></p>
              
              <!-- Reverse geocoding info for clicked location on station isochrone -->
              <div v-if="stationIsochroneClickLocation" class="reverse-geocoding-info">
                <hr style="margin: 10px 0; border: none; border-top: 1px solid #eee;">
                <div class="location-address">
                  <strong>📍 Address:</strong>
                  <div v-if="isLoadingStationIsochroneAddress" class="address-loading">Loading address...</div>
                  <div v-else-if="stationIsochroneClickAddress" class="address-text">{{ stationIsochroneClickAddress }}</div>
                  <div v-else class="address-error">Address not found</div>
                </div>
              </div>
            </div>
          </l-popup>
        </l-geo-json>

        <!-- Station markers -->
        <l-marker
          v-for="station in allVisibleStations"
          :key="station.id"
          :lat-lng="[station.latitude, station.longitude]"
          :icon="getStationIcon(station.type) as any"
          @click="onStationClick(station, station.areaId)">
          <l-popup>
            <div class="station-popup" @click="closeAreaPopupOnOutsideClick($event)">
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
              <p>type: <strong>{{ station.type === 'station' ? 'Train/Metro Station' : 'Tram Stop' }}</strong></p>
              <p>coords: <strong>{{ station.latitude.toFixed(4) }}, {{ station.longitude.toFixed(4) }}</strong> </p>
              <div class="station-actions">
                <button 
                  @click="onStationClick(station, station.areaId)"
                  class="btn"
                  :class="{ 'btn--active': selectedStationForIsochrone?.id === station.id }"
                >
                  🚶‍♂️ {{ selectedStationForIsochrone?.id === station.id ? 'Hide' : 'Show' }} Walking Distances
                </button>
              </div>

              <!-- Reuse AreaControls for the station's area -->
              <AreaControls
                :area-id="station.areaId"
                :show-stats="false"
                :station-visible="visibleStations.has(station.areaId)"
                :is-loading-stations="isLoadingForArea(station.areaId)"
                :station-count="getStationsForArea(station.areaId).length"
                :station-error="getErrorForArea(station.areaId) ?? undefined"
                :proximity-visible="visibleAreaIsochrones.has(station.areaId)"
                :is-loading-proximity="isLoadingAreaIsochroneForArea(station.areaId)"
                :proximity-count="areaIsochronesWithBorderIndex.filter(iso => iso.areaId === station.areaId).length"
                :proximity-error="getAreaIsochroneErrorForArea(station.areaId) ?? undefined"
                :selected-proximity-level="selectedProximityLevel"
                @toggle-stations="() => toggleStationsForArea(station.areaId)"
                @toggle-proximity="() => toggleAreaIsochronesForArea(station.areaId)"
              />
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
import { LocationSearchService } from '@/services/LocationSearchService'
import type { SearchResult, Station } from '@/types'
import { searchLocationIconSvg, userLocationIconSvg, stationIconSvg, tramStopIconSvg } from '@/utils/mapIcons'
import { getApiUrl } from '@/config/env'
import { LCircle, LMap, LMarker, LPopup, LTileLayer, LGeoJson } from '@vue-leaflet/vue-leaflet'
import { onMounted, ref, computed, onUnmounted } from 'vue'
import AreaControls from '@/components/AreaControls.vue'
import WelcomePopup from '@/components/WelcomePopup.vue'

// Props
interface Props {
  areaId?: string
}

const props = withDefaults(defineProps<Props>(), {
  areaId: undefined
})

// Map setup
const mapRef = ref<InstanceType<typeof LMap> | null>(null)
const zoom = ref(7)
const minZoom = ref<number | undefined>(undefined) // Minimum zoom level when targeting a specific area
const maxBounds = ref<[[number, number], [number, number]] | undefined>(undefined) // Max bounds when targeting a specific area
const areaBoundsActive = ref(false) // Track if area bounds constraints are currently active
const initialCenter = ref<[number, number]>([41.9028, 12.4964]) // Default to Rome
const selectedLocation = ref<[number, number] | null>(null)
const selectedLocationName = ref('')

// Isochrone click state for reverse geocoding
const isochroneClickLocation = ref<[number, number] | null>(null)
const isochroneClickAddress = ref('')
const isLoadingIsochroneAddress = ref(false)

// Circle click state for reverse geocoding
const circleClickLocation = ref<[number, number] | null>(null)
const circleClickAddress = ref('')
const isLoadingCircleAddress = ref(false)

// Station isochrone click state for reverse geocoding
const stationIsochroneClickLocation = ref<[number, number] | null>(null)
const stationIsochroneClickAddress = ref('')
const isLoadingStationIsochroneAddress = ref(false)

// Map configuration
const tileLayerUrl = 'https://tiles.stadiamaps.com/tiles/alidade_smooth/{z}/{x}/{y}{r}.png'
const attribution = '© <a href="http://github.com/nicolgit">Nicola Delfino</a>, © <a href="https://stadiamaps.com/">Stadia Maps</a>, © <a href="https://openmaptiles.org/">OpenMapTiles</a> © <a href="http://openstreetmap.org">OpenStreetMap</a>'

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
} = useAreas(props.areaId)

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

// Area proximity/isochrone state
const visibleAreaIsochrones = ref<Set<string>>(new Set())
const isLoadingAreaIsochrones = ref<Set<string>>(new Set())
const areaIsochroneErrors = ref<Map<string, string>>(new Map())
const areaIsochroneGeoJson = ref<Array<{
  geojson: any
  timeMinutes: number
  color: string
  areaId: string
}>>([])

// Proximity level selector state
const showProximityToolbar = ref(false)
const proximityLevelOptions = ref([5, 10, 15, 20, 30])
const selectedProximityLevel = ref(30)
const pendingProximityLevel = ref(30) // For debounced updates

// Debounced proximity level update
let proximityLevelDebounceTimer: number | null = null

const debouncedProximityLevelUpdate = (level: number) => {
  // Clear existing timer
  if (proximityLevelDebounceTimer) {
    clearTimeout(proximityLevelDebounceTimer)
  }
  
  // Update pending level immediately for UI responsiveness
  pendingProximityLevel.value = level
  
  // Set timer to update actual level after delay
  proximityLevelDebounceTimer = setTimeout(() => {
    selectedProximityLevel.value = level
    refreshVisibleAreaIsochrones()
    refreshStationIsochrones()
    proximityLevelDebounceTimer = null
  }, 300) // 300ms debounce delay
}

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
  const stations: (Station & { areaId: string })[] = []
  for (const areaId of visibleStations.value) {
    const areaStations = getStationsForArea(areaId).map(station => ({
      ...station,
      areaId
    }))
    stations.push(...areaStations)
  }
  return stations
})

// Computed property for filtered isochrone circles based on proximity level
const filteredIsochroneCircles = computed(() => {
  return isochroneCircles.value.filter(circle => circle.timeMinutes <= selectedProximityLevel.value)
})

// Computed property for filtered isochrone GeoJSON based on proximity level
const filteredIsochroneGeoJson = computed(() => {
  return isochroneGeoJson.value.filter(isochrone => isochrone.timeMinutes <= selectedProximityLevel.value)
})

// Computed property for filtered area isochrone GeoJSON based on proximity level
const filteredAreaIsochroneGeoJson = computed(() => {
  return areaIsochroneGeoJson.value.filter(isochrone => isochrone.timeMinutes <= selectedProximityLevel.value)
})

// Computed property for area isochrones with proper border indexing per area
const areaIsochronesWithBorderIndex = computed(() => {
  const filtered = filteredAreaIsochroneGeoJson.value
  const groupedByArea: { [areaId: string]: typeof filtered } = {}
  
  // Group isochrones by area
  filtered.forEach(isochrone => {
    if (!groupedByArea[isochrone.areaId]) {
      groupedByArea[isochrone.areaId] = []
    }
    groupedByArea[isochrone.areaId].push(isochrone)
  })
  
  // Sort each area's isochrones by time (descending) and add border index
  const result: Array<typeof filtered[0] & { borderIndex: number }> = []
  Object.keys(groupedByArea).forEach(areaId => {
    const areaIsochrones = groupedByArea[areaId].sort((a, b) => b.timeMinutes - a.timeMinutes)
    areaIsochrones.forEach((isochrone, index) => {
      result.push({
        ...isochrone,
        borderIndex: index // 0 for outermost (largest), 1+ for inner ones
      })
    })
  })
  
  return result
})

// Computed property for current selected area name
const currentAreaName = computed(() => {
  if (props.areaId && areas.value.length > 0) {
    const currentArea = areas.value.find(area => area.id === props.areaId)
    return currentArea?.name || 'selected area'
  }
  return 'selected area'
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
const getGeoJsonStyle = (isochrone: any, index: number = 0) => {
  return () => ({
      color: isochrone.color,
      fillColor: isochrone.color,
      fillOpacity: 0.1,
      weight: index === 0 ? 2 : 0,
      opacity: index === 0 ? 0.6 : 0.3
  })
}

// Area isochrone helper functions
const isLoadingAreaIsochroneForArea = (areaId: string) => {
  return isLoadingAreaIsochrones.value.has(areaId)
}

const getAreaIsochroneErrorForArea = (areaId: string) => {
  return areaIsochroneErrors.value.get(areaId) || null
}

// Function to toggle area isochrones
const toggleAreaIsochronesForArea = async (areaId: string) => {
  if (visibleAreaIsochrones.value.has(areaId)) {
    // Hide area isochrones
    visibleAreaIsochrones.value.delete(areaId)
    // Remove from map
    areaIsochroneGeoJson.value = areaIsochroneGeoJson.value.filter(
      isochrone => isochrone.areaId !== areaId
    )
    // Clear any errors
    areaIsochroneErrors.value.delete(areaId)
  } else {
    // Show area isochrones - load them from API
    await loadAreaIsochrones(areaId)
  }
}

// Function to load area isochrones from API
const loadAreaIsochrones = async (areaId: string) => {
  const selectedLevel = selectedProximityLevel.value
  // Load all levels up to and including the selected level
  const levelsToLoad = proximityLevelOptions.value.filter(level => level <= selectedLevel)
  const baseColor = '#8b5cf6' // purple color for area isochrones
  
  // Set loading state
  isLoadingAreaIsochrones.value.add(areaId)
  areaIsochroneErrors.value.delete(areaId)
  
  // Clear existing area isochrones for this area
  areaIsochroneGeoJson.value = areaIsochroneGeoJson.value.filter(
    isochrone => isochrone.areaId !== areaId
  )
  
  try {
    const promises = levelsToLoad.map(async (timeInterval) => {
      try {
        const response = await fetch(getApiUrl(`/area/${areaId}/isochrone/${timeInterval}`))
        
        if (!response.ok) {
          throw new Error(`HTTP error! status: ${response.status}`)
        }
        
        const geojson = await response.json()
        return { timeInterval, success: true, data: geojson }
      } catch (error) {
        console.error(`Error fetching area isochrone for ${timeInterval} minutes:`, error)
        return { timeInterval, success: false, data: null }
      }
    })
    
    const results = await Promise.all(promises)
    const successfulResults = results.filter(result => result.success && result.data)
    
    if (successfulResults.length > 0) {
      // Add to visible set
      visibleAreaIsochrones.value.add(areaId)
      
      // Add isochrones to map - reverse order so largest renders first (bottom)
      const sortedResults = successfulResults.sort((a, b) => b.timeInterval - a.timeInterval)
      sortedResults.forEach(result => {
        areaIsochroneGeoJson.value.push({
          geojson: result.data,
          timeMinutes: result.timeInterval,
          color: baseColor,
          areaId: areaId
        })
      })
      
      console.log(`✅ Loaded ${successfulResults.length} area isochrones for area ${areaId} (levels: ${successfulResults.map(r => r.timeInterval).join(', ')})`)
    } else {
      throw new Error('No isochrone data available')
    }
  } catch (error) {
    console.error('Error loading area isochrones:', error)
    areaIsochroneErrors.value.set(areaId, 'Failed to load proximity data')
  } finally {
    // Clear loading state
    isLoadingAreaIsochrones.value.delete(areaId)
  }
}

// Function to handle station click and show isochrone circles
const onStationClick = async (station: Station, areaId: string) => {
  // If clicking the same station, toggle off the isochrones
  if (selectedStationForIsochrone.value?.id === station.id) {
    selectedStationForIsochrone.value = null
    isochroneCircles.value = []
    isochroneGeoJson.value = []
  } else {
    // Show isochrones for the new station
    selectedStationForIsochrone.value = station
    await loadIsochronesForStation(station, areaId)
  }
}

// Function to load isochrones from API or fallback to calculated circles
const loadIsochronesForStation = async (station: Station, areaId: string) => {
  // Only load time intervals up to the selected proximity level
  const timeIntervals = proximityLevelOptions.value.filter(level => level <= selectedProximityLevel.value)
  const baseColor = station.type === 'station' ? '#22c55e' : '#eab308' // green for metro, yellow for tram
  
  // Clear existing isochrones
  isochroneCircles.value = []
  isochroneGeoJson.value = []
  
  // Find the area using the provided areaId
  const stationArea = areas.value.find(area => area.id === areaId)
  
  if (!stationArea) {
    console.warn('No area found for provided areaId, using calculated circles')
    generateIsochroneCircles(station)
    return
  }
  
  const apiPromises = timeIntervals.map(async (time) => {
    try {
      const response = await fetch(
        getApiUrl(`/area/${stationArea.id}/station/${station.id}/isochrone/${time}`)
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
  // Only generate circles up to the selected proximity level - reversed order so largest renders first (bottom)
  const timeIntervals = proximityLevelOptions.value
    .filter(level => level <= selectedProximityLevel.value)
    .sort((a, b) => b - a)
  
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

const onIsochroneClick = async (event: any) => {
  const { lat, lng } = event.latlng
  
  // Set isochrone click location
  isochroneClickLocation.value = [lat, lng]
  isochroneClickAddress.value = ''
  isLoadingIsochroneAddress.value = true
  
  try {
    // Perform reverse geocoding
    const result = await LocationSearchService.reverseGeocode(lat, lng)
    if (result && result.display_name) {
      isochroneClickAddress.value = result.display_name
    } else {
      isochroneClickAddress.value = 'Address not found'
    }
  } catch (error) {
    console.error('Error performing reverse geocoding on isochrone:', error)
    isochroneClickAddress.value = 'Failed to load address'
  } finally {
    isLoadingIsochroneAddress.value = false
  }
}

const onCircleClick = async (event: any) => {
  const { lat, lng } = event.latlng
  
  // Set circle click location
  circleClickLocation.value = [lat, lng]
  circleClickAddress.value = ''
  isLoadingCircleAddress.value = true
  
  try {
    // Perform reverse geocoding
    const result = await LocationSearchService.reverseGeocode(lat, lng)
    if (result && result.display_name) {
      circleClickAddress.value = result.display_name
    } else {
      circleClickAddress.value = 'Address not found'
    }
  } catch (error) {
    console.error('Error performing reverse geocoding on circle:', error)
    circleClickAddress.value = 'Failed to load address'
  } finally {
    isLoadingCircleAddress.value = false
  }
}

const onStationIsochroneClick = async (event: any) => {
  const { lat, lng } = event.latlng
  
  // Set station isochrone click location
  stationIsochroneClickLocation.value = [lat, lng]
  stationIsochroneClickAddress.value = ''
  isLoadingStationIsochroneAddress.value = true
  
  try {
    // Perform reverse geocoding
    const result = await LocationSearchService.reverseGeocode(lat, lng)
    if (result && result.display_name) {
      stationIsochroneClickAddress.value = result.display_name
    } else {
      stationIsochroneClickAddress.value = 'Address not found'
    }
  } catch (error) {
    console.error('Error performing reverse geocoding on station isochrone:', error)
    stationIsochroneClickAddress.value = 'Failed to load address'
  } finally {
    isLoadingStationIsochroneAddress.value = false
  }
}

// Location selection
const selectLocation = (result: SearchResult) => {
  const lat = parseFloat(result.lat)
  const lon = parseFloat(result.lon)
  
  selectedLocation.value = [lat, lon]
  selectedLocationName.value = result.display_name
  
  // Use map's setView method instead of reactive center
  if (mapRef.value?.leafletObject) {
    // Remove area constraints when user searches for a new location
    if (props.areaId) {
      removeAreaConstraints()
    }

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
      let lat: number, lng: number
      
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
        // Remove area constraints when going to current location (unless we have a specific area target)
        if (props.areaId) {
          removeAreaConstraints()
        }

        mapRef.value.leafletObject.setView([lat, lng], 16)
        console.log('✅ Map centered successfully')
      } else {
        console.error('❌ Map reference not available')
        // Try again after a short delay
        setTimeout(() => {
          if (mapRef.value?.leafletObject) {
            // Remove area constraints when going to current location (unless we have a specific area target)
            if (props.areaId) {
              removeAreaConstraints()
            }

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

// Area popup functionality
const closeAreaPopupOnOutsideClick = (event: Event) => {
  // Check if the click target is not inside an interactive element (button, input, etc.)
  const target = event.target as HTMLElement
  if (target && !target.closest('button') && !target.closest('input') && !target.closest('select') && !target.closest('a')) {
    // Close the popup
    if (mapRef.value?.leafletObject) {
      mapRef.value.leafletObject.closePopup()
    }
  }
}

// Proximity level toolbar functionality
const toggleProximityToolbar = () => {
  showProximityToolbar.value = !showProximityToolbar.value
}

const selectProximityLevel = (level: number) => {
  debouncedProximityLevelUpdate(level)
}

const selectProximityLevelByIndex = (indexStr: string) => {
  const index = parseInt(indexStr, 10)
  if (index >= 0 && index < proximityLevelOptions.value.length) {
    const level = proximityLevelOptions.value[index]
    selectProximityLevel(level)
  }
}

const refreshVisibleAreaIsochrones = () => {
  // Get all currently visible area IDs
  const visibleAreaIds = Array.from(visibleAreaIsochrones.value)
  
  // Clear current isochrones
  areaIsochroneGeoJson.value = []
  
  // Reload isochrones for all visible areas with new level
  visibleAreaIds.forEach(areaId => {
    loadAreaIsochrones(areaId)
  })
}

// Function to refresh station isochrones when proximity level changes
const refreshStationIsochrones = async () => {
  if (selectedStationForIsochrone.value) {
    // Find the area ID for the selected station
    let stationAreaId = null
    for (const areaId of visibleStations.value) {
      const areaStations = getStationsForArea(areaId)
      if (areaStations.some(s => s.id === selectedStationForIsochrone.value?.id)) {
        stationAreaId = areaId
        break
      }
    }
    
    if (stationAreaId) {
      await loadIsochronesForStation(selectedStationForIsochrone.value, stationAreaId)
    } else {
      // Fallback to calculated circles if area not found
      generateIsochroneCircles(selectedStationForIsochrone.value)
    }
  }
}

// Function to calculate bounds for an area
const calculateAreaBounds = (area: any): [[number, number], [number, number]] => {
  const radiusKm = area.diameter / 2000 // Convert diameter from meters to radius in km
  const lat = area.latitude
  const lng = area.longitude
  
  // Calculate approximate bounds using radius
  // 1 degree latitude ≈ 111 km
  // 1 degree longitude varies by latitude: ≈ 111 * cos(latitude) km
  const latDelta = radiusKm / 111
  const lngDelta = radiusKm / (111 * Math.cos(lat * Math.PI / 180))
  
  // Add some padding (20% extra) to prevent hitting the exact bounds
  const padding = 0.2
  const paddedLatDelta = latDelta * (1 + padding)
  const paddedLngDelta = lngDelta * (1 + padding)
  
  const southWest: [number, number] = [lat - paddedLatDelta, lng - paddedLngDelta]
  const northEast: [number, number] = [lat + paddedLatDelta, lng + paddedLngDelta]
  
  return [southWest, northEast]
}

// Function to apply area constraints to the map
const applyAreaConstraints = (area: any) => {
  if (mapRef.value?.leafletObject) {
    const bounds = calculateAreaBounds(area)
    maxBounds.value = bounds
    areaBoundsActive.value = true
    
    // Set the max bounds on the Leaflet map
    mapRef.value.leafletObject.setMaxBounds(bounds)
    
    console.log(`🔒 Applied bounds constraint for area: ${area.name}`, bounds)
  }
}

// Function to remove area constraints from the map
const removeAreaConstraints = () => {
  if (mapRef.value?.leafletObject) {
    maxBounds.value = undefined
    areaBoundsActive.value = false
    minZoom.value = undefined // Remove minimum zoom constraint
    
    // Remove the max bounds from the Leaflet map completely
    const leafletMap = mapRef.value.leafletObject as any 
    leafletMap.setMaxBounds(null)
    
    console.log('🔓 Removed bounds and zoom constraints from map')
  }
}

// Initialize on mount
onMounted(async () => {
  console.log('🚀 Component mounted')
  
  // Load areas first
  await loadAreas()
  
  // Check if we need to center on a specific area
  if (props.areaId && areas.value.length > 0) {
    const targetArea = areas.value.find(area => area.id === props.areaId)
    
    if (targetArea) {
      console.log(`🎯 Centering map on area: ${targetArea.name} (${props.areaId})`)
      
      // Set initial center to the area coordinates
      initialCenter.value = [targetArea.latitude, targetArea.longitude]
      
      // Calculate zoom level to show the entire area circle
      // The diameter is in kilometers, we need to convert to appropriate zoom
      // Zoom calculation: larger diameter needs lower zoom (more zoomed out)
      const diameterKm = targetArea.diameter/1000
      const calculatedZoomLevel = Math.max(8, Math.min(16, 16 - Math.log2(diameterKm)))
      
      // Set this as the initial zoom level and minimum zoom constraint
      zoom.value = calculatedZoomLevel
      minZoom.value = calculatedZoomLevel // Don't allow zooming out below this level
      
      // Wait a bit for the map to be ready, then set view and apply constraints
      setTimeout(() => {
        if (mapRef.value?.leafletObject) {
          console.log(`🗺️ Setting area map view: ${targetArea.latitude}, ${targetArea.longitude}, zoom: ${calculatedZoomLevel}, minZoom: ${calculatedZoomLevel}`)
          mapRef.value.leafletObject.setView([targetArea.latitude, targetArea.longitude], calculatedZoomLevel)
          
          // Apply area bounds constraints
          applyAreaConstraints(targetArea)
        }
      }, 100)
      
      return // Skip location-based centering when area is specified
    } else {
      console.warn(`⚠️ Area with ID "${props.areaId}" not found`)
    }
  }
  
  // Try to get user's current location (fallback behavior)
  await getLocation()
  
  console.log('📍 Initial location check:', currentLocation.value)
  
  if (currentLocation.value) {
    const lat = currentLocation.value.lat
    const lng = currentLocation.value.lng
    console.log(`🎯 Setting initial center to user location: ${lat}, ${lng}`)
    
    initialCenter.value = [lat, lng]
    
    // Wait a bit for the map to be ready, then set view
    setTimeout(() => {
      if (mapRef.value?.leafletObject) {
        console.log('🗺️ Setting initial map view to user location')
        mapRef.value.leafletObject.setView([lat, lng], 13)
        
        // Don't apply area constraints when using current location
        removeAreaConstraints()
      }
    }, 100)
  } else {
    // If no current location and no area ID, ensure no area constraints are applied
    setTimeout(() => {
      removeAreaConstraints()
    }, 100)
  }
})

// Cleanup debounce timer on unmount
onUnmounted(() => {
  if (proximityLevelDebounceTimer) {
    clearTimeout(proximityLevelDebounceTimer)
    proximityLevelDebounceTimer = null
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

.proximity-level-toolbar {
  position: absolute;
  bottom: 20px;
  left: 50%;
  transform: translateX(-50%);
  z-index: 1000;
  background: white;
  border-radius: 8px;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
  border: 1px solid #e5e7eb;
  min-width: 200px;
}

.proximity-level-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 16px;
  border-bottom: 1px solid #f3f4f6;
}

.proximity-level-title {
  font-size: 14px;
  font-weight: 600;
  color: #374151;
}

.proximity-level-toggle {
  background: none;
  border: none;
  cursor: pointer;
  padding: 4px;
  border-radius: 4px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #6b7280;
  transition: all 0.2s ease;
}

.proximity-level-toggle:hover {
  background-color: #f3f4f6;
  color: #374151;
}

.proximity-level-toggle--active {
  color: #8b5cf6;
}

.proximity-level-selector {
  padding: 12px 16px;
}

.proximity-level-slider-container {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.proximity-level-slider-labels {
  display: flex;
  justify-content: space-between;
  font-size: 12px;
  color: #6b7280;
  font-weight: 500;
  margin-bottom: -4px;
}

.slider-label {
  text-align: center;
  min-width: 40px;
}

.proximity-level-slider {
  width: 100%;
  height: 6px;
  border-radius: 3px;
  background: #e5e7eb;
  outline: none;
  appearance: none;
  cursor: pointer;
}

.proximity-level-slider::-webkit-slider-thumb {
  appearance: none;
  width: 20px;
  height: 20px;
  border-radius: 50%;
  background: #8b5cf6;
  cursor: pointer;
  border: 3px solid white;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.2);
  transition: all 0.2s ease;
}

.proximity-level-slider::-webkit-slider-thumb:hover {
  transform: scale(1.1);
  background: #7c3aed;
  box-shadow: 0 4px 8px rgba(0, 0, 0, 0.3);
}

.proximity-level-slider::-moz-range-thumb {
  width: 20px;
  height: 20px;
  border-radius: 50%;
  background: #8b5cf6;
  cursor: pointer;
  border: 3px solid white;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.2);
  transition: all 0.2s ease;
}

.proximity-level-slider::-moz-range-thumb:hover {
  transform: scale(1.1);
  background: #7c3aed;
  box-shadow: 0 4px 8px rgba(0, 0, 0, 0.3);
}

.proximity-level-slider::-moz-range-track {
  height: 6px;
  border-radius: 3px;
  background: #e5e7eb;
  border: none;
}

.proximity-level-current {
  text-align: center;
  font-size: 13px;
  color: #374151;
  padding: 8px 12px;
  background: #f8f9fa;
  border-radius: 6px;
  border: 1px solid #e9ecef;
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

.area-bounds-indicator {
  position: absolute;
  top: 80px;
  right: 20px;
  z-index: 1000;
  background: rgba(139, 92, 246, 0.95);
  color: white;
  border-radius: 8px;
  padding: 8px 12px;
  font-size: 12px;
  font-weight: 500;
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.15);
  display: flex;
  align-items: center;
  gap: 6px;
  backdrop-filter: blur(4px);
  border: 1px solid rgba(255, 255, 255, 0.2);
  animation: fadeInSlide 0.3s ease-out;
}

.bounds-icon {
  font-size: 14px;
}

.bounds-text {
  white-space: nowrap;
}

@keyframes fadeInSlide {
  from {
    opacity: 0;
    transform: translateX(20px);
  }
  to {
    opacity: 1;
    transform: translateX(0);
  }
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
  /* grey when stations are not shown */
  background: #6f7583; /* gray-500 */
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
  background: #4b5563; /* darker gray on hover */
  transform: translateY(-1px);
}

.station-toggle-btn--active {
  /* green when stations are shown */
  background: #22C55E; /* success green */
}

.station-toggle-btn--active:hover:not(:disabled) {
  background: #16a34a; /* darker green */
}

.station-toggle-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
  transform: none;
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

/* Proximity Toggle Button Styles */
.proximity-toggle-section {
  margin-top: 15px;
  padding-top: 10px;
  border-top: 1px solid #eee;
}

.proximity-toggle-btn {
  /* grey when proximity is not shown */
  background: #6f7583; /* gray-500 */
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

.proximity-toggle-btn:hover:not(:disabled) {
  background: #4b5563; /* darker gray on hover */
  transform: translateY(-1px);
}

.proximity-toggle-btn--active {
  /* purple when proximity is shown */
  background: #8b5cf6; /* current purple */
}

.proximity-toggle-btn--active:hover:not(:disabled) {
  background: #7c3aed; /* darker purple */
}

.proximity-toggle-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
  transform: none;
}

.proximity-count {
  margin-top: 6px;
  font-size: 12px;
  color: #666;
  font-style: italic;
}

.proximity-error {
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

/* Reverse Geocoding Info Styles */
.reverse-geocoding-info {
  margin-top: 10px;
}

.location-address {
  font-size: 12px;
}

.location-address strong {
  color: #333;
  font-size: 12px;
}

.address-loading {
  color: #6b7280;
  font-style: italic;
  margin-top: 4px;
  font-size: 11px;
}

.address-text {
  color: #374151;
  margin-top: 4px;
  font-size: 11px;
  line-height: 1.3;
  word-wrap: break-word;
}

.address-error {
  color: #dc3545;
  font-style: italic;
  margin-top: 4px;
  font-size: 11px;
}

/* Welcome Popup Styles */
/* moved to components/WelcomePopup.vue */

/* Area Controls Layout */
.area-controls {
  margin-top: 12px;
  display: grid;
  grid-template-columns: 1fr;
  gap: 8px;
}

.controls-row--buttons {
  display: flex;
  gap: 8px;
  align-items: center;
}

.controls-row--buttons .station-toggle-btn,
.controls-row--buttons .proximity-toggle-btn {
  flex: 1 1 auto;
  min-width: 120px;
}

.controls-row--station-info,
.controls-row--proximity-info {
  font-size: 13px;
  color: #666;
}

/* ensure errors keep their styling */
.controls-row--station-info .station-error,
.controls-row--proximity-info .proximity-error {
  margin: 0;
}
</style>
