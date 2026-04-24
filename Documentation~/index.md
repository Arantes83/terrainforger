# TerrainForger

TerrainForger is a Unity Editor package focused on professional terrain generation workflows driven by real-world GIS data.

It provides a controlled pipeline for converting DEM elevation data and satellite imagery into production-ready Unity Terrain assets with consistent scale, tiled workflows, and reduced manual intervention.

This package is designed for simulation environments, large-world terrain generation, military applications, and professional GIS-driven terrain pipelines.

---

# Core Features

## Get GIS Data

Download DEM and satellite data from supported providers using integrated terrain acquisition workflows.

Includes:

* DEM source acquisition
* Satellite imagery retrieval
* Provider-based terrain workflows
* Controlled source data organization

---

## GeoTIFF2Raw Export

Convert GeoTIFF inputs into Unity-compatible tiled RAW heightmaps and PNG satellite outputs.

Includes:

* DEM validation
* Satellite synchronization
* RAW export pipeline
* PNG generation
* Automatic filename detection
* Height normalization
* Tile preparation for Unity Terrain import

---

## Import Tiles

Import generated tiled terrain data into Unity Terrain assets with support for large-scale terrain generation.

Includes:

* Terrain tile generation
* Terrain stitching
* Satellite texture assignment
* Material setup
* Large world support

---

# Official Supported Unity Version

```text
Unity 2020.3.22f1 Personal
```

This is the officially supported version for production usage.

Other Unity versions may work, but they are not officially supported or guaranteed to behave identically due to differences in Terrain APIs, Editor behavior, and asset serialization.

For maximum stability, use the officially supported version whenever possible.

---

# Package Dependencies

Required Unity modules:

* `com.unity.modules.terrain`
* `com.unity.modules.imageconversion`

These are required for terrain generation, RAW processing, and texture workflows.

---

# External Requirements

## Local Requirements

* Unity 2020.3.22f1 Personal
* Local QGIS installation for GDAL-based GeoTIFF workflows
* Sufficient disk space for DEM and satellite data
* SSD storage recommended for large imports
* 32 GB RAM recommended for large terrain projects

## Provider Requirements

* Provider credentials when using supported online terrain services

Improper external setup is one of the main causes of failed terrain workflows.

---

# Unity Menu Access

TerrainForger tools are available through:

* `TerrainForger/Get GIS Data`
* `TerrainForger/Geotiff2Raw Export`
* `TerrainForger/Import Tiles`

These menus represent the official supported production workflow.

---

# Automatically Generated Output Paths

The package writes generated content directly into the consuming Unity project.

TerrainForger automatically creates the required folder structure inside `Assets/`.

Manual folder creation is not required.

Official generated paths:

* `Assets/Terrain`
* `Assets/Generated`

Typical workflow folders include:

```text
Assets/
├── Generated/
│   └── TerrainTiles/
│
└── Terrain/
    ├── GeoTIFF/
    ├── OSMCoastline/
    ├── PNG/
    ├── Raw/
    └── SAT/
```

This structure should be treated as the official supported workflow.

---

# Installation

## Unity Package Manager

Install TerrainForger using:

* Git URL
* Local disk path pointing to `package.json`

Recommended for production usage.

## Local Development

The package may also be embedded locally for development and internal testing workflows.

After installation, open Unity and wait for script compilation to complete before using the tools.

---

# Recommended Workflow

Official supported pipeline:

```text
Get GIS Data
→ GeoTIFF Processing
→ RAW + PNG Export
→ Import Terrain Tiles
→ Terrain Validation
→ Final Optimization
```

Changing the workflow order may produce invalid terrains.

---

# Full Documentation

Complete operational documentation is available here:

[User Manual](UserManual.md)

All users are strongly recommended to read the full manual before starting production workflows.

---

# Final Note

TerrainForger is not intended to be a quick import utility.

It is a controlled GIS → Unity terrain production pipeline focused on reproducibility, terrain correctness, and long-term workflow stability.
