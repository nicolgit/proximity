<template>
  <div class="welcome-popup-overlay">
    <div class="welcome-popup">
      <div class="welcome-header">
        <h2>🗺️ The Proximity project!</h2>
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
            The following metro areas are currently available in <b>{{ country }}</b>:
            <span v-for="(area) in areas" :key="area.id" class="area-item">
              <b 
                @click="navigateToArea(area.id)"
                class="clickable-area"
              >{{ area.name }}</b>,
            </span>
            <br /><br />Click on the area name to start!
          </p>
        <p v-else-if="areas && areas.length === 0" >No areas available at the moment</p>
        <div class="welcome-actions">
          <button @click="onOpenGitHub" class="welcome-btn welcome-btn--secondary">
            📱 view on GitHub
          </button>
          <button 
            v-if="areas && areas.length === 1"
            @click="navigateToArea(areas[0].id)" 
            class="welcome-btn welcome-btn--primary"
          >
            🗺️ start exploring!
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { defineProps, defineEmits } from 'vue'
import { useRouter } from 'vue-router'
import type { Area } from '@/types'

const router = useRouter()

// defineProps used to declare the component props for the template; avoid assigning to an unused variable
const props = defineProps<{
  areas?: Array<Area>
  isAreasLoading?: boolean
  areasError?: string | null
  country?: string
}>()

const emit = defineEmits<{
  (e: 'areaSelected', areaId: string): void
}>()

const navigateToArea = (areaId: string) => {
  router.push(`/${props.country}/${areaId}`)
  // Emit areaSelected event to notify parent
  emit('areaSelected', areaId)
}

const onOpenGitHub = () => {
  window.open('https://github.com/nicolgit/proximity', '_blank')
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
