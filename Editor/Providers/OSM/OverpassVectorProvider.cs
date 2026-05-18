using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;

public sealed class OverpassVectorProvider : IVectorDataProvider
{
    private const int NetworkTimeoutMilliseconds = 10 * 60 * 1000;

    public string DisplayName
    {
        get { return "OpenStreetMap / Overpass"; }
    }

    public VectorDataset Import(TerrainForgerVectorImportSettings settings, TerrainForgerVectorImportContext context)
    {
        if (!settings.importBuildings && !settings.importRoads)
        {
            throw new InvalidOperationException("Enable at least one vector layer before importing from OpenStreetMap.");
        }

        var endpoint = string.IsNullOrWhiteSpace(settings.overpassEndpoint)
            ? "https://overpass-api.de/api/interpreter"
            : settings.overpassEndpoint.Trim();
        var query = OsmQueryBuilder.BuildUrbanQuery(context.Bounds, settings.importBuildings, settings.importRoads);
        context.AddLog("Requesting OpenStreetMap data from Overpass.");
        var json = DownloadOverpassJson(endpoint, query);
        context.AddLog("OpenStreetMap data downloaded.");

        var dataset = OsmTagMapper.ToVectorDataset(json, context.Bounds, settings.importBuildings, settings.importRoads);
        context.AddLog("OpenStreetMap data normalized.");
        return dataset;
    }

    private static string DownloadOverpassJson(string endpoint, string query)
    {
        using (var client = new TimeoutWebClient(NetworkTimeoutMilliseconds))
        {
            client.Encoding = Encoding.UTF8;
            client.Headers.Add(HttpRequestHeader.UserAgent, "TerrainForger/1.0");
            client.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
            return client.UploadString(endpoint, "POST", "data=" + Uri.EscapeDataString(query));
        }
    }

    private sealed class TimeoutWebClient : WebClient
    {
        private readonly int timeoutMilliseconds;

        public TimeoutWebClient(int timeoutMilliseconds)
        {
            this.timeoutMilliseconds = timeoutMilliseconds;
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            var request = base.GetWebRequest(address);
            if (request != null)
            {
                request.Timeout = timeoutMilliseconds;
                var httpRequest = request as HttpWebRequest;
                if (httpRequest != null)
                {
                    httpRequest.ReadWriteTimeout = timeoutMilliseconds;
                }
            }

            return request;
        }
    }
}

public static class OsmQueryBuilder
{
    public static string BuildUrbanQuery(GeoBounds bounds, bool includeBuildings, bool includeRoads)
    {
        if (!bounds.IsValid)
        {
            throw new InvalidOperationException("OpenStreetMap bounds are invalid.");
        }

        var builder = new StringBuilder();
        builder.AppendLine("[out:json][timeout:180];");
        builder.AppendLine("(");

        var bbox = string.Join(",", new[]
        {
            TerrainForgerVectorExternalTools.FormatDouble(bounds.south),
            TerrainForgerVectorExternalTools.FormatDouble(bounds.west),
            TerrainForgerVectorExternalTools.FormatDouble(bounds.north),
            TerrainForgerVectorExternalTools.FormatDouble(bounds.east)
        });

        if (includeBuildings)
        {
            builder.AppendLine($"  way[\"building\"]({bbox});");
            builder.AppendLine($"  relation[\"building\"]({bbox});");
        }

        if (includeRoads)
        {
            builder.AppendLine($"  way[\"highway\"]({bbox});");
            builder.AppendLine($"  relation[\"highway\"]({bbox});");
        }

        builder.AppendLine(");");
        builder.AppendLine("out body geom;");
        return builder.ToString();
    }
}

public static class OsmTagMapper
{
    public static VectorDataset ToVectorDataset(string overpassJson, GeoBounds bounds, bool includeBuildings, bool includeRoads)
    {
        var root = TerrainForgerSimpleJson.ParseObject(overpassJson);
        object elementsValue;
        if (!root.TryGetValue("elements", out elementsValue))
        {
            throw new InvalidOperationException("Overpass response does not contain an elements array.");
        }

        var elements = elementsValue as List<object>;
        if (elements == null)
        {
            throw new InvalidOperationException("Overpass elements value is not an array.");
        }

        var dataset = new VectorDataset
        {
            Bounds = bounds,
            SourceName = "OpenStreetMap / Overpass"
        };
        var buildingLayer = new VectorLayer { Name = "osm_buildings", Kind = VectorLayerKind.Building };
        var roadLayer = new VectorLayer { Name = "osm_roads", Kind = VectorLayerKind.Road };

        for (var i = 0; i < elements.Count; i++)
        {
            var element = elements[i] as Dictionary<string, object>;
            if (element == null)
            {
                continue;
            }

            var tags = ReadTags(element);
            var kind = ResolveKind(tags);
            if (kind == VectorLayerKind.Building && !includeBuildings)
            {
                continue;
            }

            if (kind == VectorLayerKind.Road && !includeRoads)
            {
                continue;
            }

            var feature = CreateFeature(element, tags, kind);
            if (feature == null || feature.Geometry == null || feature.Geometry.Type == VectorGeometryType.Unknown)
            {
                continue;
            }

            if (feature.Kind == VectorLayerKind.Building)
            {
                buildingLayer.Features.Add(feature);
            }
            else if (feature.Kind == VectorLayerKind.Road)
            {
                roadLayer.Features.Add(feature);
            }
        }

        if (buildingLayer.Features.Count > 0)
        {
            dataset.Layers.Add(buildingLayer);
        }

        if (roadLayer.Features.Count > 0)
        {
            dataset.Layers.Add(roadLayer);
        }

        return dataset;
    }

    private static Dictionary<string, object> ReadTags(Dictionary<string, object> element)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        object tagsValue;
        if (!element.TryGetValue("tags", out tagsValue))
        {
            return result;
        }

        var tags = tagsValue as Dictionary<string, object>;
        if (tags == null)
        {
            return result;
        }

        foreach (var pair in tags)
        {
            result[pair.Key] = pair.Value;
        }

        return result;
    }

    private static VectorLayerKind ResolveKind(Dictionary<string, object> tags)
    {
        if (tags.ContainsKey("building"))
        {
            return VectorLayerKind.Building;
        }

        if (tags.ContainsKey("highway"))
        {
            return VectorLayerKind.Road;
        }

        return VectorLayerKind.Unknown;
    }

    private static VectorFeature CreateFeature(Dictionary<string, object> element, Dictionary<string, object> tags, VectorLayerKind kind)
    {
        if (kind == VectorLayerKind.Unknown)
        {
            return null;
        }

        var geometry = kind == VectorLayerKind.Building
            ? CreateBuildingGeometry(element)
            : CreateRoadGeometry(element);

        if (geometry == null || geometry.Type == VectorGeometryType.Unknown)
        {
            return null;
        }

        var sourceId = ResolveElementType(element) + "/" + ResolveElementId(element);
        var properties = new Dictionary<string, object>(tags, StringComparer.OrdinalIgnoreCase)
        {
            ["osm_type"] = ResolveElementType(element),
            ["osm_id"] = ResolveElementId(element)
        };

        return new VectorFeature
        {
            SourceId = sourceId,
            Kind = kind,
            Geometry = geometry,
            Properties = properties
        };
    }

    private static VectorGeometry CreateBuildingGeometry(Dictionary<string, object> element)
    {
        var type = ResolveElementType(element);
        if (string.Equals(type, "way", StringComparison.OrdinalIgnoreCase))
        {
            var ring = ReadGeometryCoordinates(element);
            CloseRing(ring);
            if (ring.Count < 4)
            {
                return null;
            }

            return new VectorGeometry
            {
                Type = VectorGeometryType.Polygon,
                PolygonRings = new List<List<GeoCoordinate>> { ring }
            };
        }

        if (string.Equals(type, "relation", StringComparison.OrdinalIgnoreCase))
        {
            var polygons = ReadRelationPolygons(element);
            if (polygons.Count == 0)
            {
                return null;
            }

            return new VectorGeometry
            {
                Type = VectorGeometryType.MultiPolygon,
                MultiPolygonRings = polygons
            };
        }

        return null;
    }

    private static VectorGeometry CreateRoadGeometry(Dictionary<string, object> element)
    {
        var type = ResolveElementType(element);
        if (string.Equals(type, "way", StringComparison.OrdinalIgnoreCase))
        {
            var line = ReadGeometryCoordinates(element);
            if (line.Count < 2)
            {
                return null;
            }

            return new VectorGeometry
            {
                Type = VectorGeometryType.LineString,
                LineStrings = new List<List<GeoCoordinate>> { line }
            };
        }

        if (string.Equals(type, "relation", StringComparison.OrdinalIgnoreCase))
        {
            var lines = ReadRelationLines(element);
            if (lines.Count == 0)
            {
                return null;
            }

            return new VectorGeometry
            {
                Type = VectorGeometryType.MultiLineString,
                LineStrings = lines
            };
        }

        return null;
    }

    private static List<List<List<GeoCoordinate>>> ReadRelationPolygons(Dictionary<string, object> element)
    {
        var result = new List<List<List<GeoCoordinate>>>();
        object membersValue;
        if (!element.TryGetValue("members", out membersValue))
        {
            return result;
        }

        var members = membersValue as List<object>;
        if (members == null)
        {
            return result;
        }

        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i] as Dictionary<string, object>;
            if (member == null)
            {
                continue;
            }

            var role = TerrainForgerSimpleJson.GetString(member, "role");
            if (!string.Equals(role, "outer", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(role))
            {
                continue;
            }

            var ring = ReadGeometryCoordinates(member);
            CloseRing(ring);
            if (ring.Count >= 4)
            {
                result.Add(new List<List<GeoCoordinate>> { ring });
            }
        }

        return result;
    }

    private static List<List<GeoCoordinate>> ReadRelationLines(Dictionary<string, object> element)
    {
        var result = new List<List<GeoCoordinate>>();
        object membersValue;
        if (!element.TryGetValue("members", out membersValue))
        {
            return result;
        }

        var members = membersValue as List<object>;
        if (members == null)
        {
            return result;
        }

        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i] as Dictionary<string, object>;
            if (member == null)
            {
                continue;
            }

            var line = ReadGeometryCoordinates(member);
            if (line.Count >= 2)
            {
                result.Add(line);
            }
        }

        return result;
    }

    private static List<GeoCoordinate> ReadGeometryCoordinates(Dictionary<string, object> element)
    {
        var result = new List<GeoCoordinate>();
        object geometryValue;
        if (!element.TryGetValue("geometry", out geometryValue))
        {
            return result;
        }

        var geometry = geometryValue as List<object>;
        if (geometry == null)
        {
            return result;
        }

        for (var i = 0; i < geometry.Count; i++)
        {
            var point = geometry[i] as Dictionary<string, object>;
            if (point == null)
            {
                continue;
            }

            object latValue;
            object lonValue;
            if (!point.TryGetValue("lat", out latValue) || !point.TryGetValue("lon", out lonValue))
            {
                continue;
            }

            var lat = Convert.ToDouble(latValue, CultureInfo.InvariantCulture);
            var lon = Convert.ToDouble(lonValue, CultureInfo.InvariantCulture);
            result.Add(new GeoCoordinate(lat, lon));
        }

        return result;
    }

    private static string ResolveElementType(Dictionary<string, object> element)
    {
        return TerrainForgerSimpleJson.GetString(element, "type");
    }

    private static string ResolveElementId(Dictionary<string, object> element)
    {
        object idValue;
        if (!element.TryGetValue("id", out idValue) || idValue == null)
        {
            return "unknown";
        }

        return Convert.ToString(idValue, CultureInfo.InvariantCulture);
    }

    private static void CloseRing(List<GeoCoordinate> ring)
    {
        if (ring == null || ring.Count == 0)
        {
            return;
        }

        var first = ring[0];
        var last = ring[ring.Count - 1];
        if (Math.Abs(first.latitude - last.latitude) < 0.000000001d &&
            Math.Abs(first.longitude - last.longitude) < 0.000000001d)
        {
            return;
        }

        ring.Add(first);
    }
}
