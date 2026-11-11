# Local Overpass API with Docker

This setup runs a local instance of **Overpass API** using Docker, imports OpenStreetMap data for all of Europe, and keeps the local database synchronized using incremental OSM updates for the same region.

> Overpass Turbo is the web-based query interface. This container runs **Overpass API**, the backend service that exposes the `/api/interpreter` endpoint.

## Prerequisites

Install:

- Docker
- Docker Compose v2

Verify the installation:

```bash
docker --version
docker compose version
```

---

## 1. Docker Compose configuration

The configuration is provided in [docker-compose.yml](./docker-compose.yml). Run the Docker Compose commands below from this directory.

The initial import downloads `https://download.geofabrik.de/europe-latest.osm.pbf` and converts it before importing it into Overpass. Europe requires substantially more disk space, memory, and import time than a single-country extract; allow space for the download, conversion, and expanded database.

The image saves the PBF download as `/db/planet.osm.bz2` despite its format. The preprocessing command must explicitly read that file as PBF (`-F pbf`), write the converted XML/BZip2 data to a separate file, and replace the download only after successful conversion. Both files occupy disk space during conversion.

The local database is persisted in:

```text
./overpass-db
```

Do not delete this directory unless you want to rebuild the database from scratch.

If this directory already contains an Italy-only database, changing the download and update URLs does not turn it into a Europe database. Stop the container with `docker compose down`, move the existing `overpass-db` directory to a backup location, and start again to import Europe into a new database. Do not apply Europe replication diffs using the old Italy replication state.

---

## 2. Start Overpass API

Start the container:

```bash
docker compose up -d
```

Monitor initialization:

```bash
docker compose logs -f overpass
```

The first startup can take a significant amount of time because the Europe dataset must be downloaded, converted, and imported into the Overpass database.

Once initialization is complete, the local API is available at:

```text
http://localhost:8080/api/interpreter
```

### Troubleshooting initialization

An `Empty reply from server` response during initialization means the HTTP service may not have started yet. Check `docker compose logs -f overpass` and wait for `Overpass container ready to receive requests` before testing the API.

The large `/db/planet.osm.bz2` file is the initial download, not the finished database. Removing it after a successful import is normal; the database remains in `/db/db`, with `/db/init_done` marking completed initialization.

Repeated `No database directory. Initializing` messages together with `Failed to process planet file` indicate a failed import and a restart loop, not normal cleanup. In particular:

```text
When reading from STDIN you need to use the --input-format/-F option
to specify the file format. Or are you missing a file name argument?
```

means the preprocessing command did not supply the input file. Use the complete command in [docker-compose.yml](./docker-compose.yml).

After correcting the Compose configuration, apply it with:

```bash
docker compose up -d --force-recreate overpass
```

**If initialization is incomplete, recreating the container starts the Europe download again from the beginning.** A plain `docker compose restart` does not apply changed environment variables. Do not delete `overpass-db` to fix a preprocessing command.

Use `curl -sS` instead of `curl -s` when diagnosing requests so connection errors are visible.

---

## 3. Test the API

For example, search for the city of Rome:

```bash
curl -s -X POST http://localhost:8080/api/interpreter \
  --data-urlencode 'data=[out:json];node["place"="city"]["name"="Roma"];out;'
```

A successful response should contain one or more OSM elements describing Rome.

---

## 4. Check the database freshness

Overpass exposes the timestamp of the OpenStreetMap data currently loaded in the database.

Run:

```bash
curl -s -X POST http://localhost:8080/api/interpreter \
  --data-urlencode 'data=[out:json];out;'
```

Look for:

```json
"osm3s": {
  "timestamp_osm_base": "2026-11-11T07:42:18Z"
}
```

`timestamp_osm_base` represents the timestamp of the OSM data currently available to the Overpass API.

If `jq` is installed, retrieve only the timestamp:

```bash
curl -s -X POST http://localhost:8080/api/interpreter \
  --data-urlencode 'data=[out:json];out;' \
  | jq -r '.osm3s.timestamp_osm_base'
```

Example:

```text
2026-11-11T07:42:18Z
```

Ideally, this timestamp should be reasonably close to the current UTC time.

If the container has been stopped for several days, the timestamp may initially be older while Overpass downloads and applies the missing incremental updates.

---

## 5. Automatic updates

The container periodically retrieves incremental OSM changes from:

```text
https://download.geofabrik.de/europe-updates/
```

The update interval is configured by `OVERPASS_UPDATE_SLEEP` in [docker-compose.yml](./docker-compose.yml), set to `3600` seconds. This means that the container checks for updates every hour; new diffs become available according to Geofabrik's publication schedule.

The local replication state is persisted together with the database, so restarting the container does not normally require downloading and importing Europe again.

---

## 6. Force a database update

To trigger the Overpass update process manually:

```bash
docker exec -it overpass /app/bin/update_overpass.sh
```

The script retrieves the missing replication diffs and applies them to the existing database.

You should see messages related to downloading and applying updates.

After it completes, check the database timestamp again:

```bash
curl -s -X POST http://localhost:8080/api/interpreter \
  --data-urlencode 'data=[out:json];out;' \
  | jq -r '.osm3s.timestamp_osm_base'
```

The timestamp should advance toward the current OSM dataset timestamp.

---

## 7. Check replication state

The container stores the current OSM replication state in:

```text
/db/replicate_id
```

Inspect it with:

```bash
docker exec overpass cat /db/replicate_id
```

The update mechanism uses this state to determine which incremental changes must be downloaded next.

---

## 8. Check update logs

The persistent Overpass update log is available at:

```text
/db/changes.log
```

Display the latest entries with:

```bash
docker exec overpass tail -50 /db/changes.log
```

To monitor the container itself:

```bash
docker compose logs -f overpass
```

---

## 9. Update the Docker image

To update Overpass API itself:

```bash
docker compose pull
docker compose up -d
```

Docker Compose downloads the current image and recreates the container when necessary.

The Overpass database is preserved because it is stored outside the container in:

```text
./overpass-db
```

Alternatively:

```bash
docker compose up -d --pull always
```

Old unused Docker images can be removed with:

```bash
docker image prune
```

---

## Quick reference

### Start

```bash
docker compose up -d
```

### Stop

```bash
docker compose down
```

### Restart

```bash
docker compose restart overpass
```

### Follow logs

```bash
docker compose logs -f overpass
```

### Check OSM data timestamp

```bash
curl -s -X POST http://localhost:8080/api/interpreter  --data-urlencode 'data=[out:json];out;'
```

### Force an update

```bash
docker exec -it overpass /app/bin/update_overpass.sh
```

### Check replication state

```bash
docker exec overpass cat /db/replicate_id
```

### Check update log

```bash
docker exec overpass tail -50 /db/changes.log
```

### Update the Docker image

```bash
docker compose pull
docker compose up -d
```

## Persistence

The directory:

```text
./overpass-db
```

contains the persistent Overpass database and synchronization state.

**Do not delete it** during normal container updates or restarts.
