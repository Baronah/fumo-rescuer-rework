using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class CandleManager : MonoBehaviour
{
    private static CandleManager instance;

    [SerializeField] private DarkTile darkTileReference;

    private Tilemap shroudedZonesTiles;
    private TilemapCollider2D tilemapCollider;
    private DarkTile darkTileScript;

    private HashSet<Candle> activeCandles = new HashSet<Candle>();
    private Dictionary<Vector3Int, int> tileRefreshCount = new Dictionary<Vector3Int, int>(); // Persistent tracking
    private Dictionary<Vector3Int, TileBase> pendingTileUpdates = new Dictionary<Vector3Int, TileBase>();
    private HashSet<Vector3Int> tilesToRefresh = new HashSet<Vector3Int>();

    private bool isInitialized = false;

    private void Start()
    {
        instance = this;
        InitializeManager();
    }

    private void InitializeManager()
    {
        if (isInitialized)
            return;

        if (darkTileReference != null)
        {
            darkTileScript = darkTileReference;
        }
        else
        {
            darkTileScript = FindObjectOfType<DarkTile>();
        }

        if (!darkTileScript)
        {
            return;
        }

        shroudedZonesTiles = darkTileScript.GetComponent<Tilemap>();
        if (!shroudedZonesTiles)
        {
            return;
        }

        tilemapCollider = shroudedZonesTiles.GetComponent<TilemapCollider2D>();
        if (!tilemapCollider)
        {
            return;
        }

        isInitialized = true;
    }

    private void Update()
    {
        if (!isInitialized)
            InitializeManager();

        // Collect updates every frame
        if (activeCandles.Count > 0)
        {
            pendingTileUpdates.Clear();
            tilesToRefresh.Clear();

            // Reset counts for this frame, then recalculate from all active candles
            Dictionary<Vector3Int, int> frameRefreshCount = new Dictionary<Vector3Int, int>();

            foreach (var candle in activeCandles)
            {
                candle.CollectTileUpdates(pendingTileUpdates, frameRefreshCount, tilesToRefresh);
            }

            // Now update the persistent count based on what changed
            foreach (var kvp in frameRefreshCount)
            {
                tileRefreshCount[kvp.Key] = kvp.Value;
            }

            // Remove tiles that are no longer lit by any candle
            var keysToRemove = new List<Vector3Int>();
            foreach (var kvp in tileRefreshCount)
            {
                if (!frameRefreshCount.ContainsKey(kvp.Key))
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            foreach (var key in keysToRemove)
            {
                tileRefreshCount.Remove(key);
            }
        }
    }

    private void LateUpdate()
    {
        // Apply tile changes once per frame
        if (pendingTileUpdates.Count > 0 && shroudedZonesTiles)
        {
            Vector3Int[] positions = new Vector3Int[pendingTileUpdates.Count];
            TileBase[] tiles = new TileBase[pendingTileUpdates.Count];
            int i = 0;

            foreach (var kvp in pendingTileUpdates)
            {
                positions[i] = kvp.Key;
                tiles[i] = kvp.Value;
                i++;
            }

            shroudedZonesTiles.SetTiles(positions, tiles);

            // Refresh only the changed tiles and their neighbors
            if (tilemapCollider)
            {
                foreach (var tilePos in tilesToRefresh)
                {
                    shroudedZonesTiles.RefreshTile(tilePos);

                    // Also refresh neighbors to ensure collider connectivity
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            Vector3Int neighborPos = tilePos + new Vector3Int(dx, dy, 0);
                            shroudedZonesTiles.RefreshTile(neighborPos);
                        }
                    }
                }
            }
        }
    }

    public static void RegisterCandleknight(Candle candle)
    {
        if (instance != null)
        {
            instance.activeCandles.Add(candle);
        }
    }

    public static void UnregisterCandleknight(Candle candle)
    {
        if (instance != null)
        {
            instance.activeCandles.Remove(candle);
        }
    }

    public static DarkTile GetDarkTile()
    {
        if (instance != null && !instance.isInitialized)
        {
            instance.InitializeManager();
        }
        return instance ? instance.darkTileScript : null;
    }

    public static Tilemap GetShroudedZonesTilemap()
    {
        if (instance != null && !instance.isInitialized)
        {
            instance.InitializeManager();
        }
        return instance ? instance.shroudedZonesTiles : null;
    }
}