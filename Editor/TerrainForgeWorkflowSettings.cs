using UnityEditor;
using UnityEngine;

[FilePath("UserSettings/TerrainForgeWorkflowSettings.asset", FilePathAttribute.Location.ProjectFolder)]
public class TerrainForgeWorkflowSettings : ScriptableSingleton<TerrainForgeWorkflowSettings>
{
    [Header("GIS Data Input")]
    public TerrainForgerLocalSourceType localSourceType = TerrainForgerLocalSourceType.None;
    public string localSourcePath = string.Empty;
    public string demProviderId = TerrainDataProviderIds.OpenTopography;
    public string imageryProviderId = TerrainDataProviderIds.Mapbox;
    public TerrainForgerSatelliteResolutionUnit satelliteResolutionUnit = TerrainForgerSatelliteResolutionUnit.MetersPerPixel;
    public float satelliteResolution = 1f;
    public string lastDemGeoTiffPath = string.Empty;
    public string lastSatelliteImagePath = string.Empty;

    [Header("Grid")]
    public int rows = 6;
    public int cols = 4;
    public int heightmapResolution = 1025;
    public string filePattern = "{tile}.raw";

    [Header("Input")]
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
    public Vector3 terrainOrigin = Vector3.zero;

    [Header("Terrain")]
    public int groupingId = 1;
    public bool allowAutoConnect = true;
    public bool drawInstanced = true;
    public float heightmapPixelError = 5f;
    public float basemapDistance = 1000f;

    [Header("Output")]
    public string outputFolder = "Assets/Generated/TerrainTiles";
    public string rootObjectName = "TerrainTileRoot";
    public bool replaceExistingRoot = true;
    public bool createWaterPlane;
    public Material waterMaterial;

    [Header("GeoTIFF Export")]
    public string geoTiffPath = string.Empty;
    public string satelliteGeoTiffPath = string.Empty;
    public string satelliteOutputFolder = "Assets/Terrain/PNG";
    public int satelliteTileResolution = 1024;
    public bool exportUseGshhgMask;
    public TerrainForgerCoastlineDataSource coastlineDataSource = TerrainForgerCoastlineDataSource.Gshhg;
    public TerrainForgerGshhgResolutionMode gshhgResolutionMode = TerrainForgerGshhgResolutionMode.Auto;
    public float exportWaterMaskElevation = -10f;
    public bool writeExportManifest = true;

    [Header("GeoTIFF Bounds")]
    public LatitudeDdm northBound = LatitudeDdm.Create(LatitudeHemisphere.South, 12, 30, 0);
    public LatitudeDdm southBound = LatitudeDdm.Create(LatitudeHemisphere.South, 13, 15, 0);
    public LongitudeDdm westBound = LongitudeDdm.Create(LongitudeHemisphere.West, 39, 0, 0);
    public LongitudeDdm eastBound = LongitudeDdm.Create(LongitudeHemisphere.West, 38, 20, 0);

    public TerrainTileImportConfig CreateRuntimeConfig()
    {
        var config = ScriptableObject.CreateInstance<TerrainTileImportConfig>();
        CopyTo(config);
        return config;
    }

    public void CopyFromRuntimeConfig(TerrainTileImportConfig config)
    {
        if (config == null)
        {
            return;
        }

        rows = config.rows;
        cols = config.cols;
        heightmapResolution = config.heightmapResolution;
        filePattern = config.filePattern;
        inputFolder = config.inputFolder;
        inputIsLittleEndian = config.inputIsLittleEndian;
        flipHorizontally = config.flipHorizontally;
        flipVertically = config.flipVertically;
        rowsStartAtNorth = config.rowsStartAtNorth;
        colsStartAtWest = config.colsStartAtWest;
        tileSizeX = config.tileSizeX;
        tileSizeZ = config.tileSizeZ;
        minElevation = config.minElevation;
        maxElevation = config.maxElevation;
        terrainOrigin = config.terrainOrigin;
        groupingId = config.groupingId;
        allowAutoConnect = config.allowAutoConnect;
        drawInstanced = config.drawInstanced;
        heightmapPixelError = config.heightmapPixelError;
        basemapDistance = config.basemapDistance;
        outputFolder = config.outputFolder;
        rootObjectName = config.rootObjectName;
        replaceExistingRoot = config.replaceExistingRoot;
        createWaterPlane = config.createWaterPlane;
        waterMaterial = config.waterMaterial;
        geoTiffPath = config.geoTiffPath;
        satelliteGeoTiffPath = config.satelliteGeoTiffPath;
        satelliteOutputFolder = config.satelliteOutputFolder;
        satelliteTileResolution = config.satelliteTileResolution;
        exportUseGshhgMask = config.exportUseGshhgMask;
        coastlineDataSource = config.coastlineDataSource;
        gshhgResolutionMode = config.gshhgResolutionMode;
        exportWaterMaskElevation = config.exportWaterMaskElevation;
        writeExportManifest = config.writeExportManifest;
        northBound = config.northBound;
        southBound = config.southBound;
        westBound = config.westBound;
        eastBound = config.eastBound;
    }

    public void SaveSettings()
    {
        Save(true);
    }

    private void CopyTo(TerrainTileImportConfig config)
    {
        config.rows = rows;
        config.cols = cols;
        config.heightmapResolution = heightmapResolution;
        config.filePattern = filePattern;
        config.inputFolder = inputFolder;
        config.inputIsLittleEndian = inputIsLittleEndian;
        config.flipHorizontally = flipHorizontally;
        config.flipVertically = flipVertically;
        config.rowsStartAtNorth = rowsStartAtNorth;
        config.colsStartAtWest = colsStartAtWest;
        config.tileSizeX = tileSizeX;
        config.tileSizeZ = tileSizeZ;
        config.minElevation = minElevation;
        config.maxElevation = maxElevation;
        config.terrainOrigin = terrainOrigin;
        config.groupingId = groupingId;
        config.allowAutoConnect = allowAutoConnect;
        config.drawInstanced = drawInstanced;
        config.heightmapPixelError = heightmapPixelError;
        config.basemapDistance = basemapDistance;
        config.outputFolder = outputFolder;
        config.rootObjectName = rootObjectName;
        config.replaceExistingRoot = replaceExistingRoot;
        config.createWaterPlane = createWaterPlane;
        config.waterMaterial = waterMaterial;
        config.geoTiffPath = geoTiffPath;
        config.satelliteGeoTiffPath = satelliteGeoTiffPath;
        config.satelliteOutputFolder = satelliteOutputFolder;
        config.satelliteTileResolution = satelliteTileResolution;
        config.qgisInstallFolder = TerrainDataServiceSettings.instance.QgisInstallFolder;
        config.exportUseGshhgMask = exportUseGshhgMask;
        config.coastlineDataSource = coastlineDataSource;
        config.gshhgResolutionMode = gshhgResolutionMode;
        config.exportWaterMaskElevation = exportWaterMaskElevation;
        config.writeExportManifest = writeExportManifest;
        config.northBound = northBound;
        config.southBound = southBound;
        config.westBound = westBound;
        config.eastBound = eastBound;
    }
}
