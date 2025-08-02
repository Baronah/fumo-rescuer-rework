using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerBase : EntityBase
{
    public Sprite AttackSprite, SkillSprite, SpecialSprite;
    public string AttackDes, SkillName, SkillDes, SpecialName, SpecialDes;
    protected PlayerManager playerManager;
    protected StageManager StageManager;

    private void Update()
    {
        GetControlInputs();
    }

    public override void InitializeComponents()
    {
        base.InitializeComponents();
        playerManager = FindObjectOfType<PlayerManager>();
        playerManager.Register(this);

        StageManager = FindObjectOfType<StageManager>();

        StartCoroutine(InvulnerableOnSpawn());
    }

    IEnumerator InvulnerableOnSpawn()
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(1f);
        isInvulnerable = false;
    }

    protected virtual void GetControlInputs()
    {
        if (!IsAlive()) return;

        if (Input.GetKeyDown(KeyCode.Z))
        {
            StartCoroutine(Attack());
        }
        else Move();
    }

    public override void Move()
    {
        if (IsMovementLocked) return;

        float moveHorizontal = Input.GetAxis("Horizontal");
        float moveVertical = Input.GetAxis("Vertical");

        var movementInputs = new Vector2(moveHorizontal, moveVertical).normalized;

        rb2d.velocity = CalculateMovement(movementInputs);

        animator.SetFloat("move", Mathf.Abs(moveHorizontal) + Mathf.Abs(moveVertical));
    }

    public override IEnumerator Attack()
    {
        if (!IsAlive() || IsAttackLocked) yield break;
        
        StartCoroutine(base.Attack());

        yield return new WaitForSeconds(GetWindupTime());

        var targets = SearchForEntitiesAroundCertainPoint(typeof(EnemyBase), AttackPosition.position, attackRange);
        foreach (var target in targets)
        {
            if (!target || !target.IsAlive()) continue; 
            DealDamage(target, atk);
        }

        yield return null;
    }

    public override IEnumerator LockoutMovementsOnAttack()
    {
        StartCoroutine(playerManager.AttackCooldown(Mathf.Max(
                attackWindupTime,
                attackInterval,
                animator.GetCurrentAnimatorClipInfo(0).Length / preferredAttackAnimationSpeed / animator.GetFloat("a_speed_value"))
            ));
        return base.LockoutMovementsOnAttack();
    }

    public virtual PlayerTooltipsInfo GetPlayerTooltipsInfo()
    {
        return new PlayerTooltipsInfo
        {
            Icon = Icon,
            AttackSprite = AttackSprite,
            SkillSprite = SkillSprite,
            SpecialSprite = SpecialSprite,
            attackRange = attackRange,
            attackSpeed = attackWindupTime,
            attackInterval = attackInterval,
            atk = atk,
            bAtk = bAtk,
            bDef= bDef,
            def = def,
            bRes = bRes,
            res = res,
            attackPattern = attackPattern,
            damageType = damageType,
            mHealth = mHealth,
            health = health,
            moveSpeed = moveSpeed,
            SkillName = SkillName,
            SkillText = SkillDes,
            SpecialName = SpecialName,
            SpecialText = SpecialDes,
            AttackText = "Perform an attack that deals",
        };
    }

    public override void OnDeath()
    {
        base.OnDeath();
        playerManager.OnPlayerDeath();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision || !collision.gameObject) return;

        if (collision.gameObject.CompareTag("Fumo"))
        {
            StageManager.OnPlayerFumoPickup(this);
            Destroy(collision.gameObject);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(AttackPosition ? AttackPosition.position : transform.position, attackRange);
    }
}