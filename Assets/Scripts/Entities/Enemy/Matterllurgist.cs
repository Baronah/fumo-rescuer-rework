using System.Collections;
using UnityEngine;

public class Matterllurgist : EnemyBase
{
    private bool enhanced = false;

    public override void OnFirsttimePlayerSpot(bool viaAlert = false)
    {
        base.OnFirsttimePlayerSpot(viaAlert);
        enhanced = viaAlert;
    }

    public override IEnumerator Attack()
    {
        if (IsAttackLocked || attackPattern == AttackPattern.NONE) yield break;

        StartCoroutine(base.Attack());
        animator.SetTrigger("attack");

        var target = SearchForNearestEntityAroundSelf(typeof(PlayerBase));
        yield return new WaitForSeconds(aInt);

        if (target)
        {
            Vector3 targetPosition = target.transform.position;
            CreateProjectileAndShootToward(target, ProjectileType);
            CreateProjectileAndShootToward(target, targetPosition + new Vector3(30, 0), ProjectileType);
            CreateProjectileAndShootToward(target, targetPosition - new Vector3(30, 0), ProjectileType);
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
        TooltipsDescription = "Ranged unit, attacks fire 3 projectiles that deal physical damage. <color=yellow>Keeps distance</color> from the player unit.";
    
        base.WriteStats();
    }
}