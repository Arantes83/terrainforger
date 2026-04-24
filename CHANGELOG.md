# Changelog

## 2026-04-24

### Core / Root Addon

- Centralized **Service Settings** at the root `TerrainForger` menu instead of keeping duplicated buttons inside individual modules.
- Forced automatic opening of **Service Settings** on the first addon load to avoid invalid first-time configuration states.
- Removed duplicated and unnecessary menu entry `TerrainForger/TerrainForger`.

---

### Bounds / Source File Safety

- When using a **Source File**, the addon now forces DEM and SAT downloads to use the exact bounds extracted from the source file.
- Added protection against user error by preventing conflicting manual **Map Bounds** usage when a source file is active.
- `Refill Bounds From Source` now always refreshes bounds using the exact source geometry.

---

### GeoTIFF2Raw Export

- Fixed automatic synchronization of:
  - `DEM GeoTIFF`
  - `Satellite GeoTIFF`

inside **Source And Output**.

The window now automatically searches and updates using the newest valid `.tif/.tiff` files found in:

- `Assets/Terrain/GeoTIFF`
- `Assets/Terrain/SAT`

instead of keeping stale references.

---

### Preview Consistency

- DEM Preview and SAT Preview were adjusted to maintain the same:
  - resolution
  - bounds coverage
  - geographic area

as the Source Preview.

This avoids mismatched previews and incorrect terrain interpretation.

---

### Import Resolution

- Forced Unity texture import to preserve the exact generated PNG resolution.

Example:

- Generated file: `4096x4096`
- Previous Unity import: `2048x2048`
- New behavior: `4096x4096`

This guarantees heightmap and satellite precision consistency.

---

### UI / UX Improvements

- Added contextual **tooltips** to tool options across the addon to explain the purpose of each field and operation.
- Moved:
  - `Save Tool Settings`
  - `Reset Tool Settings`

from the top of tool windows to the bottom for better workflow ergonomics.

---

### Processing Feedback

- Added visual processing log panels inside each tool window.

These logs now show:

- completed steps
- current execution stage
- processing progress visibility

This improves debugging and operational clarity for long GIS workflows.

---

### Editor Stability Fixes

- Fixed compiler error:

`CS0104: 'Object' is an ambiguous reference between 'UnityEngine.Object' and 'object'`

by replacing ambiguous calls:

```csharp
Object.DestroyImmediate(...)
```

with explicit calls:

```csharp
UnityEngine.Object.DestroyImmediate(...)
```

Affected files:

- `TerrainTileImporter.cs`
- `TerrainForgeWindowUtility.cs`

---

### Documentation

- Updated project documentation to reflect:
  - new workflow protections
  - source-file-first pipeline behavior
  - root settings architecture
  - import precision improvements
  - processing logs
  - preview consistency
  - GeoTIFF synchronization fixes
