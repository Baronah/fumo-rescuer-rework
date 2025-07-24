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
    [SerializeField] private bool showTooltips = false;

    private Transform CheckpointTransform;
    protected List<Transform> Checkpoints;
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

        if (DetectionRange <= 0) DetectionRange = baRng;
        WriteStats();
    }

    public override void FixedUpdate()
    {
        if (!IsAlive()) return;
        if (DetectSymbol)
        {
            DetectSymbol.color = RecentlyScannedPlayer ? Color.red : Color.yellow;
            DetectSymbol.gameObject.SetActive(IsAlive() && SpottedPlayer && SpottedPlayer.IsAlive());
        }

        base.FixedUpdate();

        ScanPlayer();
        Move();
    }

    public virtual Vector3 GetCurrentDestination()
    {
        if (SpottedPlayer && SpottedPlayer.IsAlive())
        {
            switch (attackPattern)
            {
                case AttackPattern.MELEE:
                    return SpottedPlayer.transform.position;

                case AttackPattern.RANGED:
                    bool PlayerIsNearby = DetectPlayer(DangerRange_RatioOfAttackRange * aRng, false) != null;
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

        return Checkpoints[CurrentCheckpointIndex].transform.position;
    }

    public override void Move()
    {
        if (MoveCnt < 10)
        {
            MoveCnt++;
            return;
        }
        MoveCnt = 0;

        if (IsMovementLocked) return;

        Vector3 destination = GetCurrentDestination();
        Vector2 direction = destination - transform.position;

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
        if (SearchCnt < 10) return;

        SearchCnt = 0;
        RecentlyScannedPlayer =
            attackPattern == AttackPattern.MELEE && SpottedPlayer
            ?
            DetectPlayer(DangerRange_RatioOfAttackRange * aRng, true)
            :
            DetectPlayer();
        if (!RecentlyScannedPlayer) return;

        if (!SpottedPlayer)
        {
            SpottedPlayer = RecentlyScannedPlayer;
            IsGuarding = false;
            OnFirsttimePlayerSpot();
        }
        else
        {
            StartCoroutine(Attack());
        }
    }

    public virtual void OnFirsttimePlayerSpot(bool viaAlert = false)
    {
        PrevPosition = new(SpottedPlayer.transform.position.x * (SpriteInitialFlipX ? 1 : -1), SpottedPlayer.transform.position.y);
        IsGuarding = false;
        MovementLockout = 0;
        if (MovelockoutCoroutine != null) StopCoroutine(MovelockoutCoroutine);
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
            yield return new WaitForSeconds(aInt);

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
            yield return new WaitForSeconds(aInt);

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
        return (PlayerBase)SearchForNearestEntityAroundCertainPoint(typeof(PlayerBase), SpottedPlayer ? AttackPosition.position : transform.position, SpottedPlayer ? aRng : DetectionRange, catchInvisible);
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
        if (SpottedPlayer && SpottedPlayer.IsAlive()) return;

        StopMovement();
        MovelockoutCoroutine = StartCoroutine(StartMovementLockout(WaitTimes[CurrentCheckpointIndex]));
        CurrentCheckpointIndex++;
        if (CurrentCheckpointIndex >= Checkpoints.Count) CurrentCheckpointIndex = 0;
    }

    public override void OnDeath()
    {
        base.OnDeath();
        DetectSymbol.gameObject.SetActive(false);
    }

    public virtual void WriteStats()
    {
        if (showTooltips && TooltipsPrefab) StartCoroutine(ShowTooltips());
    }

    IEnumerator ShowTooltips()
    {
        for (int i = 0; i < TooltipsPriority; ++i) yield return new WaitForEndOfFrame();
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
        Gizmos.DrawWireSphere(AttackPosition ? AttackPosition.position : transform.position, aRng);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(AttackPosition ? AttackPosition.position : transform.position, aRng * DangerRange_RatioOfAttackRange);
    }
}