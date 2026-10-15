# Metro Proximity SPA

A Vue.js and TypeScript Single Page Application (SPA) featuring a full-screen interactive map with location search capabilities.

## Features

- 🗺️ **Full-screen map** using Leaflet and OpenStreetMap
- 🔍 **Search field** at the top center to find locations
- 📍 **Automatic geolocation** of the user's position
- 🎯 **Markers** for selected locations and the current position
- 📱 **Responsive design** for mobile and desktop devices

## Technologies

- **Vue.js 3** - Reactive JavaScript framework
- **TypeScript** - Static typing for JavaScript
- **Leaflet** - Interactive mapping library
- **@vue-leaflet/vue-leaflet** - Vue integration for Leaflet
- **OpenStreetMap** - Mapping and geocoding service
- **Vite** - Fast build tool for development and production
- **Vue Router** - Routing for Single Page Applications

## Installation

1. Make sure Node.js is installed (version 18+ recommended)

2. Install the dependencies:
```bash
npm install
```

## Available Commands

### Development
```bash
npm run dev
```
Starts the development server at `http://localhost:3000`

### Build
```bash
npm run build
```
Creates the production build in the `dist` directory

### Preview
```bash
npm run preview
```
Previews the production build

### Type checking
```bash
npm run type-check
```
Checks TypeScript types

### Linting
```bash
npm run lint
```
Checks and automatically fixes the code

## Project Structure

```
spa/
├── public/                 # Static files
├── src/
│   ├── components/         # Reusable components
│   ├── composables/        # Vue composable functions
│   ├── router/             # Routing configuration
│   ├── services/           # API services
│   ├── types/              # TypeScript definitions
│   ├── views/              # Application pages
│   ├── App.vue             # Root component
│   ├── main.ts             # Entry point
│   └── style.css           # Global styles
├── index.html              # HTML template
├── package.json            # Dependencies and scripts
├── tsconfig.json           # TypeScript configuration
├── vite.config.ts          # Vite configuration
└── README.md               # Documentation
```

## Usage

1. **Location search**: Type at least 3 characters in the search field at the top to find a location
2. **Select a result**: Click a search result to center the map on that location
3. **Current position**: Click the 📍 button at the top right to center the map on your position
4. **Map navigation**: Use mouse or touch controls to pan, zoom, and navigate the map

## APIs Used

- **OpenStreetMap Tiles**: `https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png`
- **Nominatim Search API**: `https://nominatim.openstreetmap.org/search`
- **Nominatim Reverse Geocoding**: `https://nominatim.openstreetmap.org/reverse`

## Configuration

The main configuration files are:
- `vite.config.ts` - Build and development configuration
- `tsconfig.json` - TypeScript configuration
- `src/types/index.ts` - Custom types

## Application Insights: Minimal Tracking

At startup, the SPA requests `GET <VITE_API_ROOT>/application-insights/connection-string`
and initializes the Application Insights JavaScript SDK in the background. The backend
must provide this endpoint and have `APPLICATIONINSIGHTS_CONNECTION_STRING`
configured. The connection string is public for telemetry ingestion; it does not
grant access to read logs.

Tracking includes only:

- **Page views** for the initial visit and completed Vue Router path changes, without
  duplicates for redirects or changes limited to the query string or fragment.
- **MapReady**, once per country/area load: Leaflet must be ready, areas must be
  loaded, and, for a specific area, the initial station and isochrone requests must
  have completed without reported errors. The `durationMs` measurement starts when
  the view initializes or the country/area changes. It does not measure the loading
  of all map tiles.
- **Backend HTTP dependencies** for `fetch` and XMLHttpRequest calls targeting the
  origin and path of `VITE_API_ROOT`: duration, HTTP status code, and success.
  Requests include the W3C `traceparent` header to correlate the call with the
  Function and its Storage dependencies. Azure Maps, external services, and the
  telemetry configuration endpoint are excluded. Query strings, fragments,
  headers, and bodies are not collected.

Events include the path without query strings or fragments; the cloud role is
`proximity-spa`. We do not automatically collect exceptions, external HTTP calls,
clicks, searches, coordinates, or map movements. SDK cookies and storage are
disabled, and user/session identifiers and the referrer are removed from
telemetry. Visits represent page views, not a reliable count of unique users.
Telemetry does not replace an assessment of privacy and consent requirements.
The existing Google Analytics tag in `index.html` is independent and is not
modified by this integration.

The configuration request has a 5-second timeout. Up to 50 events are buffered in
memory until initialization completes. If initialization fails, collection is
disabled with a console warning, without preventing use of the SPA. Ad blockers
and network restrictions may prevent telemetry delivery.

Loading remains non-blocking: requests sent before SDK initialization are not
tracked and do not receive correlation headers. This includes the initial
connection string request and may include the first map load. Subsequent
navigation and requests are tracked after initialization.

If the API and SPA have different origins, the Function's CORS configuration must
allow the SPA origin and the `traceparent` header in preflight requests. Do not
enable correlation for external domains. The backend already uses the Application
Insights integration for Functions; after deployment, verify the presence of
Storage dependencies and the `operation_Id` / `operation_ParentId` chain in Azure.
Sharing the connection string alone does not guarantee this chain.

To verify, open the SPA and navigate between countries and areas, then check
Application Insights:

```kusto
pageViews
| where cloud_RoleName == "proximity-spa"
| project timestamp, name, url
```

```kusto
customEvents
| where cloud_RoleName == "proximity-spa" and name == "MapReady"
| project timestamp, path = customDimensions.path,
    durationMs = customMeasurements.durationMs
```

```kusto
dependencies
| where cloud_RoleName == "proximity-spa"
| project timestamp, name, resultCode, success, duration, operation_Id, id
```

For a selected call, use its `operation_Id` in the
**End-to-end transaction details** view or in the following query:

```kusto
union requests, dependencies, traces
| where operation_Id == "<operation-id>"
| order by timestamp asc
```

Allow a few minutes for ingestion. Azure Maps requests and search parameters
must not appear in the submitted telemetry.

## Contributing
1. Fork the repository
2. Create a feature branch (`git checkout -b feature/feature-name`)
3. Commit your changes (`git commit -am 'Add new feature'`)
4. Push the branch (`git push origin feature/feature-name`)
5. Open a Pull Request

## License

This project is distributed under the MIT license.
