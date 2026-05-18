using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public class Candle : EnemyBase
{
    [SerializeField] private GameObject Manager;
    [SerializeField] private float movementThreshold = 0.5f;

    private Tilemap shroudedZonesTiles;
    private DarkTile darkTileScript;

    private HashSet<Vector3Int> currentlyHiddenPositions = new HashSet<Vector3Int>();
    private HashSet<Vector3Int> lastCalculatedPositions = new HashSet<Vector3Int>();

    private Vector3 lastLitPosition;
    private float lastRange = -1;
    private bool isManagerInitialized = false;

    [SerializeField] Image attackRangeIndicator;

    public override void InitializeComponents()
    {
        base.InitializeComponents();
        UpdateManager();
    }

    void UpdateManager()
    {
        if (isManagerInitialized) return;

        if (!FindAnyObjectByType<CandleManager>())
        {
            Instantiate(Manager);
        }

        darkTileScript = CandleManager.GetDarkTile();
        shroudedZonesTiles = CandleManager.GetShroudedZonesTilemap();

        if (darkTileScript && shroudedZonesTiles)
        {
            lastLitPosition = AttackPosition.position;
            CandleManager.RegisterCandleknight(this);
            isManagerInitialized = true;
        }
    }


    private Vector3 offset = new(0, 1f, 0);
    private void Update()
    {
        if (!isManagerInitialized)
        {
            UpdateManager();
        }

        if (attackRangeIndicator != null)
        {
            attackRangeIndicator.transform.parent.localPosition = AttackPosition.localPosition + offset;
            attackRangeIndicator.rectTransform.sizeDelta = new Vector2(attackRange * 1.9f, attackRange * 1.9f);
        }
    }

    public override void OnFreezeEnter()
    {
        base.OnFreezeEnter();
        ApplyEffect(Effect.AffectedStat.ARNG, "CANDLE_FREEZE_DEBUFF", -100f, 9999f, true);
    }

    public override void OnFreezeExit()
    {
        base.OnFreezeExit();
        RemoveEffect("CANDLE_FREEZE_DEBUFF");
    }

    public override void TakeDamage(DamageInstance damage, EntityBase source, ProjectileScript projectileInfo = null, bool IgnoreInvulnerability = false, bool CalculateDamage = false) { }

    public override void Move() { }

    public override IEnumerator Attack() { yield break; }

    public override void OnFirsttimePlayerSpot(bool viaAlert = false) { }

    private float GetLitArena => IsFrozen || IsBlinded ? 0 : attackRange + 25f;

    private bool ShouldUpdate()
    {
        if (!isManagerInitialized || !shroudedZonesTiles || IsBeingShifted)
            return false;

        float distanceMoved = Vector3.Distance(AttackPosition.position, lastLitPosition);

        return distanceMoved > movementThreshold || !Mathf.Approximately(GetLitArena, lastRange);
    }

    public void CollectTileUpdates(Dictionary<Vector3Int, TileBase> tilesToSet, Dictionary<Vector3Int, int> tileUpdateCount, HashSet<Vector3Int> tilesToRefresh)
    {
        if (!isManagerInitialized || !shroudedZonesTiles || !darkTileScript)
            return;

        if (!ShouldUpdate())
            return;

        lastLitPosition = AttackPosition.position;
        lastRange = GetLitArena;

        float litArena = lastRange;
        Vector3Int centerCell = shroudedZonesTiles.WorldToCell(AttackPosition.position);
        lastCalculatedPositions.Clear();

        if (litArena > 0)
        {
            int radiusInt = Mathf.CeilToInt(litArena);
            float sqrRadius = litArena * litArena;

            Vector3 knightWorldPos = transform.position;
            Transform tilemapTransform = shroudedZonesTiles.transform;
            Vector3 tilemapScale = tilemapTransform.lossyScale;
            Vector3 tileAnchor = shroudedZonesTiles.tileAnchor;
            Vector3 centerCellWorldPos = shroudedZonesTiles.CellToWorld(centerCell) + tileAnchor;

            Vector3 cellWorldOffsetX = new Vector3(tilemapScale.x, 0, 0);
            Vector3 cellWorldOffsetY = new Vector3(0, tilemapScale.y, 0);

            for (int x = -radiusInt; x <= radiusInt; x++)
            {
                for (int y = -radiusInt; y <= radiusInt; y++)
                {
                    Vector3Int cellPos = new Vector3Int(centerCell.x + x, centerCell.y + y, centerCell.z);

                    Vector3 tileWorldPos = centerCellWorldPos +
                                          (cellWorldOffsetX * x) +
                                          (cellWorldOffsetY * y);

                    float diffX = knightWorldPos.x - tileWorldPos.x;
                    float diffY = knightWorldPos.y - tileWorldPos.y;
                    float sqrDist = (diffX * diffX) + (diffY * diffY);

                    if (sqrDist <= sqrRadius && darkTileScript.GetOriginalTile(cellPos) != null)
                        lastCalculatedPositions.Add(cellPos);
                }
            }
        }

        // Clear newly lit tiles
        foreach (var pos in lastCalculatedPositions)
        {
            if (!currentlyHiddenPositions.Contains(pos))
            {
                tileUpdateCount.TryGetValue(pos, out int count);
                count++;
                tileUpdateCount[pos] = count;

                if (count == 1)
                {
                    tilesToSet[pos] = null;
                    tilesToRefresh.Add(pos);
                }
            }
        }

        // Restore tiles no longer lit
        foreach (var pos in currentlyHiddenPositions)
        {
            if (!lastCalculatedPositions.Contains(pos))
            {
                tileUpdateCount.TryGetValue(pos, out int count);
                count--;

                if (count <= 0)
                {
                    tilesToSet[pos] = darkTileScript.GetOriginalTile(pos);
                    tileUpdateCount.Remove(pos);
                    tilesToRefresh.Add(pos);
                }
                else
                {
                    tileUpdateCount[pos] = count;
                }
            }
        }

        var temp = currentlyHiddenPositions;
        currentlyHiddenPositions = lastCalculatedPositions;
        lastCalculatedPositions = temp;
    }

    public override void OnDeath()
    {
        CandleManager.UnregisterCandleknight(this);

        if (currentlyHiddenPositions.Count > 0 && shroudedZonesTiles && darkTileScript)
        {
            Vector3Int[] posArray = new Vector3Int[currentlyHiddenPositions.Count];
            TileBase[] tileArray = new TileBase[currentlyHiddenPositions.Count];
            int i = 0;

            foreach (var pos in currentlyHiddenPositions)
            {
                posArray[i] = pos;
                tileArray[i] = darkTileScript.GetOriginalTile(pos);
                i++;
            }
            shroudedZonesTiles.SetTiles(posArray, tileArray);
        }

        currentlyHiddenPositions.Clear();
        base.OnDeath();
    }
}