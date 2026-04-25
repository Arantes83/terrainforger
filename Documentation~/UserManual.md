# TerrainForger User Manual

## 1. Overview

TerrainForger is a Unity Editor package for GIS-driven terrain workflows. It is designed to move source rasters through a predictable sequence:

```text
Get GIS Data
-> Geotiff2Raw Export
-> Import Tiles
```

The package focuses on:

- DEM acquisition and storage
- satellite imagery acquisition and storage
- GeoTIFF preview and export workflows
- tiled RAW and PNG generation
- Unity Terrain import
- coastline masking with `GSHHG` or `OpenStreetMap`

## 2. Package Requirements

### Unity

- Package minimum declared in `package.json`: `2020.3`
- Recommended baseline: Unity `2020.3 LTS`

### External Tools

- QGIS installed locally
- GDAL tools available through the configured QGIS installation

### Unity Modules

- `com.unity.modules.terrain`
- `com.unity.modules.imageconversion`

## 3. Installation

### Git URL

Add this dependency to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.arantes83.terrainforger": "https://github.com/Arantes83/terrainforger.git"
  }
}
```

### Package Manager

Use `Window > Package Manager > Add package from git URL...` and enter:

```text
https://github.com/Arantes83/terrainforger.git
```

### Local Development

Use `Add package from disk...` and select the repository `package.json`.

After installation, wait for Unity to finish compilation before opening the tools.

## 4. Unity Menus

TerrainForger tools are available in:

- `TerrainForger/Get GIS Data`
- `TerrainForger/Geotiff2Raw Export`
- `TerrainForger/Import Tiles`
- `TerrainForger/Service Settings`

If these menus do not appear, check the Console for compile errors first.

## 5. Data Services

The TerrainForger Data Services panel currently contains configuration for:

- `OpenTopography`
- `Mapbox`
- `Google Maps Platform`
- `QGIS`

Current integration status:

- `OpenTopography` is used by the integrated DEM acquisition workflow
- `Mapbox` is used by the integrated satellite acquisition workflow
- `Google Maps Platform` can be configured in the panel, but it is not currently used by the integrated acquisition window

Direct credential pages:

- OpenTopography: `https://portal.opentopography.org/myopentopo`
- Mapbox: `https://console.mapbox.com/account/access-tokens/`
- Google Maps Platform: `https://console.cloud.google.com/apis/credentials`

## 6. Credential Safety

TerrainForger does not ship your API keys inside the package.

Current behavior:

- credentials are stored in the consuming project's `UserSettings/TerrainDataServiceSettings.asset`
- workflow settings are stored in the consuming project's `UserSettings/TerrainForgeWorkflowSettings.asset`
- `UserSettings/` is ignored by the repository and is not part of the package payload by default
- credential fields in the code start empty and are loaded at runtime from local project settings

For distributed editor workflows:

- use restricted keys
- prefer client-safe provider tokens where applicable
- do not commit `UserSettings/`

## 7. Working Folders

TerrainForger creates and uses folders inside the consuming Unity project.

Typical structure:

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

Folder purposes:

- `Assets/Terrain/GeoTIFF`: source or downloaded DEM rasters
- `Assets/Terrain/SAT`: source or downloaded satellite rasters
- `Assets/Terrain/Raw`: exported RAW heightmap tiles
- `Assets/Terrain/PNG`: exported satellite PNG tiles
- `Assets/Terrain/GSHHG`: auto-downloaded GSHHG shoreline dataset
- `Assets/Terrain/OSMCoastline`: auto-downloaded OpenStreetMap land polygons
- `Assets/Generated/TerrainTiles`: generated Unity Terrain assets

## 8. Get GIS Data

Use `TerrainForger/Get GIS Data` to prepare source data before export.

Main capabilities:

- choose a local source file and preview it
- store local source data inside `Assets/Terrain`
- refill bounds from a spatial file using GDAL
- download DEM data with OpenTopography
- download satellite imagery with Mapbox

Important note:

- the provider dropdowns in this window currently resolve to `OpenTopography` for DEM and `Mapbox` for imagery

## 9. Geotiff2Raw Export

Use `TerrainForger/Geotiff2Raw Export` to create tiled terrain inputs for Unity.

Main capabilities:

- export DEM GeoTIFF tiles as RAW heightmaps
- export satellite GeoTIFF tiles as PNGs
- preview the DEM split before export
- write an export manifest
- apply coastline masking

### Coastline Mask

The coastline mask section currently supports:

- enabling or disabling land masking
- choosing `GSHHG` or `OpenStreetMap` as the coastline source
- choosing the GSHHG resolution when `GSHHG` is selected
- setting a `Water Elevation` used outside the land mask

Current behavior:

- `GSHHG` is auto-downloaded into `Assets/Terrain/GSHHG`
- `OpenStreetMap` land polygons are auto-downloaded into `Assets/Terrain/OSMCoastline`
- manual vector override fields are not part of the current workflow
- the old `Clamp Elevation` option is no longer part of the export UI

## 10. Import Tiles

Use `TerrainForger/Import Tiles` after export.

Main capabilities:

- read tiled RAW data
- generate Unity Terrain assets
- connect terrain neighbors
- assign satellite textures
- write generated assets into `Assets/Generated/TerrainTiles`

## 11. Recommended Workflow

Recommended order:

```text
Get GIS Data
-> Geotiff2Raw Export
-> Import Tiles
-> Validate scale and coastline
-> Final optimization
```

Changing the order can lead to invalid bounds, mismatched satellite tiles, or terrain import issues.

## 12. Troubleshooting

### Menus Do Not Appear

Check:

- Unity compilation finished successfully
- the package imported correctly
- there are no Console errors

### Coastline Result Looks Wrong

Check:

- whether `GSHHG` or `OpenStreetMap` is selected
- whether the project region is large enough to justify the chosen GSHHG resolution
- whether the source imagery and shoreline dataset match the area you expect

### Export Fails

Check:

- QGIS path is configured correctly
- source GeoTIFFs exist
- output folders are valid
- required provider credentials are configured

## 13. Documentation Entry Points

- Package entry page: `Documentation~/index.md`
- Full manual: `Documentation~/UserManual.md`
