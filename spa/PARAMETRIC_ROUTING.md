# Parametric Routing for Area IDs

This implementation adds support for parametric routes that allow direct navigation to specific areas in the Metro Proximity app.

## Features

### Route Structure
- **Base route**: `/` - Shows the map with default behavior (user location or Rome as fallback)
- **Area route**: `/:areaId` - Shows the map centered on the specified area

### Examples
- `/milan` - Centers the map on the Milan area
- `/naples` - Centers the map on the Naples area  
- `/rome` - Centers the map on the Rome area
- `/{any-area-id}` - Centers the map on the area with the matching ID

## How It Works

### 1. Router Configuration
The Vue Router is configured with a parametric route that captures the area ID:

```typescript
{
  path: '/:areaId',
  name: 'map-area',
  component: MapView,
  props: true
}
```

### 2. MapView Component Updates
The MapView component now accepts an optional `areaId` prop and implements area-specific centering logic:

```typescript
interface Props {
  areaId?: string
}
```

### 3. Area Centering Logic
When an `areaId` is provided:

1. **Load Areas**: First loads all available areas from the API
2. **Find Target Area**: Searches for the area with the matching ID
3. **Calculate Zoom**: Calculates appropriate zoom level based on area diameter
4. **Center Map**: Sets the map center to the area coordinates with proper zoom

### 4. Zoom Calculation
The zoom level is dynamically calculated to ensure the entire area circle is visible:

```typescript
const zoomLevel = Math.max(8, Math.min(16, 16 - Math.log2(diameterKm / 5)))
```

- Larger areas (bigger diameter) get lower zoom (more zoomed out)
- Smaller areas get higher zoom (more zoomed in)
- Zoom is constrained between 8 and 16 for optimal viewing

### 5. Fallback Behavior
If the area ID is not found or no area ID is provided:
- Falls back to user location-based centering
- If user location is not available, defaults to Rome coordinates

## Usage Examples

### Direct URL Navigation
Users can navigate directly to:
- `https://your-app.com/milan`
- `https://your-app.com/naples`
- `https://your-app.com/{area-id}`

### Programmatic Navigation
In Vue components:
```typescript
import { useRouter } from 'vue-router'

const router = useRouter()

// Navigate to specific area
router.push({ name: 'map-area', params: { areaId: 'milan' } })
```

## Technical Details

### Area Data Structure
Areas are expected to have the following structure:
```typescript
interface Area {
  id: string           // Unique identifier used in the URL
  name: string         // Display name
  latitude: number     // Center latitude
  longitude: number    // Center longitude  
  diameter: number     // Area diameter in kilometers
}
```

### Error Handling
- Invalid area IDs are logged as warnings but don't break the app
- Falls back gracefully to default location behavior
- Areas are loaded asynchronously with proper error handling

## Benefits

1. **Direct Access**: Users can bookmark and share specific area views
2. **SEO Friendly**: Each area has its own URL for better discoverability
3. **User Experience**: Instant navigation to areas of interest
4. **Optimal Viewing**: Automatic zoom calculation ensures the entire area is visible
5. **Progressive Enhancement**: Works as an overlay on existing functionality
