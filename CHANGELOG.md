# Changelog

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
