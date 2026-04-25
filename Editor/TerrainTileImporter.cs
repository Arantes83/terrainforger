using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class TerrainTileImporter
{
    private const string SupportedResolutionList = "33, 65, 129, 257, 513, 1025, 2049, 4097";

    public readonly struct TerrainTileImportLayoutInfo
    {
        public readonly bool isValid;
        public readonly string message;
        public readonly int rows;
        public readonly int cols;
        public readonly int heightmapResolution;
        public readonly int tileCount;
        public readonly bool usesMetadata;

        public TerrainTileImportLayoutInfo(
            bool isValid,
            string message,
            int rows,
            int cols,
            int heightmapResolution,
            int tileCount,
            bool usesMetadata)
        {
            this.isValid = isValid;
            this.message = message;
            this.rows = rows;
            this.cols = cols;
            this.heightmapResolution = heightmapResolution;
            this.tileCount = tileCount;
            this.usesMetadata = usesMetadata;
        }
    }

    public static void Import(TerrainTileImportConfig config)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        ApplyTerrainForgerRawConventions(config);
        var inputFolder = ResolveFolderPath(config.inputFolder);
        var satelliteFolder = ResolveFolderPath(config.satelliteOutputFolder);
        var tileSourceSet = BuildTileSourceSet(inputFolder);
        ValidateConfig(config, tileSourceSet);

        config.rows = tileSourceSet.Rows;
        config.cols = tileSourceSet.Cols;
        config.heightmapResolution = tileSourceSet.Resolution;

        var terrainGrid = new Terrain[tileSourceSet.Rows, tileSourceSet.Cols];
        var root = PrepareRoot(config);
        var importedBounds = ImportWorldBounds.CreateEmpty();

        try
        {
            EnsureAssetFolder(config.outputFolder);

            for (var i = 0; i < tileSourceSet.Entries.Count; i++)
            {
                var entry = tileSourceSet.Entries[i];
                var progress = i / (float)Math.Max(1, tileSourceSet.Entries.Count);
                EditorUtility.DisplayProgressBar(
                    "Importing terrain tiles",
                    $"Importing tile {TerrainTileNaming.GetTileLabel(entry.row, entry.col)}",
                    progress);

                CreateTerrainTile(config, entry, satelliteFolder, root.transform, terrainGrid, ref importedBounds);
            }

            ConnectNeighbors(terrainGrid);
            CreateWaterPlaneIfRequested(config, root.transform, importedBounds);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeGameObject = root;
            Debug.Log($"[Terrain Tile Importer] Imported {tileSourceSet.Entries.Count} terrain tiles into '{config.rootObjectName}'.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    public static TerrainTileImportLayoutInfo InspectInputFolder(string inputFolder)
    {
        try
        {
            var sourceSet = BuildTileSourceSet(ResolveFolderPath(inputFolder));
            return new TerrainTileImportLayoutInfo(
                true,
                sourceSet.UsesMetadata ? "Grid inferred from RAW files and tile metadata." : "Grid inferred from RAW file names.",
                sourceSet.Rows,
                sourceSet.Cols,
                sourceSet.Resolution,
                sourceSet.Entries.Count,
                sourceSet.UsesMetadata);
        }
        catch (Exception ex)
        {
            return new TerrainTileImportLayoutInfo(false, ex.Message, 0, 0, 0, 0, false);
        }
    }

    private static void CreateTerrainTile(
        TerrainTileImportConfig config,
        TileSourceEntry entry,
        string satelliteFolder,
        Transform parent,
        Terrain[,] terrainGrid,
        ref ImportWorldBounds importedBounds)
    {
        var sourceRow = entry.row;
        var sourceCol = entry.col;
        var tileLabel = TerrainTileNaming.GetTileLabel(sourceRow, sourceCol);
        var rawPath = entry.rawPath;
        var elevationMetadata = entry.metadata;
        var tileMinElevation = elevationMetadata?.minElevation ?? config.minElevation;
        var tileMaxElevation = elevationMetadata?.maxElevation ?? config.maxElevation;
        var tileSizeX = elevationMetadata?.tileSizeX ?? config.tileSizeX;
        var tileSizeZ = elevationMetadata?.tileSizeZ ?? config.tileSizeZ;
        var tileHeightRange = Mathf.Max(0.01f, tileMaxElevation - tileMinElevation);
        var heights = Raw16HeightmapReader.Read(
            rawPath,
            entry.resolution,
            config.inputIsLittleEndian,
            config.flipHorizontally,
            config.flipVertically);

        var terrainData = new TerrainData
        {
            heightmapResolution = entry.resolution,
            size = new Vector3(
                tileSizeX,
                tileHeightRange,
                tileSizeZ)
        };

        terrainData.SetHeights(0, 0, heights);

        var assetPath = $"{config.outputFolder}/Terrain_{tileLabel}.asset";
        if (AssetDatabase.LoadAssetAtPath<TerrainData>(assetPath) != null)
        {
            AssetDatabase.DeleteAsset(assetPath);
        }

        AssetDatabase.CreateAsset(terrainData, assetPath);

        ApplySatelliteTextureIfAvailable(config, entry, satelliteFolder, terrainData, tileLabel, tileSizeX, tileSizeZ);

        var go = Terrain.CreateTerrainGameObject(terrainData);
        go.name = $"Terrain_{tileLabel}";
        go.transform.SetParent(parent, false);
        var terrainPosition = BuildTerrainPosition(config, sourceRow, sourceCol, tileMinElevation, elevationMetadata);
        go.transform.position = terrainPosition;
        importedBounds.Encapsulate(terrainPosition.x, terrainPosition.z, tileSizeX, tileSizeZ);

        var terrain = go.GetComponent<Terrain>();
        terrain.allowAutoConnect = config.allowAutoConnect;
        terrain.groupingID = config.groupingId;
        terrain.drawInstanced = config.drawInstanced;
        terrain.heightmapPixelError = config.heightmapPixelError;
        terrain.basemapDistance = config.basemapDistance;

        var worldRow = config.rowsStartAtNorth ? (config.rows - 1 - sourceRow) : sourceRow;
        var worldCol = config.colsStartAtWest ? sourceCol : (config.cols - 1 - sourceCol);
        terrainGrid[worldRow, worldCol] = terrain;
    }

    private static void CreateWaterPlaneIfRequested(
        TerrainTileImportConfig config,
        Transform parent,
        ImportWorldBounds importedBounds)
    {
        if (!config.createWaterPlane || !importedBounds.isInitialized)
        {
            return;
        }

        var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = "WaterPlane";
        plane.transform.SetParent(parent, false);

        var centerX = (importedBounds.minX + importedBounds.maxX) * 0.5f;
        var centerZ = (importedBounds.minZ + importedBounds.maxZ) * 0.5f;
        var width = Mathf.Max(0.01f, importedBounds.maxX - importedBounds.minX);
        var depth = Mathf.Max(0.01f, importedBounds.maxZ - importedBounds.minZ);

        plane.transform.position = new Vector3(
            centerX,
            config.waterPlaneElevation,
            centerZ);
        plane.transform.localScale = new Vector3(width / 10f, 1f, depth / 10f);

        if (config.waterMaterial != null)
        {
            var renderer = plane.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = config.waterMaterial;
            }
        }
    }

    private static void ApplySatelliteTextureIfAvailable(
        TerrainTileImportConfig config,
        TileSourceEntry entry,
        string satelliteFolder,
        TerrainData terrainData,
        string tileLabel,
        float tileSizeX,
        float tileSizeZ)
    {
        if (string.IsNullOrWhiteSpace(satelliteFolder) || !Directory.Exists(satelliteFolder))
        {
            return;
        }

        var satellitePath = Path.Combine(
            satelliteFolder,
            $"{Path.GetFileNameWithoutExtension(Path.GetFileName(entry.rawPath))}.png");
        if (!File.Exists(satellitePath))
        {
            return;
        }

        var satelliteAssetPath = ToAssetRelativePath(satellitePath);
        if (string.IsNullOrWhiteSpace(satelliteAssetPath))
        {
            return;
        }

        ConfigurePngImporterForExactResolution(satelliteAssetPath);
        AssetDatabase.ImportAsset(satelliteAssetPath, ImportAssetOptions.ForceUpdate);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(satelliteAssetPath);
        if (texture == null)
        {
            return;
        }

        var terrainLayerAssetPath = $"{config.outputFolder}/TerrainLayer_{tileLabel}.terrainlayer";
        var existingLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(terrainLayerAssetPath);
        if (existingLayer != null)
        {
            AssetDatabase.DeleteAsset(terrainLayerAssetPath);
        }

        var terrainLayer = new TerrainLayer
        {
            diffuseTexture = texture,
            tileSize = new Vector2(tileSizeX, tileSizeZ),
            tileOffset = Vector2.zero
        };
        AssetDatabase.CreateAsset(terrainLayer, terrainLayerAssetPath);

        terrainData.terrainLayers = new[] { terrainLayer };
        terrainData.alphamapResolution = Mathf.Clamp(entry.resolution, 16, 4096);
        var alphamaps = new float[terrainData.alphamapResolution, terrainData.alphamapResolution, 1];
        for (var y = 0; y < terrainData.alphamapResolution; y++)
        {
            for (var x = 0; x < terrainData.alphamapResolution; x++)
            {
                alphamaps[y, x, 0] = 1f;
            }
        }

        terrainData.SetAlphamaps(0, 0, alphamaps);
    }

    private static Vector3 BuildTerrainPosition(
        TerrainTileImportConfig config,
        int sourceRow,
        int sourceCol,
        float tileMinElevation,
        TerrainTileElevationMetadata elevationMetadata)
    {
        if (elevationMetadata != null)
        {
            return new Vector3(
                config.terrainOrigin.x + elevationMetadata.positionOffsetX,
                config.terrainOrigin.y + tileMinElevation,
                config.terrainOrigin.z + elevationMetadata.positionOffsetZ);
        }

        var worldRow = config.rowsStartAtNorth ? (config.rows - 1 - sourceRow) : sourceRow;
        var worldCol = config.colsStartAtWest ? sourceCol : (config.cols - 1 - sourceCol);

        return new Vector3(
            config.terrainOrigin.x + (worldCol * config.tileSizeX),
            config.terrainOrigin.y + tileMinElevation,
            config.terrainOrigin.z + (worldRow * config.tileSizeZ));
    }

    private static void ConnectNeighbors(Terrain[,] terrainGrid)
    {
        var rows = terrainGrid.GetLength(0);
        var cols = terrainGrid.GetLength(1);

        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < cols; col++)
            {
                var terrain = terrainGrid[row, col];
                if (terrain == null)
                {
                    continue;
                }

                var left = col > 0 ? terrainGrid[row, col - 1] : null;
                var right = col < cols - 1 ? terrainGrid[row, col + 1] : null;
                var top = row < rows - 1 ? terrainGrid[row + 1, col] : null;
                var bottom = row > 0 ? terrainGrid[row - 1, col] : null;

                terrain.SetNeighbors(left, top, right, bottom);
            }
        }
    }

    private static GameObject PrepareRoot(TerrainTileImportConfig config)
    {
        var existing = GameObject.Find(config.rootObjectName);
        if (existing != null)
        {
            UnityEngine.Object.DestroyImmediate(existing);
        }

        var root = new GameObject(config.rootObjectName);
        root.transform.position = config.terrainOrigin;
        return root;
    }

    private static void ValidateConfig(TerrainTileImportConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.inputFolder))
        {
            throw new InvalidOperationException("Input folder is required.");
        }
    }

    private static void ValidateConfig(TerrainTileImportConfig config, TileSourceSet tileSourceSet)
    {
        ValidateConfig(config);

        if (tileSourceSet.Rows <= 0 || tileSourceSet.Cols <= 0)
        {
            throw new InvalidOperationException("Could not infer a valid tile grid from the RAW folder.");
        }

        if (!tileSourceSet.UsesMetadata && (config.tileSizeX <= 0f || config.tileSizeZ <= 0f))
        {
            throw new InvalidOperationException("Tile sizes must be greater than zero.");
        }

        if (!tileSourceSet.UsesMetadata && config.maxElevation <= config.minElevation)
        {
            throw new InvalidOperationException("Max elevation must be greater than min elevation.");
        }

        if (!IsSupportedHeightmapResolution(tileSourceSet.Resolution))
        {
            throw new InvalidOperationException(
                $"Unsupported heightmap resolution {tileSourceSet.Resolution}. Use one of: {SupportedResolutionList}.");
        }

        if (string.IsNullOrWhiteSpace(config.outputFolder) || !config.outputFolder.Replace('\\', '/').StartsWith("Assets/"))
        {
            throw new InvalidOperationException("Output folder must be inside the Unity project, for example Assets/Generated/TerrainTiles.");
        }
    }

    private static void ApplyTerrainForgerRawConventions(TerrainTileImportConfig config)
    {
        config.inputIsLittleEndian = true;
        config.flipHorizontally = false;
        config.flipVertically = true;
        config.rowsStartAtNorth = true;
        config.colsStartAtWest = true;
    }

    private static TileSourceSet BuildTileSourceSet(string inputFolder)
    {
        if (!Directory.Exists(inputFolder))
        {
            throw new DirectoryNotFoundException($"RAW input folder not found: {inputFolder}");
        }

        var rawPaths = Directory.GetFiles(inputFolder, "*.raw", SearchOption.TopDirectoryOnly);
        if (rawPaths.Length == 0)
        {
            throw new InvalidOperationException("No RAW files were found in the selected input folder.");
        }

        var entries = new List<TileSourceEntry>(rawPaths.Length);
        var detectedResolution = 0;
        var usesMetadata = false;
        var maxRow = -1;
        var maxCol = -1;
        var occupied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawPath in rawPaths)
        {
            var metadata = TerrainTileElevationMetadataUtility.TryRead(rawPath);
            int row;
            int col;

            if (metadata != null)
            {
                row = metadata.row;
                col = metadata.col;
                usesMetadata = true;
            }
            else if (!TerrainTileNaming.TryParseTileLabel(Path.GetFileNameWithoutExtension(rawPath), out row, out col))
            {
                throw new InvalidOperationException(
                    $"Could not infer tile coordinates from '{Path.GetFileName(rawPath)}'. Re-export the RAW tiles or use standard names like A1.raw.");
            }

            var resolution = InferResolution(rawPath);
            if (detectedResolution == 0)
            {
                detectedResolution = resolution;
            }
            else if (detectedResolution != resolution)
            {
                throw new InvalidOperationException("All RAW tiles in the folder must have the same heightmap resolution.");
            }

            var key = $"{row}:{col}";
            if (!occupied.Add(key))
            {
                throw new InvalidOperationException($"Duplicate tile coordinates detected for {TerrainTileNaming.GetTileLabel(row, col)}.");
            }

            maxRow = Mathf.Max(maxRow, row);
            maxCol = Mathf.Max(maxCol, col);
            entries.Add(new TileSourceEntry(rawPath, row, col, resolution, metadata));
        }

        var rows = maxRow + 1;
        var cols = maxCol + 1;
        var expectedCount = rows * cols;
        if (entries.Count != expectedCount)
        {
            throw new InvalidOperationException(
                $"The RAW folder does not form a complete rectangular grid. Detected {entries.Count} tile(s) for a {cols}x{rows} layout.");
        }

        entries.Sort((a, b) =>
        {
            var rowCompare = a.row.CompareTo(b.row);
            return rowCompare != 0 ? rowCompare : a.col.CompareTo(b.col);
        });

        return new TileSourceSet(entries, rows, cols, detectedResolution, usesMetadata);
    }

    private static int InferResolution(string rawPath)
    {
        var fileInfo = new FileInfo(rawPath);
        var sampleCount = fileInfo.Length / sizeof(ushort);
        var resolution = Mathf.RoundToInt(Mathf.Sqrt(sampleCount));
        if ((long)resolution * resolution * sizeof(ushort) != fileInfo.Length)
        {
            throw new InvalidDataException(
                $"Unexpected RAW size for '{rawPath}'. Could not infer a square 16-bit heightmap resolution.");
        }

        return resolution;
    }

    private static bool IsSupportedHeightmapResolution(int resolution)
    {
        switch (resolution)
        {
            case 33:
            case 65:
            case 129:
            case 257:
            case 513:
            case 1025:
            case 2049:
            case 4097:
                return true;
            default:
                return false;
        }
    }

    private static string ResolveFolderPath(string folderPath)
    {
        var normalized = folderPath.Replace('\\', '/');
        if (Path.IsPathRooted(normalized))
        {
            return normalized;
        }

        var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        return Path.GetFullPath(Path.Combine(projectRoot, normalized));
    }

    private static void EnsureAssetFolder(string assetFolder)
    {
        var normalized = assetFolder.Replace('\\', '/').TrimEnd('/');
        if (AssetDatabase.IsValidFolder(normalized))
        {
            return;
        }

        var parts = normalized.Split('/');
        var current = parts[0];

        for (var i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private static string ToAssetRelativePath(string absolutePath)
    {
        var normalized = absolutePath.Replace('\\', '/');
        var assetsRoot = Application.dataPath.Replace('\\', '/');
        if (!normalized.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return "Assets" + normalized.Substring(assetsRoot.Length);
    }

    private readonly struct TileSourceEntry
    {
        public readonly string rawPath;
        public readonly int row;
        public readonly int col;
        public readonly int resolution;
        public readonly TerrainTileElevationMetadata metadata;

        public TileSourceEntry(string rawPath, int row, int col, int resolution, TerrainTileElevationMetadata metadata)
        {
            this.rawPath = rawPath;
            this.row = row;
            this.col = col;
            this.resolution = resolution;
            this.metadata = metadata;
        }
    }

    private readonly struct TileSourceSet
    {
        public readonly List<TileSourceEntry> Entries;
        public readonly int Rows;
        public readonly int Cols;
        public readonly int Resolution;
        public readonly bool UsesMetadata;

        public TileSourceSet(List<TileSourceEntry> entries, int rows, int cols, int resolution, bool usesMetadata)
        {
            Entries = entries;
            Rows = rows;
            Cols = cols;
            Resolution = resolution;
            UsesMetadata = usesMetadata;
        }
    }

    private struct ImportWorldBounds
    {
        public bool isInitialized;
        public float minX;
        public float maxX;
        public float minZ;
        public float maxZ;

        public static ImportWorldBounds CreateEmpty()
        {
            return new ImportWorldBounds
            {
                isInitialized = false,
                minX = 0f,
                maxX = 0f,
                minZ = 0f,
                maxZ = 0f
            };
        }

        public void Encapsulate(float x, float z, float width, float depth)
        {
            var nextMinX = x;
            var nextMaxX = x + width;
            var nextMinZ = z;
            var nextMaxZ = z + depth;

            if (!isInitialized)
            {
                isInitialized = true;
                minX = nextMinX;
                maxX = nextMaxX;
                minZ = nextMinZ;
                maxZ = nextMaxZ;
                return;
            }

            minX = Mathf.Min(minX, nextMinX);
            maxX = Mathf.Max(maxX, nextMaxX);
            minZ = Mathf.Min(minZ, nextMinZ);
            maxZ = Mathf.Max(maxZ, nextMaxZ);
        }
    }

    private static void ConfigurePngImporterForExactResolution(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureType = TextureImporterType.Default;
        importer.mipmapEnabled = false;
        importer.isReadable = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = ResolveMaxTextureSize(assetPath);
        importer.SaveAndReimport();
    }

    private static int ResolveMaxTextureSize(string assetPath)
    {
        var fullPath = ResolveFolderPath(assetPath);
        if (!File.Exists(fullPath))
        {
            return 8192;
        }

        var bytes = File.ReadAllBytes(fullPath);
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (!texture.LoadImage(bytes, markNonReadable: false))
            {
                return 8192;
            }

            return Mathf.Clamp(Mathf.NextPowerOfTwo(Mathf.Max(texture.width, texture.height)), 32, 16384);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
