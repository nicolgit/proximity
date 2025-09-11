<template>
  <Transition name="area-bounds" appear>
    <div v-if="isVisible" class="area-bounds-indicator" :class="[positionClasses, variantClasses]">
      <div class="bounds-icon">
        <slot name="icon">🔒</slot>
      </div>
      <div class="bounds-text">
        <slot>
          Map limited to {{ areaName }}
        </slot>
      </div>
      <button 
        v-if="showCloseButton" 
        @click="$emit('close')"
        class="bounds-close-button"
        type="button"
        :title="closeButtonTitle"
      >
        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
          <line x1="18" y1="6" x2="6" y2="18"></line>
          <line x1="6" y1="6" x2="18" y2="18"></line>
        </svg>
      </button>
    </div>
  </Transition>
</template>

<script setup lang="ts">
import { computed } from 'vue'

interface Props {
  /**
   * Whether the area bounds indicator is visible
   */
  isVisible?: boolean
  /**
   * Name of the current area
   */
  areaName?: string
  /**
   * Position of the indicator on screen
   */
  position?: 'top-right' | 'top-left' | 'bottom-right' | 'bottom-left'
  /**
   * Whether to show a close button
   */
  showCloseButton?: boolean
  /**
   * Custom close button title for accessibility
   */
  closeButtonTitle?: string
  /**
   * Custom styling variant
   */
  variant?: 'default' | 'minimal' | 'prominent'
}

const props = withDefaults(defineProps<Props>(), {
  isVisible: false,
  areaName: 'selected area',
  position: 'top-right',
  showCloseButton: false,
  closeButtonTitle: 'Remove area bounds',
  variant: 'default'
})

interface Emits {
  close: []
}

defineEmits<Emits>()

const positionClasses = computed(() => {
  switch (props.position) {
    case 'top-left':
      return 'area-bounds-indicator--top-left'
    case 'bottom-right':
      return 'area-bounds-indicator--bottom-right'
    case 'bottom-left':
      return 'area-bounds-indicator--bottom-left'
    case 'top-right':
    default:
      return 'area-bounds-indicator--top-right'
  }
})

const variantClasses = computed(() => {
  switch (props.variant) {
    case 'minimal':
      return 'area-bounds-indicator--minimal'
    case 'prominent':
      return 'area-bounds-indicator--prominent'
    case 'default':
    default:
      return 'area-bounds-indicator--default'
  }
})
</script>

<style scoped>
.area-bounds-indicator {
  position: absolute;
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
  max-width: 280px;
}

/* Position variants */
.area-bounds-indicator--top-right {
  top: 20px;
  right: 20px;
}

.area-bounds-indicator--top-left {
  top: 20px;
  left: 20px;
}

.area-bounds-indicator--bottom-right {
  bottom: 20px;
  right: 20px;
}

.area-bounds-indicator--bottom-left {
  bottom: 20px;
  left: 20px;
}

/* Style variants */
.area-bounds-indicator--minimal {
  background: rgba(0, 0, 0, 0.7);
  padding: 6px 10px;
  font-size: 11px;
  border-radius: 6px;
}

.area-bounds-indicator--prominent {
  background: rgba(139, 92, 246, 1);
  padding: 12px 16px;
  font-size: 13px;
  font-weight: 600;
  box-shadow: 0 4px 12px rgba(139, 92, 246, 0.3);
}

.bounds-icon {
  font-size: 14px;
  flex-shrink: 0;
}

.bounds-text {
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  flex: 1;
}

.bounds-close-button {
  background: none;
  border: none;
  color: white;
  cursor: pointer;
  padding: 2px;
  border-radius: 4px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  transition: background-color 0.2s ease;
}

.bounds-close-button:hover {
  background: rgba(255, 255, 255, 0.2);
}

.bounds-close-button:focus {
  outline: 2px solid rgba(255, 255, 255, 0.5);
  outline-offset: 1px;
}

/* Transitions */
.area-bounds-enter-active,
.area-bounds-leave-active {
  transition: all 0.3s ease-out;
}

.area-bounds-enter-from {
  opacity: 0;
  transform: translateY(-10px) scale(0.95);
}

.area-bounds-leave-to {
  opacity: 0;
  transform: translateY(-10px) scale(0.95);
}

/* Responsive adjustments */
@media (max-width: 768px) {
  .area-bounds-indicator {
    font-size: 11px;
    padding: 6px 10px;
    max-width: 200px;
  }
  
  .area-bounds-indicator--top-right,
  .area-bounds-indicator--top-left {
    top: 10px;
  }
  
  .area-bounds-indicator--top-right,
  .area-bounds-indicator--bottom-right {
    right: 10px;
  }
  
  .area-bounds-indicator--top-left,
  .area-bounds-indicator--bottom-left {
    left: 10px;
  }
  
  .area-bounds-indicator--bottom-right,
  .area-bounds-indicator--bottom-left {
    bottom: 10px;
  }
}

/* Dark mode support */
@media (prefers-color-scheme: dark) {
  .area-bounds-indicator--minimal {
    background: rgba(255, 255, 255, 0.1);
    color: white;
    border: 1px solid rgba(255, 255, 255, 0.2);
  }
}

/* High contrast mode support */
@media (prefers-contrast: high) {
  .area-bounds-indicator {
    background: rgb(139, 92, 246);
    border: 2px solid white;
  }
}

/* Reduced motion support */
@media (prefers-reduced-motion: reduce) {
  .area-bounds-enter-active,
  .area-bounds-leave-active {
    transition: opacity 0.2s ease;
  }
  
  .area-bounds-enter-from,
  .area-bounds-leave-to {
    transform: none;
  }
}
</style>
