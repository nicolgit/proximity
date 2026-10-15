import { createApp } from 'vue'
import App from './App.vue'
import router from './router'
import './style.css'
import { initializeTelemetry } from './services/TelemetryService'

const app = createApp(App)

void initializeTelemetry(router)
app.use(router)

app.mount('#app')
