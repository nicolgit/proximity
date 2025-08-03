/**
 * Environment configuration
 */
export const config = {
    apiRoot: import.meta.env.VITE_API_ROOT,
} as const

/**
 * Helper function to build API URLs
 */
export function getApiUrl(endpoint: string): string {
    const baseUrl = config.apiRoot.endsWith('/')
        ? config.apiRoot.slice(0, -1)
        : config.apiRoot

    const cleanEndpoint = endpoint.startsWith('/')
        ? endpoint
        : `/${endpoint}`

    return `${baseUrl}${cleanEndpoint}`
}
