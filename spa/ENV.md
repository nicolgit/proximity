# Environment Variables

This project uses Vite environment variables for configuration.

## Available Variables

- `API_ROOT`: The base URL for API calls

## Environment Files

- `.env.development`: Development environment variables
- `.env.production`: Production environment variables  
- `.env.local`: Local overrides (gitignored)

## Usage

```typescript
import { config, getApiUrl } from '@/config/env'

// Direct access
const apiRoot = config.apiRoot

// Helper function to build URLs
const areasUrl = getApiUrl('/areas')
const stationsUrl = getApiUrl('/stations')
```

## Map Key Configuration

The Azure Maps subscription key is now fetched from the API endpoint `/api/map/key` at runtime instead of being stored in environment variables. This improves security by not exposing the key in client-side code.

The API should return:
```json
{
  "mapKey": "your-azure-maps-subscription-key"
}
```

## Development Setup

1. Copy `.env.development` and customize if needed
2. For local overrides, create `.env.local` (this file is gitignored)
3. Ensure your API endpoint `/api/map/key` is configured to return the Azure Maps key

## Production Deployment

Update `.env.production` with your production API URL before building.
