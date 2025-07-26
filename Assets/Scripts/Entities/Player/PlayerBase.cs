using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerBase : EntityBase
{
    public Sprite AttackSprite, SkillSprite, SpecialSprite;
    protected StageManager stageManager;

    private void Update()
    {
        GetControlInputs();
    }

    public override void InitializeComponents()
    {
        base.InitializeComponents();
        stageManager = FindObjectOfType<StageManager>();
        stageManager.Register(this);

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
        animator.SetTrigger("attack");

        yield return new WaitForSeconds(attackSpeed);

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
        StartCoroutine(stageManager.AttackCooldown(attackInterval));
        return base.LockoutMovementsOnAttack();
    }

    public override void OnDeath()
    {
        base.OnDeath();
        stageManager.OnPlayerDeath();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(AttackPosition ? AttackPosition.position : transform.position, attackRange);
    }
}