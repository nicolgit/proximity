<template>
  <div class="area-controls">
    <div class="controls-row controls-row--buttons">
      <button
        @click="emitToggleStations"
        class="btn"
        :class="{ 'btn--active': stationVisible }"
        :disabled="isLoadingStations"
      >
        <div v-if="isLoadingStations" class="loading-spinner-small"></div>
        <span v-else>
          {{ stationVisible ? '🚇 Hide Stations' : '🚇&nbsp;Show&nbsp;Stations' }}
        </span>
      </button>

      <button
        @click="emitToggleProximity"
        class="btn"
        :class="{ 'btn--active': proximityVisible }"
        :disabled="isLoadingProximity"
      >
        <div v-if="isLoadingProximity" class="loading-spinner-small"></div>
        <span v-else>
          {{ proximityVisible ? '🏙️ Hide Proximity' : '🏙️&nbsp;Show&nbsp;Proximity' }}
        </span>
      </button>
    </div>

    <div v-if="showStats" class="controls-row controls-row--proximity-info">
      <div v-if="stationError" class="station-error">Error: {{ stationError }}</div>
      <div v-else-if="stationVisible" class="station-count">{{ stationCount }} stations found</div>
      <div v-if="proximityError" class="proximity-error">Error: {{ proximityError }}</div>
      <div v-else-if="proximityVisible" class="proximity-count">{{ proximityCount }} proximity zones shown (up to {{ selectedProximityLevel }}min)</div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { defineProps, defineEmits } from 'vue'

const props = defineProps({
  areaId: { type: [String, Number], required: true },
  showStats: { type: Boolean, required: true },
  stationVisible: { type: Boolean, default: false },
  proximityVisible: { type: Boolean, default: false },
  stationCount: { type: Number, default: 0 },
  proximityCount: { type: Number, default: 0 },
  stationError: { type: String, default: '' },
  proximityError: { type: String, default: '' },
  isLoadingStations: { type: Boolean, default: false },
  isLoadingProximity: { type: Boolean, default: false },
  selectedProximityLevel: { type: Number, default: 0 }
})

const emit = defineEmits<{
  (e: 'toggle-stations', areaId: string | number): void
  (e: 'toggle-proximity', areaId: string | number): void
}>()

function emitToggleStations() {
  emit('toggle-stations', props.areaId)
}

function emitToggleProximity() {
  emit('toggle-proximity', props.areaId)
}
</script>

<style scoped>
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

.controls-row--buttons .btn {
  flex: 1 1 auto;
  min-width: 120px;
}

.controls-row--station-info,
.controls-row--proximity-info {
  font-size: 13px;
  color: #666;
}

.controls-row--station-info .station-error,
.controls-row--proximity-info .proximity-error {
  margin: 0;
}
</style>
