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
      @areaSelected="handleAreaSelected"
    />

    <!-- Area Proximity advanced tools -->
    <div class="proximity-toolbar">
      <div class="proximity-level-header">
        <span class="proximity-level-title">🏙️ advanced tools</span>
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
        <!-- Station Type Filter Segmented Button -->
        <div class="toolbar-section">
          <div class="toolbar-section-label">stations</div>
          <div class="segmented-control">
            <button 
              v-for="stationType in stationTypeOptions"
              :key="stationType.value"
              @click="selectStationType(stationType.value)"
              class="segmented-button"
              :class="{ 'segmented-button--active': selectedStationType === stationType.value }"
              :disabled="areas.length === 0 || isAreasLoading"
            >
              {{ stationType.icon }} {{ stationType.label }}
            </button>
          </div>
        </div>
        
        <!-- Range Toggle Segmented Button -->
        <div class="toolbar-section">
          <div class="toolbar-section-label">range</div>
          <div class="segmented-control">
            <button 
              @click="setIsochronesVisibility('none')"
              class="segmented-button"
              :class="{ 'segmented-button--active': !areAllIsochronesVisible }"
              :disabled="areas.length === 0 || isAreasLoading"
            >
              hide
            </button>
            <button 
              @click="setIsochronesVisibility(selectedStationType)"
              class="segmented-button"
              :class="{ 'segmented-button--active': areAllIsochronesVisible }"
              :disabled="areas.length === 0 || isAreasLoading"
            >
              show
            </button>
          </div>
        </div>
        <div v-if="areAllIsochronesVisible" class="proximity-level-slider-container">
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
          <div class="proximity-level-current" v-html="proximityLevelDescription">
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
      <!-- Loading state for map key -->
      <div v-if="isLoadingMapKey" class="map-loading">
        <div class="loading-spinner"></div>
        <p>Loading map...</p>
      </div>
      
      <!-- Error state for map key -->
      <div v-else-if="mapKeyError" class="map-error">
        <p>Failed to load map: {{ mapKeyError }}</p>
        <button @click="fetchMapKey" class="retry-button">Retry</button>
      </div>
      
      <!-- Map when key is loaded -->
      <l-map
        v-else-if="mapKey"
        ref="mapRef"
        v-model:zoom="zoom"
        :center="initialCenter"
        :min-zoom="minZoom"
        :use-global-leaflet="false"
        class="leaflet-map"
        @ready="onMapReady"
      >
        <!-- Azure Maps Tile Layer with grayscale styling -->
        <l-tile-layer
          :url="azureMapsUrl"
          :attribution="azureMapsAttribution"
          :tile-size="256"
          :max-zoom="19"
          layer-type="base"
          name="Azure Maps"
          class-name="azure-maps-grayscale"
        />
         <!-- Marker for selected location -->
         <l-marker
          v-if="selectedLocation"
          :lat-lng="selectedLocation"
          :icon="searchLocationIconSvg as any"
          @click="() => selectedLocationClickLocation = selectedLocation"
        >
          <l-popup>
            <div class="selected-location-popup">
              <div><strong>{{ selectedLocationName }}</strong></div>
              <ReverseGeocodingInfo :location="selectedLocationClickLocation" />
            </div>
          </l-popup>
        </l-marker>

        <!-- Marker for current location -->
        <l-marker
          v-if="currentLocation"
          :lat-lng="currentLocation"
          :icon="userLocationIconSvg as any"
          @click="() => {
            if (currentLocation) {
              const coords = currentLocation as any
              currentLocationClickLocation = Array.isArray(coords) 
                ? coords as [number, number]
                : [coords.lat, coords.lng]
            }
          }"
        >
          <l-popup>
            <div class="user-location-popup">
              <strong>🟢you're here🟢</strong>
              <ReverseGeocodingInfo 
                v-if="currentLocationClickLocation" 
                :location="currentLocationClickLocation" 
              />
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
          @click="onAreaClick"
        >
          <l-popup>
            <div class="area-popup" @click="closeAreaPopupOnOutsideClick($event)">
              <h3>📍 {{ area.name }} </h3>
              <ReverseGeocodingInfo :location="areaClickLocation" />
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
              <ReverseGeocodingInfo :location="isochroneClickLocation" />
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
              <ReverseGeocodingInfo :location="circleClickLocation" />
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
              <ReverseGeocodingInfo :location="stationIsochroneClickLocation" />
            </div>
          </l-popup>
        </l-geo-json>

        <!-- Station markers -->
        <l-marker
          v-for="station in allVisibleStations"
          :key="station.id"
          :lat-lng="[station.latitude, station.longitude]"
          :icon="getStationIcon(station.type) as any"
          @click="onStationClick(props.country, station, station.areaId)">
          <l-popup>
            <div class="station-popup" @click="closeAreaPopupOnOutsideClick($event)">
              <h4>
                <span v-if="station.type === 'station' ">🚇</span>
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
              <p>type: <strong>{{ 
                (station.type === 'station' || station.type === 'halt') ? 'Train/Metro Station' : 
                (station.type === 'tram_stop') ? 'Tram Stop' : 
                (station.type === 'trolleybus') ? 'Trolleybus Stop' : 'Unknown'
              }}</strong></p>
              <p>coords: <strong>{{ station.latitude.toFixed(4) }}, {{ station.longitude.toFixed(4) }}</strong> </p>
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
import { searchLocationIconSvg, userLocationIconSvg, stationIconSvg, tramStopIconSvg, trolleyStopIconSvg } from '@/utils/mapIcons'
import { getApiUrl, setMapKey, getMapKey } from '@/config/env'
import { MapKeyService } from '@/services/MapKeyService'
import { LCircle, LMap, LMarker, LPopup, LGeoJson, LTileLayer } from '@vue-leaflet/vue-leaflet'
import { onMounted, ref, computed, onUnmounted, watch } from 'vue'
import { useRoute } from 'vue-router'
import WelcomePopup from '@/components/WelcomePopup.vue'
import ReverseGeocodingInfo from '@/components/ReverseGeocodingInfo.vue'

// Props
interface Props {
  country: string
  area?: string
  areaid?: string // Keep for backward compatibility
}

const props = withDefaults(defineProps<Props>(), {
  country: "",
  area: undefined,
  areaid: undefined
})

// Route setup
const route = useRoute()

// Map setup
const mapRef = ref<InstanceType<typeof LMap> | null>(null)
const zoom = ref(7)
const minZoom = ref<number | undefined>(undefined) // Minimum zoom level when targeting a specific area
const initialCenter = ref<[number, number]>([41.9028, 12.4964]) // Default to Rome
const selectedLocation = ref<[number, number] | null>(null)
const selectedLocationName = ref('')

// Isochrone click state for reverse geocoding
const isochroneClickLocation = ref<[number, number] | null>(null)

// Circle click state for reverse geocoding
const circleClickLocation = ref<[number, number] | null>(null)

// Station isochrone click state for reverse geocoding
const stationIsochroneClickLocation = ref<[number, number] | null>(null)

// Selected location click state for reverse geocoding
const selectedLocationClickLocation = ref<[number, number] | null>(null)

// Current location click state for reverse geocoding
const currentLocationClickLocation = ref<[number, number] | null>(null)

// Area click state for reverse geocoding
const areaClickLocation = ref<[number, number] | null>(null)

// Map key state
const mapKey = ref<string | null>(null)
const isLoadingMapKey = ref(true)
const mapKeyError = ref<string | null>(null)

// Map configuration
// Azure Maps tile layer configuration for grayscale_light style
const azureMapsUrl = computed(() => {
  if (!mapKey.value) {
    return ''
  }
  return `https://atlas.microsoft.com/map/tile?subscription-key=${mapKey.value}&api-version=2024-04-01&tilesetId=microsoft.base.road&zoom={z}&x={x}&y={y}&tileSize=256`
})
const azureMapsAttribution = '© 2024 Microsoft Corporation, © 2024 TomTom, © OpenStreetMap contributors'

// Fetch map key on component mount
const fetchMapKey = async () => {
  try {
    isLoadingMapKey.value = true
    mapKeyError.value = null
    
    // Check if key is already cached
    const cachedKey = getMapKey()
    if (cachedKey) {
      mapKey.value = cachedKey
      isLoadingMapKey.value = false
      return
    }
    
    // Fetch key from API
    const key = await MapKeyService.fetchMapKey()
    mapKey.value = key
    setMapKey(key) // Cache the key
    isLoadingMapKey.value = false
  } catch (error) {
    console.error('Failed to fetch map key:', error)
    mapKeyError.value = error instanceof Error ? error.message : 'Failed to load map'
    isLoadingMapKey.value = false
  }
}

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

// Computed area ID - prioritize new route params, fallback to legacy areaid prop
const currentAreaId = computed(() => {
  return props.area || props.areaid
})

const {
  areas,
  isLoading: isAreasLoading,
  error: areasError,
  load: loadAreas
} = useAreas(props.country)

const {
  loadStations,
  getStationsForArea
} = useStations()

// Search UI state
const isSearchFocused = ref(false)
const highlightedIndex = ref(-1)

// Welcome popup state
const showWelcomePopup = ref(true)

// Stations state
const visibleStations = ref<Set<string>>(new Set())
const showAllStations = ref(false)

// Station type filtering
type StationType = 'all' | 'station' | 'trolleybus' | 'tram_stop' | 'none'
const selectedStationType = ref<StationType>('all')
const stationTypeOptions = ref([
  { value: 'all' as const, label: 'all', icon: null },
  { value: 'station' as const, label: 'station', icon: '🚇' },
  { value: 'trolleybus' as const, label: 'trolley bus', icon: '🚐' },
  { value: 'tram_stop' as const, label: 'tram', icon: '🚊' },
  { value: 'none' as const, label: 'none', icon: '🚫' }
])

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
const selectedProximityLevel = ref(15)
const pendingProximityLevel = ref(15) // For debounced updates

// Debounced proximity level update
let proximityLevelDebounceTimer: number | null = null

// Function to initialize map for a specific area
const initializeMapForArea = async (country: string, areaId: string | undefined) => {
  // Load areas - if areaId is specified, load that specific area, otherwise load all
  if (areaId) {
    await loadAreas(areaId)
  } else if (areas.value.length === 0) {
    await loadAreas()
  }

  if (!areaId) {
    // No area specified, use current location or default
    await getLocation()
    
    if (currentLocation.value) {
      const lat = currentLocation.value.lat
      const lng = currentLocation.value.lng
      console.log(`🎯 Setting initial center to user location: ${lat}, ${lng}`)
      
      initialCenter.value = [lat, lng]
      
      setTimeout(() => {
        if (mapRef.value?.leafletObject) {
          console.log('🗺️ Setting initial map view to user location')
          mapRef.value.leafletObject.setView([lat, lng], 13)
        }
      }, 100)
    } else {
      // No user location, just remove constraints
    }
    return
  }

  const targetArea = areas.value.find(area => area.id === areaId)
  
  if (targetArea) {
    console.log(`🎯 Centering map on area: ${targetArea.name} (${areaId})`)
    
    // Set initial center to the area coordinates
    initialCenter.value = [targetArea.latitude, targetArea.longitude]
    
    // Calculate zoom level to show the entire area circle
    const diameterKm = targetArea.diameter/1000
    const calculatedZoomLevel = Math.max(8, Math.min(16, 16 - Math.log2(diameterKm)))
    
    // Set this as the initial zoom level and minimum zoom constraint
    zoom.value = calculatedZoomLevel
    minZoom.value = calculatedZoomLevel
    
    // Wait a bit for the map to be ready, then set view
    setTimeout(() => {
      if (mapRef.value?.leafletObject) {
        console.log(`🗺️ Setting area map view: ${targetArea.latitude}, ${targetArea.longitude}, zoom: ${calculatedZoomLevel}, minZoom: ${calculatedZoomLevel}`)
        mapRef.value.leafletObject.setView([targetArea.latitude, targetArea.longitude], calculatedZoomLevel)
      }
    }, 100)

    // Automatically show stations and isochrones for the target area
    setTimeout(async () => {
      console.log(`🚇 Automatically loading stations and isochrones for area: ${targetArea.name}`)
      
      // Load stations if not already visible
      if (!visibleStations.value.has(areaId)) {
        if (getStationsForArea(areaId).length === 0) {
          await loadStations(country, areaId)
        }
        visibleStations.value.add(areaId)
        console.log(`✅ Stations loaded for area: ${targetArea.name}`)
      }
      
      // Load area isochrones if not already visible
      if (!visibleAreaIsochrones.value.has(areaId)) {
        await loadAreaIsochrones(country, areaId)
        console.log(`✅ Isochrones loaded for area: ${targetArea.name}`)
      }
    }, 200) // Slight delay to ensure map is ready
  } else {
    console.warn(`⚠️ Area with ID "${areaId}" not found`)
    // Fallback to user location
    await getLocation()
    if (currentLocation.value) {
      const lat = currentLocation.value.lat
      const lng = currentLocation.value.lng
      initialCenter.value = [lat, lng]
      
      setTimeout(() => {
        if (mapRef.value?.leafletObject) {
          mapRef.value.leafletObject.setView([lat, lng], 13)
        }
      }, 100)
    }
  }
}

// Watch for route parameter changes
watch(
  () => route.params,
  async (newParams, oldParams) => {
    const newAreaId = (newParams.area || newParams.areaid) as string | undefined
    const oldAreaId = (oldParams?.area || oldParams?.areaid) as string | undefined
    
    if (newAreaId !== oldAreaId) {
      console.log(`🔄 Route changed from area "${oldAreaId}" to "${newAreaId}"`)
      await initializeMapForArea(props.country, newAreaId)
    }
  },
  { deep: true }
)

// Initialize on mount
onMounted(async () => {
  console.log('🚀 Component mounted')
  
  // Fetch map key first
  await fetchMapKey()
  
  // Use the shared initialization function
  await initializeMapForArea(props.country, currentAreaId.value)
})

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

// Computed property for all visible stations across all areas, filtered by type
const allVisibleStations = computed(() => {
  const stations: (Station & { areaId: string })[] = []
  for (const areaId of visibleStations.value) {
    const areaStations = getStationsForArea(areaId).map(station => ({
      ...station,
      areaId
    }))
    stations.push(...areaStations)
  }
  
  // Filter by selected station type
  if (selectedStationType.value === 'all') {
    return stations
  }
  
  if (selectedStationType.value === 'none') {
    return []
  }
  
  return stations.filter(station => {
    switch (selectedStationType.value) {
      case 'station':
        return station.type === 'station' || station.type === 'halt'
      case 'tram_stop':
        return station.type === 'tram_stop'
      case 'trolleybus':
        return station.type === 'trolleybus'
      default:
        return true
    }
  })
})

// Computed property to check if all areas have stations visible
const areAllStationsVisible = computed(() => {
  if (areas.value.length === 0) return false
  return areas.value.every(area => visibleStations.value.has(area.id))
})

// Computed property to check if all areas have isochrones visible
const areAllIsochronesVisible = computed(() => {
  if (areas.value.length === 0) return false
  return areas.value.every(area => visibleAreaIsochrones.value.has(area.id))
})

// Watch for changes in visible stations to update showAllStations flag
watch(areAllStationsVisible, (newValue) => {
  showAllStations.value = newValue
}, { immediate: true })

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

// Computed property to get descriptive text based on station type
const proximityLevelDescription = computed(() => {
  const minutes = pendingProximityLevel.value
  
  switch (selectedStationType.value) {
    case 'station':
      return `within <strong>${minutes} minutes</strong> from a train station`
    case 'trolleybus':
      return `within <strong>${minutes} minutes</strong> from a trolleybus stop`
    case 'tram_stop':
      return `within <strong>${minutes} minutes</strong> from a tram stop`
    case 'none':
      return `within <strong>${minutes} minutes</strong> (no station filter)`
    case 'all':
    default:
      return `within <strong>${minutes} minutes</strong> from any station`
  }
})

// Toggle all stations functionality
const toggleAllStations = async () => {
  if (areAllStationsVisible.value) {
    // Hide all stations
    visibleStations.value.clear()
  } else {
    // Show stations for all areas
    for (const area of areas.value) {
      if (!visibleStations.value.has(area.id)) {
        // Load stations if not already loaded
        if (getStationsForArea(area.id).length === 0) {
          await loadStations(props.country, area.id)
        }
        visibleStations.value.add(area.id)
      }
    }
  }
}

// Set isochrones visibility functionality
const setIsochronesVisibility = async (stationType: StationType) => {
  if (stationType === 'none') {
    // Hide all isochrones
    visibleAreaIsochrones.value.clear()
    // Remove all from map
    areaIsochroneGeoJson.value = []
    // Clear any errors
    areaIsochroneErrors.value.clear()
  } else {
    // Update the selected station type
    selectedStationType.value = stationType
    
    // Show isochrones for all areas with the specified station type
    for (const area of areas.value) {
      if (!visibleAreaIsochrones.value.has(area.id)) {
        await loadAreaIsochrones(props.country, area.id)
      } else {
        // Reload existing isochrones with new station type
        await loadAreaIsochrones(props.country, area.id)
      }
    }
  }
}

// Station type selection functionality
const selectStationType = async (type: StationType) => {
  selectedStationType.value = type
  
  // Auto-load stations for all areas when switching types (if not already visible)
  // Skip auto-loading for 'none' since we don't want to show any stations
  if (type !== 'all' && type !== 'none' && !areAllStationsVisible.value) {
    toggleAllStations()
  }
  
  // Reload isochrones if they are currently visible, since different station types use different APIs
  if (areAllIsochronesVisible.value) {
    // Get all currently visible area isochrones
    const visibleAreaIds = Array.from(visibleAreaIsochrones.value)
    
    // Reload isochrones for all visible areas with the new station type
    for (const areaId of visibleAreaIds) {
      await loadAreaIsochrones(props.country, areaId)
    }
  }
}

// Helper function to get icon for station type
const getStationIcon = (type: string) => {
  if (type === 'station' || type === 'halt') {
    return stationIconSvg
  } else if (type === 'tram_stop') {
    return tramStopIconSvg
  } else if (type === 'trolleybus') {
    return trolleyStopIconSvg
  }
  return stationIconSvg // fallback
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

// Function to get the appropriate isochrone API endpoint based on station type
const getIsochroneApiEndpoint = (country: string, areaId: string, timeInterval: number, stationType: StationType): string => {
  const baseUrl = `/area/${country}/${areaId}/isochrone`
  
  switch (stationType) {
    case 'station':
      return `${baseUrl}/station/${timeInterval}`
    case 'trolleybus':
      return `${baseUrl}/trolleybus/${timeInterval}`
    case 'tram_stop':
      return `${baseUrl}/tram_stop/${timeInterval}`
    case 'all':
    case 'none':
    default:
      return `${baseUrl}/${timeInterval}`
  }
}

// Function to load area isochrones from API
const loadAreaIsochrones = async (country: string, areaId: string) => {
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
        // Use the appropriate API endpoint based on selected station type
        const endpoint = getIsochroneApiEndpoint(country, areaId, timeInterval, selectedStationType.value)
        const response = await fetch(getApiUrl(endpoint))
        
        if (!response.ok) {
          throw new Error(`HTTP error! status: ${response.status}`)
        }
        
        const geojson = await response.json()
        return { timeInterval, success: true, data: geojson }
      } catch (error) {
        console.error(`Error fetching area isochrone for ${timeInterval} minutes (station type: ${selectedStationType.value}):`, error)
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
const onStationClick = async (country: string, station: Station, areaId: string) => {
  // If clicking the same station, toggle off the isochrones
  if (selectedStationForIsochrone.value?.id === station.id) {
    selectedStationForIsochrone.value = null
    isochroneCircles.value = []
    isochroneGeoJson.value = []
  } else {
    // Show isochrones for the new station
    selectedStationForIsochrone.value = station
    await loadIsochronesForStation(country, station, areaId)
  }
}

// Function to load isochrones from API or fallback to calculated circles
const loadIsochronesForStation = async (country: string, station: Station, areaId: string) => {
  // Only load time intervals up to the selected proximity level
  const timeIntervals = proximityLevelOptions.value.filter(level => level <= selectedProximityLevel.value)
  const baseColor = (station.type === 'station' || station.type === 'halt') ? '#22c55e' : 
                   (station.type === 'tram_stop') ? '#eab308' : 
                   (station.type === 'trolleybus') ? '#3b82f6' : '#22c55e' 
                   // green for station/halt, yellow for tram_stop, blue for trolleybus
  
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
        getApiUrl(`/area/${country}/${stationArea.id}/station/${station.id}/isochrone/${time}`)
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
  // Azure Maps tiles are now added via LTileLayer component in template
  console.log('✅ Azure Maps tile layer ready with grayscale_light style')
}

const onIsochroneClick = async (event: any) => {
  const { lat, lng } = event.latlng
  
  // Set isochrone click location
  isochroneClickLocation.value = [lat, lng]
}

const onCircleClick = async (event: any) => {
  const { lat, lng } = event.latlng
  
  // Set circle click location
  circleClickLocation.value = [lat, lng]
}

const onStationIsochroneClick = async (event: any) => {
  const { lat, lng } = event.latlng
  
  // Set station isochrone click location
  stationIsochroneClickLocation.value = [lat, lng]
}

// Area click handler
const onAreaClick = async (event: any) => {
  const { lat, lng } = event.latlng
  console.log('Area clicked:', lat, lng)
  
  // Set area click location
  areaClickLocation.value = [lat, lng]
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
const handleAreaSelected = (areaId: string) => {
  console.log('🎯 Area selected for navigation:', areaId)
  // Just close the popup when an area is selected for navigation
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
    loadAreaIsochrones(props.country, areaId)
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
      await loadIsochronesForStation(props.country, selectedStationForIsochrone.value, stationAreaId)
    } else {
      // Fallback to calculated circles if area not found
      generateIsochroneCircles(selectedStationForIsochrone.value)
    }
  }
}

// Cleanup debounce timer on unmount
onUnmounted(() => {
  if (proximityLevelDebounceTimer) {
    clearTimeout(proximityLevelDebounceTimer)
    proximityLevelDebounceTimer = null
  }
})
</script>

<style scoped>
/* Map loading and error states */
.map-loading,
.map-error {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 100%;
  background: #f8f9fa;
  color: #495057;
}

.map-loading .loading-spinner {
  width: 32px;
  height: 32px;
  border: 3px solid #e9ecef;
  border-top: 3px solid #007bff;
  border-radius: 50%;
  animation: spin 1s linear infinite;
  margin-bottom: 1rem;
}

.map-error p {
  margin-bottom: 1rem;
  color: #dc3545;
}

.retry-button {
  background: #007bff;
  color: white;
  border: none;
  padding: 8px 16px;
  border-radius: 4px;
  cursor: pointer;
  font-size: 14px;
}

.retry-button:hover {
  background: #0056b3;
}

@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}

.map-view {
  height: 100vh;
  height: 100dvh; /* Dynamic viewport height for better mobile support */
  width: 100vw;
  position: relative;
}

/* Mobile-specific adjustments */
@supports (height: 100dvh) {
  .map-view {
    height: 100dvh;
  }
}

@media screen and (max-width: 768px) {
  .map-view {
    height: calc(100vh - env(safe-area-inset-top) - env(safe-area-inset-bottom));
    min-height: -webkit-fill-available;
    min-height: fill-available;
  }
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

/* Mobile adjustments for search container */
@media screen and (max-width: 768px) {
  .search-container {
    top: calc(env(safe-area-inset-top, 0px) + 10px);
    left: 10px;
    right: 10px;
    transform: none;
    min-width: unset;
    max-width: unset;
    width: calc(100vw - 20px);
  }
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

/* Mobile adjustments for location button */
@media screen and (max-width: 768px) {
  .location-btn {
    top: calc(env(safe-area-inset-top, 0px) + 80px);
    right: 15px;
    width: 45px;
    height: 45px;
  }
}

.proximity-toolbar {
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

/* Mobile adjustments for proximity toolbar */
@media screen and (max-width: 768px) {
  .proximity-toolbar {
    bottom: calc(env(safe-area-inset-bottom, 0px) + 15px);
    left: 10px;
    right: 10px;
    transform: none;
    min-width: unset;
    width: calc(100vw - 20px);
  }
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

.toolbar-section {
  border-bottom: 1px solid #f3f4f6;
  padding-bottom: 12px;
  margin-bottom: 12px;
}

.toolbar-section:last-child {
  border-bottom: none;
  padding-bottom: 0;
  margin-bottom: 0;
}

.toolbar-section-label {
  font-size: 12px;
  font-weight: 600;
  color: #6b7280;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  margin-bottom: 8px;
}

.toolbar-button {
  width: 100%;
  padding: 10px 12px;
  border: 1px solid #e5e7eb;
  border-radius: 6px;
  background: white;
  color: #374151;
  font-size: 13px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  transition: all 0.2s ease;
  font-weight: 500;
}

.toolbar-button:hover:not(:disabled) {
  background-color: #f9fafb;
  border-color: #d1d5db;
  color: #111827;
}

.toolbar-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.toolbar-button--active {
  background-color: #3b82f6;
  border-color: #3b82f6;
  color: white;
}

.toolbar-button--active:hover:not(:disabled) {
  background-color: #2563eb;
  border-color: #2563eb;
}

/* Segmented Control Styles */
.segmented-control {
  display: flex;
  border: 1px solid #e5e7eb;
  border-radius: 6px;
  overflow: hidden;
  background: white;
}

.segmented-button {
  flex: 1;
  padding: 8px 12px;
  border: none;
  background: white;
  color: #374151;
  font-size: 12px;
  cursor: pointer;
  transition: all 0.2s ease;
  font-weight: 500;
  border-right: 1px solid #e5e7eb;
  position: relative;
}

.segmented-button:last-child {
  border-right: none;
}

.segmented-button:hover:not(:disabled) {
  background-color: #f9fafb;
  color: #111827;
}

.segmented-button:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.segmented-button--active {
  background-color: #3b82f6;
  color: white;
  font-weight: 600;
}

.segmented-button--active:hover:not(:disabled) {
  background-color: #2563eb;
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

/* Ensure map container uses full viewport on mobile */
.map-container {
  height: 100vh;
  height: 100dvh;
  width: 100vw;
  position: relative;
}

@supports (height: 100dvh) {
  .map-container {
    height: 100dvh;
  }
}

@media screen and (max-width: 768px) {
  .map-container {
    height: calc(100vh - env(safe-area-inset-top) - env(safe-area-inset-bottom));
    min-height: -webkit-fill-available;
    min-height: fill-available;
  }
}

.user-location-popup {
  text-align: center;
  font-size: 14px;
  min-width: 200px;
}

.selected-location-popup {
  min-width: 200px;
}

.selected-location-popup > div:first-child {
  margin-bottom: 8px;
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

/* Azure Maps grayscale styling for grayscale_light appearance */
:deep(.azure-maps-grayscale) {
  filter: grayscale(100%) brightness(1.1) contrast(0.9);
  transition: filter 0.3s ease;
}

/* Maintain interactivity on hover */
:deep(.azure-maps-grayscale:hover) {
  filter: grayscale(90%) brightness(1.05) contrast(0.95);
}

/* Hide zoom controls on mobile devices */
@media screen and (max-width: 768px) {
  :deep(.leaflet-control-zoom) {
    display: none !important;
  }
}

</style>
