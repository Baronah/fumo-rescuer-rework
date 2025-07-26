using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class EnemyBase : EntityBase
{
    [SerializeField] private GameObject TooltipsPrefab;
    [SerializeField] private int TooltipsPriority = 0;
    [SerializeField] private float TooltipsHoldtime = 6f;
    protected string Description = "Enemy lore or description";
    protected string Skillset = "Enemy skillset";
    protected string TooltipsDescription = "the thing to appear on tooltips";
    [SerializeField] protected float DetectionRange = -1f;
    [SerializeField] protected float DangerRange_RatioOfAttackRange = 0.75f;
    [SerializeField] protected float MinimumDistanceFromPlayer = 20f;
    [SerializeField] private bool showTooltips = false;

    private Transform CheckpointTransform;
    protected List<Transform> Checkpoints;

    [SerializeField] private float OverridePositionCheckRadius = 25f;
    private Vector2 OverridePosition;
    [SerializeField] private float MoveToOverridePositionSpeedMultiplier = 1.5f, MoveToOverridePositionSpeedMultiplierJump = 0.35f;
    private short MoveToOverridePositionJumpCnt = 0;
    private bool MoveToOverridePosition = false;

    [SerializeField] private List<float> WaitTimes;
    [SerializeField] private float InitWaittime = 0f;
    protected PlayerBase SpottedPlayer, RecentlyScannedPlayer;
    protected bool IsGuarding = true;
    private short CurrentCheckpointIndex = 0;

    private short SearchCnt = 0, MoveCnt = 0;
    Coroutine MovelockoutCoroutine = null;
    TMP_Text DetectSymbol;

    public override void InitializeComponents()
    {
        base.InitializeComponents();

        DetectSymbol = transform.Find("Spotted").GetComponentInChildren<TMP_Text>();

        CheckpointTransform = transform.Find("Checkpoints");
        Checkpoints = CheckpointTransform.GetComponentsInChildren<Transform>().ToList();
        if (Checkpoints.Count > 0)
        {
            WaitTimes.Insert(0, InitWaittime);
        }

        if (DetectionRange <= 0) DetectionRange = b_attackRange;
        WriteStats();
    }

    public override void FixedUpdate()
    {
        if (!IsAlive()) return;
        if (DetectSymbol)
        {
            DetectSymbol.color = RecentlyScannedPlayer ? Color.red : Color.yellow;

            bool isPlayerSpotted = SpottedPlayer && SpottedPlayer.IsAlive();

            DetectSymbol.text = isPlayerSpotted ? "!" : "?";
            DetectSymbol.gameObject.SetActive(IsAlive() && (isPlayerSpotted || MoveToOverridePosition));
        }

        base.FixedUpdate();

        ScanPlayer();
        Move();
    }

    public override Vector2 CalculateMovement(Vector2 normalizedMovementVector, float speed)
    {
        var result = base.CalculateMovement(normalizedMovementVector, speed);
        return MoveToOverridePosition ? result * (MoveToOverridePositionSpeedMultiplier + Mathf.Min(MoveToOverridePositionSpeedMultiplierJump * MoveToOverridePositionJumpCnt, 0.5f)) : result;
    }

    public virtual Vector3 GetCurrentDestination()
    {
        if (SpottedPlayer && SpottedPlayer.IsAlive())
        {
            switch (attackPattern)
            {
                case AttackPattern.MELEE:
                    if (Vector3.Distance(AttackPosition.position, SpottedPlayer.transform.position) <= Mathf.Min(attackRange * DangerRange_RatioOfAttackRange / 2, MinimumDistanceFromPlayer))
                    {
                        return AttackPosition.position;
                    }
                    else return SpottedPlayer.transform.position;

                case AttackPattern.RANGED:
                    bool PlayerIsNearby = DetectPlayer(DangerRange_RatioOfAttackRange * attackRange, false) != null;
                    Vector3 dirToPlayer = (SpottedPlayer.transform.position - transform.position).normalized;

                    if (PlayerIsNearby)
                        return transform.position - dirToPlayer;
                    else if (RecentlyScannedPlayer)
                        return transform.position;
                    else
                        return SpottedPlayer.transform.position;

                default:
                    return transform.position;
            }
        }

        return MoveToOverridePosition ? OverridePosition : Checkpoints[CurrentCheckpointIndex].transform.position;
    }

    public override void Move()
    {
        if (MoveCnt < 15)
        {
            MoveCnt++;
            return;
        }
        MoveCnt = 0;

        if (!SpottedPlayer && MoveToOverridePosition && Vector3.Distance(AttackPosition.position, OverridePosition) <= OverridePositionCheckRadius)
        {
            MoveToOverridePosition = false;
            StartCoroutine(StartMovementLockout(UnityEngine.Random.Range(2f, 6f)));
        }

        if (IsMovementLocked) return;

        Vector3 destination = GetCurrentDestination();
        Vector2 direction = destination - AttackPosition.position;

        if (Mathf.Abs(direction.x) <= 0.3f && Mathf.Abs(direction.y) <= 0.3f)
        {
            rb2d.velocity = Vector2.zero;
            animator.SetFloat("move", 0);
            return;
        }

        var movement = direction.normalized;

        rb2d.velocity = CalculateMovement(movement);

        animator.SetFloat("move", 1);
    }

    public void ScanPlayer()
    {
        SearchCnt++;
        if (SearchCnt < 15) return;

        SearchCnt = 0;

        if (SpottedPlayer && SpottedPlayer.IsAlive())
        {
            var enemies = SearchForEntitiesAroundSelf(DetectionRange, typeof(EnemyBase), true);
            foreach (var e in enemies)
            {
                EnemyBase enemy = e as EnemyBase;

                if (!enemy || !enemy.IsAlive() || enemy.SpottedPlayer) continue;
                    enemy.SpottedPlayer = SpottedPlayer;
                    enemy.OnFirsttimePlayerSpot();
            }
        }

        RecentlyScannedPlayer =
            attackPattern == AttackPattern.MELEE && SpottedPlayer
            ?
            DetectPlayer(DangerRange_RatioOfAttackRange * attackRange, true)
            :
            DetectPlayer();
        if (!RecentlyScannedPlayer || !RecentlyScannedPlayer.IsAlive()) return;

        if (!SpottedPlayer)
        {
            SpottedPlayer = RecentlyScannedPlayer;
            IsGuarding = false;
            OnFirsttimePlayerSpot();
        }
        else
        {
            AttackCoroutine = StartCoroutine(Attack());
        }
    }

    public virtual void OnFirsttimePlayerSpot(bool viaAlert = false)
    {
        MoveToOverridePosition = false;
        MoveToOverridePositionJumpCnt = 0;
        FaceToward(SpottedPlayer.transform.position);
        IsGuarding = false;
        MovementLockout = 0;
        DetectionRange = Mathf.Max(DetectionRange * 0.5f, 200);

        if (attackPattern == AttackPattern.NONE)
        {
            List<EnemyBase> enemies = FindObjectsOfType<EnemyBase>().Where(e => e && e.IsAlive() && !e.SpottedPlayer).ToList();
            foreach (var item in enemies)
            {
                item.SpottedPlayer = SpottedPlayer;
                item.OnFirsttimePlayerSpot(true);
            }
        }
    }

    public override IEnumerator Attack()
    {
        if (IsAttackLocked || attackPattern == AttackPattern.NONE) yield break;

        StartCoroutine(base.Attack());
        animator.SetTrigger("attack");

        if (attackPattern == AttackPattern.RANGED)
        {
            var target = SearchForNearestEntityAroundSelf(typeof(PlayerBase));
            FaceToward(target.transform.position);

            yield return new WaitForSeconds(attackSpeed);

            if (target)
            {
                if (ProjectilePrefab) 
                { 
                    CreateProjectileAndShootToward(target, ProjectileType);
                } 
                else DealDamage(target, atk);
            }
        }
        else if (attackPattern == AttackPattern.MELEE)
        {
            yield return new WaitForSeconds(attackSpeed);

            var target = SearchForNearestEntityAroundSelf(typeof(PlayerBase));
            if (target) DealDamage(target, atk);
        }
        yield return null;
    }

    public PlayerBase DetectPlayer(float radius, bool catchInvisible = false)
    {
        return (PlayerBase)SearchForNearestEntityAroundCertainPoint(typeof(PlayerBase), SpottedPlayer ? AttackPosition.position : transform.position, radius, catchInvisible);
    }

    public PlayerBase DetectPlayer(bool catchInvisible = false)
    {
        return (PlayerBase)SearchForNearestEntityAroundCertainPoint(typeof(PlayerBase), SpottedPlayer ? AttackPosition.position : transform.position, SpottedPlayer ? attackRange : DetectionRange, catchInvisible);
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
        if (MoveToOverridePosition || (SpottedPlayer && SpottedPlayer.IsAlive())) return;

        StopMovement();
        MovelockoutCoroutine = StartCoroutine(StartMovementLockout(WaitTimes[CurrentCheckpointIndex]));
        CurrentCheckpointIndex++;
        if (CurrentCheckpointIndex >= Checkpoints.Count) CurrentCheckpointIndex = 0;
    }

    public override void TakeDamage(DamageInstance damage, EntityBase source)
    {
        OnAttackReceive(source);

        base.TakeDamage(damage, source);
    }

    public override void OnAttackReceive(EntityBase source)
    {
        if (source as PlayerBase && !SpottedPlayer)
        {
            MoveToOverridePositionJumpCnt++;
            FaceToward(source.transform.position);
            MovementLockout = 0;
            if (MovelockoutCoroutine != null) StopCoroutine(MovelockoutCoroutine);
            MoveToOverridePosition = true;
            OverridePosition = source.transform.position;
        }
    }

    public override void OnDeath()
    {
        base.OnDeath();
        DetectSymbol.gameObject.SetActive(false);
    }

    public void ChangeAggro(PlayerBase player)
    {
        if (!SpottedPlayer) return;
        SpottedPlayer = player;
    }

    public virtual void WriteStats()
    {
        if (showTooltips && TooltipsPrefab) StartCoroutine(ShowTooltips());
    }

    IEnumerator ShowTooltips()
    {
        for (int i = 0; i < TooltipsPriority; ++i) yield return null;
        Instantiate(TooltipsPrefab, Vector3.zero, Quaternion.identity, transform);
    }

    public TooltipsData GetTooltipsData()
    {
        return new TooltipsData
        {
            Icon = this.Icon,
            Name = this.Name,
            Description = this.TooltipsDescription,
            HoldTime = this.TooltipsHoldtime
        };
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, DetectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(AttackPosition ? AttackPosition.position : transform.position, attackRange);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(AttackPosition ? AttackPosition.position : transform.position, attackRange * DangerRange_RatioOfAttackRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(OverridePosition, OverridePositionCheckRadius);
    }
}