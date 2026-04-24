# TerrainForger

TerrainForger is a Unity Editor package focused on terrain workflows driven by GIS data.

## Features

- Download DEM and satellite data from supported providers
- Export GeoTIFF inputs to tiled RAW and PNG outputs
- Import tiled terrain data into Unity Terrain assets

## Unity Menus

- `Tools/TerrainForger/Get GIS Data`
- `Tools/TerrainForger/Geotiff2Raw Export`
- `Tools/TerrainForger/Import Tiles`

## Output Paths

The package writes generated content into the consuming Unity project, using paths such as:

- `Assets/Terrain`
- `Assets/Generated`

## Installation

Install through the Unity Package Manager using the package Git URL or a local disk path to `package.json`.
