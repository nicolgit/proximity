# Metro Proximity SPA

Una Single Page Application (SPA) basata su Vue.js e TypeScript che mostra una mappa interattiva a schermo intero con funzionalità di ricerca geografica.

## Funzionalità

- 🗺️ **Mappa a schermo intero** utilizzando Leaflet e OpenStreetMap
- 🔍 **Campo di ricerca** in alto al centro per cercare località
- 📍 **Geolocalizzazione** automatica della posizione dell'utente
- 🎯 **Marcatori** per località selezionate e posizione corrente
- 📱 **Design responsive** per dispositivi mobile e desktop

## Tecnologie utilizzate

- **Vue.js 3** - Framework JavaScript reattivo
- **TypeScript** - Tipizzazione statica per JavaScript
- **Leaflet** - Libreria per mappe interattive
- **@vue-leaflet/vue-leaflet** - Integrazione Vue per Leaflet
- **OpenStreetMap** - Servizio di mappe e geocoding
- **Vite** - Build tool veloce per sviluppo e produzione
- **Vue Router** - Routing per Single Page Application

## Installazione

1. Assicurati di avere Node.js installato (versione 18+ raccomandata)

2. Installa le dipendenze:
```bash
npm install
```

## Comandi disponibili

### Sviluppo
```bash
npm run dev
```
Avvia il server di sviluppo su `http://localhost:3000`

### Build
```bash
npm run build
```
Crea il build di produzione nella cartella `dist`

### Preview
```bash
npm run preview
```
Anteprima del build di produzione

### Type checking
```bash
npm run type-check
```
Controllo dei tipi TypeScript

### Linting
```bash
npm run lint
```
Controllo e correzione automatica del codice

## Struttura del progetto

```
spa/
├── public/                 # File statici
├── src/
│   ├── components/         # Componenti riutilizzabili
│   ├── composables/        # Funzioni composable Vue
│   ├── router/             # Configurazione routing
│   ├── services/           # Servizi API
│   ├── types/              # Definizioni TypeScript
│   ├── views/              # Pagine dell'applicazione
│   ├── App.vue             # Componente root
│   ├── main.ts             # Entry point
│   └── style.css           # Stili globali
├── index.html              # Template HTML
├── package.json            # Dipendenze e script
├── tsconfig.json           # Configurazione TypeScript
├── vite.config.ts          # Configurazione Vite
└── README.md               # Documentazione
```

## Utilizzo

1. **Ricerca località**: Digita almeno 3 caratteri nel campo di ricerca in alto per cercare una località
2. **Selezione risultato**: Clicca su un risultato della ricerca per centrare la mappa su quella località
3. **Posizione corrente**: Clicca sul pulsante 📍 in alto a destra per centrare la mappa sulla tua posizione
4. **Navigazione mappa**: Usa mouse/touch per trascinare, zoom e navigare la mappa

## API utilizzate

- **OpenStreetMap Tiles**: `https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png`
- **Nominatim Search API**: `https://nominatim.openstreetmap.org/search`
- **Nominatim Reverse Geocoding**: `https://nominatim.openstreetmap.org/reverse`

## Configurazione

Le configurazioni principali si trovano in:
- `vite.config.ts` - Configurazione build e sviluppo
- `tsconfig.json` - Configurazione TypeScript
- `src/types/index.ts` - Tipi personalizzati

## Contribuire

1. Fork del repository
2. Crea un branch per la feature (`git checkout -b feature/nome-feature`)
3. Commit delle modifiche (`git commit -am 'Aggiungi nuova feature'`)
4. Push del branch (`git push origin feature/nome-feature`)
5. Apri una Pull Request

## Licenza

Questo progetto è distribuito sotto licenza MIT.
