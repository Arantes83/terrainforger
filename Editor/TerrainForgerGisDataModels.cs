public enum TerrainForgerLocalSourceType
{
    None,
    GeoTiff,
    KapChart
}

public enum TerrainForgerSatelliteResolutionUnit
{
    PixelsPerMeter,
    MetersPerPixel
}

public struct TerrainForgerSatelliteDownloadPlan
{
    public bool isValid;
    public string providerId;
    public double widthMeters;
    public double heightMeters;
    public double pixelsPerMeter;
    public double metersPerPixel;
    public int totalWidthPixels;
    public int totalHeightPixels;
    public int maxTileSize;
    public int tilesX;
    public int tilesY;
    public int totalTiles;
    public int maxTileWidthPixels;
    public int maxTileHeightPixels;
    public bool requiresTiling;
    public string warningMessage;
}
