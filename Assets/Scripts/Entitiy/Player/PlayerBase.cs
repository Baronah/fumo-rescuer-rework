using System.Collections;
using UnityEngine;

public class PlayerBase : EntityBase
{
    public override void FixedUpdate()
    {
        base.FixedUpdate();
        GetControlInputs();
    }

    protected void GetControlInputs()
    {
        if (Input.GetKeyDown(KeyCode.Z))
        {
            StartCoroutine(Attack());
        }
        else
        {
            Move();
        }
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
        if (IsAttackLocked) yield break;

        StartCoroutine(base.Attack());
        animator.SetTrigger("attack");

        yield return new WaitForSeconds(aInt);

        var target = SearchForNearestEntityAroundSelf(typeof(EnemyBase));
        if (target) DealDamage(target, atk);

        yield return null;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(AttackPosition ? AttackPosition.position : transform.position, aRng);
    }
}