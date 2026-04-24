using UnityEditor;
using UnityEngine;

public class TerrainForgeImportTilesWindow : EditorWindow
{
    private const string RawInputDefault = "Assets/Terrain/Raw";
    private const string PngInputDefault = "Assets/Terrain/PNG";

    private Vector2 scrollPosition;
    private static readonly System.Collections.Generic.List<string> workflowLog = new System.Collections.Generic.List<string>();

    [MenuItem("TerrainForger/Import Tiles")]
    public static void Open()
    {
        var window = GetWindow<TerrainForgeImportTilesWindow>("Import Tiles");
        window.minSize = new Vector2(620f, 520f);
        window.Show();
        window.Focus();
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        var settings = TerrainForgeWorkflowSettings.instance;
        SyncDefaultInputFolder(settings);

        TerrainForgeWindowUtility.DrawSettingsHeader(
            settings,
            "TerrainForger: Import Tiles",
            "Import RAW 16-bit height tiles into Unity Terrain with proper offsets, vertical scale and neighbor stitching.");
        TerrainForgeWindowUtility.DrawImportSummary(settings);

        var serializedObject = new SerializedObject(settings);
        serializedObject.Update();

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Terrain Settings", EditorStyles.boldLabel);
            TerrainForgeWindowUtility.DrawProperty(serializedObject, "groupingId", "Grouping ID");
            TerrainForgeWindowUtility.DrawProperty(serializedObject, "allowAutoConnect", "Allow Auto Connect");
            TerrainForgeWindowUtility.DrawProperty(serializedObject, "drawInstanced", "Draw Instanced");
            TerrainForgeWindowUtility.DrawProperty(serializedObject, "heightmapPixelError", "Heightmap Pixel Error");
            TerrainForgeWindowUtility.DrawProperty(serializedObject, "basemapDistance", "Basemap Distance");
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
            TerrainForgeWindowUtility.DrawProperty(serializedObject, "outputFolder", "Terrain Asset Folder");
            TerrainForgeWindowUtility.DrawProperty(serializedObject, "rootObjectName", "Root Object Name");
            TerrainForgeWindowUtility.DrawProperty(serializedObject, "replaceExistingRoot", "Replace Existing Root");
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Water", EditorStyles.boldLabel);
            settings.createWaterPlane = EditorGUILayout.Toggle("Create Water Plane At 0", settings.createWaterPlane);
            settings.waterMaterial = (Material)EditorGUILayout.ObjectField(
                "Water Material",
                settings.waterMaterial,
                typeof(Material),
                allowSceneObjects: false);
            EditorGUILayout.HelpBox(
                "When enabled, TerrainForger creates a plane covering the imported terrain footprint at altitude 0 and applies the selected water material when available.",
                MessageType.None);
        }

        serializedObject.ApplyModifiedProperties();
        settings.SaveSettings();

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Reveal RAW Folder"))
            {
                TerrainForgeWindowUtility.RevealFolder(settings.inputFolder, "RAW Folder Missing");
            }

            if (GUILayout.Button("Reveal Terrain Assets"))
            {
                TerrainForgeWindowUtility.RevealFolder(settings.outputFolder, "Terrain Asset Folder Missing");
            }
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Import Terrains", GUILayout.Height(32f)))
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

    private static void SyncDefaultInputFolder(TerrainForgeWorkflowSettings settings)
    {
        settings.inputFolder = RawInputDefault;
        settings.satelliteOutputFolder = PngInputDefault;
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
