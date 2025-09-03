import MapView from '@/views/MapView.vue'
import { createRouter, createWebHistory } from 'vue-router'

const router = createRouter({
    history: createWebHistory(import.meta.env.BASE_URL),
    routes: [
        {
            path: '/',
            name: 'map',
            component: MapView
        },
        {
            path: '/:areaId',
            name: 'map-area',
            component: MapView,
            props: true
        }
    ]
})

export default router
