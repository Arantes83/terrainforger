# TerrainForger

TerrainForger is a Unity Editor package for GIS-based terrain production workflows.

It provides a controlled pipeline for:

- acquiring DEM and satellite data
- exporting tiled RAW and PNG terrain inputs
- importing tiled terrain data back into Unity Terrain
- masking shorelines with `GSHHG` or `OpenStreetMap`

## Package Baseline

- Package name: `com.arantes83.terrainforger`
- Declared Unity version: `2020.3`
- Package type: editor-only

## Main Menus

- `TerrainForger/Get GIS Data`
- `TerrainForger/Geotiff2Raw Export`
- `TerrainForger/Import Tiles`

## Integrated Providers

The current integrated workflow uses:

- `OpenTopography` for DEM downloads
- `Mapbox` for satellite downloads

The Data Services panel also exposes `Google Maps Platform` credentials, but the current integrated acquisition workflow does not use Google downloads directly.

## Generated Folders

Typical output and cache folders inside the consuming Unity project:

```text
Assets/
|-- Generated/
|   `-- TerrainTiles/
`-- Terrain/
    |-- GSHHG/
    |-- GeoTIFF/
    |-- OSMCoastline/
    |-- PNG/
    |-- Raw/
    `-- SAT/
```

## Credential Safety

- Credentials are stored locally in `UserSettings/TerrainDataServiceSettings.asset`
- API keys are not hardcoded in the package
- `UserSettings/` is not distributed with the package by default

## Full Manual

[Open the User Manual](UserManual.md)
