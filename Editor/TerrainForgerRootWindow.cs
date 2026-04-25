using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class TerrainForgerStartup
{
    private const string FirstOpenKey = "TerrainForger.ServiceSettingsOpenedOnce";

    static TerrainForgerStartup()
    {
        EditorApplication.delayCall += OpenServiceSettingsOnce;
    }

    private static void OpenServiceSettingsOnce()
    {
        if (SessionState.GetBool(FirstOpenKey, false))
        {
            return;
        }

        SessionState.SetBool(FirstOpenKey, true);
        if (!TerrainForgerRootWindow.AreServicesConfigured())
        {
            TerrainForgerRootWindow.Open();
            TerrainDataServiceSettingsProvider.OpenSettings();
        }
    }
}

public class TerrainForgerRootWindow : EditorWindow
{
    public static void Open()
    {
        var window = GetWindow<TerrainForgerRootWindow>("TerrainForger");
        window.minSize = new Vector2(520f, 360f);
        window.Show();
        window.Focus();
    }

    [MenuItem("TerrainForger/Service Settings")]
    public static void OpenServiceSettings()
    {
        TerrainDataServiceSettingsProvider.OpenSettings();
    }

    public static bool AreServicesConfigured()
    {
        var settings = TerrainDataServiceSettings.instance;
        return !string.IsNullOrWhiteSpace(settings.OpenTopographyApiKey) &&
               (!string.IsNullOrWhiteSpace(settings.MapboxAccessToken) ||
                !string.IsNullOrWhiteSpace(settings.GoogleMapsApiKey)) &&
               !string.IsNullOrWhiteSpace(settings.QgisInstallFolder);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("TerrainForger", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Central entry point for TerrainForger. Configure data services here before using DEM download, satellite download, GeoTIFF export, or terrain import workflows.", MessageType.Info);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Service Settings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("OpenTopography is used for DEM downloads, Mapbox or Google Maps Platform can be used for satellite imagery, and QGIS/GDAL is used for raster bounds, previews, cropping, reprojection, and export.", MessageType.None);
            if (GUILayout.Button(new GUIContent("Open Service Settings", "Configure OpenTopography, Mapbox, Google Maps Platform and QGIS/GDAL paths."), GUILayout.Height(28f)))
            {
                TerrainDataServiceSettingsProvider.OpenSettings();
            }
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);
            if (GUILayout.Button(new GUIContent("Get GIS Data", "Load a source file, force bounds from it, download DEM and satellite rasters for exactly that area.")))
            {
                TerrainForgeDownloadGeoDataWindow.Open();
            }
            if (GUILayout.Button(new GUIContent("Geotiff2Raw Export", "Split aligned DEM and satellite GeoTIFF files into matching RAW and PNG tile sets.")))
            {
                TerrainForgeGeotiff2RawExportWindow.Open();
            }
            if (GUILayout.Button(new GUIContent("Import Tiles", "Import RAW terrain tiles and exact-resolution PNG satellite layers into Unity Terrain.")))
            {
                TerrainForgeImportTilesWindow.Open();
            }
        }
    }
}
