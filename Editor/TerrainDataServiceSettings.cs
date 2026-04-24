using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[FilePath("UserSettings/TerrainDataServiceSettings.asset", FilePathAttribute.Location.ProjectFolder)]
public class TerrainDataServiceSettings : ScriptableSingleton<TerrainDataServiceSettings>
{
    [SerializeField]
    private string openTopographyApiKey = string.Empty;

    [SerializeField]
    private string mapboxAccessToken = string.Empty;

    [SerializeField]
    private string googleMapsApiKey = string.Empty;

    [SerializeField]
    private string qgisInstallFolder = string.Empty;

    public string OpenTopographyApiKey
    {
        get => openTopographyApiKey;
        set => openTopographyApiKey = value ?? string.Empty;
    }

    public string MapboxAccessToken
    {
        get => mapboxAccessToken;
        set => mapboxAccessToken = value ?? string.Empty;
    }

    public string GoogleMapsApiKey
    {
        get => googleMapsApiKey;
        set => googleMapsApiKey = value ?? string.Empty;
    }

    public string QgisInstallFolder
    {
        get => qgisInstallFolder;
        set => qgisInstallFolder = value ?? string.Empty;
    }

    public string GetApiKey(string providerId)
    {
        if (string.Equals(providerId, TerrainDataProviderIds.OpenTopography, StringComparison.OrdinalIgnoreCase))
        {
            return OpenTopographyApiKey;
        }

        if (string.Equals(providerId, TerrainDataProviderIds.Mapbox, StringComparison.OrdinalIgnoreCase))
        {
            return MapboxAccessToken;
        }

        if (string.Equals(providerId, TerrainDataProviderIds.GoogleMapsPlatform, StringComparison.OrdinalIgnoreCase))
        {
            return GoogleMapsApiKey;
        }

        return string.Empty;
    }

    public string GetValue(string providerId, string key)
    {
        if (string.Equals(providerId, TerrainDataProviderIds.OpenTopography, StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(key, "apiKey", StringComparison.OrdinalIgnoreCase) ? OpenTopographyApiKey : string.Empty;
        }

        if (string.Equals(providerId, TerrainDataProviderIds.Mapbox, StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(key, "accessToken", StringComparison.OrdinalIgnoreCase))
            {
                return MapboxAccessToken;
            }
        }

        if (string.Equals(providerId, TerrainDataProviderIds.GoogleMapsPlatform, StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(key, "apiKey", StringComparison.OrdinalIgnoreCase))
            {
                return GoogleMapsApiKey;
            }
        }

        return string.Empty;
    }

    public void SaveSettings()
    {
        Save(true);
    }

    public static IReadOnlyList<TerrainBuiltInProviderInfo> GetBuiltInProviders()
    {
        return new[]
        {
            new TerrainBuiltInProviderInfo
            {
                providerId = TerrainDataProviderIds.OpenTopography,
                displayName = "OpenTopography",
                docsUrl = "https://portal.opentopography.org/myopentopo",
                accessModel = "Free API key",
                supportsElevation = true,
                supportsImagery = false,
                notes = "Global DEM and elevation APIs. Free API key with daily limits."
            },
            new TerrainBuiltInProviderInfo
            {
                providerId = TerrainDataProviderIds.Mapbox,
                displayName = "Mapbox",
                docsUrl = "https://console.mapbox.com/account/access-tokens/",
                accessModel = "Commercial access token",
                supportsElevation = false,
                supportsImagery = true,
                notes = "Satellite and map APIs use a Mapbox access token. Check Mapbox terms before bulk download or offline bake workflows."
            },
            new TerrainBuiltInProviderInfo
            {
                providerId = TerrainDataProviderIds.GoogleMapsPlatform,
                displayName = "Google Maps Platform",
                docsUrl = "https://console.cloud.google.com/apis/credentials",
                accessModel = "API key with billing-enabled project",
                supportsElevation = false,
                supportsImagery = true,
                notes = "Satellite tiles use a Google Maps API key and session token flow. Check Google Maps Platform terms before storage or offline processing."
            }
        };
    }
}

public static class TerrainDataProviderIds
{
    public const string OpenTopography = "opentopography";
    public const string Mapbox = "mapbox";
    public const string GoogleMapsPlatform = "google-maps-platform";
}

[Serializable]
public class TerrainBuiltInProviderInfo
{
    public string providerId = string.Empty;
    public string displayName = string.Empty;
    public string docsUrl = string.Empty;
    public string accessModel = string.Empty;
    public bool supportsElevation;
    public bool supportsImagery;
    public string notes = string.Empty;
    public bool requiresConfiguration;

    public bool IsConfigured(TerrainDataServiceSettings settings)
    {
        switch (providerId)
        {
            case TerrainDataProviderIds.OpenTopography:
                return !string.IsNullOrWhiteSpace(settings.OpenTopographyApiKey);
            case TerrainDataProviderIds.Mapbox:
                return !string.IsNullOrWhiteSpace(settings.MapboxAccessToken);
            case TerrainDataProviderIds.GoogleMapsPlatform:
                return !string.IsNullOrWhiteSpace(settings.GoogleMapsApiKey);
            default:
                return false;
        }
    }
}
