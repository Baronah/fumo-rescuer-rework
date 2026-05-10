using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
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

    [Header("A* Pathfinding")]
    private float gridCellSize = 65f;
    [SerializeField] private LayerMask obstacleLayer = 6;
    [SerializeField] private float pathfindingRadius = 3000f;
    [SerializeField] private float pathUpdateInterval = 0.5f;
    [SerializeField] private float waypointReachDistance = 50f; 
    [SerializeField] private bool showPathGizmos = true;
    [SerializeField] private bool allowDiagonalMovement = true;
    [SerializeField] private float pathfindingDistanceThreshold = 100f;
    [SerializeField] private float cornerAvoidanceDistance = 75f; 
    [SerializeField] private float pathSmoothingLookahead = 2; // How many waypoints ahead to look for smoothing

    [Header("Checkpoints System")]
    protected Transform FeetPosition;
    protected List<Transform> Checkpoints = new();
    [SerializeField] private List<float> WaitTimes = new();

    private readonly float OverridePositionCheckRadius = 75f;
    private Vector3 OverridePosition;
    [SerializeField] private float MoveToOverridePositionSpeedMultiplier = 1.5f, MoveToOverridePositionSpeedMultiplierJump = 0.35f;
    private short MoveToOverridePositionJumpCnt = 0;
    private bool MoveToOverridePosition = false;

    protected PlayerBase SpottedPlayer, RecentlyScannedPlayer;
    public bool HasSpottedPlayer => SpottedPlayer;

    protected bool IsGuarding = true;
    public bool CanDetectThroughWalls = false;
    private short CurrentCheckpointIndex = 0;

    Coroutine MovelockoutCoroutine = null;
    TMP_Text DetectSymbol;

    // A* Pathfinding variables
    private List<Vector2> currentPath = new List<Vector2>();
    private int currentWaypointIndex = 0;
    private float lastPathUpdateTime = 0f;
    private Vector2 lastTargetPosition = Vector2.zero;
    private static PathfindingGrid pathfindingGrid;
    private bool isUsingPathfinding = false; // Track if we're currently using pathfinding
    private float stuckTimer = 0f; // Track how long we've been stuck
    private Vector2 lastPosition = Vector2.zero; // Track last position for stuck detection
    private readonly float stuckThreshold = 0.85f; // Time before considering stuck
    private readonly float stuckMovementThreshold = 50f; // Distance moved to not be considered stuck

    private const short PathfindCntThreshold = 100, ScanPlayerCntThreshold = 20, MoveCntThreshold = 15;
    private short ScanPlayerCnt = 0, MoveCnt = 0, PathfindCnt = 0;

    public bool IsInsignificant = false;

    protected StageManager stageManager;

    public bool hasDRWhenNotCombat = false;
    public override void InitializeComponents()
    { 
        stageManager = FindObjectOfType<StageManager>(true);

        base.InitializeComponents();
        PathfindCnt = (short)UnityEngine.Random.Range(0, PathfindCntThreshold);
        MoveCnt = (short)UnityEngine.Random.Range(0, ScanPlayerCntThreshold);
        ScanPlayerCnt = (short)UnityEngine.Random.Range(0, MoveCntThreshold);

        gridCellSize = size switch
        {
            Size.SMALL => 50f,
            Size.MEDIUM => 65f,
            Size.LARGE => 80f,
            Size.HUGE => 100f,
            _ => gridCellSize
        };

        if (!ViewOnlyMode)
        {
            DetectSymbol = transform.Find("Spotted").GetComponentInChildren<TMP_Text>();
            FeetPosition = transform.Find("Feetposition");

            // Initialize pathfinding grid (shared among all enemies)
            pathfindingGrid ??= new PathfindingGrid(gridCellSize, obstacleLayer);
        }

        if (detectionRange <= 0) detectionRange = b_attackRange;
        b_detectionRange = detectionRange;

        WriteStats();
        if (!ViewOnlyMode)
        {
            hasDRWhenNotCombat = (CharacterPrefabsStorage.DifficultyLevel - 1) >= (int)DiffType.EnemiesUncombatDRBuff;

            if (SpotPlayerUponSpawn) ForceSpotPlayer();
            
            IsComponentsInitialized = true;

            if (CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.HIBERNATE)) StartCoroutine(Deforst());
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
            SpottedPlayer = FindObjectOfType<PlayerBase>();
            count++;
        }
    }

    public override void FixedUpdate()
    {
        if (ViewOnlyMode) return;
        
        if (DetectSymbol)
        {
            DetectSymbol.color = RecentlyScannedPlayer ? Color.red : Color.yellow;

            bool isPlayerSpotted = SpottedPlayer && SpottedPlayer.IsAlive();

            DetectSymbol.text = isPlayerSpotted ? "!" : "?";
            DetectSymbol.gameObject.SetActive(IsAlive() && (isPlayerSpotted || MoveToOverridePosition));
        }

        base.FixedUpdate();

        EnemyFixedBehaviors();
    }

    public virtual void EnemyFixedBehaviors()
    {
        if (!IsAlive() || ViewOnlyMode) return;
        ScanPlayer();
        UpdatePathfinding();
        Move();
    }

    private Vector2 GetUniversalDestination()
    {
        if (SpottedPlayer && SpottedPlayer.IsAlive())
        {
            return GetPathfindingTarget();
        }
        else if (MoveToOverridePosition)
        {
            return OverridePosition;
        }
        else
        {
            return Checkpoints.Count > 1 ? Checkpoints[CurrentCheckpointIndex].transform.position : FeetPosition.position;
        }
    }

    protected Vector3 StopVector = new Vector3(Mathf.Epsilon, Mathf.Epsilon, 0);
    private static int pathfindsThisFrame = 0;
    private static int lastBudgetFrame = -1;
    private const int MaxPathfindsPerFrame = 2; // tune to taste
    private void UpdatePathfinding()
    {
        if (Time.frameCount != lastBudgetFrame)
        {
            pathfindsThisFrame = 0;
            lastBudgetFrame = Time.frameCount;
        }

        PathfindCnt++;

        if (IsFrozen || IsStunned || !IsAlive() || rb2d.velocity.magnitude <= 0 || IsBound || MovementLockout > 0) return;

        if (PathfindCnt <= PathfindCntThreshold) return;

        if (pathfindsThisFrame >= MaxPathfindsPerFrame) return;

        PathfindCnt = 0;
        pathfindsThisFrame++;

        Vector2 currentPos = FeetPosition.position;
        Vector2 desiredDestination = GetUniversalDestination();
        Vector3 toVector3 = new(desiredDestination.x, desiredDestination.y);
        if (toVector3 == StopVector)
        {
            isUsingPathfinding = false;
            currentPath.Clear();
            return;
        }

        float distanceToDestination = Vector2.Distance(currentPos, desiredDestination);

        // Use pathfinding threshold for all movement types
        if (distanceToDestination <= 70f)
        {
            isUsingPathfinding = false;
            currentPath.Clear();
            return;
        }

        // Check if there's a direct line to our desired destination
        Vector2 directionToDestination = (desiredDestination - currentPos).normalized;
        RaycastHit2D hit = Physics2D.Raycast(currentPos, directionToDestination, distanceToDestination, obstacleLayer);

        // Only skip pathfinding if we have completely clear path
        bool hasDirectPath = hit.collider == null || colliders.Contains(hit.collider);

        if (hasDirectPath)
        {
            isUsingPathfinding = false;
            currentPath.Clear();
            return;
        }

        pathfindingRadius = Mathf.Clamp(distanceToDestination, 400, 1750);
        // Determine what changed to decide if we need path updates
        Vector2 currentTargetPos = SpottedPlayer ? SpottedPlayer.transform.position : desiredDestination;

        // Need pathfinding - make updates more responsive
        bool shouldUpdatePath = Time.time - lastPathUpdateTime > pathUpdateInterval ||
                               currentPath.Count == 0 ||
                               Vector2.Distance(currentTargetPos, lastTargetPosition) > gridCellSize * 1.5f;

        if (shouldUpdatePath)
        {
            isUsingPathfinding = true;

            // Update grid around current area with larger radius for better planning
            pathfindingGrid.UpdateGrid(currentPos, pathfindingRadius * 1.2f);

            // Find path to our desired destination
            List<Vector2> newPath = pathfindingGrid.FindPath(currentPos, desiredDestination, allowDiagonalMovement);

            if (newPath.Count > 0)
            {
                currentPath = newPath;
                currentWaypointIndex = 0;
            }
            else
            {
                // Fallback: try different approach based on current mode
                List<Vector2> fallbackPath = null;
                if (SpottedPlayer && SpottedPlayer.IsAlive())
                {
                    // Try direct path to player
                    fallbackPath = pathfindingGrid.FindPath(currentPos, SpottedPlayer.transform.position, allowDiagonalMovement);
                }
                else if (MoveToOverridePosition)
                {
                    // Try different points around override position
                    for (float angle = 0; angle < 360; angle += 45)
                    {
                        Vector2 offset = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * 30f;
                        Vector2 alternateTarget = new Vector2(OverridePosition.x, OverridePosition.y) + offset;
                        fallbackPath = pathfindingGrid.FindPath(currentPos, alternateTarget, allowDiagonalMovement);
                        if (fallbackPath.Count > 0) break;
                    }
                }
                else
                {
                    // Try points around checkpoint
                    Vector2 checkpointPos = Checkpoints[CurrentCheckpointIndex].transform.position;
                    for (float angle = 0; angle < 360; angle += 45)
                    {
                        Vector2 offset = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * 30f;
                        Vector2 alternateTarget = checkpointPos + offset;
                        fallbackPath = pathfindingGrid.FindPath(currentPos, alternateTarget, allowDiagonalMovement);
                        if (fallbackPath.Count > 0) break;
                    }
                }

                if (fallbackPath != null && fallbackPath.Count > 0)
                {
                    currentPath = fallbackPath;
                    currentWaypointIndex = 0;
                }
                else
                {
                    // Last resort: clear path and use direct movement with obstacle avoidance
                    isUsingPathfinding = false;
                    currentPath.Clear();
                }
            }

            lastPathUpdateTime = Time.time;
            lastTargetPosition = currentTargetPos;
            lastPosition = currentPos;
        }
    }

    protected virtual Vector2 GetPathfindingTarget()
    {
        if (!SpottedPlayer) return StopVector;

        var playerInRange = DetectPlayer(attackPattern == AttackPattern.MELEE ? attackRange * DangerRange_RatioOfAttackRange : attackRange, false);

        float DistanceTransformToPlayerTransform = Vector2.Distance(transform.position, SpottedPlayer.transform.position);
        bool playerIsFarAway = 
            playerInRange == null 
            ||
            isUsingPathfinding 
            || 
            DistanceTransformToPlayerTransform > attackRange * 1.2f;

        Vector2 playerPos = playerIsFarAway ? SpottedPlayer.Feetposition : SpottedPlayer.transform.position;
        Vector2 enemyPos = playerIsFarAway ? FeetPosition.position : AttackPosition.position;

        switch (attackPattern)
        {
            case AttackPattern.MELEE:
                float distanceToPlayer = Vector2.Distance(enemyPos, playerPos);
                if (distanceToPlayer <= attackRange * DangerRange_RatioOfAttackRange)
                {
                    if (!RecentlyScannedPlayer) FaceToward(playerPos);
                    return StopVector;
                }

                return playerPos;

            case AttackPattern.RANGED:
                bool PlayerIsNearby = playerInRange != null && Vector2.Distance(enemyPos, playerPos) <= attackRange * DangerRange_RatioOfAttackRange;
                if (PlayerIsNearby)
                {
                    enemyPos = transform.position;
                    Vector2 dirAwayFromPlayer = (enemyPos - playerPos).normalized;
                    return enemyPos + dirAwayFromPlayer * (attackRange * DangerRange_RatioOfAttackRange);
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
        return MoveToOverridePosition ? result * (MoveToOverridePositionSpeedMultiplier + Mathf.Min(MoveToOverridePositionSpeedMultiplierJump * MoveToOverridePositionJumpCnt, 0.5f)) : result;
    }

    public virtual Vector3 GetCurrentDestination()
    {
        // If we're using pathfinding and have a valid path (for ANY destination type)
        if (isUsingPathfinding && currentPath.Count > 0)
        {
            // Check if we've reached current waypoint
            if (currentWaypointIndex < currentPath.Count)
            {
                Vector2 currentWaypoint = currentPath[currentWaypointIndex];
                float distanceToWaypoint = Vector2.Distance(FeetPosition.position, currentWaypoint); // Changed from transform.position

                if (distanceToWaypoint < waypointReachDistance)
                {
                    currentWaypointIndex++;
                }

                // Return current waypoint if we still have waypoints
                if (currentWaypointIndex < currentPath.Count)
                {
                    Vector2 smoothedTarget = GetSmoothedPathTarget();
                    return smoothedTarget;
                }
            }

            // Check if pathfinding still needed
            Vector2 finalTarget = GetUniversalDestination();
            Vector2 currentPos = FeetPosition.position; // Changed from transform.position
            Vector2 dirToFinal = (finalTarget - currentPos).normalized;
            float distToFinal = Vector2.Distance(currentPos, finalTarget);

            // Only force recalculation if there are still obstacles in the way
            RaycastHit2D directHit = Physics2D.Raycast(currentPos, dirToFinal, distToFinal, obstacleLayer);
            if (directHit.collider != null && !colliders.Contains(directHit.collider))
            {
                // Still need pathfinding - force recalculation
                lastPathUpdateTime = 0f;
                currentPath.Clear();
            }

            return finalTarget;
        }

        // No pathfinding active - return the appropriate destination directly
        return GetUniversalDestination();
    }

    private Vector2 GetSmoothedPathTarget()
    {
        if (currentWaypointIndex >= currentPath.Count) return currentPath[currentPath.Count - 1];

        Vector2 currentPos = FeetPosition.position; // Changed from transform.position
        Vector2 currentWaypoint = currentPath[currentWaypointIndex];

        // Look ahead to see if we can skip waypoints
        for (int i = currentWaypointIndex; i < Mathf.Min(currentWaypointIndex + (int)pathSmoothingLookahead + 1, currentPath.Count); i++)
        {
            Vector2 futureWaypoint = currentPath[i];
            Vector2 directionToFuture = (futureWaypoint - currentPos).normalized;
            float distanceToFuture = Vector2.Distance(currentPos, futureWaypoint);

            // Check if we can reach this future waypoint directly
            RaycastHit2D hit = Physics2D.Raycast(currentPos, directionToFuture, distanceToFuture, obstacleLayer);
            if (hit.collider == null || colliders.Contains(hit.collider))
            {
                // We can reach this waypoint directly, so target it instead
                currentWaypoint = futureWaypoint;
                if (i > currentWaypointIndex)
                {
                    currentWaypointIndex = i; // Skip intermediate waypoints
                }
            }
            else
            {
                break; // Can't reach further waypoints, stop here
            }
        }

        return currentWaypoint;
    }

    public override void Move()
    {
        MoveCnt++;

        if (IsFrozen || IsStunned || !IsAlive() || IsMovementLocked || IsBound) return;

        if (!SpottedPlayer
            && MoveToOverridePosition
            && Vector2.Distance(FeetPosition.position, OverridePosition) <= OverridePositionCheckRadius)
        {
            MoveToOverridePosition = false;
            StartCoroutine(StartMovementLockout(UnityEngine.Random.Range(2f, 6f)));
        }

        if (MoveCnt < MoveCntThreshold) return;
        MoveCnt = 0;

        // Stuck detection and handling using FeetPosition
        Vector2 currentPos = FeetPosition.position; // Changed from transform.position
        if (Vector2.Distance(currentPos, lastPosition) < stuckMovementThreshold)
        {
            stuckTimer += Time.fixedDeltaTime * MoveCntThreshold; // Account for MoveCnt skip
            if (stuckTimer > stuckThreshold)
            {
                HandleStuckState();
            }
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

        Vector2 direction = destination - (SpottedPlayer && !isUsingPathfinding ? AttackPosition.position : (Vector3)FeetPosition.position); // Changed from transform.position
        float distanceToDestination = direction.magnitude;

        // Stop if we're very close to destination
        if (distanceToDestination <= 20f) // Slightly increased
        {
            StopMovement();
            return;
        }

        Vector2 finalDirection = direction.normalized;

        // Enhanced obstacle avoidance with corner handling
        finalDirection = GetAvoidanceDirection(finalDirection, distanceToDestination);

        rb2d.velocity = CalculateMovement(finalDirection);
        animator.SetFloat("move", 1);
        base.Move();
    }

    private void HandleStuckState()
    {
        if (!IsValidForTerrainIgnore)
        {
            stuckTimer = 0;
            return;
        }

        if (currentIgnoreCoroutine == null) 
            currentIgnoreCoroutine = StartCoroutine(TemporarilyDisableHitbox());

        // ForceChangePath();
        StopObstacleIgnore();
    }

    public void StopObstacleIgnore()
    {
        Physics2D.IgnoreLayerCollision(gameObject.layer, 8, false);
        stuckTimer = -1f;
    }

    Coroutine currentIgnoreCoroutine = null;
    IEnumerator TemporarilyDisableHitbox()
    {
        yield return new WaitForEndOfFrame();
        yield return null;
        if (!IsValidForTerrainIgnore) 
        {
            currentIgnoreCoroutine = null;
            yield break;
        }

        float c = 0, d = 1;
        while (c < d)
        {
            Physics2D.IgnoreLayerCollision(gameObject.layer, 8, IsValidForTerrainIgnore);
            c += Time.deltaTime;
            yield return null;
        }
        Physics2D.IgnoreLayerCollision(gameObject.layer, 8, false);
        currentIgnoreCoroutine = null;
    }

    bool IsValidForTerrainIgnore 
        => isUsingPathfinding && !IsBeingShifted && !IsStunned && !IsFrozen && animator.GetFloat("move") >= 0.1f;

    private Vector2 GetAvoidanceDirection(Vector2 originalDirection, float distanceToDestination)
    {
        Vector2 currentPos = FeetPosition.position;
        Vector2 finalDirection = originalDirection;

        // Always do immediate obstacle avoidance, regardless of pathfinding state
        float checkDistance = Mathf.Min(cornerAvoidanceDistance, distanceToDestination);
        RaycastHit2D frontHit = Physics2D.Raycast(currentPos, finalDirection, checkDistance, obstacleLayer);

        if (frontHit.collider != null && !colliders.Contains(frontHit.collider))
        {
            // We're about to hit an obstacle
            Vector2 avoidanceDir = GetBestAvoidanceDirection(currentPos, finalDirection, frontHit.point);
            if (avoidanceDir != Vector2.zero)
            {
                finalDirection = avoidanceDir;

                // If we're avoiding obstacles while not using pathfinding, consider enabling pathfinding
                if (!isUsingPathfinding && SpottedPlayer)
                {
                    float distanceToTarget = Vector2.Distance(currentPos, GetPathfindingTarget());
                    if (distanceToTarget > 75f)
                    {
                        // Force pathfinding recalculation next frame
                        lastPathUpdateTime = 0f;
                    }
                }
            }
        }

        return finalDirection;
    }

    private Vector2 GetBestAvoidanceDirection(Vector2 currentPos, Vector2 originalDirection, Vector2 obstaclePoint)
    {
        // Try multiple angles to find the best path around the obstacle
        float[] angles = { 45f, -45f, 90f, -90f, 30f, -30f, 135f, -135f };
        float bestScore = float.MinValue;
        Vector2 bestDirection = originalDirection;

        foreach (float angle in angles)
        {
            Vector2 testDirection = Quaternion.Euler(0, 0, angle) * originalDirection;
            float testDistance = cornerAvoidanceDistance;

            // Check if this direction is clear
            RaycastHit2D testHit = Physics2D.Raycast(currentPos, testDirection, testDistance, obstacleLayer);

            if (testHit.collider == null || colliders.Contains(testHit.collider))
            {
                // This direction is clear, score it based on how close it is to original direction
                float directionScore = Vector2.Dot(testDirection, originalDirection);

                // Bonus for directions that move away from the obstacle
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

    protected virtual List<EntityBase> GetNearbyEnemiesToFindPlayer()
    {
        var result = SearchForEntitiesAroundSelf(detectionRange, typeof(EnemyBase), true);
        if (IsStandingOnEnvironmentalTile(StageManager.EnvironmentType.DARK_ZONE)) return result;

        return result.Where(e => !e.IsStandingOnEnvironmentalTile(StageManager.EnvironmentType.DARK_ZONE)).ToList();
    }

    bool CanFindPlayerFromNearbyAllies()
    {
        var enemies = GetNearbyEnemiesToFindPlayer();
        foreach (var e in enemies)
        {
            EnemyBase enemy = e as EnemyBase;

            if (!enemy || !enemy.IsAlive() || !enemy.SpottedPlayer) continue;

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

    public void ScanPlayer()
    {
        if (ScanPlayerCnt < ScanPlayerCntThreshold)
        {
            ScanPlayerCnt++;
            return;
        }
        ScanPlayerCnt = 0;

        if (IsFrozen || IsStunned) return;

        bool spottedViaAlert = false;
        if (!SpottedPlayer)
        {
            spottedViaAlert = CanFindPlayerFromNearbyAllies();
        }
        else
        {
            RecentlyScannedPlayer =
                attackPattern == AttackPattern.MELEE
                ?
                DetectPlayer(DangerRange_RatioOfAttackRange * attackRange, false)
                :
                DetectPlayer();
        }

        if (!RecentlyScannedPlayer || !RecentlyScannedPlayer.IsAlive()) return;

        if (!SpottedPlayer)
        {
            if (!spottedViaAlert && !CanDetectThroughWalls)
            {
                float distance = Vector3.Distance(RecentlyScannedPlayer.Feetposition, FeetPosition.position);
                if (distance > Mathf.Max(100f, detectionRange * 0.25f))
                {
                    var checkObstacle = Physics2D.Raycast(
                        transform.position,
                        (RecentlyScannedPlayer.Feetposition - FeetPosition.position).normalized,
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
        PathfindCnt = PathfindCntThreshold;
        currentPath.Clear();
        currentWaypointIndex = 0;
        isUsingPathfinding = false;
        lastPathUpdateTime = 0f;
    }

    public virtual void OnFirsttimePlayerSpot(bool viaAlert = false)
    {
        MoveToOverridePosition = false;
        MoveToOverridePositionJumpCnt = 0;
        FaceToward(SpottedPlayer.transform.position);
        IsGuarding = false;
        MovementLockout = 0;

        // Clear current path when spotting player
        ForceChangePath();

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
            {
                CreateProjectileAndShootToward(SpottedPlayer, ProjectileType);
            }
            else DealDamage(SpottedPlayer, atk);
        }
        else if (attackPattern == AttackPattern.MELEE)
        {
            var target = SearchForNearestEntityAroundSelf(typeof(PlayerBase));
            if (target) DealDamage(target, atk);
        }

        yield return null;
    }

    public PlayerBase DetectPlayer(Vector3 position, float radius, bool catchInvisible = false)
    {
        return (PlayerBase)SearchForNearestEntityAroundCertainPoint(typeof(PlayerBase), position, radius, catchInvisible);
    }

    public PlayerBase DetectPlayer(float radius, bool catchInvisible = false)
    {
        return (PlayerBase)SearchForNearestEntityAroundCertainPoint(typeof(PlayerBase), AttackPosition.position, radius, catchInvisible);
    }

    public PlayerBase DetectPlayer(bool catchInvisible = false)
    {
        return (PlayerBase)SearchForNearestEntityAroundCertainPoint(typeof(PlayerBase), AttackPosition.position, SpottedPlayer ? attackRange : detectionRange, catchInvisible);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision || !enabled) return;

        if (collision.gameObject.CompareTag("Checkpoint") 
            && Checkpoints.Count > 0
            && collision.gameObject == Checkpoints[CurrentCheckpointIndex].gameObject)
        {
            OnCheckpointReach();
        }

        FumoScript fumoScript = collision.gameObject.GetComponent<FumoScript>();
        if (fumoScript && fumoScript.ObjectiveType == FumoScript.FumoObjectiveType.PROTECT && collision.gameObject.CompareTag("Fumo"))
        {
            stageManager.OnEnemyFumoPickup(this, collision);
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (!collision || !enabled) return;

        if (collision.gameObject.CompareTag("Checkpoint") 
            && Checkpoints.Count > 0
            && collision.gameObject == Checkpoints[CurrentCheckpointIndex].gameObject)
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

    readonly protected bool Adaption = CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.ADAPTION);
    protected float Adaption_DefJump = 5;
    protected float Adaption_ResJump = 3;
    protected short Adaption_MaxCount = 40;
    public override void TakeDamage(DamageInstance damage, EntityBase source, ProjectileScript projectileInfo = null, bool IgnoreInvulnerability = false, bool CalculateDamage = false)
    {
        OnAttackReceive(source);
        base.TakeDamage(damage, source, projectileInfo, IgnoreInvulnerability, CalculateDamage);
        ProcessAdaption(damage, source);
    }

    protected void ProcessAdaption(DamageInstance damage, EntityBase source)
    {
        if (!source || !Adaption || !IsAlive()) return;

        if (damage.PhysicalDamage > 0)
        {
            string key = "ADAPTION_DEF_BUFF";
            float CurrentStrength = DefBuffs.ContainsKey(key) ? DefBuffs[key].Value : 0;
            float Strength = Mathf.Min(CurrentStrength + Adaption_DefJump, Adaption_DefJump * Adaption_MaxCount);
            ApplyEffect(Effect.AffectedStat.DEF, key, Strength, 9999f, false);
        }

        if (damage.MagicalDamage > 0)
        {
            string key = "ADAPTION_RES_BUFF";
            float CurrentStrength = ResBuffs.ContainsKey(key) ? ResBuffs[key].Value : 0;
            float Strength = Mathf.Min(CurrentStrength + Adaption_ResJump, Adaption_ResJump * Adaption_MaxCount);
            ApplyEffect(Effect.AffectedStat.RES, key, Strength, 9999f, false);
        }
    }

    public override void OnAttackReceive(EntityBase source)
    {
        if (source as PlayerBase && !SpottedPlayer)
        {
            var nearbyEnemies = SearchForEntitiesAroundSelf(100, typeof(EnemyBase), true);
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
        if (SpottedPlayer) return;
        if (MoveToOverridePosition && OverridePosition == source.transform.position) return;

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
            {
                bk.OnEnemyDeath();
            }
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

    public virtual void SetCheckpoints(float InitWaittime, List<EnemyCheckpointScript> enemyCheckpoints, bool showTooltips = false, int TooltipsPriority = 0)
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
        {
            CurrentCheckpointIndex = 0;
        }
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
        if (!Application.isPlaying || !showPathGizmos) return;

        // Draw detection and attack ranges
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(AttackPosition.position, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(AttackPosition ? AttackPosition.position : transform.position, attackRange);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(AttackPosition ? AttackPosition.position : transform.position, attackRange * DangerRange_RatioOfAttackRange);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(OverridePosition, OverridePositionCheckRadius);

        // Draw current path
        if (currentPath.Count > 1)
        {
            Gizmos.color = isUsingPathfinding ? Color.blue : Color.gray;
            for (int i = 0; i < currentPath.Count - 1; i++)
            {
                Gizmos.DrawLine(currentPath[i], currentPath[i + 1]);
            }

            // Draw waypoints
            for (int i = 0; i < currentPath.Count; i++)
            {
                if (i == currentWaypointIndex)
                {
                    Gizmos.color = Color.green; // Current waypoint
                    Gizmos.DrawWireSphere(currentPath[i], waypointReachDistance);
                }
                else
                {
                    Gizmos.color = Color.white;
                    Gizmos.DrawWireSphere(currentPath[i], 5f);
                }
            }

            // Draw line from enemy to current waypoint
            if (currentWaypointIndex < currentPath.Count)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(transform.position, currentPath[currentWaypointIndex]);
            }
        }

        // Draw direct line to target when not using pathfinding
        if (!isUsingPathfinding && SpottedPlayer && SpottedPlayer.IsAlive())
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(AttackPosition.position, GetPathfindingTarget());
        }
    }
}

