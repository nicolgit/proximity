import * as L from 'leaflet'

// Custom marker icons
export const createCustomIcon = (color: string, size: [number, number] = [25, 41]) => {
    return L.divIcon({
        className: 'custom-marker',
        html: `
      <div class="marker-pin" style="background-color: ${color};">
        <div class="marker-icon">📍</div>
      </div>
    `,
        iconSize: size,
        iconAnchor: [size[0] / 2, size[1]],
        popupAnchor: [0, -size[1]]
    })
}

// Predefined icons
export const userLocationIcon = L.divIcon({
    className: 'custom-marker user-location-marker',
    html: `
    <div class="marker-pin user-location-pin">
      <div class="marker-icon">📍</div>
    </div>
  `,
    iconSize: [30, 45],
    iconAnchor: [15, 45],
    popupAnchor: [0, -45]
})

export const searchLocationIcon = L.divIcon({
    className: 'custom-marker search-location-marker',
    html: `
    <div class="marker-pin search-location-pin">
      <div class="marker-icon">📍</div>
    </div>
  `,
    iconSize: [25, 41],
    iconAnchor: [12.5, 41],
    popupAnchor: [0, -41]
})

// Alternative green icon using SVG
export const userLocationIconSvg = L.divIcon({
    className: 'custom-marker user-location-marker-svg',
    html: `
    <svg width="30" height="45" viewBox="0 0 30 45" fill="none" xmlns="http://www.w3.org/2000/svg">
      <path d="M15 0C6.716 0 0 6.716 0 15C0 26.25 15 45 15 45S30 26.25 30 15C30 6.716 23.284 0 15 0Z" fill="#22C55E"/>
      <circle cx="15" cy="15" r="8" fill="white"/>
      <circle cx="15" cy="15" r="5" fill="#16A34A"/>
    </svg>
  `,
    iconSize: [30, 45],
    iconAnchor: [15, 45],
    popupAnchor: [0, -45]
})

export const searchLocationIconSvg = L.divIcon({
    className: 'custom-marker search-location-marker-svg',
    html: `
    <svg width="25" height="41" viewBox="0 0 25 41" fill="none" xmlns="http://www.w3.org/2000/svg">
      <path d="M12.5 0C5.597 0 0 5.597 0 12.5C0 21.875 12.5 41 12.5 41S25 21.875 25 12.5C25 5.597 19.403 0 12.5 0Z" fill="#3B82F6"/>
      <circle cx="12.5" cy="12.5" r="6" fill="white"/>
      <circle cx="12.5" cy="12.5" r="3.5" fill="#1D4ED8"/>
    </svg>
  `,
    iconSize: [25, 41],
    iconAnchor: [12.5, 41],
    popupAnchor: [0, -41]
})
