using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Tilemap))]
public class OriginiumPollution : EnvironmentalTileBase
{
    [SerializeField] private float TrueDamagePerTick = 15f;
    [SerializeField] private float EnemyDamageMultiplier = 1.0f;

    public override StageManager.EnvironmentType GetEnvironmentType()
    {
        return StageManager.EnvironmentType.ORIGINIUM_TILE;
    }

    public override void OnStageStart()
    {
        if (CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.TERRAIN))
        {
            bool hasGeologist = CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.GEOLOGIST_OBSERVE)
                || CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.GEOLOGIST_EXPLORE)
                || CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.GEOLOGIST_STUDY);
            float multiplier = hasGeologist ? 3f : 2f;

            TrueDamagePerTick *= multiplier;
        }
        base.OnStageStart();
        StartCoroutine(Pulse());
    }

    IEnumerator Pulse()
    {
        Tilemap tilemap = GetComponent<Tilemap>();

        float duration = 2f;
        while (true)
        {
            float c = 0;

            while (c < duration)
            {
                tilemap.color = Color.Lerp(Color.white, Color.black, c * 1.0f / duration);
                c += Time.deltaTime;
                yield return null;
            }

            c = 0;
            while (c < duration)
            {
                tilemap.color = Color.Lerp(Color.black, Color.white, c * 1.0f / duration);
                c += Time.deltaTime;
                yield return null;
            }

            tilemap.color = Color.white;
        }
    }

    public override void OnEntityEnter(EntityBase entity)
    {
        base.OnEntityEnter(entity);
        if (entity is Sudaram s)
        {
            s.OnOriginiumPollutionEnter();
        }
        else if (entity is OriginiumSpider os)
        {
            os.Pollute();
            os.InstaKill();
        }
        else if (entity is OriginiumSpiderAlpha osa)
        {
            osa.Pollute();
            osa.InstaKill();
        }
    }

    public override void OnEntityStay(EntityBase e)
    {
        base.OnEntityStay(e);
        int damage = (int)(e as EnemyBase ? TrueDamagePerTick * EnemyDamageMultiplier : TrueDamagePerTick);

        if (e && e.IsAlive())
        {
            if (e is Sudaram sr) damage = (int)(damage * sr.originiumPollutionDamageMultiplier);

            e.TakeDamage(new(0, 0, damage), null, null, false, true);
        }
    }

    public override void OnEntityExit(EntityBase entity)
    {
        base.OnEntityExit(entity);

        if (entity is Sudaram s)
        {
            s.OnOriginiumPollutionExit();
        }
    }
}