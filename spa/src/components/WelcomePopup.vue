<template>
  <div class="welcome-popup-overlay">
    <div class="welcome-popup">
      <div class="welcome-header">
        <h2>🗺️ The Proximity project!</h2>
        <button @click="onClose" class="welcome-close-btn" type="button">
          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <line x1="18" y1="6" x2="6" y2="18"></line>
            <line x1="6" y1="6" x2="18" y2="18"></line>
          </svg>
        </button>
      </div>

      <div class="welcome-content">
        <p>Welcome to the Proximity project!</p>
        <p v-if="areas && areas.length === 1">
          A tool that helps you understand how far you are from a metro, train or tram stop in the <b>{{ areas[0].name }}</b> metro area!
        </p>
        <p v-else-if="areas && areas.length !== 1">a tool that helps you understand how far you are from a metro, train or tram stop!</p>
          <p v-if="isAreasLoading">Loading available areas... ⏳</p>
          <p v-else-if="areasError">Error loading areas: {{ areasError }}</p>
          <p v-else-if="areas && areas.length > 1">
            Following metro areas are currently available:
            <span v-for="(area, index) in areas" :key="area.id" class="area-item">
              &nbsp;<button 
                @click="toggleAreaSelection(area.id)"
                :class="['area-toggle-btn', { selected: selectedAreas.includes(area.id), disabled: !canSelectArea(area.id) }]"
                :disabled="!canSelectArea(area.id)"
                type="button"
              >
                {{ selectedAreas.includes(area.id) ? '✓' : '○' }}
              </button>
              <b 
                v-if="areas.length > 1" 
                @click="navigateToArea(area.id)"
                class="clickable-area"
              >{{ area.name }}</b>
              <b v-else>{{ area.name }}</b>
            </span>
            <br /><br />Select up to 3 areas ({{ selectedAreas.length }}/3) or click on area names to navigate directly. You must select at least one area to start exploring!
          </p>
        <p v-else-if="areas && areas.length === 0" >No areas available at the moment</p>
        <div class="welcome-actions">
          <button @click="onOpenGitHub" class="welcome-btn welcome-btn--secondary">
            📱 view on GitHub
          </button>
          <button 
            @click="onClose" 
            :class="['welcome-btn', 'welcome-btn--primary', { 'disabled': !canStartExploring }]"
            :disabled="!canStartExploring"
          >
            🗺️ start exploring the Map!
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { defineProps, defineEmits, ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import type { Area } from '@/types'

const router = useRouter()

// defineProps used to declare the component props for the template; avoid assigning to an unused variable
const props = defineProps<{
  areas?: Array<Area>
  isAreasLoading?: boolean
  areasError?: string | null
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'close', filteredAreas: Area[]): void
}>()

// Reactive variable to store selected areas (max 3)
const selectedAreas = ref<string[]>([])

// Computed property to check if we can start exploring (has selected areas)
const canStartExploring = computed(() => selectedAreas.value.length > 0)

// Function to toggle area selection (max 3 areas)
const toggleAreaSelection = (areaId: string) => {
  const index = selectedAreas.value.indexOf(areaId)
  if (index > -1) {
    // Remove area from selection
    selectedAreas.value.splice(index, 1)
  } else if (selectedAreas.value.length < 3) {
    // Add area to selection only if less than 3 are selected
    selectedAreas.value.push(areaId)
  }
}

// Check if an area can be selected (not selected and under limit)
const canSelectArea = (areaId: string) => {
  return selectedAreas.value.includes(areaId) || selectedAreas.value.length < 3
}

const onClose = () => {
  // Only allow closing if areas are selected
  if (props.areas && selectedAreas.value.length > 0) {
    const filteredAreas = props.areas.filter(area => selectedAreas.value.includes(area.id))
    console.log('Filtered areas to selected only:', filteredAreas)
    emit('close', filteredAreas)
  } else {
    console.log('Cannot close: No areas selected')
    // Do nothing - require selection before allowing close
  }
}
const onOpenGitHub = () => {
  window.open('https://github.com/nicolgit/proximity', '_blank')
}

const navigateToArea = (areaId: string) => {
  router.push(`/italy/${areaId}`)
  // When navigating directly to an area, don't filter - just close
  emit('close') // Close the popup after navigation
}
</script>

<style scoped>
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

.area-item {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.area-toggle-btn {
  background: none;
  border: 1px solid #ddd;
  border-radius: 50%;
  width: 20px;
  height: 20px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  font-size: 10px;
  transition: all 0.2s ease;
  color: #666;
}

.area-toggle-btn:hover {
  border-color: #007bff;
  color: #007bff;
}

.area-toggle-btn.selected {
  background-color: #007bff;
  border-color: #007bff;
  color: white;
}

.area-toggle-btn.disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.area-toggle-btn.disabled:hover {
  border-color: #ddd;
  color: #666;
}

.clickable-area {
  cursor: pointer;
  color: #007bff;
  transition: color 0.2s ease;
}

.clickable-area:hover {
  color: #0056b3;
  text-decoration: underline;
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

.welcome-btn--primary.disabled {
  background-color: #ccc;
  color: #666;
  cursor: not-allowed;
  opacity: 0.6;
}

.welcome-btn--primary.disabled:hover {
  background-color: #ccc;
  transform: none;
}

.welcome-btn--secondary {
  background-color: #ffd700;
  color: #333;
  border: 1px solid #ffcc00;
}

.welcome-btn--secondary:hover {
  background-color: #ffcc00;
  color: #000;
  transform: translateY(-1px);
}

@keyframes fadeIn {
  from { opacity: 0; }
  to { opacity: 1; }
}

@keyframes slideUp {
  from { opacity: 0; transform: translateY(20px); }
  to { opacity: 1; transform: translateY(0); }
}

@media (max-width: 480px) {
  .welcome-popup { margin: 10px; border-radius: 12px; }
  .welcome-header { padding: 20px 20px 12px; }
  .welcome-header h2 { font-size: 20px; }
  .welcome-content { padding: 20px; }
  .welcome-actions { flex-direction: column; }
  .welcome-btn { min-width: auto; }
}
</style>
