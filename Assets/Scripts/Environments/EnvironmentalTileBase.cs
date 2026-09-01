using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static StageManager;

public class EnvironmentalTileBase : MonoBehaviour
{
    [SerializeField] protected float Interval = 1.0f;
    
    protected List<EntityBase> entitiesWithin = new List<EntityBase>();
    List<EntityBase> entitiesWithinClone;

    public virtual float GetValue() => 0;

    public virtual EnvironmentType GetEnvironmentType() => EnvironmentType.KEYS;

    private void Start()
    {
        OnStageStart();
    }

    public virtual void OnStageStart()
    {
        StartCoroutine(HandleUnitWithinRange());
    }

    public virtual IEnumerator HandleUnitWithinRange()
    {
        while (true)
        {
            yield return new WaitForSeconds(Interval);
            ProcessTick();
        }
    }

    public virtual void ProcessTick()
    {
        entitiesWithin.RemoveAll(e => !e || !e.IsAlive());

        entitiesWithinClone = new(entitiesWithin);
        entitiesWithinClone.ForEach(e =>
        {
            OnEntityStay(e);
        });
    }

    public virtual void OnEntityEnter(EntityBase entity)
    {
        entity.AddEnvironmentalTilesThisUnitStandingOn(GetEnvironmentType());
        entitiesWithin.Add(entity);
        if (entity is PlayerBase pb)
        {
            ProcessPlayerGeologistUpgrade(pb);
        }
    }

    public virtual void OnEntityStay(EntityBase entity)
    {
        entity.AddEnvironmentalTilesThisUnitStandingOn(GetEnvironmentType());
        if (!entitiesWithin.Contains(entity)) entitiesWithin.Add(entity);

        if (entity is PlayerBase pb)
        {
            ProcessPlayerGeologistUpgrade(pb);
        }
    }

    public virtual void ProcessPlayerGeologistUpgrade(PlayerBase pb)
    {
        if (!pb) return;

        if (pb.Skills.Contains(SkillTree_Manager.SkillName.GEOLOGIST_OBSERVE))
        {
            pb.ReduceUltimateCooldown(Interval * 0.4f, PlayerBase.CooldownReductionType.FLAT);
        }
        
        if (pb.Skills.Contains(SkillTree_Manager.SkillName.GEOLOGIST_STUDY))
        {
            pb.ApplyEffect(Effect.AffectedStat.ATK, "GEOLOGIST_ATK_BUFF", 50f, 9999f, true);
        }
        
        if (pb.Skills.Contains(SkillTree_Manager.SkillName.GEOLOGIST_EXPLORE))
        {
            pb.ApplyEffect(Effect.AffectedStat.MSPD, "GEOLOGIST_MSPD_BUFF", 50f, 9999f, true);
        }
    }

    public virtual void OnEntityExit(EntityBase entity)
    {
        entity.RemoveEnvironmentalTilesThisUnitStandingOn(GetEnvironmentType());
        entitiesWithin.Remove(entity);
        if (entity is PlayerBase pb && pb.environmentalTilesStandingOn.Count <= 0)
        {
            if (pb.Skills.Contains(SkillTree_Manager.SkillName.GEOLOGIST_STUDY))
            {
                pb.RemoveEffect("GEOGOLIST_ATK_BUFF");
            }
            else if (pb.Skills.Contains(SkillTree_Manager.SkillName.GEOLOGIST_EXPLORE))
            {
                pb.RemoveEffect("GEOGOLIST_MSPD_BUFF");
            }
        }
    }

    public virtual void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision) return;

        EntityBase entityBase = collision.GetComponent<EntityBase>();
        if (!entityBase || !entityBase.IsAlive() || entitiesWithin.Contains(entityBase)) return;
        if (collision.isTrigger) return;

        OnEntityEnter(entityBase);
    }

    public virtual void OnTriggerStay2D(Collider2D collision)
    {
        if (!collision) return;

        EntityBase entityBase = collision.GetComponent<EntityBase>();
        if (!entityBase || !entityBase.IsAlive() || entitiesWithin.Contains(entityBase)) return;
        if (collision.isTrigger) return;

        entitiesWithin.Add(entityBase);
    }

    public virtual void OnTriggerExit2D(Collider2D collision)
    {
        if (!collision) return;

        EntityBase entityBase = collision.GetComponent<EntityBase>();
        if (!entityBase || !entityBase.IsAlive() || !entitiesWithin.Contains(entityBase)) return;
        if (collision.isTrigger) return;

        OnEntityExit(entityBase);
    }
}