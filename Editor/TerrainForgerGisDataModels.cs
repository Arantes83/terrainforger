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

public enum TerrainForgerGshhgResolutionMode
{
    Auto,
    Full,
    High,
    Intermediate,
    Low,
    Crude
}

public enum TerrainForgerCoastlineDataSource
{
    Gshhg,
    OpenStreetMap
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
    public int zoomLevel;
    public int tilePixelWidth;
    public int tilePixelHeight;
    public int firstTileX;
    public int lastTileX;
    public int firstTileY;
    public int lastTileY;
    public string warningMessage;
}

[System.Serializable]
public class TerrainForgerGoogleMapsSessionRequest
{
    public string mapType;
    public string language;
    public string region;
    public string imageFormat;
}

[System.Serializable]
public class TerrainForgerGoogleMapsSessionResponse
{
    public string session;
    public string expiry;
    public int tileWidth;
    public int tileHeight;
    public string imageFormat;
}
