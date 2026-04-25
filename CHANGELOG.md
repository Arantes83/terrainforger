# Changelog

## 2026-04-24

### UI / UX

- Moved preview panels into a dedicated right-hand column in `Get GIS Data` and `Geotiff2Raw Export`.
- Changed `Map Bounds` to start collapsed by default and only expand on demand.
- Added contextual tooltips to editable fields and action buttons across the main TerrainForger windows and Data Services settings.
- Increased tool window minimum widths so the two-column layout remains usable.

### Satellite Download

- Added Google Maps Platform satellite downloads through the official Map Tiles API session flow.
- The integrated `Get GIS Data` workflow now lets you choose `Mapbox` or `Google Maps Platform` as the satellite provider.
- Updated startup/service configuration checks so TerrainForger accepts either Mapbox or Google Maps Platform for imagery workflows.
- Added download-plan feedback for Google tile zoom level and tile mosaic size.

### Import / Export

- Simplified `Import Tiles` by removing unnecessary `Input`, `Terrain Settings`, and `Output` editors from the window.
- `Import Tiles` now always uses `Assets/Terrain/Raw`, `Assets/Terrain/PNG`, `Assets/Generated/TerrainTiles`, and `TerrainTileRoot`.
- `Import Tiles` now always deletes and recreates the previous `TerrainTileRoot` before importing.
- Added a configurable `Water Plane Elevation` for the generated water plane instead of forcing placement at terrain origin.
- The water plane importer now places the generated plane at the explicit configured elevation.

### Coastline / Terrain Data

- Continued support for coastline masking with `GSHHG` and `OpenStreetMap`.
- Kept `GSHHG` naming in uppercase in the export UI.

### Documentation

- Updated `README.md`, `Documentation~/index.md`, and `Documentation~/UserManual.md` for:
  - Google Maps Platform satellite download support
  - right-column preview layout
  - collapsed `Map Bounds`
  - configurable water plane elevation
  - current provider behavior and policy caveats
