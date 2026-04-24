# TerrainForger User Manual

---

# 1. Introduction

## 1.1 What is TerrainForger

TerrainForger is a Unity Editor addon designed for importing, processing, and generating large-scale terrains from real-world geospatial data sources such as DEM GeoTIFFs and satellite imagery.

Its primary goal is to provide a production-ready workflow for converting GIS elevation data and satellite imagery into optimized Unity Terrain workflows.

TerrainForger standardizes the GIS → Unity terrain pipeline, reducing manual work, import errors, and inconsistent terrain generation.

This manual covers installation, configuration, usage, workflows, troubleshooting, performance guidelines, and best practices.

---

## 1.2 Primary Purpose

TerrainForger was created to simplify the transformation of GIS datasets into Unity terrains while preserving scale consistency, reducing manual intervention, and avoiding common terrain import failures.

Main goals:

* Convert DEM GeoTIFF files into Unity-compatible RAW heightmaps
* Import tiled terrain data efficiently
* Apply satellite imagery to terrain tiles
* Support large world terrain generation workflows
* Standardize production pipelines for GIS-driven terrain projects

---

## 1.3 Supported Use Cases

TerrainForger is recommended for:

* Military simulation environments
* Professional simulators
* Training systems
* Geographic visualization
* Open world terrain generation
* Large-scale terrain reconstruction
* Real-world terrain replication
* Defense and institutional simulation projects

---

## 1.4 Target Users

This addon is intended for:

* Unity technical artists
* GIS professionals
* Simulation developers
* Terrain artists
* Environment teams
* Defense simulation teams
* Large world environment production pipelines

---

## 1.5 Official Supported Unity Version

TerrainForger was developed and validated using:

```text
Unity 2020.3.22f1 Personal
```

This is the officially supported version for production usage.

Other Unity versions may work, but they are not officially supported or guaranteed to behave identically due to API differences in Unity Editor tools, Terrain systems, asset serialization, and terrain import pipelines.

Using different Unity versions may cause:

* Editor menu issues
* Terrain import inconsistencies
* Asset serialization conflicts
* Terrain API incompatibilities
* Unexpected behavior during GeoTIFF import workflows

For maximum stability, always use the officially supported version whenever possible.

---

# 2. Installation

## 2.1 Project Requirements

Before using TerrainForger, ensure your project includes:

* Unity 2020.3.22f1 Personal
* Sufficient disk space for DEM and satellite imagery
* SSD storage recommended for large imports
* At least 32 GB RAM recommended for large terrain workflows
* Dedicated GPU recommended for editor stability

Large terrain projects require significantly more disk throughput and memory than standard Unity projects.

---

## 2.2 Importing TerrainForger into Unity

1. Clone or download the repository
2. Install or import the TerrainForger package into your Unity project
3. Open Unity using the officially supported version
4. Wait for script compilation to finish
5. Resolve any compile issues before continuing
6. Access the addon through the Unity menu

Repository:

```bash
git clone https://github.com/Arantes83/terrainforger.git
```

After compilation, access the tool through:

```text
Tools > TerrainForger
```

If the menu does not appear, see the Troubleshooting section.

---

## 2.3 Automatically Generated Folder Structure

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

Unity also generates `.meta` files automatically for every folder and asset. These files must remain versioned when committed.

Avoid manually changing this structure unless there is a specific production requirement.

---

# 3. System Architecture Overview

## 3.1 TerrainForger Modules

TerrainForger is divided into the following core modules:

* GeoTIFF2Raw Export
* Terrain Tile Importer
* Terrain Data Services
* DEM Processing
* Satellite Integration

Each module supports a specific stage of the GIS → Unity workflow.

---

## 3.2 Official Processing Pipeline

Supported pipeline:

```text
Prepare GIS Data
→ Export GeoTIFF to RAW
→ Import Terrain Tiles
→ Apply Satellite Textures
→ Validate Terrain Scale
→ Final Optimization
```

Changing the order of this workflow may produce invalid terrains.

This is the official supported production workflow.

---

# 4. GIS Data Preparation

## 4.1 Supported Source Formats

Primary supported formats:

* DEM GeoTIFF (.tif)
* Satellite GeoTIFF (.tif)

Recommended:

* tiled source datasets
* clean DEM elevation data
* matching DEM and satellite coverage
* validated raster boundaries

---

## 4.2 Coordinate Systems and Projection Considerations

Always validate:

* projection consistency
* coordinate reference system (CRS)
* tile alignment
* terrain scale compatibility
* elevation reference consistency

Recommended:

* consistent projected coordinate systems
* validated UTM workflows when applicable

Avoid mixing CRS definitions between DEM and imagery.

Projection mismatch is one of the most common causes of invalid terrain generation.

---

# 5. GeoTIFF2Raw Export

## 5.1 Module Purpose

This module converts DEM GeoTIFF files into RAW heightmaps compatible with Unity Terrain.

It also validates source folders and supports synchronized DEM + satellite workflows.

This is the most critical module of the addon.

---

## 5.2 Source and Output Section

### DEM GeoTIFF Folder

Source folder containing DEM elevation files.

### Satellite GeoTIFF Folder

Source folder containing satellite imagery.

### RAW Output Folder

Destination folder for generated RAW heightmaps.

### Automatic Filename Detection

TerrainForger automatically updates detected filenames based on folder contents.

This reduces manual errors and ensures correct file pairing.

---

## 5.3 Export Parameters

Main parameters:

* Heightmap Resolution
* Bit Depth
* Height Normalization
* Terrain Size Configuration
* Validation Rules

Incorrect values here create invalid terrain imports.

Always validate export parameters before generating RAW files.

---

## 5.4 Common Export Errors

Typical issues:

* DEM not detected
* Satellite mismatch
* invalid height normalization
* unsupported resolution
* RAW export failure
* wrong tile pairing

Most failures are caused by invalid source preparation.

---

# 6. Terrain Tile Importer

## 6.1 Module Purpose

This module imports generated RAW files and creates Unity Terrain tiles.

It supports:

* tile generation
* terrain stitching
* satellite texture assignment
* material setup
* grid configuration
* large world support

---

## 6.2 Import Considerations

Before importing:

* validate RAW output
* confirm DEM and imagery alignment
* confirm expected terrain resolution
* verify tile count consistency

Incorrect imports usually originate from incorrect exports.

---

# 7. Terrain Data Services

## 7.1 Module Purpose

Responsible for controlled terrain data workflows and external data service configuration.

This module must be configured carefully to avoid credential exposure and unstable production pipelines.

---

## 7.2 Best Practices

Recommended:

* avoid exposing credentials in repositories
* keep service configuration separated from generated assets
* validate imported data before production usage

Never treat terrain services as disposable temporary setup.

This is production infrastructure.

---

# 8. Performance Guidelines

## 8.1 Terrain Resolution Limits

Avoid extremely large single terrains.

Prefer:

* multiple terrain tiles
* controlled resolution
* predictable memory usage
* staged imports instead of full-world imports

Large terrains should be split intentionally.

---

## 8.2 Import Performance

Recommended:

* SSD storage
* 32 GB RAM or more
* dedicated GPU
* tile-based workflows

Avoid importing massive terrains as a single operation.

Editor freezes are often caused by excessive terrain resolution.

---

# 9. Troubleshooting

## 9.1 TerrainForger Menu Does Not Appear

Check:

* successful script compilation
* Unity Console errors
* supported Unity version
* package imported correctly

This is usually caused by compile errors or version mismatch.

---

## 9.2 Incorrect Terrain Scale

Usually caused by:

* CRS mismatch
* invalid normalization
* wrong export parameters

Always verify source data before debugging import code.

---

## 9.3 Import Freezes Unity

Usually caused by:

* terrain too large
* insufficient RAM
* invalid RAW dimensions
* oversized satellite textures

Reduce terrain scope before attempting full imports.

---

# 10. Best Practices

## 10.1 Folder Discipline

Never mix:

* source GIS files
* generated RAW files
* final Unity terrain assets

Use strict separation between source and generated content.

This prevents accidental corruption and improves reproducibility.

---

## 10.2 Data Safety

Always:

* backup original DEM files
* keep DEM and satellite datasets synchronized
* validate coordinate systems before importing
* use consistent tile naming conventions
* preserve reproducible workflows

Terrain pipelines must be deterministic.

---

# 11. Frequently Asked Questions

## 11.1 Can I Use Unity 2021 or Newer?

Possibly yes.

Official support exists only for:

```text
Unity 2020.3.22f1 Personal
```

Other versions may work but are not guaranteed.

---

## 11.2 Should I Use One Large Terrain or Multiple Tiles?

Multiple tiles.

This improves:

* performance
* memory management
* terrain streaming
* production stability

Single massive terrains are rarely the correct production solution.

---

# 12. Appendix

## 12.1 Known Limitations

* very large DEM imports may freeze the editor
* unsupported CRS may create invalid scaling
* mismatched tile sizes cause stitching issues
* extremely large textures may exceed memory budgets

Always validate source data before import.

---

## 12.2 Documentation Entry Point

For Unity Package Manager documentation flow:

```text
Documentation~/index.md
```

should remain as the short entry page.

The complete operational documentation should remain in:

```text
Documentation~/UserManual.md
```

This is the recommended professional structure.

---
