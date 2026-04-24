# TerrainForger

TerrainForger is a Unity Editor package focused on terrain workflows driven by GIS data.

## Features

- Download DEM and satellite data from supported providers
- Export GeoTIFF inputs to tiled RAW and PNG outputs
- Import tiled terrain data into Unity Terrain assets

## Dependencies

- `com.unity.modules.terrain`
- `com.unity.modules.imageconversion`

## External Requirements

- Unity 2020.3 or newer
- Local QGIS installation for GDAL-based GeoTIFF workflows
- Provider credentials when using online data sources

## Unity Menus

- `TerrainForger/Get GIS Data`
- `TerrainForger/Geotiff2Raw Export`
- `TerrainForger/Import Tiles`

## Output Paths

The package writes generated content into the consuming Unity project, using paths such as:

- `Assets/Terrain`
- `Assets/Generated`

## Installation

Install through the Unity Package Manager using the package Git URL or a local disk path to `package.json`.
