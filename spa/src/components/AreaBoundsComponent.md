# AreaBoundsComponent

A reusable Vue component for displaying area boundary constraints in map applications.

## Features

- 🎨 Multiple position options (top-right, top-left, bottom-right, bottom-left)
- 🎯 Three style variants (default, minimal, prominent)
- ✨ Smooth animations with accessibility support
- 🔧 Customizable content via slots
- ♿ Full accessibility support
- 📱 Responsive design
- 🌙 Dark mode and high contrast support
- ⚡ TypeScript support

## Basic Usage

```vue
<template>
  <div class="map-container">
    <!-- Your map component -->
    <AreaBoundsComponent 
      :is-visible="areaBoundsActive"
      :area-name="currentAreaName"
    />
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import AreaBoundsComponent from '@/components/AreaBoundsComponent.vue'

const areaBoundsActive = ref(true)
const currentAreaName = ref('Downtown Area')
</script>
```

## Advanced Usage

```vue
<template>
  <div class="map-container">
    <!-- Custom positioned indicator with close button -->
    <AreaBoundsComponent 
      :is-visible="showBounds"
      :area-name="selectedArea.name"
      position="bottom-left"
      variant="prominent"
      :show-close-button="true"
      close-button-title="Clear area restrictions"
      @close="clearAreaBounds"
    >
      <!-- Custom icon -->
      <template #icon>
        🌍
      </template>
      
      <!-- Custom content -->
      <template #default>
        Search limited to {{ selectedArea.name }} ({{ selectedArea.diameter }}km radius)
      </template>
    </AreaBoundsComponent>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import AreaBoundsComponent from '@/components/AreaBoundsComponent.vue'

const showBounds = ref(true)
const selectedArea = ref({
  name: 'Milan City Center',
  diameter: 5
})

const clearAreaBounds = () => {
  showBounds.value = false
  // Additional cleanup logic
}
</script>
```

## Props

| Prop | Type | Default | Description |
|------|------|---------|-------------|
| `isVisible` | `boolean` | `false` | Whether the indicator is visible |
| `areaName` | `string` | `'selected area'` | Name of the current area |
| `position` | `'top-right' \| 'top-left' \| 'bottom-right' \| 'bottom-left'` | `'top-right'` | Position on screen |
| `showCloseButton` | `boolean` | `false` | Whether to show close button |
| `closeButtonTitle` | `string` | `'Remove area bounds'` | Accessibility title for close button |
| `variant` | `'default' \| 'minimal' \| 'prominent'` | `'default'` | Visual styling variant |

## Events

| Event | Payload | Description |
|-------|---------|-------------|
| `close` | `void` | Emitted when close button is clicked |

## Slots

| Slot | Description |
|------|-------------|
| `icon` | Custom icon content (default: 🔒) |
| `default` | Custom message content |

## Style Variants

### Default
Standard purple indicator with medium padding and shadow.

### Minimal
Compact black indicator with reduced padding, ideal for subtle notifications.

### Prominent
Bold purple indicator with enhanced padding and glow effect for important notifications.

## Accessibility

- Full keyboard navigation support
- Screen reader friendly
- High contrast mode support
- Reduced motion support for users with vestibular disorders
- Proper ARIA labels and roles

## Integration with MapView

To replace the existing area bounds indicator in MapView.vue:

```vue
<!-- Replace the existing div with: -->
<AreaBoundsComponent 
  :is-visible="areaBoundsActive"
  :area-name="currentAreaName"
  position="top-right"
/>
```

## Responsive Behavior

The component automatically adjusts:
- Font size and padding on mobile devices
- Position margins for better mobile UX
- Text truncation for long area names
- Touch-friendly close button size
