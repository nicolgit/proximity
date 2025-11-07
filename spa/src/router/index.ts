import MapView from '@/views/MapView.vue'
import { createRouter, createWebHistory } from 'vue-router'

const router = createRouter({
    history: createWebHistory(import.meta.env.BASE_URL),
    routes: [
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
    ]
})

export default router
