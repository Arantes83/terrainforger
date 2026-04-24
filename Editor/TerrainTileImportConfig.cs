using UnityEngine;

public class TerrainTileImportConfig : ScriptableObject
{
    [Header("Grid")]
    public int rows = 6;
    public int cols = 4;
    public int heightmapResolution = 1025;
    [Tooltip("Supported placeholders: {tile}, {colLetter}, {row1}, {row}, {col}. Example: {tile}.raw => A1.raw")]
    public string filePattern = "{tile}.raw";

    [Header("Input")]
    [Tooltip("Folder with the RAW files. Can be absolute or project-relative, for example Assets/TerrainSource.")]
    public string inputFolder = "Assets/Terrain/Raw";
    public bool inputIsLittleEndian = true;
    public bool flipHorizontally;
    public bool flipVertically = true;
    public bool rowsStartAtNorth = true;
    public bool colsStartAtWest = true;

    [Header("World Scale")]
    public float tileSizeX = 5000f;
    public float tileSizeZ = 5000f;
    public float minElevation = -20f;
    public float maxElevation = 980f;
    [Tooltip("World-space origin of the terrain grid. The importer adds minElevation to Y when placing tiles.")]
    public Vector3 terrainOrigin = Vector3.zero;

    [Header("Terrain")]
    public int groupingId = 1;
    public bool allowAutoConnect = true;
    public bool drawInstanced = true;
    public float heightmapPixelError = 5f;
    public float basemapDistance = 1000f;

    [Header("Output")]
    [Tooltip("Folder for generated TerrainData assets. Must stay inside Assets.")]
    public string outputFolder = "Assets/Generated/TerrainTiles";
    public string rootObjectName = "TerrainTileRoot";
    public bool replaceExistingRoot = true;
    public bool createWaterPlane;
    public Material waterMaterial;

    [Header("GeoTIFF Export")]
    [Tooltip("Path to the source GeoTIFF to crop, resample and convert into RAW tiles.")]
    public string geoTiffPath = string.Empty;
    [Tooltip("Path to the source satellite GeoTIFF to crop and export into PNG tiles.")]
    public string satelliteGeoTiffPath = string.Empty;
    [Tooltip("Folder for exported satellite PNG tiles. Can be absolute or project-relative.")]
    public string satelliteOutputFolder = "Assets/Terrain/PNG";
    [Tooltip("Texture resolution of each exported satellite tile.")]
    public int satelliteTileResolution = 1024;
    [Tooltip("Path to the QGIS installation folder. Example: C:/Program Files/QGIS 3.36.1")]
    public string qgisInstallFolder = string.Empty;
    [Tooltip("When enabled, TerrainForger uses a GSHHG land polygon vector to decide which samples remain land during DEM export.")]
    public bool exportUseGshhgMask;
    [Tooltip("Resolution used when selecting the GSHHG coastline dataset. Auto chooses based on the project region extent.")]
    public TerrainForgerGshhgResolutionMode gshhgResolutionMode = TerrainForgerGshhgResolutionMode.Auto;
    [Tooltip("Path to a GSHHG land polygon vector file, usually a .shp extracted from the official dataset.")]
    public string gshhgVectorPath = string.Empty;
    [Tooltip("Elevation assigned to DEM samples outside the GSHHG land mask.")]
    public float exportWaterMaskElevation = -10f;
    [Tooltip("When enabled, values are clamped to the min/max elevation range before conversion.")]
    public bool exportClampElevation = true;
    [Tooltip("Writes a text manifest with the export settings next to the RAW tiles.")]
    public bool writeExportManifest = true;

    [Header("GeoTIFF Bounds")]
    public LatitudeDdm northBound = LatitudeDdm.Create(LatitudeHemisphere.South, 12, 30, 0);
    public LatitudeDdm southBound = LatitudeDdm.Create(LatitudeHemisphere.South, 13, 15, 0);
    public LongitudeDdm westBound = LongitudeDdm.Create(LongitudeHemisphere.West, 39, 0, 0);
    public LongitudeDdm eastBound = LongitudeDdm.Create(LongitudeHemisphere.West, 38, 20, 0);
}
