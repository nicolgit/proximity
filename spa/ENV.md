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

## Development Setup

1. Copy `.env.development` and customize if needed
2. For local overrides, create `.env.local` (this file is gitignored)

## Production Deployment

Update `.env.production` with your production API URL before building.
