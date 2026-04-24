# Changelog

## [Unreleased] - 2026-04-24

- Moved the auto-downloaded GSHHG cache from `Library/TerrainForger/GSHHG` to `Assets/Terrain/GSHHG` so the shoreline dataset stays visible inside the Unity project.
- Added an OpenStreetMap coastline alternative using processed OSM land polygons in WGS84 as a second shoreline mask source.
- Added a `Coastline Source` dropdown so DEM export can switch between `GSHHG` and `OpenStreetMap`.
- Simplified the coastline mask UI to always use auto-downloaded datasets, removing manual vector overrides and browse actions for both `GSHHG` and `OpenStreetMap`.
- Removed the unused `Clamp Elevation` option from the export workflow.
- Removed `OpenAerialMap` and `Copernicus Data Space` from the TerrainForger Data Services UI and supported provider list.
- Updated provider documentation links to direct credential pages for `OpenTopography`, `Mapbox`, and `Google Maps Platform`.
- Documented that API keys are stored only in the consuming project's local `UserSettings` and are not distributed with the package.
- Added a dedicated top-level `TerrainForger` menu in the Unity editor instead of nesting the tools under the shared `Tools` menu.
- Fixed GSHHG ZIP extraction to support the actual flat archive layout used by the official dataset.
- Reduced shoreline mask bias by rasterizing the GSHHG land mask without the previous all-touched expansion behavior.

## [0.1.0] - 2026-04-23

- Initial UPM package structure for TerrainForger.
- Added `package.json`, editor assembly definition, and package documentation.
- Isolated the addon from the Unity project so it can be installed as a standalone package.
- Renamed the published package identifier to `com.arantes83.terrainforger`.
- Added GitHub-ready README improvements, including the TerrainForger logo.
- Declared Unity package dependencies for `com.unity.modules.terrain` and `com.unity.modules.imageconversion`.
- Added package publication metadata: `documentationUrl`, `changelogUrl`, and MIT license.
- Added a `LICENSE` file with the MIT license text.
- Replaced DEM coastline clipping by elevation threshold with an optional GSHHG-based land mask workflow.
- Added export UI fields for GSHHG vector selection and configurable water elevation during DEM export.
- Added automatic download and local caching of the official GSHHG dataset when no custom vector override is provided.
- Added a GSHHG resolution dropdown with `Auto`, `Full`, `High`, `Intermediate`, `Low`, and `Crude` modes.
- Added automatic GSHHG resolution selection by project region extent when the dropdown is set to `Auto`.
