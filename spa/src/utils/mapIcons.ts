import * as L from 'leaflet'

// Alternative red icon using SVG
export const userLocationIconSvg = L.divIcon({
    className: 'custom-marker user-location-marker-svg',
    html: `
    <svg width="30" height="45" viewBox="0 0 30 45" fill="none" xmlns="http://www.w3.org/2000/svg">
      <path d="M15 0C6.716 0 0 6.716 0 15C0 26.25 15 45 15 45S30 26.25 30 15C30 6.716 23.284 0 15 0Z" fill="#DC2626"/>
      <circle cx="15" cy="15" r="8" fill="white"/>
      <circle cx="15" cy="15" r="5" fill="#B91C1C"/>
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
      <path d="M12.5 0C5.597 0 0 5.597 0 12.5C0 21.875 12.5 41 12.5 41S25 21.875 25 12.5C25 5.597 19.403 0 12.5 0Z" fill="#DC2626"/>
      <circle cx="12.5" cy="12.5" r="6" fill="white"/>
      <circle cx="12.5" cy="12.5" r="3.5" fill="#B91C1C"/>
    </svg>
  `,
    iconSize: [25, 41],
    iconAnchor: [12.5, 41],
    popupAnchor: [0, -41]
})

// Station icons for metro stations
export const stationIconSvg = L.divIcon({
    className: 'custom-marker station-marker-svg',
    html: `
    <svg width="16" height="24" viewBox="0 0 16 24" fill="none" xmlns="http://www.w3.org/2000/svg">
      <path d="M8 0C3.582 0 0 3.582 0 8C0 14 8 24 8 24S16 14 16 8C16 3.582 12.418 0 8 0Z" fill="#16A34A"/>
      <circle cx="8" cy="8" r="4" fill="white"/>
      <rect x="5.5" y="5.5" width="5" height="5" rx="0.8" fill="#15803D"/>
    </svg>
  `,
    iconSize: [16, 24],
    iconAnchor: [8, 24],
    popupAnchor: [0, -24]
})

// Tram stop icons
export const tramStopIconSvg = L.divIcon({
    className: 'custom-marker tram-stop-marker-svg',
    html: `
    <svg width="16" height="24" viewBox="0 0 16 24" fill="none" xmlns="http://www.w3.org/2000/svg">
      <path d="M8 0C3.582 0 0 3.582 0 8C0 14 8 24 8 24S16 14 16 8C16 3.582 12.418 0 8 0Z" fill="#F59E0B"/>
      <circle cx="8" cy="8" r="4" fill="white"/>
      <circle cx="8" cy="8" r="2.5" fill="#D97706"/>
    </svg>
  `,
    iconSize: [16, 24],
    iconAnchor: [8, 24],
    popupAnchor: [0, -24]
})

// Trolleybus stop icons
export const trolleyStopIconSvg = L.divIcon({
    className: 'custom-marker trolley-stop-marker-svg',
    html: `
    <svg width="16" height="24" viewBox="0 0 16 24" fill="none" xmlns="http://www.w3.org/2000/svg">
      <path d="M8 0C3.582 0 0 3.582 0 8C0 14 8 24 8 24S16 14 16 8C16 3.582 12.418 0 8 0Z" fill="#3B82F6"/>
      <circle cx="8" cy="8" r="4" fill="white"/>
      <rect x="6" y="6" width="4" height="4" rx="1" fill="#1D4ED8"/>
    </svg>
  `,
    iconSize: [16, 24],
    iconAnchor: [8, 24],
    popupAnchor: [0, -24]
})
