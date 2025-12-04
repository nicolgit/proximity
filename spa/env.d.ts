/// <reference types="vite/client" />

interface ImportMetaEnv {
    readonly VITE_API_ROOT: string
    readonly VITE_AZURE_MAPS_SUBSCRIPTION_KEY: string
    // more env variables...
}

interface ImportMeta {
    readonly env: ImportMetaEnv
}

declare module '*.vue' {
    import type { DefineComponent } from 'vue'
    const component: DefineComponent<{}, {}, any>
    export default component
}
