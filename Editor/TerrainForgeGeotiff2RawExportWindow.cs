using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public class TerrainForgeGeotiff2RawExportWindow : EditorWindow
{
    private static readonly int[] HeightmapResolutionOptions = { 513, 1025, 2049, 4097 };
    private static readonly int[] SatelliteResolutionOptions = { 512, 1024, 2048, 4096 };
    private const string RawOutputDefault = "Assets/Terrain/Raw";
    private const string PngOutputDefault = "Assets/Terrain/PNG";

    private Vector2 scrollPosition;
    private Texture2D demGridPreviewTexture;
    private string demGridPreviewSourcePath = string.Empty;
    private string demGridPreviewStatus = "No DEM preview loaded.";

    [MenuItem("Tools/TerrainForger/Geotiff2Raw Export")]
    public static void Open()
    {
        var window = GetWindow<TerrainForgeGeotiff2RawExportWindow>("Geotiff2Raw Export");
        window.minSize = new Vector2(760f, 620f);
        window.Show();
        window.Focus();
    }

    private void OnDisable()
    {
        ReleasePreviewTexture();
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        var settings = TerrainForgeWorkflowSettings.instance;
        SyncDefaultSourcePaths(settings);
        TerrainForgeWindowUtility.DrawSettingsHeader(
            settings,
            "TerrainForger: Geotiff2Raw Export",
            "Preview the DEM cut lines, then export DEM tiles as RAW 16-bit and satellite tiles as PNG using the same rows, columns and tile names.");

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Source And Output", EditorStyles.boldLabel);
            settings.geoTiffPath = EditorGUILayout.TextField("DEM GeoTIFF", settings.geoTiffPath);
            if (GUILayout.Button("Browse DEM GeoTIFF"))
            {
                BrowseGeoTiff(ref settings.geoTiffPath, "Select DEM GeoTIFF");
            }

            settings.satelliteGeoTiffPath = EditorGUILayout.TextField("Satellite GeoTIFF", settings.satelliteGeoTiffPath);
            if (GUILayout.Button("Browse Satellite GeoTIFF"))
            {
                BrowseGeoTiff(ref settings.satelliteGeoTiffPath, "Select Satellite GeoTIFF");
            }

            settings.inputFolder = EditorGUILayout.TextField("RAW Output Folder", settings.inputFolder);
            settings.satelliteOutputFolder = EditorGUILayout.TextField("PNG Output Folder", settings.satelliteOutputFolder);
            settings.writeExportManifest = EditorGUILayout.Toggle("Write Export Manifest", settings.writeExportManifest);
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Tile Layout", EditorStyles.boldLabel);
            settings.rows = DrawStepperField("Rows", settings.rows, 1, 128);
            settings.cols = DrawStepperField("Columns", settings.cols, 1, 128);
            settings.heightmapResolution = DrawResolutionPopup(
                "Heightmap Resolution",
                settings.heightmapResolution,
                HeightmapResolutionOptions);
            settings.satelliteTileResolution = DrawResolutionPopup(
                "Satellite Tile Resolution",
                settings.satelliteTileResolution,
                SatelliteResolutionOptions);
            settings.filePattern = EditorGUILayout.TextField("File Pattern", settings.filePattern);
            EditorGUILayout.HelpBox(
                "The DEM elevation range is detected automatically from the source raster during export and stored for the next import step.",
                MessageType.None);
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Coastline Mask", EditorStyles.boldLabel);
            settings.exportClampElevation = EditorGUILayout.Toggle("Clamp Elevation", settings.exportClampElevation);
            settings.exportUseGshhgMask = EditorGUILayout.Toggle("Use GSHHG Land Mask", settings.exportUseGshhgMask);
            using (new EditorGUI.DisabledScope(!settings.exportUseGshhgMask))
            {
                settings.gshhgVectorPath = EditorGUILayout.TextField("GSHHG Vector", settings.gshhgVectorPath);
                if (GUILayout.Button("Browse GSHHG Vector"))
                {
                    BrowseVectorFile(ref settings.gshhgVectorPath, "Select GSHHG Land Vector");
                }

                settings.exportWaterMaskElevation = EditorGUILayout.FloatField("Water Elevation", settings.exportWaterMaskElevation);
            }
            EditorGUILayout.HelpBox(
                "Use a GSHHG land polygon vector to define the shoreline. Samples outside the land mask are exported at the configured water elevation, which avoids clipping the coastline from DEM altitude alone.",
                MessageType.None);
        }

        DrawPreviewSection(settings);

        settings.SaveSettings();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Reveal RAW Folder"))
            {
                TerrainForgeWindowUtility.RevealFolder(settings.inputFolder, "RAW Folder Missing");
            }

            if (GUILayout.Button("Reveal PNG Folder"))
            {
                TerrainForgeWindowUtility.RevealFolder(settings.satelliteOutputFolder, "PNG Folder Missing");
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Export DEM Tiles", GUILayout.Height(28f)))
            {
                RunExport(settings, TerrainGeoTiffExporter.ExportToRawTiles, "DEM Export Complete", "DEM GeoTIFF exported to RAW tiles successfully.");
            }

            if (GUILayout.Button("Export SAT Tiles", GUILayout.Height(28f)))
            {
                RunExport(settings, TerrainGeoTiffExporter.ExportSatelliteTiles, "Satellite Export Complete", "Satellite GeoTIFF exported to PNG tiles successfully.");
            }

            if (GUILayout.Button("Export Both", GUILayout.Height(28f)))
            {
                RunExport(settings, TerrainGeoTiffExporter.ExportTerrainPackage, "Export Complete", "DEM and satellite tiles exported successfully.");
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawPreviewSection(TerrainForgeWorkflowSettings settings)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Tile Preview", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Load a DEM preview to validate how the source raster will be split into rows and columns before exporting.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(settings.geoTiffPath)))
                {
                    if (GUILayout.Button("Load DEM Preview"))
                    {
                        TryLoadPreview(settings.geoTiffPath);
                    }

                    if (GUILayout.Button("Refresh Preview"))
                    {
                        TryLoadPreview(settings.geoTiffPath, forceRefresh: true);
                    }
                }

                using (new EditorGUI.DisabledScope(demGridPreviewTexture == null))
                {
                    if (GUILayout.Button("Clear Preview"))
                    {
                        ReleasePreviewTexture();
                        demGridPreviewStatus = "DEM preview cleared.";
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(settings.geoTiffPath))
            {
                EditorGUILayout.HelpBox("Choose a DEM GeoTIFF first to enable the tile preview.", MessageType.Info);
            }
            else if (demGridPreviewTexture != null)
            {
                var maxWidth = Mathf.Min(320f, EditorGUIUtility.currentViewWidth * 0.28f);
                var aspect = demGridPreviewTexture.height > 0 ? demGridPreviewTexture.width / (float)demGridPreviewTexture.height : 1f;
                var rect = GUILayoutUtility.GetRect(maxWidth, maxWidth / Mathf.Max(0.01f, aspect), GUILayout.ExpandWidth(false));
                EditorGUI.DrawPreviewTexture(rect, demGridPreviewTexture, null, ScaleMode.ScaleToFit);
                DrawTileGridOverlay(rect, settings.rows, settings.cols);
                EditorGUILayout.LabelField("Preview Size", $"{demGridPreviewTexture.width} x {demGridPreviewTexture.height}");
            }

            EditorGUILayout.HelpBox(demGridPreviewStatus, demGridPreviewTexture != null ? MessageType.None : MessageType.Info);
        }
    }

    private static int DrawResolutionPopup(string label, int currentValue, int[] options)
    {
        var labels = new string[options.Length];
        var selectedIndex = 0;

        for (var i = 0; i < options.Length; i++)
        {
            labels[i] = options[i].ToString();
            if (options[i] == currentValue)
            {
                selectedIndex = i;
            }
        }

        selectedIndex = EditorGUILayout.Popup(label, selectedIndex, labels);
        return options[Mathf.Clamp(selectedIndex, 0, options.Length - 1)];
    }

    private static int DrawStepperField(string label, int currentValue, int minValue, int maxValue)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel(label);

            if (GUILayout.Button("<", GUILayout.Width(28f)))
            {
                currentValue = Mathf.Max(minValue, currentValue - 1);
            }

            GUILayout.Label(currentValue.ToString(), EditorStyles.centeredGreyMiniLabel, GUILayout.Width(48f));

            if (GUILayout.Button(">", GUILayout.Width(28f)))
            {
                currentValue = Mathf.Min(maxValue, currentValue + 1);
            }
        }

        return currentValue;
    }

    private static void SyncDefaultSourcePaths(TerrainForgeWorkflowSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.inputFolder) ||
            string.Equals(settings.inputFolder, "Assets/TerrainSource", StringComparison.OrdinalIgnoreCase))
        {
            settings.inputFolder = RawOutputDefault;
        }

        if (string.IsNullOrWhiteSpace(settings.satelliteOutputFolder) ||
            string.Equals(settings.satelliteOutputFolder, "Assets/TerrainTiles/SAT", StringComparison.OrdinalIgnoreCase))
        {
            settings.satelliteOutputFolder = PngOutputDefault;
        }

        if (string.IsNullOrWhiteSpace(settings.geoTiffPath) &&
            !string.IsNullOrWhiteSpace(settings.lastDemGeoTiffPath))
        {
            settings.geoTiffPath = settings.lastDemGeoTiffPath;
        }

        if (string.IsNullOrWhiteSpace(settings.satelliteGeoTiffPath) &&
            !string.IsNullOrWhiteSpace(settings.lastSatelliteImagePath))
        {
            settings.satelliteGeoTiffPath = settings.lastSatelliteImagePath;
        }
    }

    private static void BrowseGeoTiff(ref string targetPath, string title)
    {
        var startFolder = string.IsNullOrWhiteSpace(targetPath)
            ? TerrainForgeWindowUtility.ResolveFolderPath("Assets")
            : Path.GetDirectoryName(TerrainForgeWindowUtility.ResolveFolderPath(targetPath));

        var selected = EditorUtility.OpenFilePanel(title, startFolder, string.Empty);
        if (!string.IsNullOrEmpty(selected))
        {
            targetPath = selected;
        }
    }

    private static void BrowseVectorFile(ref string targetPath, string title)
    {
        var startFolder = string.IsNullOrWhiteSpace(targetPath)
            ? TerrainForgeWindowUtility.ResolveFolderPath("Assets")
            : Path.GetDirectoryName(TerrainForgeWindowUtility.ResolveFolderPath(targetPath));

        var selected = EditorUtility.OpenFilePanel(title, startFolder, string.Empty);
        if (!string.IsNullOrEmpty(selected))
        {
            targetPath = selected;
        }
    }

    private void DrawTileGridOverlay(Rect rect, int rows, int cols)
    {
        Handles.BeginGUI();
        var previousColor = Handles.color;
        Handles.color = new Color(1f, 0.55f, 0.1f, 0.95f);

        for (var col = 1; col < cols; col++)
        {
            var x = rect.xMin + (rect.width * (col / (float)cols));
            Handles.DrawLine(new Vector3(x, rect.yMin), new Vector3(x, rect.yMax));
        }

        for (var row = 1; row < rows; row++)
        {
            var y = rect.yMin + (rect.height * (row / (float)rows));
            Handles.DrawLine(new Vector3(rect.xMin, y), new Vector3(rect.xMax, y));
        }

        Handles.color = previousColor;
        Handles.EndGUI();

        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < cols; col++)
            {
                var tileRect = new Rect(
                    rect.xMin + (rect.width * (col / (float)cols)),
                    rect.yMin + (rect.height * (row / (float)rows)),
                    rect.width / cols,
                    rect.height / rows);
                var labelRect = new Rect(tileRect.center.x - 24f, tileRect.center.y - 10f, 48f, 20f);
                var previousContentColor = GUI.contentColor;
                GUI.contentColor = Color.white;
                GUI.Label(labelRect, TerrainTileNaming.GetTileLabel(row, col), EditorStyles.whiteBoldLabel);
                GUI.contentColor = previousContentColor;
            }
        }
    }

    private void TryLoadPreview(string previewSource, bool forceRefresh = false)
    {
        if (!forceRefresh &&
            demGridPreviewTexture != null &&
            string.Equals(demGridPreviewSourcePath, previewSource, StringComparison.OrdinalIgnoreCase))
        {
            demGridPreviewStatus = "DEM preview is already up to date.";
            return;
        }

        try
        {
            ReleasePreviewTexture();
            demGridPreviewTexture = TerrainForgerGisDataUtility.CreateDemPreviewTexture(previewSource);
            demGridPreviewSourcePath = previewSource;
            demGridPreviewStatus = "DEM preview generated successfully.";
            Repaint();
        }
        catch (System.Exception ex)
        {
            ReleasePreviewTexture();
            demGridPreviewStatus = $"DEM preview failed: {ex.Message}";
            Debug.LogException(ex);
        }
    }

    private void ReleasePreviewTexture()
    {
        if (demGridPreviewTexture != null)
        {
            DestroyImmediate(demGridPreviewTexture);
            demGridPreviewTexture = null;
        }

        demGridPreviewSourcePath = string.Empty;
    }

    private static void RunExport(
        TerrainForgeWorkflowSettings settings,
        System.Action<TerrainTileImportConfig> exportAction,
        string title,
        string message)
    {
        try
        {
            TerrainForgeWindowUtility.ExecuteWithRuntimeConfig(settings, exportAction);
            EditorUtility.DisplayDialog(title, message, "OK");
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Export Failed", ex.Message, "OK");
        }
    }
}
