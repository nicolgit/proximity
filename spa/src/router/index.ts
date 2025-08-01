import MapView from '@/views/MapView.vue'
import { createRouter, createWebHistory } from 'vue-router'

const router = createRouter({
    history: createWebHistory(import.meta.env.BASE_URL),
    routes: [
        {
            path: '/',
            name: 'map',
            component: MapView
        }
    ]
})

export default router
