using System.Collections;
using System.Linq;
using UnityEngine;

public class PlayerRanged : PlayerBase
{
    [SerializeField] private float ProjectileSpeed = 1250f;
    [SerializeField] private GameObject AttackRangeIndicator, Warning, SkillEffect;

    [SerializeField] private Transform SkillPosition;
    [SerializeField] private float SkillCooldown = 30f;
    [SerializeField] private float SkillDuration = 7f;
    [SerializeField] private float Skill_DamageMulitplier = 0.25f;
    [SerializeField] private float Skill_AtkInterval = 0.25f;

    private bool CanUseSkill = true, IsSkillActive = false;

    public override void FixedUpdate()
    {
        base.FixedUpdate();
        if (!IsAlive())
        {
            AttackRangeIndicator.SetActive(false);
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

        else Move();
    }

    IEnumerator SkillLockout()
    {
        CanUseSkill = false;
        StartCoroutine(stageManager.SkillCooldown(SkillCooldown));
        yield return new WaitForSeconds(SkillCooldown);
        CanUseSkill = true;
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

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}