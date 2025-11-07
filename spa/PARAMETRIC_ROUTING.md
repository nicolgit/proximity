# Parametric Routing for Country and Area

This implementation adds support for parametric routes that allow direct navigation to specific areas in the Metro Proximity app with country/area structure.

## Features

### Route Structure
- **Base route**: `/` - Redirects to `/italy` (default country)
- **Country route**: `/:country` - Shows the map with areas for the specified country
- **Area route**: `/:country/:area` - Shows the map centered on the specified area within the country

### Examples
- `/` - Redirects to `/italy`
- `/italy` - Shows the map with Italian areas
- `/italy/rome` - Centers the map on the Rome area 
- `/italy/milan` - Centers the map on the Milan area 
- `/italy/naples` - Centers the map on the Naples area

## How It Works

### 1. Router Configuration
The Vue Router is configured with parametric routes that capture country and area parameters:

```typescript
{
  path: '/',
  redirect: '/italy'
},
{
  path: '/:country/:area',
  name: 'map-area', 
  component: MapView,
  props: true
},
{
  path: '/:country',
  name: 'map-country',
  component: MapView,
  props: true
}
```

### 2. MapView Component Updates
The MapView component now accepts optional `country` and `area` props, with backward compatibility for legacy `areaid` prop:

```typescript
interface Props {
  country?: string
  area?: string
  areaid?: string // Keep for backward compatibility
}
```

### 3. Area Centering Logic
The component uses a computed property to determine the current area ID:

```typescript
const currentAreaId = computed(() => {
  return props.area || props.areaid
})
```

When an area ID is provided:

1. **Load Areas**: Loads the specific area or all areas from the API
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
- `https://your-app.com/` (redirects to `/italy`)
- `https://your-app.com/italy` (shows Italian areas)
- `https://your-app.com/italy/RM` (Rome area)
- `https://your-app.com/italy/MI` (Milan area)
- `https://your-app.com/italy/NA` (Naples area)
- `https://your-app.com/{country}/{area-id}` (any country/area combination)

### Programmatic Navigation
In Vue components:
```typescript
import { useRouter } from 'vue-router'

const router = useRouter()

// Navigate to specific area
router.push({ name: 'map-area', params: { country: 'italy', area: 'RM' } })

// Navigate to country view
router.push({ name: 'map-country', params: { country: 'italy' } })
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
