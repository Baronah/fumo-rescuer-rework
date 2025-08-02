using System.Collections;
using UnityEngine;

public class Matterllurgist : EnemyBase
{
    [SerializeField] private Transform ProjectilePosition;

    public override void FlipAttackPosition()
    {
        base.FlipAttackPosition();
        ProjectilePosition.localPosition = new Vector3(
            -ProjectilePosition.localPosition.x,
            ProjectilePosition.localPosition.y,
            ProjectilePosition.localPosition.z
        );
    }

    public override void OnFirsttimePlayerSpot(bool viaAlert = false)
    {
        base.OnFirsttimePlayerSpot(viaAlert);
        if (viaAlert) ASPD += 100f;
    }

    public override IEnumerator Attack()
    {
        if (IsAttackLocked || attackPattern == AttackPattern.NONE) yield break;

        animator.SetTrigger("attack");
        StartCoroutine(LockoutMovementsOnAttack());

        var target = SearchForNearestEntityAroundSelf(typeof(PlayerBase));
        FaceToward(target.transform.position);

        yield return new WaitForSeconds(GetWindupTime());

        if (target)
        {
            short ProjectileBaseSpeed = 600, ProjectileAcceleration = 250;

            Vector3 targetPosition = target.transform.position;
            CreateProjectileAndShootToward(target, ProjectilePosition.position, targetPosition, ProjectileType, ProjectileBaseSpeed, ProjectileAcceleration * 2);
            CreateProjectileAndShootToward(target, ProjectilePosition.position, targetPosition + new Vector3(30, 0), ProjectileType, ProjectileBaseSpeed, ProjectileAcceleration);
            CreateProjectileAndShootToward(target, ProjectilePosition.position, targetPosition - new Vector3(30, 0), ProjectileType, ProjectileBaseSpeed, ProjectileAcceleration);
        }
        yield return null;
    }

    public override void InitializeComponents()
    {
        attackPattern = AttackPattern.RANGED;
        damageType = DamageType.PHYSICAL;

        base.InitializeComponents();
    }

    public override void WriteStats()
    {
        Description = "A matterllurgist is a master of manipulating matter at the atomic level, capable of altering the properties of objects and materials.";
        Skillset = "Matterllurgists can reshape materials, create barriers, and manipulate the environment to their advantage.";
        TooltipsDescription = "Ranged unit, attacks fire 3 projectiles that deal physical damage. <color=yellow>Keeps distance</color> from the player unit. <color=yellow>If alerted early</color>, ASPD greatly increases.";
    
        base.WriteStats();
    }
}