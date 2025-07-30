using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class EnemyBase : EntityBase
{
    public enum EnemyCode
    {
        MATTERLLURGIST,
        SENTINEL,
        ZEALOT,
        HEIR,
    }

    public EnemyCode enemyCode;

    [SerializeField] private bool SpotPlayerUponSpawn = false;
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

    [Header("A* Pathfinding")]
    [SerializeField] private float gridCellSize = 30f;
    [SerializeField] private LayerMask obstacleLayer = 7;
    [SerializeField] private float pathfindingRadius = 500f;
    [SerializeField] private float pathUpdateInterval = 0.5f;
    [SerializeField] private float waypointReachDistance = 50f; 
    [SerializeField] private bool showPathGizmos = true;
    [SerializeField] private bool allowDiagonalMovement = true;
    [SerializeField] private float pathfindingDistanceThreshold = 100f;
    [SerializeField] private float cornerAvoidanceDistance = 50f; 
    [SerializeField] private float pathSmoothingLookahead = 2; // How many waypoints ahead to look for smoothing

    [Header("Checkpoints System")]
    private Transform FeetPosition;
    protected List<Transform> Checkpoints = new();
    [SerializeField] private List<float> WaitTimes = new();

    [SerializeField] private float OverridePositionCheckRadius = 25f;
    private Vector2 OverridePosition;
    [SerializeField] private float MoveToOverridePositionSpeedMultiplier = 1.5f, MoveToOverridePositionSpeedMultiplierJump = 0.35f;
    private short MoveToOverridePositionJumpCnt = 0;
    private bool MoveToOverridePosition = false;

    protected PlayerBase SpottedPlayer, RecentlyScannedPlayer;
    protected bool IsGuarding = true;
    private short CurrentCheckpointIndex = 0;

    private short SearchCnt = 0, MoveCnt = 0;
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
    private float stuckThreshold = 3f; // Time before considering stuck
    private float stuckMovementThreshold = 5f; // Distance moved to not be considered stuck

    public override void InitializeComponents()
    {
        base.InitializeComponents();

        DetectSymbol = transform.Find("Spotted").GetComponentInChildren<TMP_Text>();

        FeetPosition = transform.Find("Feetposition");

        if (DetectionRange <= 0) DetectionRange = b_attackRange;

        // Initialize pathfinding grid (shared among all enemies)
        if (pathfindingGrid == null)
        {
            pathfindingGrid = new PathfindingGrid(gridCellSize, obstacleLayer);
        }

        StartCoroutine(OnStartCoroutine());
        WriteStats();
        if (SpotPlayerUponSpawn) ForceSpotPlayer();
    }

    IEnumerator OnStartCoroutine()
    {
        Color transparentBlack = new Color(0, 0, 0, 0);
        spriteRenderer.color = transparentBlack;

        float c = 0, d = 1;
        while (c < d)
        {
            spriteRenderer.color = Color.Lerp(transparentBlack, InitSpriteColor, c * 1.0f / d);
            c += Time.deltaTime;
            yield return null;
        }

        spriteRenderer.color = InitSpriteColor;
    }

    public void ForceSpotPlayer() => SpottedPlayer = FindObjectOfType<PlayerBase>();

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
            return Checkpoints[CurrentCheckpointIndex].transform.position;
        }
    }

    private void UpdatePathfinding()
    {
        Vector2 currentPos = transform.position;
        Vector2 desiredDestination = GetUniversalDestination();
        float distanceToDestination = Vector2.Distance(currentPos, desiredDestination);

        // Use pathfinding threshold for all movement types
        if (distanceToDestination < 50f)
        {
            isUsingPathfinding = false;
            currentPath.Clear();
            return;
        }

        // Check if there's a direct line to our desired destination
        Vector2 directionToDestination = (desiredDestination - currentPos).normalized;
        RaycastHit2D hit = Physics2D.Raycast(currentPos, directionToDestination, distanceToDestination, obstacleLayer);

        // Only skip pathfinding if we have completely clear path
        bool hasDirectPath = (hit.collider == null || colliders.Contains(hit.collider));

        // Additional check: ensure the direct path doesn't take us too close to other obstacles
        if (hasDirectPath)
        {
            // Sample points along the direct path to ensure it's truly safe
            int sampleCount = Mathf.Max(3, Mathf.RoundToInt(distanceToDestination / 30f));
            for (int i = 1; i < sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                Vector2 samplePoint = Vector2.Lerp(currentPos, desiredDestination, t);

                // Check for obstacles around this point
                if (Physics2D.OverlapCircle(samplePoint, 25f, obstacleLayer) != null)
                {
                    hasDirectPath = false;
                    break;
                }
            }
        }

        if (hasDirectPath)
        {
            isUsingPathfinding = false;
            currentPath.Clear();
            return;
        }

        // Determine what changed to decide if we need path updates
        Vector2 currentTargetPos = SpottedPlayer ? SpottedPlayer.transform.position : desiredDestination;

        // Need pathfinding - make updates more responsive
        bool shouldUpdatePath = Time.time - lastPathUpdateTime > pathUpdateInterval ||
                               Vector2.Distance(currentTargetPos, lastTargetPosition) > gridCellSize * 1.5f ||
                               Vector2.Distance(currentPos, lastPosition) > gridCellSize ||
                               currentPath.Count == 0;

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
                        Vector2 alternateTarget = OverridePosition + offset;
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

    private Vector2 GetPathfindingTarget()
    {
        if (!SpottedPlayer) return transform.position;

        Vector2 playerPos = SpottedPlayer.transform.position;
        Vector2 enemyPos = transform.position;

        switch (attackPattern)
        {
            case AttackPattern.MELEE:
                // For melee, path close to player but maintain minimum distance
                float distanceToPlayer = Vector2.Distance(enemyPos, playerPos);
                if (distanceToPlayer <= MinimumDistanceFromPlayer)
                {
                    return enemyPos; // Stop moving if too close
                }

                // Target a position slightly away from the player to avoid getting too close
                Vector2 dirToPlayer = (playerPos - enemyPos).normalized;
                return playerPos - dirToPlayer * (MinimumDistanceFromPlayer * 0.8f);

            case AttackPattern.RANGED:
                // For ranged, try to maintain distance
                bool PlayerIsNearby = DetectPlayer(DangerRange_RatioOfAttackRange * attackRange, false) != null;
                if (PlayerIsNearby)
                {
                    // Move away from player
                    Vector2 dirAwayFromPlayer = (enemyPos - playerPos).normalized;
                    return enemyPos + dirAwayFromPlayer * (attackRange * 0.8f);
                }
                else
                {
                    // Move to optimal attack range
                    dirToPlayer = (playerPos - enemyPos).normalized;
                    return playerPos - dirToPlayer * (attackRange * 0.7f);
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
                float distanceToWaypoint = Vector2.Distance(transform.position, currentWaypoint);

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

            // If we've reached the end of path, check if we still need pathfinding
            Vector2 finalTarget = GetUniversalDestination();
            Vector2 currentPos = transform.position;
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

        Vector2 currentPos = transform.position;
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

        // Stuck detection and handling
        Vector2 currentPos = transform.position;
        if (Vector2.Distance(currentPos, lastPosition) < stuckMovementThreshold)
        {
            stuckTimer += Time.fixedDeltaTime * 15f; // Account for MoveCnt skip
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
        Vector2 direction =  destination - transform.position;
        float distanceToDestination = direction.magnitude;

        // Stop if we're very close to destination
        if (distanceToDestination <= 20f) // Slightly increased
        {
            rb2d.velocity = Vector2.zero;
            animator.SetFloat("move", 0);
            return;
        }

        Vector2 finalDirection = direction.normalized;

        // Enhanced obstacle avoidance with corner handling
        finalDirection = GetAvoidanceDirection(finalDirection, distanceToDestination);

        rb2d.velocity = CalculateMovement(finalDirection);
        animator.SetFloat("move", 1);
    }

    private void HandleStuckState()
    {
        // Force path recalculation
        currentPath.Clear();
        currentWaypointIndex = 0;
        lastPathUpdateTime = 0f;
        stuckTimer = 0f;

        // Try a different approach - move slightly away from current position
        Vector2 randomDirection = UnityEngine.Random.insideUnitCircle.normalized;
        Vector2 escapeTarget = (Vector2) FeetPosition.position + randomDirection * gridCellSize * 2f;

        // Check if escape direction is clear
        RaycastHit2D escapeHit = Physics2D.Raycast(FeetPosition.position, randomDirection, gridCellSize * 2f, obstacleLayer);
        if (escapeHit.collider == null || colliders.Contains(escapeHit.collider))
        {
            rb2d.velocity = CalculateMovement(randomDirection);
        }
    }

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
                    if (distanceToTarget > 75f) // Lower threshold
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

    public void ScanPlayer()
    {
        SearchCnt++;
        if (SearchCnt < 15 || IsFrozen || IsStunned) return;

        SearchCnt = 0;

        bool spottedViaAlert = false;
        if (!SpottedPlayer)
        {
            var enemies = SearchForEntitiesAroundSelf(DetectionRange, typeof(EnemyBase), true);
            foreach (var e in enemies)
            {
                EnemyBase enemy = e as EnemyBase;

                if (!enemy || !enemy.IsAlive() || !enemy.SpottedPlayer) continue;
                RecentlyScannedPlayer = enemy.SpottedPlayer;
                spottedViaAlert = true;
                break;
            }

            if (!RecentlyScannedPlayer) RecentlyScannedPlayer = DetectPlayer();
        }
        else
        {
            RecentlyScannedPlayer =
                attackPattern == AttackPattern.MELEE
                ?
                DetectPlayer(DangerRange_RatioOfAttackRange * attackRange, true)
                :
                DetectPlayer();
        }

        if (!RecentlyScannedPlayer || !RecentlyScannedPlayer.IsAlive()) return;

        if (!SpottedPlayer)
        {
            if (!spottedViaAlert)
            {
                float distance = Vector3.Distance(RecentlyScannedPlayer.transform.position, transform.position);
                if (distance > Mathf.Max(110f, DetectionRange * 0.5f))
                {
                    var checkObstacle = Physics2D.Raycast(
                        transform.position,
                        (RecentlyScannedPlayer.transform.position - transform.position).normalized,
                        distance - 65f,
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

    public virtual void OnFirsttimePlayerSpot(bool viaAlert = false)
    {
        MoveToOverridePosition = false;
        MoveToOverridePositionJumpCnt = 0;
        FaceToward(SpottedPlayer.transform.position);
        IsGuarding = false;
        MovementLockout = 0;

        // Clear current path when spotting player
        currentPath.Clear();
        currentWaypointIndex = 0;
        isUsingPathfinding = false;

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

            if (target && !IsStunned && !IsFrozen)
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
            if (target && !IsStunned && !IsFrozen) DealDamage(target, atk);
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
        if (!collision || !enabled) return;

        if (collision.gameObject.CompareTag("Checkpoint") 
            && collision.gameObject == Checkpoints[CurrentCheckpointIndex].gameObject)
        {
            OnCheckpointReach();
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (!collision || !enabled) return;

        if (collision.gameObject.CompareTag("Checkpoint") 
            && collision.gameObject == Checkpoints[CurrentCheckpointIndex].gameObject)
        {
            OnCheckpointReach();
        }
    }

    protected virtual void OnCheckpointReach()
    {
        if (MoveToOverridePosition || (SpottedPlayer && SpottedPlayer.IsAlive())) return;

        if (WaitTimes[CurrentCheckpointIndex] > 0)
        {
            StopMovement();
            MovelockoutCoroutine = StartCoroutine(StartMovementLockout(WaitTimes[CurrentCheckpointIndex]));
        }

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
    }

    public void ChangeAggro(PlayerBase player)
    {
        if (!SpottedPlayer) return;
        SpottedPlayer = player;

        // Clear path when changing target
        currentPath.Clear();
        currentWaypointIndex = 0;
        isUsingPathfinding = false;
    }

    public virtual void WriteStats()
    {
        if (showTooltips && TooltipsPrefab) StartCoroutine(ShowTooltips());
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
            HoldTime = this.TooltipsHoldtime
        };
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !showPathGizmos) return;

        // Draw detection and attack ranges
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, DetectionRange);

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
            Gizmos.DrawLine(transform.position, GetPathfindingTarget());
        }
    }
}

