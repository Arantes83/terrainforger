# TerrainForger

<p align="center">
  <img src="Documentation~/images/TerrainForger.png" alt="TerrainForger" width="420" />
</p>

TerrainForger is a Unity Editor package for GIS-driven terrain workflows. It helps you acquire DEM and satellite data, export tiled RAW and PNG terrain inputs, and import them back into Unity Terrain with a consistent pipeline.

## Package

- Package name: `com.arantes83.terrainforger`
- Unity version declared in the package: `2020.3`
- Package type: editor-only

## Current Workflow

The current supported menu flow is:

```text
TerrainForger/Get GIS Data
-> TerrainForger/Geotiff2Raw Export
-> TerrainForger/Import Tiles
```

## Features

- Download DEM data through the integrated OpenTopography workflow
- Download satellite imagery through the integrated Mapbox workflow
- Preview source rasters and DEM coverage inside the Unity Editor
- Export DEM GeoTIFFs to tiled RAW heightmaps
- Export satellite GeoTIFFs to tiled PNGs
- Mask coastlines during DEM export with either `GSHHG` or `OpenStreetMap`
- Import tiled RAW terrain data into Unity Terrain assets

## Dependencies

Required Unity modules:

- `com.unity.modules.terrain`
- `com.unity.modules.imageconversion`

External requirements:

- Unity `2020.3` or newer within the package baseline
- QGIS installed locally for GDAL-based bounds, preview, cropping, reprojection, and export workflows
- Enough disk space for DEM, satellite, and generated terrain data

## Installation

### Git URL

Add the package to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.arantes83.terrainforger": "https://github.com/Arantes83/terrainforger.git"
  }
}
```

### Package Manager

Open `Window > Package Manager`, choose `Add package from git URL...`, and paste:

```text
https://github.com/Arantes83/terrainforger.git
```

### Local Development

Use `Add package from disk...` and point Unity to this repository's `package.json`.

## Generated Project Folders

TerrainForger writes generated content into the consuming Unity project. Typical folders are:

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

Folder summary:

- `Assets/Terrain/GeoTIFF`: source or downloaded DEM rasters
- `Assets/Terrain/SAT`: source or downloaded satellite rasters
- `Assets/Terrain/Raw`: exported RAW heightmap tiles
- `Assets/Terrain/PNG`: exported satellite PNG tiles
- `Assets/Terrain/GSHHG`: auto-downloaded GSHHG shoreline dataset
- `Assets/Terrain/OSMCoastline`: auto-downloaded OpenStreetMap land polygons
- `Assets/Generated/TerrainTiles`: generated Unity Terrain assets

## Data Services

The TerrainForger Data Services panel currently exposes:

- `OpenTopography`
- `Mapbox`
- `Google Maps Platform`
- `QGIS`

Important note:

- The integrated `Get GIS Data` workflow currently uses `OpenTopography` for DEM downloads and `Mapbox` for satellite downloads.
- `Google Maps Platform` remains available in service settings, but it is not currently used by the integrated acquisition window.

Direct credential pages:

- OpenTopography: `https://portal.opentopography.org/myopentopo`
- Mapbox: `https://console.mapbox.com/account/access-tokens/`
- Google Maps Platform: `https://console.cloud.google.com/apis/credentials`

## Credential Safety

- API keys are not hardcoded in the package.
- Credentials are stored locally in the consuming project's `UserSettings/TerrainDataServiceSettings.asset`.
- `UserSettings/` is ignored by the repository, so those values are not distributed with the package by default.
- For distributed editor workflows, prefer client-safe keys and restrict them on the provider side.

## Documentation

- Package entry page: [Documentation~/index.md](Documentation~/index.md)
- Full manual: [Documentation~/UserManual.md](Documentation~/UserManual.md)
- Changelog: [CHANGELOG.md](CHANGELOG.md)

## License

This package is released under the MIT license. See [LICENSE](LICENSE).
