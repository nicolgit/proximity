let map, datasource, routeRangeLayer;

import "https://unpkg.com/leaflet@1.9.4/dist/leaflet.js";
import "https://unpkg.com/maplibre-gl@4.7.1/dist/maplibre-gl.js";
import "https://unpkg.com/@maplibre/maplibre-gl-leaflet@0.0.22/leaflet-maplibre-gl.js";
import "./bundle.js"; // ND.polygonClipping

export function initMap(center, zoom) {
    map = L.map('theMap').setView(center, zoom)

    ND.helloWord("ciao roma!");

    L.maplibreGL({
        style: 'https://tiles.openfreemap.org/styles/positron',
        attribution: '&copy; <a href="http://nicolgit.github.io">Nicola Delfino</a>'
    }).addTo(map);
}

export function showMetroStations(url, color, stationName) {
    fetch(url)
        .then(response => response.json())
        .then(data => {
            let stations = data.stops;
            for (let i = 0; i < stations.length; i++) {

                if (typeof stationName === 'undefined' ||
                    (stationName !== null && stationName === stations[i][0])) {

                    let station = stations[i];
                    let name = station[0];

                    // add MARKER with name and color
                    let pin = L.marker([station[1], station[2]]).addTo(map);
                    pin.bindPopup(name);
                    pin.setIcon(L.icon({
                        iconUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-' + color + '.png',
                        shadowUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.7.1/images/marker-shadow.png',
                        iconSize: [25, 41],
                        iconAnchor: [12, 41],
                        popupAnchor: [1, -34],
                        shadowSize: [41, 41]
                    }));
                }
            }
        })
        .catch(error => console.error('Error fetching metro stations:', error));
}

export async function showMetroRange(url, color, stationFolder, stationName) {
    var unionPolygon = [[[]]];

    const metroResult = await fetch(url);
    const metroData = await metroResult.json();

    let stations = metroData.stops;
    for (let i = 0; i < stations.length; i++) {
        let station = stations[i];
        let name = station[0];

        if (typeof stationName === 'undefined') {
            let polygonUrl = stationFolder + '/' + name + '-' + i + '.json';

            const response = await fetch(polygonUrl);
            const data = await response.json();

            if (i === 0) {
                unionPolygon = [data[0]];
            }
            else {
                unionPolygon = ND.polygonClipping.union(unionPolygon, [data[0]]);
            }

            if (i === stations.length - 1) {
                unionPolygon.forEach(polygon => {
                    let stationPolygon = L.polygon(polygon, { color: color, stroke: true, weight: 1 }).addTo(map);
                    stationPolygon.bindPopup(metroData.name);
                }
                );
            }
        }

        if ((stationName !== null && stationName === stations[i][0])) {

            let station = stations[i];
            let name = station[0];

            // poligon around the station
            let polygonUrl = stationFolder + '/' + name + '-' + i + '.json';
            const response = await fetch(polygonUrl);
            const data = await response.json();

            for (let l=0; l<data.length; l++) {
                let stationPolygon;
                if (l == data.length - 1) {
                    stationPolygon = L.polygon(data[l], { color: color, stroke: true, weight: 1 }).addTo(map);
                }
                else {
                stationPolygon = L.polygon(data[l], { color: color, stroke: false }).addTo(map);
                }
                stationPolygon.bindPopup(name);
            }
        }

    }
}


function addRouteRangeLayer(map, center, range, color, opacity) {
    let centerString = center[1] + ',' + center[0];
    let travelMode = 'car';

    let routeRangeUrl = `https://atlas.microsoft.com/route/range/json?api-version=1.0&query=` + centerString + `&distanceBudgetInMeters=` + range + `&TravelMode=` + travelMode + `&subscription-key=${subscriptionkey}`;
    fetch(routeRangeUrl)
        .then(response => response.json())
        .then(data => {

            let convertedArray = convertCoordinates(data.reachableRange.boundary);
            let rangePolygon = new atlas.data.Polygon(convertedArray);
            let feature = new atlas.data.Feature(rangePolygon);
            datasource.add(new atlas.Shape(feature));

            map.layers.add(new atlas.layer.PolygonLayer(datasource, null, {
                fillColor: color,
                fillOpacity: opacity
            }), 'labels')

        })
        .catch(error => console.error('Error fetching route range:', error));
}

function convertCoordinates(coordinates) {
    return coordinates.map(coord => [coord.longitude, coord.latitude]);
}