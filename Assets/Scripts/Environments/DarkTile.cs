using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Tilemap))]
public class DarkTile : EnvironmentalTileBase
{
    [SerializeField] private GameObject DarkZoneEffectPrefab;
    private Dictionary<EntityBase, UI_DarkZoneEffect> activeEffects = new Dictionary<EntityBase, UI_DarkZoneEffect>();

    private Tilemap tilemap;
    private bool isInitialized = false;

    [SerializeField] private float P_VisionReductionPercent = 0.4f;
    [SerializeField] private float E_VisionReductionPercent = 0.5f;

    public override void OnStageStart()
    {
        tilemap = GetComponent<Tilemap>();
        if (CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.TERRAIN))
        {
            bool hasGeologist = CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.GEOLOGIST_OBSERVE)
                || CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.GEOLOGIST_EXPLORE)
                || CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.GEOLOGIST_STUDY);
            float multiplier = hasGeologist ? 1.75f : 1.5f;
            P_VisionReductionPercent *= multiplier;
            E_VisionReductionPercent *= multiplier;
        }
        base.OnStageStart();
    }

    private Dictionary<Vector3Int, TileBase> originalTiles = new Dictionary<Vector3Int, TileBase>();
    private void CaptureTilemapData()
    {
        // Store every tile currently in the DarkTile tilemap
        BoundsInt bounds = tilemap.cellBounds;
        foreach (Vector3Int pos in bounds.allPositionsWithin)
        {
            TileBase tile = tilemap.GetTile(pos);
            if (tile != null)
            {
                originalTiles[pos] = tile;
            }
        }
        isInitialized = true;
    }

    // Helper method for Candleknights to check what should be there
    public TileBase GetOriginalTile(Vector3Int pos)
    {
        if (!isInitialized) CaptureTilemapData();
        return originalTiles.TryGetValue(pos, out TileBase tile) ? tile : null;
    }

    public override StageManager.EnvironmentType GetEnvironmentType()
    {
        return StageManager.EnvironmentType.DARK_ZONE;
    }

    public override void OnEntityEnter(EntityBase entity)
    {
        ApplyBlindnessEffect(entity);

        if (activeEffects.ContainsKey(entity))
        {
            activeEffects[entity].Initialize(entity);
        }
        else
        {
            UI_DarkZoneEffect effectInstance = Instantiate(DarkZoneEffectPrefab, entity.transform.position + Vector3.up * 100f, Quaternion.identity).GetComponent<UI_DarkZoneEffect>();
            effectInstance.Initialize(entity);
            activeEffects.Add(entity, effectInstance);
        }

        base.OnEntityEnter(entity);

        if (entity is Gloompincer g) g.OnShroudedZoneEnter();
        else if (entity is Toy t) t.OnShroudedZoneEnter();
    }

    public override void OnEntityStay(EntityBase entity)
    {
        ApplyBlindnessEffect(entity);

        if (activeEffects.ContainsKey(entity) && activeEffects[entity])
        {
            activeEffects[entity].gameObject.SetActive(true);
        }

        base.OnEntityStay(entity);
    }

    void ApplyBlindnessEffect(EntityBase entity)
    {
        if (entity as Gloompincer || entity as Candle || entity as Toy) return;

        float percentage = entity is PlayerBase ? P_VisionReductionPercent : E_VisionReductionPercent;

        float minRange = entity.b_attackRange < 100 ? entity.b_attackRange : 100;

        if (entity.b_attackRange * percentage < minRange)
        {
            percentage = 1.0f - minRange / entity.b_attackRange;
        }

        entity.ApplyEffect(Effect.AffectedStat.ARNG, "DARK_TILE_VISION_REDUCTION", -percentage * 100f, 9999f, true);
    }

    public override void OnEntityExit(EntityBase entity)
    {
        base.OnEntityExit(entity);

        entity.RemoveEffect("DARK_TILE_VISION_REDUCTION");
        if (activeEffects.ContainsKey(entity))
        {
            activeEffects[entity].DisableEffect();
        }

        if (entity is HibernatorKnight h) h.OnShroudedZoneExit();
        else if (entity is Gloompincer g) g.OnShroudedZoneExit();
        else if (entity is Toy t) t.OnShroudedZoneExit(); 
    }
}
