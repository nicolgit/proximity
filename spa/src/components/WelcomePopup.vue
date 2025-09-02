<template>
  <div class="welcome-popup-overlay" @click="onOverlayClick">
    <div class="welcome-popup" @click.stop>
      <div class="welcome-header">
        <h2>🗺️ Welcome to Metro Proximity!</h2>
        <button @click="onClose" class="welcome-close-btn" type="button">
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
        <p v-else-if="areas && areas.length > 0">
          Following areas are currently available:
          <span v-for="(area, index) in areas" :key="area.id">
            📍<b>{{ area.name }}</b><span v-if="index < areas.length - 1">, </span>
          </span>
        </p>
        <p v-else>No areas available at the moment</p>

        <div class="welcome-actions">
          <button @click="onOpenGitHub" class="welcome-btn welcome-btn--secondary">
            📱 View on GitHub
          </button>
          <button @click="onClose" class="welcome-btn welcome-btn--primary">
            🗺️ start explore Map!
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { defineProps, defineEmits } from 'vue'

// defineProps used to declare the component props for the template; avoid assigning to an unused variable
defineProps<{
  areas?: Array<any>
  isAreasLoading?: boolean
  areasError?: string | null
}>()

const emit = defineEmits<{
  (e: 'close'): void
  (e: 'open-github'): void
}>()

const onOverlayClick = () => emit('close')
const onClose = () => emit('close')
const onOpenGitHub = () => emit('open-github')
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
