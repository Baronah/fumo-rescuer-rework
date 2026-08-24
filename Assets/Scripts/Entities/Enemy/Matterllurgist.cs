using System.Collections;
using UnityEngine;

public class Matterllurgist : EnemyBase
{
    [SerializeField] GameObject EnhanceEffect;

    public override void FixedUpdate()
    {
        base.FixedUpdate();
        EnhanceEffect.SetActive(gainedASPD && IsAlive());
    }

    bool gainedASPD = false;
    public override void OnFirsttimePlayerSpot(bool viaAlert = false)
    {
        base.OnFirsttimePlayerSpot(viaAlert);
        if (viaAlert && !gainedASPD)
        {
            ApplyEffect(Effect.AffectedStat.ASPD, "ALERT_SELF_ASPD_BUFF", 200f, 9999f, false);
            StageManager.OnEnemySpecialAbilityActivate(this);
            gainedASPD = true;
        }
    }

    public override IEnumerator OnAttackComplete()
    {
        if (!CanFinishAttack || !SpottedPlayer) yield break;

        short ProjectileBaseSpeed = 600, ProjectileAcceleration = 250;

        Vector2 playerDir = (SpottedPlayerPositionBeforeAttack - AttackPosition.position).normalized;
        Vector3 sourcePosition = AttackPosition.position;

        float[] angles = { 0f, -15f, 15f };
        bool hasAccleration = true;

        foreach (float angle in angles)
        {
            Vector2 rotatedDir = Quaternion.Euler(0, 0, angle) * playerDir;

            Vector3 targetPosition = sourcePosition + (Vector3)rotatedDir;

            CreateProjectileAndShootToward(
                ProjectilePrefab,
                new DamageInstance(atk, 0, 0),
                sourcePosition,
                targetPosition,
                projectileType: ProjectileScript.ProjectileType.CATCH_FIRST_TARGET_OF_TYPE,
                travelSpeed: ProjectileBaseSpeed,
                acceleration: hasAccleration ? ProjectileAcceleration : 0,
                lifeSpan: 8f,
                targetType: typeof(PlayerBase));

            hasAccleration = false;
        }
    }

    public override void InitializeComponents()
    {
        attackPattern = AttackPattern.RANGED;
        damageType = DamageType.PHYSICAL;

        base.InitializeComponents();
    }

    public override void WriteStats()
    {
        Description = "A ranged combatant. Integrating Sarkaz tactics with Leithanien Arts, they are capable of inflicting lethal damage with a block of energy.";
        Skillset = "• Attacks fire up to 3 projectiles at once.\n" +
            "• When alerted by a Sentinel, ASPD is greatly increased."; ;
        TooltipsDescription = "Ranged unit, attacks fire 3 projectiles that deal physical damage. <color=yellow>Keeps distance</color> from the player unit. <color=yellow>If alerted early</color>, ASPD greatly increases.";
    
        base.WriteStats();
    }
}