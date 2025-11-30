<template>
  <div v-if="location" class="reverse-geocoding-info">
    <hr style="margin: 10px 0; border: none; border-top: 1px solid #eee;">
    <div class="location-address">
      <strong>📍 Address:</strong>
      <div v-if="isLoading" class="address-loading">Loading address...</div>
      <div v-else-if="address" class="address-text">{{ address }}</div>
      <div v-else class="address-error">Address not found</div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { LocationSearchService } from '@/services/LocationSearchService'
import { ref, watch } from 'vue'

interface Props {
  location: [number, number] | null
}

const props = defineProps<Props>()

const address = ref('')
const isLoading = ref(false)

// Watch for location changes and perform reverse geocoding
watch(
  () => props.location,
  async (newLocation) => {
    if (!newLocation) {
      address.value = ''
      isLoading.value = false
      return
    }

    const [lat, lng] = newLocation
    address.value = ''
    isLoading.value = true

    try {
      const result = await LocationSearchService.reverseGeocode(lat, lng)
      if (result && result.display_name) {
        address.value = result.display_name
      } else {
        address.value = 'Address not found'
      }
    } catch (error) {
      console.error('Error performing reverse geocoding:', error)
      address.value = 'Failed to load address'
    } finally {
      isLoading.value = false
    }
  },
  { immediate: true }
)
</script>

<style scoped>
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
</style>