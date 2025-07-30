using System.Collections;
using System.Linq;
using UnityEngine;

public class PlayerRanged : PlayerBase
{
    [SerializeField] private float ProjectileSpeed = 1250f;
    [SerializeField] private GameObject AttackRangeIndicator, Warning, SkillEffect, FreezeEffect;

    [SerializeField] private Transform SkillPosition;
    [SerializeField] private float SkillCooldown = 30f;
    [SerializeField] private float SkillDuration = 7f;
    [SerializeField] private float Skill_DamageMulitplier = 0.25f;
    [SerializeField] private float Skill_AtkInterval = 0.25f;

    [SerializeField] private float FreezeRange = 800f;
    [SerializeField] private float FreezeDurationMin = 1f, FreeDurationMax = 4f, MinDistanceForFreezeDuration = 150f;
    [SerializeField] private float FreezeCooldown = 11.5f;
    [SerializeField] private float FreezeCastDuration = 0.25f;

    private bool CanUseSkill = true, IsSkillActive = false, CanUseFreeze = true;
    private RectTransform AttackRangeIndicatorRect;

    public override void Start()
    {
        base.Start();
        AttackRangeIndicatorRect = AttackRangeIndicator.GetComponent<RectTransform>();
    }

    public override void FixedUpdate()
    {
        base.FixedUpdate();
        if (!IsAlive())
        {
            AttackRangeIndicator.SetActive(false);
        }

        if (AttackRangeIndicatorRect)
        {
            AttackRangeIndicatorRect.sizeDelta = new Vector2(
                attackRange * 2,
                attackRange * 2
            );
        }

        SkillEffect.SetActive(IsSkillActive && IsAlive());
    }

    public override void FlipAttackPosition()
    {
        base.FlipAttackPosition();
        SkillPosition.localPosition = new Vector3(
            -SkillPosition.localPosition.x,
            SkillPosition.localPosition.y,
            SkillPosition.localPosition.z
        );

        SkillEffect.transform.localPosition = new Vector3(
            -SkillEffect.transform.localPosition.x,
            SkillEffect.transform.localPosition.y,
            SkillEffect.transform.localPosition.z
        );
    }

    protected override void GetControlInputs()
    {
        if (!IsAlive() || IsSkillActive) return;

        if (Input.GetKeyDown(stageManager.AttackKey))
        {
            AttackCoroutine = StartCoroutine(Attack());
        }
        else if (Input.GetKeyDown(stageManager.SkillKey) && CanUseSkill)
        {
            StartCoroutine(CastSkill());
        }
        else if (Input.GetKeyDown(stageManager.SpecialKey) && CanUseFreeze)
        {
            StartCoroutine(CastFreeze());
        }
        else 
            Move();
    }

    IEnumerator SkillLockout()
    {
        CanUseSkill = false;
        StartCoroutine(stageManager.SkillCooldown(SkillCooldown));
        yield return new WaitForSeconds(SkillCooldown);
        CanUseSkill = true;
    }

    IEnumerator FreezeLockout()
    {
        CanUseFreeze = false;
        StartCoroutine(stageManager.SpecialCooldown(FreezeCooldown));
        yield return new WaitForSeconds(FreezeCooldown);
        CanUseFreeze = true;
    }

    public override IEnumerator Attack()
    {
        if (!IsAlive() || IsAttackLocked) yield break;

        AttackRangeIndicator.SetActive(true);
        var target = SearchForNearestEntityAroundCertainPoint(typeof(EnemyBase), transform.position, attackRange);
        if (!target)
        {
            Warning.SetActive(true);
            yield return new WaitForSeconds(1f);
            Warning.SetActive(false);
            AttackRangeIndicator.SetActive(false);
            yield break;
        }

        LockoutMovementOnAttackCoroutine = StartCoroutine(LockoutMovementsOnAttack());
        
        animator.SetTrigger("attack");

        yield return new WaitForSeconds(attackSpeed);

        if (target)
        {
            CreateProjectileAndShootToward(target, ProjectileType, ProjectileSpeed);
        }

        AttackRangeIndicator.SetActive(false);
        yield return null;
    }

    public IEnumerator CastFreeze()
    {
        if (!IsAlive() || !CanUseFreeze || IsFrozen || IsStunned) yield break;

        StartCoroutine(FreezeLockout());
        StartCoroutine(StartMovementLockout(FreezeCastDuration));
        StartCoroutine(StartAttackLockout(FreezeCastDuration));
        
        animator.SetTrigger("skill");
        IsSkillActive = true;

        Instantiate(FreezeEffect, SkillPosition.position, Quaternion.identity);
        yield return new WaitForSeconds(FreezeCastDuration - Time.fixedDeltaTime);

        var hitEnemies = SearchForEntitiesAroundCertainPoint(typeof(EnemyBase), SkillPosition.position, FreezeRange, true);
        foreach (EntityBase e in hitEnemies)
        {
            EnemyBase enemy = e as EnemyBase;
            float distance = Vector3.Distance(SkillPosition.position, enemy.transform.position);
            float freezeDuration = distance >= FreezeRange * 0.8f
                ?
                FreezeDurationMin
                : 
                Mathf.Lerp(FreezeDurationMin, FreeDurationMax, MinDistanceForFreezeDuration * 1.0f / distance);
            ApplyFreeze(enemy, freezeDuration);
        }

        animator.SetTrigger("skill_end");
        IsSkillActive = false;
        yield return null;
    }

    public IEnumerator CastSkill()
    {
        if (!IsAlive()) yield break;

        StartCoroutine(StartAttackLockout(SkillDuration));
        StartCoroutine(StartMovementLockout(SkillDuration));
        StartCoroutine(SkillLockout());

        animator.SetTrigger("skill");
        IsSkillActive = true;
        float count = 0;
        float angleOffset = 0; 
        while (count < SkillDuration)
        {
            Vector3 sourcePosition = SkillPosition.position;

            for (int i = 0; i < 360; i += 30) 
            {
                float currentAngle = i + angleOffset;

                float angleInRadians = currentAngle * Mathf.Deg2Rad;

                float circleRadius = 30f + (count * 5f); 
                Vector3 targetPosition = new Vector3(
                    sourcePosition.x + Mathf.Cos(angleInRadians) * circleRadius,
                    sourcePosition.y + Mathf.Sin(angleInRadians) * circleRadius,
                    sourcePosition.z
                );

                CreateProjectileAndShootToward(
                    ProjectilePrefab,
                    new DamageInstance(0, (int)(atk * Skill_DamageMulitplier), 0),
                    targetType: typeof(EnemyBase),
                    sourcePosition,
                    targetPosition,
                    projectileType: ProjectileScript.ProjectileType.CATCH_FIRST_TARGET_OF_TYPE,
                    travelSpeed: ProjectileSpeed * 0.25f,
                    acceleration: ProjectileSpeed * 0.25f,
                    lifeSpan: 1.5f);
            }

            angleOffset += 6;
            yield return new WaitForSeconds(Skill_AtkInterval);
            count += Skill_AtkInterval;
        }

        animator.SetTrigger("skill_end");
        IsSkillActive = false;
        yield return null;
    }

    public override PlayerTooltipsInfo GetPlayerTooltipsInfo()
    {
        var info = base.GetPlayerTooltipsInfo();

        info.AttackText = $"Lauches a projectile toward the nearest enemy within range, " +
            $"dealing {atk} {damageType.ToString().ToLower()} damage.";

        info.SkillName = "Aupiciousness";
        info.SkillText =
            $"In the next {SkillDuration} seconds: becomes unable to move and attack, continuously unleashes a wave of projectiles " +
            $"spreading in all direction around self. Each projectile hits the first enemy it comes into contact with, dealing {Skill_DamageMulitplier * 100}% ATK damage each. " +
            $"{SkillCooldown}s cooldown.";

        info.SpecialName = "Zeropoint Burst";
        info.SpecialText =
            $"After a short delay, inflicts freeze to all enemies within attack range for {FreezeDurationMin} - {FreeDurationMax} seconds based on distance. " +
            $"{FreezeCooldown}s cooldown.";

        return info;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}