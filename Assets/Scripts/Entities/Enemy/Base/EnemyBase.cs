using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Serialization;
using static LevelDifficultyModifier;

public class EnemyBase : EntityBase
{
    public override Type GetGenericType() => typeof(EnemyBase);

    public enum EnemyCode
    {
        HOUND,
        MATTERLLURGIST,
        SENTINEL,
        ZEALOT,
        HEIR,
        BLOODBOIL_KNIGHT,
        ARCHER,
        WETWORK,
        DUMMY,
        ORIGINIUM_SPIDER,
        ORIGINIUTANT,
        ORIGINIUM_SPIDER_ALPHA,
        SUDARAM,
        SHROUDED_ASSASSIN,
        HIBERNATOR_KNIGHT,
        GLOOMPINCER,
        CANDLE_KNIGHT,
        CANDLE,
        TOY,
        SAINT_STATUE,
    }

    public EnemyCode enemyCode;

    public enum Size
    {
        SMALL,
        MEDIUM,
        LARGE,
        HUGE,
    }

    public Size size = Size.MEDIUM;

    public bool CanDetectPlayer = true;

    private bool SpotPlayerUponSpawn = false;
    [SerializeField] private GameObject TooltipsPrefab;
    private int TooltipsPriority = 0;
    [SerializeField] private float TooltipsHoldtime = 6f;
    public string Description = "Enemy lore or description";
    public string Skillset = "Enemy skillset";
    protected string TooltipsDescription = "the thing to appear on tooltips";

    [FormerlySerializedAs("DetectionRange")] public float detectionRange = -1f;
    [HideInInspector] public float b_detectionRange;

    [SerializeField] public float DangerRange_RatioOfAttackRange = 0.75f;
    [SerializeField] protected float MinimumDistanceFromPlayer = 20f;
    bool showTooltips = false;

    /// <summary>
    /// The agent is used *only* for path-sampling.
    /// It is set to updatePosition = false and updateRotation = false so
    /// the existing Rigidbody2D / CalculateMovement pipeline keeps working.
    /// </summary>
    private NavMeshAgent navAgent;

    // Whether the agent currently has a valid, incomplete path to follow.
    private bool isUsingPathfinding()
    {
        return navAgent && navAgent.hasPath && navAgent.pathStatus != NavMeshPathStatus.PathInvalid;
    }

    private bool hasClearSightToTarget()
    {
        Vector3 destination = GetUniversalDestination(), 
                selfPos = FeetPosition.position;

        if (destination == StopVector) return true;

        Vector2 directionToDestination = (destination - selfPos).normalized;
        RaycastHit2D hit = Physics2D.Raycast(
            selfPos, 
            directionToDestination, 
            Vector2.Distance(selfPos, destination) - 30f, 
            obstacleLayer
            );

        bool clearSighted = hit.collider == null || colliders.Contains(hit.collider);

        return clearSighted;
    }

    private bool hasClearSightToTargetOnThisMoveOppoturnity = false;

    // Stuck detection
    private float stuckTimer = 0f;
    private Vector2 lastPosition = Vector2.zero;
    private readonly float stuckThreshold = 0.9f;
    private readonly float stuckMovementThreshold = 15f;
    // -------------------------------------------------------------------------

    [Header("Checkpoints System")]
    protected List<Transform> Checkpoints = new();
    [SerializeField] private List<float> WaitTimes = new();

    private readonly float OverridePositionCheckRadius = 75f;
    private Vector3 OverridePosition;
    [SerializeField] public float MoveToOverridePositionSpeedMultiplier = 1.5f, MoveToOverridePositionSpeedMultiplierJump = 0.35f;
    private short MoveToOverridePositionJumpCnt = 0;
    private bool MoveToOverridePosition = false;

    protected PlayerBase SpottedPlayer, RecentlyScannedPlayer;
    public bool HasSpottedPlayer => SpottedPlayer;

    protected bool IsGuarding = true;
    public bool CanDetectThroughWalls = false;
    private short CurrentCheckpointIndex = 0;

    Coroutine MovelockoutCoroutine = null;
    TMP_Text DetectSymbol;

    private const short PathfindCntThreshold = 75, ScanPlayerCntThreshold = 25, MoveCntThreshold = 15;
    bool FirstPathfindAfterSpawn = true;

    private short ScanPlayerCnt = 0, MoveCnt = 0, PathfindCnt = 0;

    public bool IsInsignificant = false;

    public float DamageReductionOutsideCombat = 0f;

    public override void InitializeComponents()
    {
        base.InitializeComponents();

        PathfindCnt = (short)UnityEngine.Random.Range(0, PathfindCntThreshold);
        MoveCnt = (short) UnityEngine.Random.Range(0, MoveCntThreshold);
        ScanPlayerCnt = (short)UnityEngine.Random.Range(0, ScanPlayerCntThreshold);

        if (!ViewOnlyMode)
        {
            DetectSymbol = transform.Find("Spotted").GetComponentInChildren<TMP_Text>();
            FeetPosition = transform.Find("Feetposition");

            navAgent = GetComponentInChildren<NavMeshAgent>();

            navAgent.updatePosition = false;
            navAgent.updateRotation = false;
            navAgent.updateUpAxis = false;

            navAgent.radius = size switch
            {
                Size.SMALL => 2f,
                Size.MEDIUM => 4f,
                Size.LARGE => 6f,
                Size.HUGE => 8f,
                _ => 4f,
            };

            navAgent.height = size switch
            {
                Size.SMALL => 1.5f,
                Size.MEDIUM => 3f,
                Size.LARGE => 5f,
                Size.HUGE => 8f,
                _ => 3f,
            };

            navAgent.baseOffset = 0f;

            navAgent.speed = 0f;
            navAgent.angularSpeed = 0f;
            navAgent.acceleration = 0f;
            navAgent.autoBraking = false;
            navAgent.avoidancePriority = EntityManager.Enemies.IndexOf(this) % 100;
        }

        if (detectionRange <= 0) detectionRange = b_attackRange;
        b_detectionRange = detectionRange;

        WriteStats();
        if (!ViewOnlyMode)
        {
            if (SpotPlayerUponSpawn) ForceSpotPlayer();

            IsComponentsInitialized = true;

            if (CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.HIBERNATE))
                StartCoroutine(Deforst());
        }
        else
        {
            IsComponentsInitialized = true;
        }
    }

    IEnumerator Deforst()
    {
        SetHealth(mHealth * 0.5f);

        yield return null;

        while (IsFrozen)
        {
            yield return new WaitForSeconds(1f);
            Heal(mHealth * 0.02f);
        }

        RemoveEffect("ICEAGE_DEF_BUFF");
        RemoveEffect("ICEAGE_RES_BUFF");
    }

    public void ForceSpotPlayer() => StartCoroutine(ForceSpotCoroutine());

    IEnumerator ForceSpotCoroutine()
    {
        int count = 0;
        while (!SpottedPlayer || count <= 60)
        {
            yield return null;
            SpottedPlayer = EntityManager.Players.FirstOrDefault(p => p && p.IsAlive());
            count++;
        }
    }

    public override void FixedUpdate()
    {
        if (ViewOnlyMode || Time.timeScale <= 0) return;

        if (DetectSymbol)
        {
            DetectSymbol.color = RecentlyScannedPlayer ? Color.red : Color.yellow;

            bool isPlayerSpotted = SpottedPlayer && SpottedPlayer.IsAlive();
            DetectSymbol.text = isPlayerSpotted ? "!" : "?";
            DetectSymbol.gameObject.SetActive(IsAlive() && CanDetectPlayer && (isPlayerSpotted || MoveToOverridePosition));
        }

        navAgent.nextPosition = FeetPosition.position;

        base.FixedUpdate();

        EnemyFixedBehaviors();
    }

    public virtual void EnemyFixedBehaviors()
    {
        if (Time.timeScale <= 0 || !IsAlive() || ViewOnlyMode) return;
        ScanPlayer();
        UpdatePathfinding();
        Move();
    }

    private Vector3 GetUniversalDestination()
    {
        if (SpottedPlayer && SpottedPlayer.IsAlive())
            return GetPathfindingTarget();

        if (MoveToOverridePosition)
            return OverridePosition;

        return Checkpoints.Count > 1
            ? Checkpoints[CurrentCheckpointIndex].transform.position
            : StopVector;
    }

    protected static Vector3 StopVector = new Vector3(-12495 + Mathf.Epsilon, -23720 - Mathf.Epsilon, 0);

    private void UpdatePathfinding()
    {
        PathfindCnt++;

        if (!AllowMovement) return;

        if (navAgent == null) return;

        if (!FirstPathfindAfterSpawn && PathfindCnt <= PathfindCntThreshold) return;
        
        if (!FirstPathfindAfterSpawn) PathfindCnt = 0;
        else FirstPathfindAfterSpawn = false;

        Vector3 desiredDestination = GetUniversalDestination();

        navAgent.SetDestination(desiredDestination);
    }

    protected virtual Vector2 GetPathfindingTarget()
    {
        if (!SpottedPlayer || !SpottedPlayer.IsAlive()) return StopVector;

        var playerInRange = DetectPlayer(
            attackPattern == AttackPattern.MELEE
                ? attackRange * DangerRange_RatioOfAttackRange
                : attackRange,
            false);

        float distTransformToPlayer = Vector2.Distance(transform.position, SpottedPlayer.transform.position);
        bool playerIsFarAway =
            playerInRange == null ||
            isUsingPathfinding() ||
            distTransformToPlayer > attackRange * 1.2f;

        Vector2 playerPos = playerIsFarAway ? SpottedPlayer.FeetPosition.position : SpottedPlayer.transform.position;
        Vector2 enemyPos = playerIsFarAway ? FeetPosition.position : AttackPosition.position;

        switch (attackPattern)
        {
            case AttackPattern.MELEE:
                float distToPlayer = Vector2.Distance(enemyPos, playerPos);
                if (distToPlayer <= attackRange * DangerRange_RatioOfAttackRange)
                {
                    if (!RecentlyScannedPlayer) FaceToward(playerPos);
                    return StopVector;
                }
                return playerPos;

            case AttackPattern.RANGED:
                float retreatDistance = Mathf.Min(800, attackRange * DangerRange_RatioOfAttackRange);   
                bool playerIsNearby = playerInRange != null &&
                                      Vector2.Distance(enemyPos, playerPos) <= retreatDistance;
                if (playerIsNearby)
                {
                    Vector2 dirAway = ((Vector2)transform.position - playerPos).normalized;
                    return (Vector2)transform.position + dirAway * retreatDistance;
                }
                else if (RecentlyScannedPlayer && playerInRange)
                {
                    return StopVector;
                }
                else
                {
                    return playerPos;
                }

            default:
                return playerPos;
        }
    }

    public override Vector2 CalculateMovement(Vector2 normalizedMovementVector, float speed)
    {
        var result = base.CalculateMovement(normalizedMovementVector, speed);
        return MoveToOverridePosition
            ? result * (MoveToOverridePositionSpeedMultiplier +
                        Mathf.Min(MoveToOverridePositionSpeedMultiplierJump * MoveToOverridePositionJumpCnt, 0.5f))
            : result;
    }

    float waypointReachDistance = 40f;
    public virtual Vector3 GetCurrentDestination()
    {
        bool shouldUsePathfinding = !hasClearSightToTargetOnThisMoveOppoturnity;

        if (shouldUsePathfinding && isUsingPathfinding())
        {
            NavMeshPath path = navAgent.path;

            for (int i = 0; i < path.corners.Length; i++)
            {
                if (Vector2.Distance(FeetPosition.position, path.corners[i]) > waypointReachDistance)
                    return path.corners[i];
            }
        }

        return GetUniversalDestination();
    }

    private int obstacleLayerIndex = 8;
    private LayerMask obstacleLayer = 1 << 8;
    private float cornerAvoidanceDistance = 100f;

    bool AllowMovement => !IsFrozen && !IsStunned && IsAlive() && !IsMovementLocked && !IsBound;

    public override void Move()
    {
        MoveCnt++;

        if (!AllowMovement) return;

        if (!SpottedPlayer &&
            MoveToOverridePosition &&
            Vector2.Distance(FeetPosition.position, OverridePosition) <= OverridePositionCheckRadius)
        {
            MoveToOverridePosition = false;
            StartCoroutine(StartMovementLockout(UnityEngine.Random.Range(2f, 5f)));
        }

        if (MoveCnt < MoveCntThreshold) return;
        MoveCnt = 0;

        if (MoveToOverridePosition && SpottedPlayer)
        {
            MoveToOverridePosition = false;
        }

        hasClearSightToTargetOnThisMoveOppoturnity = hasClearSightToTarget();

        Vector2 currentPos = FeetPosition.position;
        // Stuck detection
        if (Vector2.Distance(currentPos, lastPosition) < stuckMovementThreshold)
        {
            stuckTimer += Time.fixedDeltaTime * MoveCntThreshold;
            if (stuckTimer > stuckThreshold) HandleStuckState();
        }
        else
        {
            stuckTimer = 0f;
        }
        lastPosition = currentPos;

        Vector3 destination = GetCurrentDestination();
        if (destination == StopVector)
        {
            StopMovement();
            return;
        }

        Vector3 selfPosition = (SpottedPlayer && hasClearSightToTargetOnThisMoveOppoturnity
                ? AttackPosition.position
                : (Vector3)FeetPosition.position);

        Vector2 direction = destination == StopVector ? Vector3.zero : (destination - selfPosition).normalized;

        Vector2 finalDirection = GetAvoidanceDirection(direction, selfPosition, destination);

        rb2d.velocity = CalculateMovement(finalDirection);
        animator.SetFloat("move", 1);
        base.Move();
    }

    private void HandleStuckState()
    {
        if (!IsValidForTerrainIgnore)
        {
            stuckTimer = -stuckThreshold;
            return;
        }

        if (currentIgnoreCoroutine == null)
        {
            currentIgnoreCoroutine = StartCoroutine(TemporarilyDisableHitbox());
            stuckTimer = (stuckThreshold + terrainIgnoreDuration) * -1;
        }
    }

    public void StopObstacleIgnore()
    {
        if (currentIgnoreCoroutine != null)
        {
            StopCoroutine(currentIgnoreCoroutine);
        }

        currentIgnoreCoroutine = null;

        if (StageManager.ObstacleCollider) 
            Physics2D.IgnoreCollision(FeetCollider, StageManager.ObstacleCollider, false);

        stuckTimer = -1f;
    }

    Coroutine currentIgnoreCoroutine = null;
    float terrainIgnoreDuration = 2f;
    IEnumerator TemporarilyDisableHitbox()
    {
        yield return new WaitForEndOfFrame();
        yield return null;

        if (StageManager.ObstacleCollider) 
            Physics2D.IgnoreCollision(FeetCollider, StageManager.ObstacleCollider, true);

        float c = 0;
        while (c < terrainIgnoreDuration && IsValidForTerrainIgnore)
        {
            c += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        StopObstacleIgnore();
    }

    protected override bool TriggerHunger()
    {
        return base.TriggerHunger() && SpottedPlayer && SpottedPlayer.IsAlive();
    }

    bool IsValidForTerrainIgnore
        => !IsBeingShifted 
        && !IsStunned 
        && !IsFrozen 
        && !IsMovementLocked
        && rb2d.velocity.magnitude > 0;

    private Vector2 GetAvoidanceDirection(Vector2 originalDirection, Vector3 selfPosition, Vector3 destination)
    {
        if (hasClearSightToTargetOnThisMoveOppoturnity || !isUsingPathfinding()) return originalDirection;
        if (originalDirection == Vector2.zero) return Vector2.zero;

        Vector2 currentPos = FeetPosition.position;
        Vector2 finalDirection = originalDirection;

        RaycastHit2D frontHit = Physics2D.Raycast(currentPos, finalDirection, cornerAvoidanceDistance, obstacleLayer);

        if (frontHit.collider != null && !colliders.Contains(frontHit.collider))
        {
            Vector2 avoidanceDir = GetBestAvoidanceDirection(currentPos, finalDirection, frontHit.point);
            if (avoidanceDir != Vector2.zero)
            {
                finalDirection = avoidanceDir;
            }
        }

        return finalDirection;
    }

    private Vector2 GetBestAvoidanceDirection(Vector2 currentPos, Vector2 originalDirection, Vector2 obstaclePoint)
    {
        float[] angles = { 45f, -45f, 135f, -135f };
        float bestScore = float.MinValue;
        Vector2 bestDirection = originalDirection;

        foreach (float angle in angles)
        {
            Vector2 testDirection = Quaternion.Euler(0, 0, angle) * originalDirection;
            RaycastHit2D testHit = Physics2D.Raycast(currentPos, testDirection, cornerAvoidanceDistance, obstacleLayer);

            if (testHit.collider == null || colliders.Contains(testHit.collider))
            {
                float directionScore = Vector2.Dot(testDirection, originalDirection);
                Vector2 awayFromObstacle = (currentPos - obstaclePoint).normalized;
                float avoidanceScore = Vector2.Dot(testDirection, awayFromObstacle) * 0.5f;
                float totalScore = directionScore + avoidanceScore;

                if (totalScore > bestScore)
                {
                    bestScore = totalScore;
                    bestDirection = testDirection;
                }
            }
        }

        return bestDirection;
    }

    private static readonly List<EnemyBase> _nearbyEnemyBuffer = new();
    protected virtual List<EnemyBase> GetNearbyEnemiesToFindPlayer()
    {
        var result = SearchForEntitiesAroundSelf(detectionRange, typeof(EnemyBase), true);
        if (IsStandingOnEnvironmentalTile(StageManager.EnvironmentType.DARK_ZONE))
            return result.Cast<EnemyBase>().ToList();

        _nearbyEnemyBuffer.Clear();
        foreach (var e in result)
        {
            if (!e.IsStandingOnEnvironmentalTile(StageManager.EnvironmentType.DARK_ZONE))
                _nearbyEnemyBuffer.Add(e as EnemyBase);
        }
        return _nearbyEnemyBuffer;
    }

    bool CanFindPlayerFromNearbyAllies()
    {
        if (!IsValidForPlayerDetection()) return false;

        var enemies = GetNearbyEnemiesToFindPlayer();
        foreach (var e in enemies)
        {
            EnemyBase enemy = e as EnemyBase;
            if (!enemy || !enemy.IsAlive() || !enemy.SpottedPlayer || !enemy.IsValidForPlayerDetection()) continue;

            RaycastHit2D checkObstacle = Physics2D.Raycast(
                FeetPosition.position,
                (enemy.FeetPosition.position - FeetPosition.position).normalized,
                Vector2.Distance(FeetPosition.position, enemy.FeetPosition.position) - 50f,
                obstacleLayer);

            if (checkObstacle && checkObstacle.collider) continue;

            RecentlyScannedPlayer = enemy.SpottedPlayer;
            return true;
        }

        if (!RecentlyScannedPlayer) RecentlyScannedPlayer = DetectPlayer();
        return false;
    }

    public virtual bool IsValidForPlayerDetection() => CanDetectPlayer && !IsFrozen && !IsStunned;
    public void ScanPlayer()
    {
        if (ScanPlayerCnt < ScanPlayerCntThreshold)
        {
            ScanPlayerCnt++;
            return;
        }
        ScanPlayerCnt = 0;

        if (!IsValidForPlayerDetection()) return;

        bool spottedViaAlert = false;
        if (!SpottedPlayer)
        {
            spottedViaAlert = CanFindPlayerFromNearbyAllies();
        }
        else
        {
            RecentlyScannedPlayer =
                attackPattern == AttackPattern.MELEE
                    ? DetectPlayer(DangerRange_RatioOfAttackRange * attackRange, false)
                    : DetectPlayer();
        }

        if (!RecentlyScannedPlayer || !RecentlyScannedPlayer.IsAlive()) return;

        if (!SpottedPlayer)
        {
            if (!spottedViaAlert && !CanDetectThroughWalls)
            {
                float distance = Vector3.Distance(RecentlyScannedPlayer.FeetPosition.position, FeetPosition.position);
                if (distance > Mathf.Max(100f, detectionRange * 0.25f))
                {
                    Vector3 direction = (RecentlyScannedPlayer.FeetPosition.position - FeetPosition.position).normalized;
                    var checkObstacle = Physics2D.Raycast(
                        FeetPosition.position + direction * 30f,
                        direction,
                        distance - 30f,
                        obstacleLayer);

                    if (checkObstacle.collider != null && !colliders.Contains(checkObstacle.collider))
                    {
                        RecentlyScannedPlayer = null;
                        return;
                    }
                }
            }

            SpottedPlayer = RecentlyScannedPlayer;
            IsGuarding = false;
            OnFirsttimePlayerSpot();
        }
        else
        {
            AttackCoroutine = StartCoroutine(Attack());
        }
    }

    void ForceChangePath()
    {
        if (navAgent == null) return;
        
        navAgent.path.ClearCorners();
        navAgent.ResetPath();
        FirstPathfindAfterSpawn = true;
    }

    public virtual void OnFirsttimePlayerSpot(bool viaAlert = false)
    {
        MoveToOverridePosition = false;
        MoveToOverridePositionJumpCnt = 0;
        FaceToward(SpottedPlayer.transform.position);
        IsGuarding = false;
        MovementLockout = 0;

        ForceChangePath();

        if (attackPattern == AttackPattern.NONE)
        {
            List<EnemyBase> enemies = EntityManager.Enemies.Where(e => e && e.IsAlive() && !e.SpottedPlayer).ToList();
            foreach (var item in enemies)
            {
                item.SpottedPlayer = SpottedPlayer;
                item.OnFirsttimePlayerSpot(true);
            }
        }
    }

    protected Vector3 SpottedPlayerPositionBeforeAttack;
    public override IEnumerator Attack()
    {
        if (IsAttackLocked || attackPattern == AttackPattern.NONE) yield break;

        SpottedPlayerPositionBeforeAttack = SpottedPlayer ? SpottedPlayer.transform.position : transform.position;
        StartCoroutine(base.Attack());

        if (attackPattern == AttackPattern.RANGED)
        {
            var target = SearchForNearestEntityAroundSelf(typeof(PlayerBase));
            FaceToward(target.transform.position);
        }

        yield return new WaitForSeconds(GetWindupTime());
        yield return null;
    }

    public override IEnumerator OnAttackComplete()
    {
        if (!SpottedPlayer || !CanFinishAttack) yield break;

        if (attackPattern == AttackPattern.RANGED)
        {
            if (ProjectilePrefab)
                CreateProjectileAndShootToward(SpottedPlayer, SpottedPlayerPositionBeforeAttack, ProjectileType);
            else
                DealDamage(SpottedPlayer, atk);
        }
        else if (attackPattern == AttackPattern.MELEE)
        {
            var target = SearchForNearestEntityAroundSelf(typeof(PlayerBase));
            if (target) DealDamage(target, atk);
        }

        yield return null;
    }

    public PlayerBase DetectPlayer(Vector3 position, float radius, bool catchInvisible = false)
        => (PlayerBase)SearchForNearestEntityAroundCertainPoint(typeof(PlayerBase), position, radius, catchInvisible);

    public PlayerBase DetectPlayer(float radius, bool catchInvisible = false)
        => (PlayerBase)SearchForNearestEntityAroundCertainPoint(typeof(PlayerBase), AttackPosition.position, radius, catchInvisible);

    public PlayerBase DetectPlayer(bool catchInvisible = false)
        => (PlayerBase)SearchForNearestEntityAroundCertainPoint(typeof(PlayerBase), AttackPosition.position,
            SpottedPlayer ? attackRange : detectionRange, catchInvisible);

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision || !enabled) return;

        if (collision.gameObject.CompareTag("Checkpoint") &&
            Checkpoints.Count > 0 &&
            collision.gameObject == Checkpoints[CurrentCheckpointIndex].gameObject)
        {
            OnCheckpointReach();
        }

        FumoScript fumoScript = collision.gameObject.GetComponent<FumoScript>();
        if (fumoScript &&
            fumoScript.ObjectiveType == FumoScript.FumoObjectiveType.PROTECT &&
            collision.gameObject.CompareTag("Fumo"))
        {
            StageManager.OnEnemyFumoPickup(this, collision);
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (!collision || !enabled) return;

        if (collision.gameObject.CompareTag("Checkpoint") &&
            Checkpoints.Count > 0 &&
            collision.gameObject == Checkpoints[CurrentCheckpointIndex].gameObject)
        {
            OnCheckpointReach();
        }
    }

    protected virtual void OnCheckpointReach()
    {
        if (MoveToOverridePosition || (SpottedPlayer && SpottedPlayer.IsAlive())) return;

        var tunnel = Checkpoints[CurrentCheckpointIndex].GetComponent<Tunnel>();
        if (tunnel) tunnel.EnterTunnel(this);

        if (WaitTimes[CurrentCheckpointIndex] > 0)
        {
            StopMovement();
            MovelockoutCoroutine = StartCoroutine(StartMovementLockout(WaitTimes[CurrentCheckpointIndex]));
        }

        CurrentCheckpointIndex++;
        if (CurrentCheckpointIndex >= Checkpoints.Count) CurrentCheckpointIndex = 0;
    }

    protected float Adaption_DefJump = 5;
    protected float Adaption_ResJump = 3;
    protected short Adaption_MaxCount = 40;

    public override void TakeDamage(DamageInstance damage, EntityBase source, ProjectileScript projectileInfo = null,
        bool IgnoreInvulnerability = false, bool CalculateDamage = false)
    {
        OnAttackReceive(source);
        base.TakeDamage(damage, source, projectileInfo, IgnoreInvulnerability, CalculateDamage);
        ProcessAdaption(damage, source);
    }

    protected void ProcessAdaption(DamageInstance damage, EntityBase source)
    {
        if (!source || !StageManager.Adaption || !IsAlive()) return;

        if (damage.PhysicalDamage > 0)
        {
            string key = "ADAPTION_DEF_BUFF";
            float cur = DefBuffs.ContainsKey(key) ? DefBuffs[key].Value : 0;
            float str = Mathf.Min(cur + Adaption_DefJump, Adaption_DefJump * Adaption_MaxCount);
            ApplyEffect(Effect.AffectedStat.DEF, key, str, 9999f, false);
        }

        if (damage.MagicalDamage > 0)
        {
            string key = "ADAPTION_RES_BUFF";
            float cur = ResBuffs.ContainsKey(key) ? ResBuffs[key].Value : 0;
            float str = Mathf.Min(cur + Adaption_ResJump, Adaption_ResJump * Adaption_MaxCount);
            ApplyEffect(Effect.AffectedStat.RES, key, str, 9999f, false);
        }
    }

    public override void OnAttackReceive(EntityBase source)
    {
        if (source as PlayerBase && !SpottedPlayer)
        {
            var nearbyEnemies = SearchForEntitiesAroundSelf(115, typeof(EnemyBase), true);
            nearbyEnemies.Add(this);

            foreach (var en in nearbyEnemies)
            {
                var enemy = en.GetComponent<EnemyBase>();
                if (!enemy) continue;
                enemy.MoveTowardTheSourceOfAttack(source);
            }
        }
    }

    protected void MoveTowardTheSourceOfAttack(EntityBase source)
    {
        if (!IsAlive()) return;
        if (SpottedPlayer) return;
        if (MoveToOverridePosition && OverridePosition == source.FeetPosition.position) return;

        ForceChangePath();
        MoveToOverridePositionJumpCnt++;
        FaceToward(source.transform.position);
        MovementLockout = 0;
        if (MovelockoutCoroutine != null) StopCoroutine(MovelockoutCoroutine);
        MoveToOverridePosition = true;
        OverridePosition = source.transform.position;
    }

    public override void OnDeath()
    {
        base.OnDeath();
        DetectSymbol.gameObject.SetActive(false);

        EntityManager.Enemies.ForEach(enemy =>
        {
            if (enemy && enemy.IsAlive() && enemy != this && enemy is BloodboilKnight bk)
                bk.OnEnemyDeath();
        });
    }

    public void ChangeAggro(PlayerBase player)
    {
        if (player == null)
        {
            SpottedPlayer = RecentlyScannedPlayer = null;
            return;
        }

        if (!SpottedPlayer) return;
        SpottedPlayer = RecentlyScannedPlayer = player;
    }

    public virtual void WriteStats()
    {
        if (ViewOnlyMode || !showTooltips || !TooltipsPrefab) return;
        StartCoroutine(ShowTooltips());
    }

    public virtual void SetCheckpoints(float InitWaittime, List<EnemyCheckpointScript> enemyCheckpoints,
        bool showTooltips = false, int TooltipsPriority = 0)
    {
        this.showTooltips = showTooltips;
        this.TooltipsPriority = TooltipsPriority;

        Checkpoints.Clear();
        WaitTimes.Clear();

        foreach (var checkpoint in enemyCheckpoints)
        {
            if (checkpoint.Checkpoint)
            {
                Checkpoints.Add(checkpoint.Checkpoint);
                WaitTimes.Add(checkpoint.WaitTime);
            }
        }

        if (Checkpoints.Count > 0)
            CurrentCheckpointIndex = 0;
    }

    IEnumerator ShowTooltips()
    {
        yield return new WaitForSeconds(Time.fixedDeltaTime * 5 * TooltipsPriority);
        GameObject o = Instantiate(TooltipsPrefab, Vector3.negativeInfinity, Quaternion.identity);
        o.GetComponent<EnemyTooltipsScript>().Initialize(this);
    }

    public TooltipsData GetTooltipsData()
    {
        return new TooltipsData
        {
            Icon = this.Icon,
            Name = this.Name,
            Description = this.TooltipsDescription,
            HoldTime = this.TooltipsHoldtime,
            Code = this.enemyCode,
        };
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(AttackPosition.position, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(AttackPosition ? AttackPosition.position : transform.position, attackRange);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(AttackPosition ? AttackPosition.position : transform.position,
            attackRange * DangerRange_RatioOfAttackRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(OverridePosition, OverridePositionCheckRadius);

        // Draw NavMesh path corners
        NavMeshPath path = navAgent.path;
        Gizmos.color = isUsingPathfinding() ? Color.blue : Color.gray;

        for (int i = 0; i < path.corners.Length - 1; i++)
            Gizmos.DrawLine(path.corners[i], path.corners[i + 1]);

        foreach (var corner in path.corners)
            Gizmos.DrawWireSphere(corner, 25f);

        if (path.corners.Length > 0)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(FeetPosition.position, path.corners[0]);
        }

        // Direct line to target when not using pathfinding
        if (!isUsingPathfinding() && SpottedPlayer && SpottedPlayer.IsAlive())
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(AttackPosition.position, GetPathfindingTarget());
        }
    }
}