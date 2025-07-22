using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EnemyBase : EntityBase
{
    [SerializeField] private float DetectionRange = -1f;
    
    private Transform CheckpointTransform;
    private List<Transform> Checkpoints;
    [SerializeField] private List<float> WaitTimes;
    [SerializeField] private float InitWaittime = 0f;
    private PlayerBase SpottedPlayer;
    private bool IsGuarding = true;
    private short CurrentCheckpointIndex = 0;

    private short SearchCnt = 0;
    Coroutine MovelockoutCoroutine = null;
    GameObject DetectSymbol;

    public override void InitializeComponents()
    {
        base.InitializeComponents();

        DetectSymbol = transform.Find("Spotted").gameObject;

        CheckpointTransform = transform.Find("Checkpoints");
        Checkpoints = CheckpointTransform.GetComponentsInChildren<Transform>().ToList();
        if (Checkpoints.Count > 0)
        {
            WaitTimes.Insert(0, InitWaittime);
        }

        if (DetectionRange <= 0) DetectionRange = baRng;
    }

    public override void FixedUpdate()
    {
        if (!IsAlive()) return;
        if (DetectSymbol) DetectSymbol.SetActive(IsAlive() && SpottedPlayer && SpottedPlayer.IsAlive());

        base.FixedUpdate();

        Move();
        
        if (!SpottedPlayer) ScanPlayer();
        else
        {
            var player = DetectPlayer();
            if (player) StartCoroutine(Attack());
        }
    }

    public virtual Vector3 GetCurrentDestination()
    {
        if (SpottedPlayer && SpottedPlayer.IsAlive()) return SpottedPlayer.transform.position;

        return Checkpoints[CurrentCheckpointIndex].transform.position;
    }

    public override void Move()
    {
        if (IsMovementLocked) return;

        Vector2 direction = (GetCurrentDestination() - transform.position).normalized;

        var movement = direction.normalized;

        rb2d.velocity = CalculateMovement(movement);

        animator.SetFloat("move", Mathf.Abs(direction.x) + Mathf.Abs(direction.y));
    }

    public void ScanPlayer()
    {
        SearchCnt++;
        if (SearchCnt < 10) return;

        SearchCnt = 0;
        var player = DetectPlayer();
        if (!player) return;

        if (!SpottedPlayer)
        {
            SpottedPlayer = player;
            IsGuarding = false;
            OnFirsttimePlayerSpot();
        }
        else
        {
            StartCoroutine(Attack());
        }
    }

    public virtual void OnFirsttimePlayerSpot()
    {
        IsGuarding = false;
        MovementLockout = 0;
        if (MovelockoutCoroutine != null) StopCoroutine(MovelockoutCoroutine);
    }

    public override IEnumerator Attack()
    {
        if (IsAttackLocked) yield break;

        StartCoroutine(base.Attack());
        animator.SetTrigger("attack");

        yield return new WaitForSeconds(aInt);

        var target = SearchForNearestEntityAroundSelf(typeof(PlayerBase));
        if (target) DealDamage(target, atk);

        yield return null;
    }

    public PlayerBase DetectPlayer(bool catchInvisible = false)
    {
        return (PlayerBase) SearchForNearestEntityAroundCertainPoint(typeof(PlayerBase), transform.position, SpottedPlayer ? aRng : DetectionRange, catchInvisible);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Checkpoint") && collision.gameObject == Checkpoints[CurrentCheckpointIndex].gameObject)
        {
            OnCheckpointReach();
        }
    }

    protected virtual void OnCheckpointReach()
    {
        if (SpottedPlayer) return;

        StopMovement();
        MovelockoutCoroutine = StartCoroutine(StartMovementLockout(WaitTimes[CurrentCheckpointIndex]));
        CurrentCheckpointIndex++;
        if (CurrentCheckpointIndex >= Checkpoints.Count) CurrentCheckpointIndex = 0;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, DetectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(AttackPosition ? AttackPosition.position : transform.position, aRng);
    }
}