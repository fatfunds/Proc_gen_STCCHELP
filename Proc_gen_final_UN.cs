using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Tilemaps;

public class BiomeGenerator : MonoBehaviour
{
    [Header("Events")]
    public UnityEvent OnWorldGenerated;

    [Header("Tilemaps")]
    public Tilemap waterLayer;
    public AnimatedTile waterTile;
    public Tilemap deepwaterLayer;
    public RuleTile deepwaterTile;
    public Tilemap sandLayer;
    public RuleTile sandTile;
    public Tilemap grassLayer;
    public RuleTile grassTile;
    public RuleTile magicgrassTile;
    public Tilemap dirtLayer;
    public RuleTile magicdirtTile;
    public RuleTile dirtTile;
    public Tilemap pathLayer;

    [Header("Dependencies")]
    public RoadGenerator roadGenerator;
    public EnvironmentObjectPlacer objectPlacer;
    public VillagePlacer villagePlacer;
    public WorldSettings worldSettings;

    [Header("Sub Biome Configs")]
    public List<SubBiomeConfig> subBiomeConfigs = new();

    [Header("Grid Reference")]
    public Grid grid;

    [Header("Debug")]
    public int seed;
    public bool showSubBiomeDebugGizmos = true;

    public Dictionary<Vector3Int, TileData> tileDataMap = new();
    private List<VillageArea> _debugVillageAreas;
    private Dictionary<SubBiomeType, Color> subBiomeColors = new();
    private Dictionary<SubBiomeType, List<Vector3>> subBiomeRegions = new();

    public void Regenerate()
    {
        seed = worldSettings.randomizeSeedOnStart ? Random.Range(0, 100_000) : worldSettings.seed;
        worldSettings.seed = seed;

        Debug.Log($"[BiomeGenerator] Seed: {seed}");
        ClearTiles();
        GenerateBiomes();
    }

    void GenerateBiomes()
    {
        int width = worldSettings.worldWidth;
        int height = worldSettings.worldHeight;

        tileDataMap.Clear();
        var noise = new NoiseGenerator(seed, worldSettings);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3Int pos = new(x, y, 0);

                float baseHeight = noise.GetHeight(x, y);
                float falloff = CalculateIslandFalloff(x, y);
                float heightVal = baseHeight * falloff;

                float objVal = noise.GetObject(x, y);
                float wealthVal = noise.GetWealth(x, y);
                float magicVal = noise.GetMagic(x, y);
                float hostilityVal = noise.GetHostility(x, y);

                TileType type = PlaceBiomeTile(pos, heightVal);

                tileDataMap[pos] = new TileData
                {
                    pos = pos,
                    height = heightVal,
                    objVal = objVal,
                    wealth = wealthVal,
                    magic = magicVal,
                    hostility = hostilityVal,
                    tileType = type,
                    subBiomeType = SubBiomeType.None
                };
            }
        }

        GenerateSubBiomeFloodFill();
        objectPlacer?.PlaceEnvironmentObjects(tileDataMap, subBiomeConfigs);
        OnWorldGenerated?.Invoke();

        Debug.Log($"[BiomeGenerator] Generated {tileDataMap.Count} tiles, {objectPlacer.placedObjectIDs.Count} objects");
        FindVillageAreas();
    }

    private float CalculateIslandFalloff(int x, int y)
    {
        float width = worldSettings.worldWidth;
        float height = worldSettings.worldHeight;
        float power = worldSettings.islandFalloffPower;
        float landRadiusPercent = worldSettings.islandLandRadiusPercent;

        Vector2 center = new Vector2(width / 2f, height / 2f);
        Vector2 pos = new Vector2(x, y);
        float distance = Vector2.Distance(pos, center);
        float maxRadius = Mathf.Min(width, height) * 0.5f * landRadiusPercent;
        float normalized = distance / maxRadius;
        return Mathf.Clamp01(1f - Mathf.Pow(Mathf.Clamp01(normalized), power));
    }

    TileType PlaceBiomeTile(Vector3Int pos, float height)
    {
        TileType type = TileType.Unknown;

        if (height < 0.1f)
        {
            deepwaterLayer.SetTile(pos, deepwaterTile);
            type = TileType.DeepWater;
        }
        else if (height < 0.2f)
        {
            deepwaterLayer.SetTile(pos, deepwaterTile);
            waterLayer.SetTile(pos, waterTile);
            type = TileType.Water;
        }
        else if (height < 0.4f)
        {
            waterLayer.SetTile(pos, waterTile);
            sandLayer.SetTile(pos, sandTile);
            type = TileType.Sand;
        }
        else if (height < 0.6f)
        {
            sandLayer.SetTile(pos, sandTile);
            grassLayer.SetTile(pos, grassTile);
            type = TileType.Grass;
        }
        else
        {
            grassLayer.SetTile(pos, grassTile);
            dirtLayer.SetTile(pos, dirtTile);
            type = TileType.Dirt;
        }

        return type;
    }

    public void GenerateSubBiomeFloodFill()
    {
        List<Vector3Int> unassigned = tileDataMap.Keys.ToList();
        subBiomeRegions.Clear();
        HashSet<Vector3Int> reserved = new();

        var shuffled = subBiomeConfigs.OrderBy(_ => Random.value).ToList();

        foreach (var config in shuffled)
        {
            int clusterCount = Random.Range(config.minClusters, config.maxClusters + 1);

            var eligible = unassigned
                .Where(pos =>
                    config.allowedTileTypes.Contains(tileDataMap[pos].tileType) &&
                    (config.nearTileType == TileType.Unknown || IsNearTileType(pos, config.nearTileType, config.nearRadius)))
                .ToList();

            int created = 0, attempts = 0;
            while (created < clusterCount && eligible.Count > 0 && attempts < clusterCount * 3)
            {
                attempts++;
                Vector3Int seed = eligible[Random.Range(0, eligible.Count)];
                eligible.Remove(seed);
                if (reserved.Contains(seed)) continue;

                int targetSize = Random.Range(config.minSize, config.maxSize + 1);

                HashSet<Vector3Int> region = new();
                Queue<Vector3Int> queue = new();
                region.Add(seed);
                queue.Enqueue(seed);
                reserved.Add(seed);
                unassigned.Remove(seed);

                while (region.Count < targetSize && queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    foreach (var neighbor in GetNeighbors(current))
                    {
                        if (!eligible.Contains(neighbor) || reserved.Contains(neighbor)) continue;
                        if (!config.allowedTileTypes.Contains(tileDataMap[neighbor].tileType)) continue;

                        region.Add(neighbor);
                        queue.Enqueue(neighbor);
                        reserved.Add(neighbor);
                        unassigned.Remove(neighbor);
                        eligible.Remove(neighbor);
                    }
                }

                if (region.Count < config.minSize / 2) continue;

                foreach (var pos in region)
                {
                    var td = tileDataMap[pos];
                    td.subBiomeType = config.type;
                    tileDataMap[pos] = td;
                }

                if (!subBiomeRegions.ContainsKey(config.type))
                    subBiomeRegions[config.type] = new List<Vector3>();
                subBiomeRegions[config.type].AddRange(region.Select(p => grid.CellToWorld(p)));

                Debug.Log($"[SubBiome] {config.type} blob of {region.Count} tiles placed.");
                created++;
            }
        }

        foreach (var pos in unassigned)
        {
            var td = tileDataMap[pos];
            td.subBiomeType = td.tileType switch
            {
                TileType.Sand => SubBiomeType.SeaShellCluster,
                TileType.Grass => SubBiomeType.OakForest,
                TileType.Dirt => SubBiomeType.RockyLands,
                _ => SubBiomeType.None
            };
            tileDataMap[pos] = td;
        }

        Debug.Log($"[SubBiome] Total assigned: {tileDataMap.Count(k => k.Value.subBiomeType != SubBiomeType.None)}");
    }

    public void FindVillageAreas()
    {
        // (unchanged)
    }

    private SubBiomeType FindDominantSubBiome(VillageArea area)
    {
        var counts = new Dictionary<SubBiomeType, int>();
        foreach (var pos in area.tiles)
        {
            if (tileDataMap.TryGetValue(pos, out var td) && td.subBiomeType != SubBiomeType.None)
                counts[td.subBiomeType] = counts.GetValueOrDefault(td.subBiomeType) + 1;
        }
        return counts.OrderByDescending(p => p.Value).FirstOrDefault().Key;
    }

    bool IsNearTileType(Vector3Int pos, TileType target, int radius)
    {
        for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
            {
                Vector3Int check = new(pos.x + dx, pos.y + dy, 0);
                if (tileDataMap.TryGetValue(check, out var td) && td.tileType == target)
                    return true;
            }
        return false;
    }

    IEnumerable<Vector3Int> GetNeighbors(Vector3Int pos)
    {
        yield return pos + Vector3Int.right;
        yield return pos + Vector3Int.left;
        yield return pos + Vector3Int.up;
        yield return pos + Vector3Int.down;
    }

    void ClearTiles()
    {
        waterLayer.ClearAllTiles();
        sandLayer.ClearAllTiles();
        grassLayer.ClearAllTiles();
        dirtLayer.ClearAllTiles();
        pathLayer.ClearAllTiles();
    }

    public TileData[] SerializeWorld() => tileDataMap.Values.ToArray();

    public void RebuildFromData(TileData[] tiles)
    {
        ClearTiles();
        tileDataMap.Clear();
        foreach (var tile in tiles)
        {
            tileDataMap[tile.pos] = tile;
            PlaceBiomeTile(tile.pos, tile.height);
        }
    }

    public void LoadFromChunks(int slot)
    {
        string tilePath = SaveSystem.GetTilePath(slot);
        string objectPath = SaveSystem.GetObjectPath(slot);

        if (File.Exists(tilePath))
        {
            var tileData = JsonUtility.FromJson<WorldTileData>(File.ReadAllText(tilePath));
            RebuildFromData(tileData.tiles);
        }

        if (File.Exists(objectPath))
        {
            var objectData = JsonUtility.FromJson<WorldObjectData>(File.ReadAllText(objectPath));

            
            if (objectPlacer != null && objectPlacer.TryGetComponent<ObjectStreamer>(out var streamer))
            {
                streamer.loadedSaveObjects = objectData.objects;
                Debug.Log($"[BiomeGenerator] Loaded {objectData.objects.Count} saved objects.");
            }
        }

        OnWorldGenerated?.Invoke();
    }

    public Dictionary<Vector3Int, float> GetHeightMap() => tileDataMap.ToDictionary(p => p.Key, p => p.Value.height);
    public Dictionary<Vector3Int, float> GetWealthMap() => tileDataMap.ToDictionary(p => p.Key, p => p.Value.wealth);
    public Dictionary<Vector3Int, float> GetMagicMap() => tileDataMap.ToDictionary(p => p.Key, p => p.Value.magic);
    public Dictionary<Vector3Int, float> GetHostilityMap() => tileDataMap.ToDictionary(p => p.Key, p => p.Value.hostility);

    [System.Serializable]
    public class WorldTileData { public TileData[] tiles; }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !showSubBiomeDebugGizmos) return;

        if (grid == null) grid = Object.FindFirstObjectByType<Grid>();
        if (grid == null) return;

        Vector3 cellSize = grid.cellSize;
        Vector3 offset = new Vector3(cellSize.x, cellSize.y) * 0.5f;

        foreach (var kvp in tileDataMap)
        {
            var type = kvp.Value.subBiomeType;
            if (type == SubBiomeType.None) continue;

            if (!subBiomeColors.ContainsKey(type))
                subBiomeColors[type] = GetStaticColor(type);

            Gizmos.color = subBiomeColors[type];
            Gizmos.DrawCube(grid.CellToWorld(kvp.Key) + offset, cellSize * 0.8f);
        }

        foreach (var pair in subBiomeRegions)
        {
            if (pair.Value.Count == 0) continue;
            Color fill = GetStaticColor(pair.Key); fill.a = 0.15f;
            Color border = GetStaticColor(pair.Key); border.a = 1f;

            Vector3 center = pair.Value.Aggregate(Vector3.zero, (acc, v) => acc + v) / pair.Value.Count;
            float avgRadius = pair.Value.Sum(p => Vector3.Distance(center, p)) / pair.Value.Count;

            UnityEditor.Handles.color = fill;
            UnityEditor.Handles.DrawSolidDisc(center, Vector3.forward, avgRadius);
            UnityEditor.Handles.color = border;
            UnityEditor.Handles.DrawWireDisc(center, Vector3.forward, avgRadius);
            UnityEditor.Handles.Label(center + Vector3.up * 0.5f, $"{pair.Key} ({pair.Value.Count})");
        }

        if (_debugVillageAreas != null)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
            foreach (var area in _debugVillageAreas)
                foreach (var t in area.tiles)
                    Gizmos.DrawCube(grid.CellToWorld(t) + offset, cellSize);
        }
    }

    private Color GetStaticColor(SubBiomeType type)
    {
        return type switch
        {
            SubBiomeType.None => Color.gray,
            SubBiomeType.SeaShellCluster => Color.magenta,
            SubBiomeType.OakForest => Color.green,
            SubBiomeType.RockyLands => Color.gray * 0.5f,
            SubBiomeType.MushroomGrove => new Color(0.6f, 0.2f, 0.8f),
            SubBiomeType.FlowerField => new Color(1f, 0.5f, 0.5f),
            SubBiomeType.CrystalCove => Color.cyan,
            SubBiomeType.PalmCluster => Color.yellow,
            SubBiomeType.CocunutPalmCluster => new Color(1f, 0.85f, 0.5f),
            SubBiomeType.SunFlowerField => Color.yellow * 0.8f,
            SubBiomeType.PeonyField => new Color(1f, 0.3f, 0.8f),
            SubBiomeType.RockyOutcrop => Color.gray,
            SubBiomeType.CrystalOutcrop => Color.cyan * 0.8f,
            _ => Random.ColorHSV(0f, 1f, 0.5f, 1f, 0.8f, 1f)
        };
    }
#endif
}
