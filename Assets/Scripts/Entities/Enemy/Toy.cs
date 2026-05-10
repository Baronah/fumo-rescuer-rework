using System.Collections;
using UnityEngine;

public class Toy : EnemyBase
{
    [SerializeField] AnimationClip StartAni;

    private bool IsStarted = false;
    public bool WakenUp = false;
    public bool IsActive => WakenUp && IsStarted && IsAlive();

    public override void InitializeComponents()
    {
        base.InitializeComponents();
        defaultInsignificance = IsInsignificant;
        if (!ViewOnlyMode) StartCoroutine(OnStartCoroutine());
    }

    bool defaultInsignificance;

    IEnumerator OnStartCoroutine()
    {
        yield return new WaitForSeconds(StartAni.length);
        IsStarted = true;
        if (!environmentalTilesStandingOn.Contains(StageManager.EnvironmentType.DARK_ZONE)) OnShroudedZoneExit();
        else OnShroudedZoneEnter();
    }

    public override void EnemyFixedBehaviors()
    {
        if (!IsActive) return;
        base.EnemyFixedBehaviors();
    }

    public override void Move()
    {
        if (!IsActive) return;
        base.Move();
    }

    public override IEnumerator Attack()
    {
        if (!IsActive) yield break;
        yield return StartCoroutine(base.Attack());
    }

    public override void TakeDamage(DamageInstance damage, EntityBase source, ProjectileScript projectileInfo = null, bool IgnoreInvulnerability = false, bool CalculateDamage = false)
    {
        if (!IsStarted || (!IsActive && !IgnoreInvulnerability)) return;
        base.TakeDamage(damage, source, projectileInfo, IgnoreInvulnerability, CalculateDamage);
    }

    public override IEnumerator OnAttackComplete()
    {
        if (!CanAttack || !SpottedPlayer) yield break;

        float ProjectileAcceleration = 500;

        Vector2 playerDir = (SpottedPlayer.transform.position - AttackPosition.position).normalized;
        Vector3 sourcePosition = AttackPosition.position;

        int projCount = 4;
        int jump = 180 / projCount;
        for (int i = -45; i <= 45; i += jump)
        {
            float angle = i;
            Vector2 rotatedDir = Quaternion.Euler(0, 0, angle) * playerDir;

            Vector3 targetPosition = sourcePosition + (Vector3)rotatedDir;

            CreateProjectileAndShootToward(
                ProjectilePrefab,
                new DamageInstance(0, atk, 0),
                sourcePosition,
                targetPosition,
                projectileType: ProjectileScript.ProjectileType.CATCH_FIRST_TARGET_OF_TYPE,
                travelSpeed: ProjectileSpeed,
                acceleration: ProjectileAcceleration,
                lifeSpan: 8f,
                targetType: typeof(PlayerBase));
        }
    }

    protected override Vector2 GetPathfindingTarget()
    {
        if (SpottedPlayer
            && !SpottedPlayer.environmentalTilesStandingOn.Contains(StageManager.EnvironmentType.DARK_ZONE))
        {
            return StopVector;
        }
        return base.GetPathfindingTarget();
    }

    public void OnShroudedZoneExit()
    {
        WakenUp = false;
        IsInsignificant = true;
        CanBeHitByProjectiles = false;
        isInvisible = true;

        healthBar.gameObject.SetActive(false);

        CancelAttack();
        StopMovement();
        animator.SetBool("sleep", true);
    }

    public void OnShroudedZoneEnter()
    {
        StartCoroutine(WakeUp());
    }

    public override void OnAttackReceive(EntityBase source)
    {
        if (!IsActive) return;
        base.OnAttackReceive(source);
    }

    IEnumerator WakeUp()
    {
        animator.SetBool("sleep", false);

        float c = 0, d = 0.5f;
        while (c < d)
        {
            if (!environmentalTilesStandingOn.Contains(StageManager.EnvironmentType.DARK_ZONE) || !IsAlive()) yield break;

            c += Time.deltaTime;
            yield return null;
        }

        WakenUp = true;
        IsInsignificant = defaultInsignificance;
        CanBeHitByProjectiles = true;
        isInvisible = false;

        healthBar.gameObject.SetActive(true);
    }

    public override void WriteStats()
    {
        Description = "Automata built for the sake of art, amusement, and simulation have been described since antiquity, " +
            "these kind of wind-up toys were very popular. It gets more lively the harder you wind it up, " +
            "or when it thinks no one else is watching...";
        Skillset =
            "• Normally does not act, can not be attacked and does not count toward battle progress.\n" +
            "• Comes into life when inside shrouded areas, performs long-ranged magical attack that lauches several projectiles.\n" +
            "• Will not chase the player if they are outside shrouded zones.";
        TooltipsDescription =
            "<color=yellow>Comes into life</color> while inside shrouded areas, performs long-ranged magical attacks.";

        base.WriteStats();
    }
}