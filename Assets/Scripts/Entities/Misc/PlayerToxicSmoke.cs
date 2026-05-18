using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerToxicSmoke : MonoBehaviour
{
    public float Duration = 5f;
    private float Tick = 0.5f;
    public float MaxDamagePerTick = 6f, MinDamagePerTick = 1f, PercentageHealthDamage = 0.02f;
    private float timer = 0f;

    HashSet<EntityBase> entitiesWithin = new();
    HashSet<EntityBase> entitiesWithinClone;

    SpriteRenderer spriteRenderer;
    Color originalColor;

    public enum BUG_SPRAY_TYPE
    {
        NONE,
        SMOKE,
        POISON,
    }

    public BUG_SPRAY_TYPE TYPE = BUG_SPRAY_TYPE.SMOKE;

    public void SetType(BUG_SPRAY_TYPE Type)
    {
        if (Type == BUG_SPRAY_TYPE.NONE)
        {
            Destroy(gameObject);
            return;
        }

        TYPE = Type;

        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.color = TYPE switch
        {
            BUG_SPRAY_TYPE.SMOKE => new Color(0.65f, 0.65f, 0.65f, 0.7f),
            BUG_SPRAY_TYPE.POISON => new Color(0f, 0.81f, 0.23f, 0.7f),
            _ => spriteRenderer.color,
        };

        originalColor = spriteRenderer.color;

        StartCoroutine(HandleTicks());
    }

    IEnumerator HandleTicks()
    {
        float tickTimer = Tick;
        while (timer < Duration)
        {
            tickTimer += Time.deltaTime;
            timer += Time.deltaTime;

            if (tickTimer >= Tick)
            {
                DoTick();
                tickTimer = 0;
            }

            yield return null;
        }

        DoTick();
        StartCoroutine(Disappear());
    }

    IEnumerator Disappear()
    {
        float c = 0, d = 1;
        Color targetColor = new Color(originalColor.r, originalColor.g, originalColor.b, 0);

        while (c < d)
        {
            c += Time.deltaTime;
            spriteRenderer.color = Color.Lerp(originalColor, targetColor, c * 1.0f / d);
            yield return null;
        }

        Destroy(gameObject);
    }

    void DoTick()
    {
        entitiesWithinClone = new(entitiesWithin);

        if (TYPE == BUG_SPRAY_TYPE.SMOKE) 
        {
            foreach (EntityBase entity in entitiesWithinClone)
            {
                if (!entity || !entity.IsAlive()) continue;

                entity.ApplyEffect(Effect.AffectedStat.ARNG, "BLINDNESS", -200f, Tick + 0.2f, true);
            }
        }
        else if (TYPE == BUG_SPRAY_TYPE.POISON)
        {
            float damage = Mathf.Lerp(MaxDamagePerTick, MinDamagePerTick, timer * 1.0f / Duration);
            foreach (EntityBase entity in entitiesWithinClone)
            {
                if (!entity || !entity.IsAlive()) continue;

                DamageInstance damageInstance = new DamageInstance(0, damage + entity.mHealth * PercentageHealthDamage / 2, 0);
                entity.TakeDamage(damageInstance, null, null, false, true);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision || !collision.gameObject) return;
        EntityBase entity = collision.GetComponent<EnemyBase>();
        if (entity && !entitiesWithin.Contains(entity))
        {
            entitiesWithin.Add(entity);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!collision || !collision.gameObject) return;
        EntityBase entity = collision.GetComponent<EnemyBase>();
        if (entity && entitiesWithin.Contains(entity))
        {
            entitiesWithin.Remove(entity);
        }
    }
}