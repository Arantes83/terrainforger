# TerrainForger

<p align="center">
  <img src="Documentation~/images/TerrainForger.png" alt="TerrainForger" width="420" />
</p>

TerrainForger is a Unity Editor addon designed for importing, processing, and generating large-scale terrains from real-world geospatial data sources such as DEM GeoTIFFs and satellite imagery.

Its primary goal is to provide a production-ready workflow for converting GIS elevation data and satellite imagery into optimized Unity Terrain workflows.

TerrainForger standardizes the GIS → Unity terrain pipeline, reducing manual work, import errors, and inconsistent terrain generation.

---

# Official Supported Unity Version

```text
Unity 2020.3.22f1 Personal
```

This is the officially supported version for production use.

Other Unity versions may work, but they are not officially supported or guaranteed to behave identically due to API differences in Unity Editor tools, Terrain systems, and asset import pipelines.

For maximum stability, use the supported version whenever possible.

---

# Core Features

## GeoTIFF2Raw Export

Convert DEM GeoTIFF files into Unity-compatible RAW heightmaps with validation and synchronized DEM + satellite workflows.

Includes:

* DEM folder validation
* Satellite GeoTIFF integration
* RAW export pipeline
* Automatic filename detection
* Height normalization
* Terrain size configuration

---

## Terrain Tile Importer

Import RAW files and generate Unity Terrain tiles with proper terrain stitching and material assignment.

Includes:

* Tile grid generation
* Terrain stitching
* Satellite texture assignment
* Material setup
* Large world support

---

## Terrain Data Services

Simplified and secure terrain data service configuration for controlled production workflows.

Includes:

* safer credential handling
* production-ready configuration
* validation workflows

---

# Recommended Workflow

Official supported pipeline:

```text
Prepare GIS Data
→ Export GeoTIFF to RAW
→ Import Terrain Tiles
→ Apply Satellite Textures
→ Validate Terrain Scale
→ Final Optimization
```

Changing the order of this workflow may produce invalid terrains.

---

# Installation

## 1. Clone or Download the Repository

```bash
git clone https://github.com/Arantes83/terrainforger.git
```

---

## 2. Copy Into Your Unity Project

Recommended location:

```text
Assets/Editor/TerrainForger
```

---

## 3. Open Unity

Open the project using:

```text
Unity 2020.3.22f1 Personal
```

Wait for script compilation to finish.

Do not interrupt the first compilation.

---

## 4. Access the Tool

After compilation, access the addon through:

```text
Tools > TerrainForger
```

If the menu does not appear, check the Troubleshooting section in the full documentation.

---

# Full Documentation

Complete usage documentation is available here:

[User Manual](Documentation~/UserManual.md)

All users are strongly recommended to read the manual before using the addon.

---

# Automatically Generated Folder Structure

TerrainForger automatically creates the required working folder structure inside `Assets/` during the workflow.

Manual folder creation is not required.

Official generated structure:

```text
Assets/
├── Generated/
│   └── TerrainTiles/
│       └── Generated Unity terrain tile assets
│
└── Terrain/
    ├── GeoTIFF/
    │   └── Source GeoTIFF files used as geographic reference/input
    │
    ├── OSMCoastline/
    │   └── OpenStreetMap coastline/vector support data
    │
    ├── PNG/
    │   └── Generated satellite PNG tiles
    │
    ├── Raw/
    │   └── Generated RAW 16-bit heightmap tiles
    │
    └── SAT/
        └── Source or downloaded satellite raster data
```

Folder purpose:

* `GeoTIFF/` stores source geographic raster data
* `SAT/` stores satellite raster source files
* `Raw/` stores exported RAW heightmaps for Unity Terrain
* `PNG/` stores generated satellite image tiles
* `OSMCoastline/` stores OpenStreetMap coastline support data
* `Generated/TerrainTiles/` stores the final generated Unity terrain tiles

Unity will also generate `.meta` files automatically for every folder and asset.
These files must remain versioned when committed.

This structure represents the real operational workflow of the addon and should be treated as the official supported pipeline.

Avoid manually changing this structure unless there is a specific production requirement.

---

# Performance Notes

For large terrain projects:

Recommended:

* SSD storage
* 32 GB RAM or more
* Dedicated GPU
* Tiled terrain workflows instead of massive single terrains

Avoid extremely large single terrain imports whenever possible.

Large terrains should be split intentionally.

---

# Best Practices

* Never mix source GIS files with generated Unity assets
* Always backup original DEM files
* Keep DEM and satellite datasets synchronized
* Validate coordinate systems before importing
* Use consistent tile naming conventions
* Keep generated RAW files separated from source files

This prevents data corruption and long-term maintenance problems.

---

# Changelog

Project updates and feature changes are documented in:

[CHANGELOG.md](CHANGELOG.md)

---

# License

Please refer to the repository license information for usage permissions and restrictions.

---

# Final Note

TerrainForger is designed for production environments where terrain correctness, reproducibility, and workflow stability matter.

This is not intended to be a quick import utility.

It is a controlled GIS → Unity terrain pipeline.
