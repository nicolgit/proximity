let map, datasource, routeRangeLayer;

import "https://unpkg.com/leaflet@1.9.4/dist/leaflet.js";
import "https://unpkg.com/maplibre-gl@4.7.1/dist/maplibre-gl.js";
import "https://unpkg.com/@maplibre/maplibre-gl-leaflet@0.0.22/leaflet-maplibre-gl.js";
import "./bundle.js"; // ND.polygonClipping

var layerControl;

export function initMap(center, zoom) {
    ND.helloWord("ciao roma!");

    let positronBase = L.maplibreGL({
        style: 'https://tiles.openfreemap.org/styles/positron',
        attribution: '&copy; <a href="http://nicolgit.github.io">Nicola Delfino</a>'
    });

    let brightBase = L.maplibreGL({
        style: 'https://tiles.openfreemap.org/styles/bright',
        attribution: '&copy; <a href="http://nicolgit.github.io">Nicola Delfino</a>'
    });

    map = L.map('theMap', {
        center: center,
        zoom:zoom,
        layers: [brightBase, positronBase]
    });

    let baseMaps = {
        "Positron": positronBase,
        "Bright": brightBase
    };

    let overlayMaps = {
    };

    layerControl = L.control.layers(baseMaps, overlayMaps).addTo(map);
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
    var group = L.layerGroup();

    var fullLine500 = [[[]]];
    var fullLine1000 = [[[]]];
    var fullLine1600 = [[[]]];

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
                fullLine500 = [data.distance500];
                fullLine1000 = [data.distance1000];
                fullLine1600 = [data.distance1600];
            }
            else {
                fullLine500 = ND.polygonClipping.union(fullLine500, [data.distance500]);
                fullLine1000 = ND.polygonClipping.union(fullLine1000, [data.distance1000]);
                fullLine1600 = ND.polygonClipping.union(fullLine1600, [data.distance1600]);
            }

            if (i === stations.length - 1) {
                fullLine500.forEach(polygon => {
                    group.addLayer (CreatePolygon(polygon, color, false));
                });
                fullLine1000.forEach(polygon => {
                    group.addLayer (CreatePolygon(polygon, color, false));
                });
                fullLine1600.forEach(polygon => {
                    group.addLayer (CreatePolygon(polygon, color, true));
                });
            }
        }

        if ((stationName !== null && stationName === stations[i][0])) {

            let station = stations[i];
            let name = station[0];

            // poligon around the station
            let polygonUrl = stationFolder + '/' + name + '-' + i + '.json';
            const response = await fetch(polygonUrl);
            const data = await response.json();

            group.addLayer (CreatePolygon(data.distance500, color, false));
            group.addLayer (CreatePolygon(data.distance1000, color, false));
            group.addLayer (CreatePolygon(data.distance1600, color, true));
            //AddPolygon(data.distance500, color, name, false);
            //AddPolygon(data.distance1000, color, name, false);
            //AddPolygon(data.distance1600, color, name, true);
        }
    }
    layerControl.addOverlay(group, metroData.name);
}

function AddPolygon(polygon, color, name, showStroke) {
    let stationPolygon = L.polygon(polygon, { color: color, stroke: showStroke, weight: 1 }).addTo(map);
    stationPolygon.bindPopup(name);
}

function CreatePolygon(polygon, color, showStroke) {
    return L.polygon(polygon, { color: color, stroke: showStroke, weight: 1 });
}