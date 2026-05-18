using System;
using System.Collections.Generic;
using UnityEngine;
using static StageManager;

public class CasterProjectileScript : ProjectileScript
{
    private bool fieldExpertEnabled = false;

    private HashSet<EnvironmentType> contactedEnvironmentType = new();
    
    PlayerManager playerManager;
    PlayerRanged caster;
    void GetFieldExpert()
    {
        caster = ProjectileFirer ? ProjectileFirer.GetComponent<PlayerRanged>() : null;
        PlayerCasterIllusion playerCasterIllusion = ProjectileFirer && !caster ? ProjectileFirer.GetComponent<PlayerCasterIllusion>() : null;

        fieldExpertEnabled =
            (caster && caster.Skills.Contains(SkillTree_Manager.SkillName.SPIRAL_FIELD_EXPERT))
            ||
            (playerCasterIllusion && playerCasterIllusion.FieldExpert);

        playerManager = caster ? caster.getPlayerManager : playerCasterIllusion ? playerCasterIllusion.playerManager : null;
    }

    public override void ShootTowards(Vector3 targetPosition, ProjectileType projectileType, float ProjectileLifespan, bool useAbsoluteDirection = false, params Type[] enemy)
    {
        GetFieldExpert();

        base.ShootTowards(targetPosition, projectileType, ProjectileLifespan, useAbsoluteDirection, enemy);
    }

    public override void ShootTowards(Vector3 targetPosition, EntityBase enemy, ProjectileType projectileType, float ProjectileLifespan, bool useAbsoluteDirection = false)
    {
        GetFieldExpert();

        base.ShootTowards(targetPosition, enemy, projectileType, ProjectileLifespan, useAbsoluteDirection);
    }

    short bounceCount = 0;
    public override void OnHitEvent(EntityBase target)
    {
        if (!target || target == excludedTarget) return;

        if (ProjectileFirer)
        {
            ApplyContactedEnvironmentalEffects(target);
        }

        bool resetOnHit = false;
        if (fieldExpertEnabled && bounceCount < 5)
        {
            resetOnHit = true;
            Bounce(target);
        }

        if (doesDamage)
        {
            if (ProjectileFirer) 
                ProjectileFirer.DealDamage(target, DamageInstance.PhysicalDamage, DamageInstance.MagicalDamage, DamageInstance.TrueDamage, true, this);
            else
            {
                target.TakeDamage(
                    target.DamageOutput(target, DamageInstance.PhysicalDamage, DamageInstance.MagicalDamage, DamageInstance.TrueDamage), 
                    null);
            }

            if (!resetOnHit) allowingUpdate = false;
        }

        if (displayMsg != string.Empty) ProjectileFirer.DisplayDamage(displayMsg, msgDisplayOffset);

        if (!resetOnHit)
        {
            gameObject.SetActive(false);
            Destroy(this.gameObject, 0.5f);
        }
    }

    bool appliedDamage = false;
    readonly bool ragingTerrain = CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.TERRAIN);
    void ApplyContactedEnvironmentalEffects(EntityBase target)
    {
        foreach (var contactedEnvironmentType in contactedEnvironmentType)
        {
            float value;
            switch (contactedEnvironmentType)
            {
                case EnvironmentType.ORIGINIUM_TILE:
                    if (appliedDamage) break;

                    value = 0.3f;
                    if (ragingTerrain) value *= 2;

                    DamageInstance.TrueDamage += (int)(Mathf.Max(1, DamageInstance.TotalDamage * value));
                    appliedDamage = true;
                    break;

                case EnvironmentType.MEDICAL_TILE:
                    value = 0.04f;
                    if (ragingTerrain) value *= 2;

                    if (playerManager) playerManager.activePlayer.Heal(playerManager.activePlayer.mHealth * value);
                    else ProjectileFirer.Heal(ProjectileFirer.mHealth * value);

                    break;

                case EnvironmentType.HEAT_PUMP_VENT:
                    value = 1.5f;
                    if (ragingTerrain) value *= 2;

                    ProjectileFirer.PushEntityFrom(target, ProjectileFirer.GetAttackPosition(), value, 0.1f);
                    break;

                case EnvironmentType.DARK_ZONE:
                    value = 1.25f;
                    if (ragingTerrain) value *= 2;

                    target.ApplyEffect(Effect.AffectedStat.ARNG, "BLINDESS", -200f, value, true);
                    break;
            }
        }
    }

    void Bounce(EntityBase initTarget)
    {
        bounceCount++;

        Collider2D col = initTarget.selfColliders[0];

        Vector2 contact = col.ClosestPoint(rb2d.position);
        Vector2 normal = (rb2d.position - contact).normalized;
        if (normal.magnitude < 0.1f)
        {
            normal = -rb2d.velocity.normalized;
        }

        Vector2 reflected = Vector2.Reflect(rb2d.velocity, normal).normalized;

        excludedTarget = initTarget;
        Acceleration = 0;

        ShootTowards(reflected, ProjectileType.CATCH_FIRST_TARGET_OF_TYPE, Mathf.Max(Lifespan, 5f), true, initTarget.GetGenericType());
        if (caster)
        {
            caster.ProjectileBounceCallback();
        }
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();
    }

    public override void HandleHit(GameObject other)
    {
        if (!allowingUpdate) return;

        bool checkForEnvironmentalTile = fieldExpertEnabled;
        if (checkForEnvironmentalTile)
        {
            EnvironmentalTileBase environmentalTileBase = other.GetComponent<EnvironmentalTileBase>();
            if (environmentalTileBase) AddContactedEnvironmental(environmentalTileBase.GetEnvironmentType());
        }

        base.HandleHit(other);
    }

    SpriteRenderer spriteRenderer;
    void AddContactedEnvironmental(EnvironmentType environmentType)
    {
        if (!fieldExpertEnabled) return;
        if (contactedEnvironmentType.Contains(environmentType)) return;
        
        if (!spriteRenderer) spriteRenderer = GetComponent<SpriteRenderer>();

        contactedEnvironmentType.Add(environmentType);

        Color finalColor = Color.black;

        foreach (var envType in contactedEnvironmentType)
        {
            finalColor += envType switch
            {
                EnvironmentType.ORIGINIUM_TILE => new(0.72f, 0, 0),
                EnvironmentType.MEDICAL_TILE => new(0, 0.72f, 0.13f),
                EnvironmentType.HEAT_PUMP_VENT => Color.yellow,
                EnvironmentType.DARK_ZONE => Color.black,
                _ => Color.white,
            };
        }

        finalColor /= contactedEnvironmentType.Count;
        spriteRenderer.color = finalColor;

        var trail = GetComponent<TrailRenderer>();

        Color finalTrailColor = Color.black;

        foreach (var envType in contactedEnvironmentType)
        {
            finalTrailColor += envType switch
            {
                EnvironmentType.ORIGINIUM_TILE => new(0.72f, 0, 0),
                EnvironmentType.MEDICAL_TILE => new(0, 0.72f, 0.13f),
                EnvironmentType.HEAT_PUMP_VENT => Color.yellow,
                EnvironmentType.DARK_ZONE => Color.black,
                _ => Color.white,
            };
        }

        finalTrailColor /= contactedEnvironmentType.Count;

        trail.startColor = new Color(
            finalTrailColor.r,
            finalTrailColor.g,
            finalTrailColor.b,
            0.7f
        );

        trail.endColor = new Color(
            trail.startColor.r,
            trail.startColor.g,
            trail.startColor.b,
            0f
        );

        trail.enabled = true;

        if (caster)         
        {
            caster.ProjectileEnvironmentEnhanceCallback();
        }
    }
}