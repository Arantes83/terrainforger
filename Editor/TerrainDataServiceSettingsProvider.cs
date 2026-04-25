using UnityEditor;
using UnityEngine;
using System.IO;

public static class TerrainDataServiceSettingsProvider
{
    private const string SettingsPath = "Project/TerrainForger Data Services";
    private static bool showSecrets;
    private static bool showQgis = true;
    private static bool showOpenTopography = true;
    private static bool showMapbox;
    private static bool showGoogleMaps;

    [SettingsProvider]
    public static SettingsProvider CreateSettingsProvider()
    {
        var provider = new SettingsProvider(SettingsPath, SettingsScope.Project)
        {
            label = "TerrainForger Data Services",
            guiHandler = DrawGui,
            keywords = new System.Collections.Generic.HashSet<string>
            {
                "terrain",
                "opentopography",
                "mapbox",
                "google",
                "api",
                "key",
                "gis",
                "dem",
                "imagery"
            }
        };

        return provider;
    }

    public static void OpenSettings()
    {
        SettingsService.OpenProjectSettings(SettingsPath);
    }

    private static void DrawGui(string searchContext)
    {
        var settings = TerrainDataServiceSettings.instance;

        EditorGUILayout.LabelField("TerrainForger Data Services", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Credentials are stored locally in UserSettings for this project. This panel includes open/free data providers and optional commercial imagery providers.",
            MessageType.Info);

        DrawQgisSection(settings);
        DrawOpenTopographySection(settings);
        DrawMapboxSection(settings);
        DrawGoogleMapsSection(settings);

        EditorGUILayout.Space();
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent(showSecrets ? "Hide Secrets" : "Show Secrets", "Toggle whether API keys and tokens are shown in plain text."), GUILayout.Width(120f)))
            {
                showSecrets = !showSecrets;
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(new GUIContent("Save Settings", "Persist the current service credentials and QGIS path into UserSettings for this project."), GUILayout.Width(120f)))
            {
                settings.SaveSettings();
                GUIUtility.ExitGUI();
            }
        }
    }

    private static void DrawOpenTopographySection(TerrainDataServiceSettings settings)
    {
        EditorGUILayout.Space();
        showOpenTopography = EditorGUILayout.Foldout(showOpenTopography, new GUIContent("OpenTopography", "Expand or collapse the OpenTopography credential section."), true);
        if (!showOpenTopography)
        {
            return;
        }

        DrawProviderSummary(TerrainDataProviderIds.OpenTopography);

        EditorGUI.BeginChangeCheck();
        var apiKey = showSecrets
            ? EditorGUILayout.TextField(new GUIContent("API Key", "OpenTopography API key used for DEM downloads."), settings.OpenTopographyApiKey)
            : EditorGUILayout.PasswordField(new GUIContent("API Key", "OpenTopography API key used for DEM downloads."), settings.OpenTopographyApiKey);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(settings, "Edit OpenTopography API Key");
            settings.OpenTopographyApiKey = apiKey;
            EditorUtility.SetDirty(settings);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Paste", "Paste the current clipboard contents into the OpenTopography API key field."), GUILayout.Width(80f)))
            {
                Undo.RecordObject(settings, "Paste OpenTopography API Key");
                settings.OpenTopographyApiKey = EditorGUIUtility.systemCopyBuffer ?? string.Empty;
                EditorUtility.SetDirty(settings);
                GUIUtility.ExitGUI();
            }

            if (GUILayout.Button(new GUIContent("Clear", "Remove the stored OpenTopography API key from this project."), GUILayout.Width(80f)))
            {
                Undo.RecordObject(settings, "Clear OpenTopography API Key");
                settings.OpenTopographyApiKey = string.Empty;
                EditorUtility.SetDirty(settings);
                GUIUtility.ExitGUI();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(new GUIContent("Open Docs", "Open the official OpenTopography page where you can manage your API key."), GUILayout.Width(120f)))
            {
                OpenProviderDocs(TerrainDataProviderIds.OpenTopography);
            }
        }
    }

    private static void DrawQgisSection(TerrainDataServiceSettings settings)
    {
        EditorGUILayout.Space();
        showQgis = EditorGUILayout.Foldout(showQgis, new GUIContent("QGIS", "Expand or collapse the QGIS and GDAL path settings."), true);
        if (!showQgis)
        {
            return;
        }

        EditorGUILayout.HelpBox(
            "Set the root folder of your QGIS installation here. TerrainForger uses this global path to find GDAL tools like gdalinfo and gdalwarp.",
            MessageType.None);

        EditorGUI.BeginChangeCheck();
        var installFolder = EditorGUILayout.TextField(new GUIContent("Install Folder", "Root folder of the QGIS installation that contains GDAL tools such as gdalinfo and gdalwarp."), settings.QgisInstallFolder);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(settings, "Edit QGIS Install Folder");
            settings.QgisInstallFolder = installFolder;
            EditorUtility.SetDirty(settings);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Browse QGIS Folder", "Choose the QGIS installation folder that TerrainForger should use."), GUILayout.Width(140f)))
            {
                var startFolder = string.IsNullOrWhiteSpace(settings.QgisInstallFolder)
                    ? "C:\\Program Files"
                    : settings.QgisInstallFolder;
                var selected = EditorUtility.OpenFolderPanel("Select QGIS Installation Folder", startFolder, string.Empty);
                if (!string.IsNullOrEmpty(selected))
                {
                    Undo.RecordObject(settings, "Browse QGIS Install Folder");
                    settings.QgisInstallFolder = selected;
                    EditorUtility.SetDirty(settings);
                    GUIUtility.ExitGUI();
                }
            }

            if (GUILayout.Button(new GUIContent("Reveal", "Open the currently configured QGIS installation folder in the file explorer."), GUILayout.Width(80f)))
            {
                var folder = settings.QgisInstallFolder;
                if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
                {
                    EditorUtility.RevealInFinder(folder);
                }
            }

            if (GUILayout.Button(new GUIContent("Clear", "Remove the stored QGIS installation path from this project."), GUILayout.Width(80f)))
            {
                Undo.RecordObject(settings, "Clear QGIS Install Folder");
                settings.QgisInstallFolder = string.Empty;
                EditorUtility.SetDirty(settings);
                GUIUtility.ExitGUI();
            }
        }
    }

    private static void DrawMapboxSection(TerrainDataServiceSettings settings)
    {
        EditorGUILayout.Space();
        showMapbox = EditorGUILayout.Foldout(showMapbox, new GUIContent("Mapbox", "Expand or collapse the Mapbox credential section."), true);
        if (!showMapbox)
        {
            return;
        }

        DrawProviderSummary(TerrainDataProviderIds.Mapbox);

        EditorGUI.BeginChangeCheck();
        var accessToken = showSecrets
            ? EditorGUILayout.TextField(new GUIContent("Access Token", "Mapbox access token used for satellite downloads."), settings.MapboxAccessToken)
            : EditorGUILayout.PasswordField(new GUIContent("Access Token", "Mapbox access token used for satellite downloads."), settings.MapboxAccessToken);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(settings, "Edit Mapbox Access Token");
            settings.MapboxAccessToken = accessToken;
            EditorUtility.SetDirty(settings);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Paste", "Paste the current clipboard contents into the Mapbox access token field."), GUILayout.Width(80f)))
            {
                Undo.RecordObject(settings, "Paste Mapbox Access Token");
                settings.MapboxAccessToken = EditorGUIUtility.systemCopyBuffer ?? string.Empty;
                EditorUtility.SetDirty(settings);
                GUIUtility.ExitGUI();
            }

            if (GUILayout.Button(new GUIContent("Clear", "Remove the stored Mapbox access token from this project."), GUILayout.Width(80f)))
            {
                Undo.RecordObject(settings, "Clear Mapbox Access Token");
                settings.MapboxAccessToken = string.Empty;
                EditorUtility.SetDirty(settings);
                GUIUtility.ExitGUI();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(new GUIContent("Open Docs", "Open the official Mapbox access tokens page."), GUILayout.Width(120f)))
            {
                OpenProviderDocs(TerrainDataProviderIds.Mapbox);
            }
        }
    }

    private static void DrawGoogleMapsSection(TerrainDataServiceSettings settings)
    {
        EditorGUILayout.Space();
        showGoogleMaps = EditorGUILayout.Foldout(showGoogleMaps, new GUIContent("Google Maps Platform", "Expand or collapse the Google Maps Platform credential section."), true);
        if (!showGoogleMaps)
        {
            return;
        }

        DrawProviderSummary(TerrainDataProviderIds.GoogleMapsPlatform);

        EditorGUI.BeginChangeCheck();
        var apiKey = showSecrets
            ? EditorGUILayout.TextField(new GUIContent("API Key", "Google Maps Platform API key used for satellite tile downloads."), settings.GoogleMapsApiKey)
            : EditorGUILayout.PasswordField(new GUIContent("API Key", "Google Maps Platform API key used for satellite tile downloads."), settings.GoogleMapsApiKey);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(settings, "Edit Google Maps API Key");
            settings.GoogleMapsApiKey = apiKey;
            EditorUtility.SetDirty(settings);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(new GUIContent("Paste", "Paste the current clipboard contents into the Google Maps API key field."), GUILayout.Width(80f)))
            {
                Undo.RecordObject(settings, "Paste Google Maps API Key");
                settings.GoogleMapsApiKey = EditorGUIUtility.systemCopyBuffer ?? string.Empty;
                EditorUtility.SetDirty(settings);
                GUIUtility.ExitGUI();
            }

            if (GUILayout.Button(new GUIContent("Clear", "Remove the stored Google Maps API key from this project."), GUILayout.Width(80f)))
            {
                Undo.RecordObject(settings, "Clear Google Maps API Key");
                settings.GoogleMapsApiKey = string.Empty;
                EditorUtility.SetDirty(settings);
                GUIUtility.ExitGUI();
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button(new GUIContent("Open Docs", "Open the official Google Cloud credentials page for API keys."), GUILayout.Width(120f)))
            {
                OpenProviderDocs(TerrainDataProviderIds.GoogleMapsPlatform);
            }
        }
    }

    private static void DrawProviderSummary(string providerId)
    {
        var provider = GetProvider(providerId);
        if (provider == null)
        {
            return;
        }

        EditorGUILayout.HelpBox(
            $"Access: {provider.accessModel}\nSupports imagery: {YesNo(provider.supportsImagery)}\nSupports elevation: {YesNo(provider.supportsElevation)}\n{provider.notes}",
            MessageType.None);
    }

    private static void OpenProviderDocs(string providerId)
    {
        var provider = GetProvider(providerId);
        if (provider == null || string.IsNullOrWhiteSpace(provider.docsUrl))
        {
            return;
        }

        Application.OpenURL(provider.docsUrl);
    }

    private static TerrainBuiltInProviderInfo GetProvider(string providerId)
    {
        var providers = TerrainDataServiceSettings.GetBuiltInProviders();
        for (var i = 0; i < providers.Count; i++)
        {
            if (providers[i].providerId == providerId)
            {
                return providers[i];
            }
        }

        return null;
    }

    private static string YesNo(bool value)
    {
        return value ? "Yes" : "No";
    }
}
