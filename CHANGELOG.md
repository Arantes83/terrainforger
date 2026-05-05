# Changelog

## 0.1.1 - 2026-05-05

### Release / Packaging

- Updated the Unity package metadata to version `0.1.1`.
- Documented Git URL, pinned release tag, and minimal GitHub release archive installation paths in `README.md`.
- Excluded `dist/` release archives and their Unity `.meta` file from source control because they are generated package artifacts.
- Removed the root `LICENSE.meta` Unity metadata file so GitHub license detection only exposes the real MIT `LICENSE` file.

### Stability / Reliability

- Added timeouts and non-blocking stdout/stderr capture for QGIS/GDAL subprocesses to prevent editor hangs when external tools stall or emit large error output.
- Added satellite download plan limits to reject oversized imagery requests before they can overflow counters, allocate excessive memory, or consume unexpected provider quota.
- Added network timeouts for DEM, satellite, GSHHG, and OpenStreetMap coastline dataset downloads.

### Import / Export

- Reduced satellite texture import memory pressure by using a compact single-layer alphamap instead of matching the full terrain heightmap resolution.
- Reduced gloss on generated terrain tile satellite layers by setting low smoothness, zero metallic, and black specular values.
- Hardened coastline dataset ZIP extraction so downloaded archives cannot write outside the TerrainForger cache folder.

### UI / UX

- Updated `Get GIS Data`, `Geotiff2Raw Export`, and `Import Tiles` so workflow settings are saved only after real value changes instead of every editor repaint.
- Stored local source files now go to `Assets/Terrain/Source` instead of `Assets/Terrain/SAT`, keeping the satellite preview focused on downloaded satellite GeoTIFFs.
- Changed `Geotiff2Raw Export` tile preview names to explicit RGB green `(0, 1, 0)` for better contrast over DEM previews.

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
