using UnityEditor;
using UnityEngine;

public class TerrainForgeImportTilesWindow : EditorWindow
{
    private const string RawInputDefault = "Assets/Terrain/Raw";
    private const string PngInputDefault = "Assets/Terrain/PNG";
    private const string TerrainAssetsDefault = "Assets/Generated/TerrainTiles";
    private const string TerrainRootDefault = "TerrainTileRoot";

    private Vector2 scrollPosition;
    private static readonly System.Collections.Generic.List<string> workflowLog = new System.Collections.Generic.List<string>();

    [MenuItem("TerrainForger/Import Tiles")]
    public static void Open()
    {
        var window = GetWindow<TerrainForgeImportTilesWindow>("Import Tiles");
        window.minSize = new Vector2(700f, 560f);
        window.Show();
        window.Focus();
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        var settings = TerrainForgeWorkflowSettings.instance;
        SyncImportDefaults(settings);

        TerrainForgeWindowUtility.DrawSettingsHeader(
            settings,
            "TerrainForger: Import Tiles",
            "Import RAW 16-bit height tiles into Unity Terrain with proper offsets, vertical scale and neighbor stitching.");
        TerrainForgeWindowUtility.DrawImportSummary(settings);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Import Defaults", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This window now always imports from Assets/Terrain/Raw and Assets/Terrain/PNG, writes TerrainData assets to Assets/Generated/TerrainTiles, and recreates TerrainTileRoot on every import.",
                MessageType.None);
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Water", EditorStyles.boldLabel);
            settings.createWaterPlane = EditorGUILayout.Toggle(new GUIContent("Create Water Plane", "Create a plane that covers the imported terrain footprint after the tiles are generated."), settings.createWaterPlane);
            using (new EditorGUI.DisabledScope(!settings.createWaterPlane))
            {
                settings.waterPlaneElevation = EditorGUILayout.FloatField(new GUIContent("Water Plane Elevation", "World-space Y altitude where the generated water plane should be placed."), settings.waterPlaneElevation);
            }
            settings.waterMaterial = (Material)EditorGUILayout.ObjectField(
                new GUIContent("Water Material", "Optional material assigned to the generated water plane."),
                settings.waterMaterial,
                typeof(Material),
                allowSceneObjects: false);
            EditorGUILayout.HelpBox(
                "When enabled, TerrainForger creates a plane covering the imported terrain footprint at the selected water elevation and applies the selected material when available.",
                MessageType.None);
        }

        settings.SaveSettings();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Reveal RAW Folder", "Open the folder currently configured as the RAW tile input source.")))
            {
                TerrainForgeWindowUtility.RevealFolder(settings.inputFolder, "RAW Folder Missing");
            }

            if (GUILayout.Button(new GUIContent("Reveal Terrain Assets", "Open the folder where TerrainForger will create TerrainData assets and terrain layers.")))
            {
                TerrainForgeWindowUtility.RevealFolder(settings.outputFolder, "Terrain Asset Folder Missing");
            }
        }

        EditorGUILayout.Space();
        if (GUILayout.Button(new GUIContent("Import Terrains", "Import the current RAW and PNG tile set into Unity Terrain using the active settings."), GUILayout.Height(32f)))
        {
            RunImport(settings);
        }

        DrawWorkflowLog();
        TerrainForgeWindowUtility.DrawSettingsFooter(settings);
        EditorGUILayout.EndScrollView();
    }

    private void RunImport(TerrainForgeWorkflowSettings settings)
    {
        try
        {
            TerrainForgeWindowUtility.ExecuteWithRuntimeConfig(settings, TerrainTileImporter.Import);
            AddLog("Terrain tiles imported successfully.");
            EditorUtility.DisplayDialog("Import Complete", "Terrain tiles imported successfully.", "OK");
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("Import Failed", ex.Message, "OK");
        }
    }

    private static void SyncImportDefaults(TerrainForgeWorkflowSettings settings)
    {
        settings.inputFolder = RawInputDefault;
        settings.satelliteOutputFolder = PngInputDefault;
        settings.outputFolder = TerrainAssetsDefault;
        settings.rootObjectName = TerrainRootDefault;
        settings.replaceExistingRoot = true;
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
                EditorGUILayout.HelpBox("No import steps have run in this tool window yet.", MessageType.Info);
                return;
            }

            for (var i = 0; i < workflowLog.Count; i++)
            {
                EditorGUILayout.LabelField(workflowLog[i]);
            }
        }
    }
}
