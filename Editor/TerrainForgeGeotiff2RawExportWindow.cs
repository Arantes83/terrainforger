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
    private const string DemGeoTiffFolder = "Assets/Terrain/GeoTIFF";
    private const string SatelliteGeoTiffFolder = "Assets/Terrain/SAT";

    private Vector2 scrollPosition;
    private Texture2D demGridPreviewTexture;
    private string demGridPreviewSourcePath = string.Empty;
    private string demGridPreviewStatus = "No DEM preview loaded.";
    private static readonly System.Collections.Generic.List<string> workflowLog = new System.Collections.Generic.List<string>();

    [MenuItem("TerrainForger/Geotiff2Raw Export")]
    public static void Open()
    {
        var window = GetWindow<TerrainForgeGeotiff2RawExportWindow>("Geotiff2Raw Export");
        window.minSize = new Vector2(980f, 620f);
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
        RefreshSourcePathsFromFolders(settings);
        TerrainForgeWindowUtility.DrawSettingsHeader(
            settings,
            "TerrainForger: Geotiff2Raw Export",
            "Preview the DEM cut lines, then export DEM tiles as RAW 16-bit and satellite tiles as PNG using the same rows, columns and tile names.");

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Source And Output", EditorStyles.boldLabel);
                    settings.geoTiffPath = EditorGUILayout.TextField(new GUIContent("DEM GeoTIFF", "Source DEM GeoTIFF used for RAW tile export."), settings.geoTiffPath);
                    if (GUILayout.Button(new GUIContent("Browse DEM GeoTIFF", "Choose the DEM GeoTIFF that TerrainForger should split into RAW tiles.")))
                    {
                        BrowseGeoTiff(ref settings.geoTiffPath, "Select DEM GeoTIFF");
                    }

                    settings.satelliteGeoTiffPath = EditorGUILayout.TextField(new GUIContent("Satellite GeoTIFF", "Source satellite GeoTIFF used for PNG tile export."), settings.satelliteGeoTiffPath);
                    if (GUILayout.Button(new GUIContent("Browse Satellite GeoTIFF", "Choose the satellite GeoTIFF that TerrainForger should split into PNG tiles.")))
                    {
                        BrowseGeoTiff(ref settings.satelliteGeoTiffPath, "Select Satellite GeoTIFF");
                    }

                    settings.inputFolder = EditorGUILayout.TextField(new GUIContent("RAW Output Folder", "Folder where TerrainForger writes exported RAW terrain tiles."), settings.inputFolder);
                    settings.satelliteOutputFolder = EditorGUILayout.TextField(new GUIContent("PNG Output Folder", "Folder where TerrainForger writes exported PNG satellite tiles."), settings.satelliteOutputFolder);
                    settings.writeExportManifest = EditorGUILayout.Toggle(new GUIContent("Write Export Manifest", "Write a text manifest with export settings and bounds next to the generated tiles."), settings.writeExportManifest);
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Tile Layout", EditorStyles.boldLabel);
                    settings.rows = DrawStepperField("Rows", settings.rows, 1, 128, "Number of terrain rows to export from the source raster.");
                    settings.cols = DrawStepperField("Columns", settings.cols, 1, 128, "Number of terrain columns to export from the source raster.");
                    settings.heightmapResolution = DrawResolutionPopup(
                        "Heightmap Resolution",
                        settings.heightmapResolution,
                        HeightmapResolutionOptions,
                        "Unity heightmap resolution used for each exported RAW tile.");
                    settings.satelliteTileResolution = DrawResolutionPopup(
                        "Satellite Tile Resolution",
                        settings.satelliteTileResolution,
                        SatelliteResolutionOptions,
                        "Texture resolution used for each exported PNG satellite tile.");
                    settings.filePattern = EditorGUILayout.TextField(new GUIContent("File Pattern", "Naming pattern used for exported RAW files. Example: {tile}.raw"), settings.filePattern);
                    EditorGUILayout.HelpBox(
                        "The DEM elevation range is detected automatically from the source raster during export and stored for the next import step.",
                        MessageType.None);
                }

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Coastline Mask", EditorStyles.boldLabel);
                    settings.exportUseGshhgMask = EditorGUILayout.Toggle(new GUIContent("Use Coastline Land Mask", "Replace DEM samples outside the selected land mask with the configured water elevation."), settings.exportUseGshhgMask);
                    using (new EditorGUI.DisabledScope(!settings.exportUseGshhgMask))
                    {
                        settings.coastlineDataSource = DrawCoastlineSourcePopup("Coastline Source", settings.coastlineDataSource);

                        if (settings.coastlineDataSource == TerrainForgerCoastlineDataSource.Gshhg)
                        {
                            settings.gshhgResolutionMode = (TerrainForgerGshhgResolutionMode)EditorGUILayout.EnumPopup(new GUIContent("GSHHG Resolution", "Resolution level TerrainForger should use when selecting the GSHHG shoreline dataset."), settings.gshhgResolutionMode);
                        }

                        settings.exportWaterMaskElevation = EditorGUILayout.FloatField(new GUIContent("Water Elevation", "Elevation assigned to DEM samples outside the selected coastline land mask."), settings.exportWaterMaskElevation);
                    }
                    var coastlineHelp = settings.coastlineDataSource == TerrainForgerCoastlineDataSource.Gshhg
                        ? "Use GSHHG land polygons to define the shoreline. TerrainForger auto-downloads the official dataset and, in Auto mode, picks the best resolution for the current region. Samples outside the land mask are exported at the configured water elevation, which avoids clipping the coastline from DEM altitude alone."
                        : "Use OpenStreetMap-derived land polygons to define the shoreline. TerrainForger auto-downloads the processed OSM land polygons in WGS84. This option can better match edited or recent coastlines, while still masking the DEM by land polygons instead of clipping by elevation.";
                    EditorGUILayout.HelpBox(coastlineHelp, MessageType.None);
                }

                settings.SaveSettings();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Reveal RAW Folder", "Open the folder where TerrainForger writes RAW height tiles.")))
                    {
                        TerrainForgeWindowUtility.RevealFolder(settings.inputFolder, "RAW Folder Missing");
                    }

                    if (GUILayout.Button(new GUIContent("Reveal PNG Folder", "Open the folder where TerrainForger writes PNG satellite tiles.")))
                    {
                        TerrainForgeWindowUtility.RevealFolder(settings.satelliteOutputFolder, "PNG Folder Missing");
                    }
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Export DEM Tiles", "Export the active DEM GeoTIFF into RAW terrain tiles."), GUILayout.Height(28f)))
                    {
                        RunExport(settings, TerrainGeoTiffExporter.ExportToRawTiles, "DEM Export Complete", "DEM GeoTIFF exported to RAW tiles successfully.");
                    }

                    if (GUILayout.Button(new GUIContent("Export SAT Tiles", "Export the active satellite GeoTIFF into PNG terrain textures."), GUILayout.Height(28f)))
                    {
                        RunExport(settings, TerrainGeoTiffExporter.ExportSatelliteTiles, "Satellite Export Complete", "Satellite GeoTIFF exported to PNG tiles successfully.");
                    }

                    if (GUILayout.Button(new GUIContent("Export Both", "Export both DEM RAW tiles and satellite PNG tiles using the current layout."), GUILayout.Height(28f)))
                    {
                        RunExport(settings, TerrainGeoTiffExporter.ExportTerrainPackage, "Export Complete", "DEM and satellite tiles exported successfully.");
                    }
                }
            }

            using (new EditorGUILayout.VerticalScope(GUILayout.Width(360f)))
            {
                DrawPreviewSection(settings);
            }
        }

        DrawWorkflowLog();
        TerrainForgeWindowUtility.DrawSettingsFooter(settings);
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
                    if (GUILayout.Button(new GUIContent("Load DEM Preview", "Generate a DEM preview with the current grid overlay.")))
                    {
                        TryLoadPreview(settings.geoTiffPath);
                    }

                    if (GUILayout.Button(new GUIContent("Refresh Preview", "Regenerate the DEM preview texture from disk.")))
                    {
                        TryLoadPreview(settings.geoTiffPath, forceRefresh: true);
                    }
                }

                using (new EditorGUI.DisabledScope(demGridPreviewTexture == null))
                {
                    if (GUILayout.Button(new GUIContent("Clear Preview", "Release the DEM preview texture from memory.")))
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

    private static int DrawResolutionPopup(string label, int currentValue, int[] options, string tooltip)
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

        selectedIndex = EditorGUILayout.Popup(new GUIContent(label, tooltip), selectedIndex, labels);
        return options[Mathf.Clamp(selectedIndex, 0, options.Length - 1)];
    }

    private static int DrawStepperField(string label, int currentValue, int minValue, int maxValue, string tooltip)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PrefixLabel(new GUIContent(label, tooltip));

            if (GUILayout.Button(new GUIContent("<", $"Decrease {label.ToLowerInvariant()} by one."), GUILayout.Width(28f)))
            {
                currentValue = Mathf.Max(minValue, currentValue - 1);
            }

            GUILayout.Label(currentValue.ToString(), EditorStyles.centeredGreyMiniLabel, GUILayout.Width(48f));

            if (GUILayout.Button(new GUIContent(">", $"Increase {label.ToLowerInvariant()} by one."), GUILayout.Width(28f)))
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

    private static void RefreshSourcePathsFromFolders(TerrainForgeWorkflowSettings settings)
    {
        var latestDemPath = FindLatestGeoTiffAssetPath(DemGeoTiffFolder);
        if (ShouldReplaceSourcePath(settings.geoTiffPath, latestDemPath))
        {
            settings.geoTiffPath = latestDemPath;
            settings.lastDemGeoTiffPath = latestDemPath;
        }

        var latestSatellitePath = FindLatestGeoTiffAssetPath(SatelliteGeoTiffFolder);
        if (ShouldReplaceSourcePath(settings.satelliteGeoTiffPath, latestSatellitePath))
        {
            settings.satelliteGeoTiffPath = latestSatellitePath;
            settings.lastSatelliteImagePath = latestSatellitePath;
        }
    }

    private static bool ShouldReplaceSourcePath(string currentAssetPath, string candidateAssetPath)
    {
        if (string.IsNullOrWhiteSpace(candidateAssetPath))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(currentAssetPath))
        {
            return true;
        }

        if (string.Equals(currentAssetPath, candidateAssetPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var currentFullPath = TerrainForgeWindowUtility.ResolveFolderPath(currentAssetPath);
        var candidateFullPath = TerrainForgeWindowUtility.ResolveFolderPath(candidateAssetPath);

        if (!File.Exists(currentFullPath))
        {
            return true;
        }

        if (!File.Exists(candidateFullPath))
        {
            return false;
        }

        return File.GetLastWriteTimeUtc(candidateFullPath) > File.GetLastWriteTimeUtc(currentFullPath);
    }

    private static string FindLatestGeoTiffAssetPath(string assetFolder)
    {
        var folderFullPath = TerrainForgeWindowUtility.ResolveFolderPath(assetFolder);
        if (!Directory.Exists(folderFullPath))
        {
            return string.Empty;
        }

        FileInfo latestFile = null;
        foreach (var filePath in Directory.EnumerateFiles(folderFullPath, "*.*", SearchOption.TopDirectoryOnly))
        {
            if (!IsGeoTiffFile(filePath))
            {
                continue;
            }

            var fileInfo = new FileInfo(filePath);
            if (latestFile == null || fileInfo.LastWriteTimeUtc > latestFile.LastWriteTimeUtc)
            {
                latestFile = fileInfo;
            }
        }

        return latestFile == null ? string.Empty : ToAssetPath(latestFile.FullName);
    }

    private static bool IsGeoTiffFile(string path)
    {
        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".tif", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".tiff", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToAssetPath(string fullPath)
    {
        var normalizedFullPath = Path.GetFullPath(fullPath).Replace('\\', '/');
        var normalizedAssetsPath = Path.GetFullPath(Application.dataPath).Replace('\\', '/');

        if (normalizedFullPath.StartsWith(normalizedAssetsPath, StringComparison.OrdinalIgnoreCase))
        {
            return "Assets" + normalizedFullPath.Substring(normalizedAssetsPath.Length);
        }

        return normalizedFullPath;
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

    private static TerrainForgerCoastlineDataSource DrawCoastlineSourcePopup(string label, TerrainForgerCoastlineDataSource currentValue)
    {
        var options = new[] { "GSHHG", "OpenStreetMap" };
        var selectedIndex = currentValue == TerrainForgerCoastlineDataSource.OpenStreetMap ? 1 : 0;
        selectedIndex = EditorGUILayout.Popup(new GUIContent(label, "Choose which shoreline dataset TerrainForger should use when masking exported DEM tiles."), selectedIndex, options);
        return selectedIndex == 1 ? TerrainForgerCoastlineDataSource.OpenStreetMap : TerrainForgerCoastlineDataSource.Gshhg;
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
            AddLog(title + ": " + message);
            EditorUtility.DisplayDialog(title, message, "OK");
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Export Failed", ex.Message, "OK");
        }
    }

    private static void AddLog(string message)
    {
        workflowLog.Add(string.Format("{0:HH:mm:ss} - {1}", System.DateTime.Now, message));
        while (workflowLog.Count > 12)
        {
            workflowLog.RemoveAt(0);
        }
    }

    private static void DrawWorkflowLog()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Processing Log", EditorStyles.boldLabel);
            if (workflowLog.Count == 0)
            {
                EditorGUILayout.HelpBox("No export steps have run in this tool window yet.", MessageType.Info);
                return;
            }

            for (var i = 0; i < workflowLog.Count; i++)
            {
                EditorGUILayout.LabelField(workflowLog[i]);
            }
        }
    }
}
