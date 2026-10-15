import { getApiUrl } from '@/config/env'
import type { ApplicationInsights } from '@microsoft/applicationinsights-web'
import type { Router } from 'vue-router'

type PageView = {
    kind: 'page'
    path: string
    timestamp: Date
}

type MapReady = {
    kind: 'map'
    path: string
    durationMs: number
}

type Telemetry = PageView | MapReady

let client: ApplicationInsights | undefined
let state: 'pending' | 'ready' | 'disabled' = 'pending'
const pending: Telemetry[] = []

function send(item: Telemetry): void {
    if (state === 'disabled') return
    if (!client) {
        if (pending.length === 50) {
            pending.shift()
            console.warn('Telemetry startup buffer full; oldest event discarded.')
        }
        pending.push(item)
        return
    }

    try {
        if (item.kind === 'page') {
            client.trackPageView({
                name: item.path,
                uri: `${window.location.origin}${item.path}`,
                refUri: '',
                startTime: item.timestamp,
                properties: { path: item.path, duration: 0 }
            })
        } else {
            client.trackEvent({
                name: 'MapReady',
                properties: { path: item.path },
                measurements: { durationMs: item.durationMs }
            })
        }
    } catch {
        console.warn('Unable to record browser telemetry.')
    }
}

export function trackMapReady(path: string, durationMs: number): void {
    send({ kind: 'map', path: cleanPath(path), durationMs })
}

function cleanPath(path: string): string {
    return path.split(/[?#]/, 1)[0]
}

export async function initializeTelemetry(router: Router): Promise<void> {
    let previousPath: string | undefined
    router.afterEach((to, _from, failure) => {
        const path = cleanPath(to.path)
        if (!failure && path !== previousPath) {
            previousPath = path
            send({ kind: 'page', path, timestamp: new Date() })
        }
    })

    const controller = new AbortController()
    const timeout = window.setTimeout(() => controller.abort(), 5000)
    try {
        const response = await fetch(getApiUrl('/application-insights/connection-string'), {
            signal: controller.signal
        })
        if (!response.ok) {
            throw new Error('Telemetry configuration request failed.')
        }
        const data: unknown = await response.json()
        if (
            typeof data !== 'object' || data === null ||
            !('connectionString' in data) ||
            typeof data.connectionString !== 'string' ||
            !data.connectionString.trim()
        ) {
            throw new Error('Invalid telemetry configuration.')
        }

        const { ApplicationInsights, DistributedTracingModes } = await import('@microsoft/applicationinsights-web')
        const apiRoot = new URL(getApiUrl('/'), window.location.href)
        const apiPrefix = apiRoot.href.replace(/\/$/, '')
        const escapedApiPrefix = apiPrefix.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
        const apiRequests = new RegExp(`^${escapedApiPrefix}/`, 'i')
        const insights = new ApplicationInsights({
            config: {
                connectionString: data.connectionString,
                cookieCfg: { enabled: false },
                isStorageUseDisabled: true,
                enableSessionStorageBuffer: false,
                disableExceptionTracking: true,
                disableAjaxTracking: false,
                disableFetchTracking: false,
                excludeRequestFromAutoTrackingPatterns: [
                    new RegExp(`^(?!${escapedApiPrefix}/)`, 'i'),
                    /\/application-insights\/connection-string(?:[?#]|$)/i
                ],
                enableCorsCorrelation: true,
                correlationHeaderDomains: [apiRoot.host],
                distributedTracingMode: DistributedTracingModes.W3C,
                enableRequestHeaderTracking: false,
                enableResponseHeaderTracking: false,
                enableAjaxErrorStatusText: false,
                enableAutoRouteTracking: false,
                autoTrackPageVisitTime: false,
                enableUnhandledPromiseRejectionTracking: false,
                isBrowserLinkTrackingEnabled: false,
                disablePageUnloadEvents: ['unload'],
                extensionConfig: {
                    AppInsightsCfgSyncPlugin: { syncMode: 0, blkCdnCfg: true }
                },
                samplingPercentage: 100
            }
        })
        insights.loadAppInsights()
        insights.addTelemetryInitializer(item => {
            const isDependency = item.baseType === 'RemoteDependencyData'
            if (item.baseType !== 'PageviewData' &&
                !isDependency &&
                !(item.baseType === 'EventData' && item.baseData?.name === 'MapReady')) {
                return false
            }
            if (isDependency && item.baseData) {
                const data = item.baseData
                if (typeof data.target !== 'string' || !apiRequests.test(data.target)) {
                    return false
                }
                const url = new URL(data.target)
                url.search = ''
                url.hash = ''
                data.target = url.href
                data.name = typeof data.name === 'string' ? cleanPath(data.name) : url.pathname
                data.properties = { path: url.pathname }
            }
            const tags = item.tags ?? (item.tags = {})
            tags['ai.cloud.role'] = 'proximity-spa'
            tags['ai.operation.name'] = item.baseData?.properties?.path
            delete tags['ai.user.id']
            delete tags['ai.user.authUserId']
            delete tags['ai.user.accountId']
            delete tags['ai.session.id']
            if (item.ext) {
                delete item.ext.user
                if (item.ext.app) delete item.ext.app.sesId
            }
            if (item.baseData) {
                item.baseData.refUri = ''
            }
        })
        client = insights
        state = 'ready'
        pending.splice(0).forEach(send)
    } catch {
        state = 'disabled'
        pending.length = 0
        console.warn('Browser telemetry unavailable; the application will continue without tracking.')
    } finally {
        window.clearTimeout(timeout)
    }
}
