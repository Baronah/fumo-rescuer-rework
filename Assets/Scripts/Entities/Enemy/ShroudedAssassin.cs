using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

public class ShroudedAssassin : EnemyBase
{
    [SerializeField] private GameObject shadowClonePrefab;
    [SerializeField] private GameObject feudBond, atkUpEffect;
    [SerializeField] private float dashCooldown = 30f;
    [SerializeField] private float dashDuration = 0.1f;
    [SerializeField] private float dashInterval = 0.1f;
    [SerializeField] private float dashDistance = 600f;
    [SerializeField] private float dashAtkScale = 1f;

    public float TowardDeathHpTriggerThreshold = 0.3f;

    AssassinFeudBondObject feudBondScript;
    bool reviving = false;

    public Transform TowardDeathRestPort = null;

    public enum IllusionSpawnShape
    {
        RANDOM,
        STAR,
        TRIANGLE,
        LINE,
        LINE_EXACT,
        FIREWORK,
        INF,
        Z,
        BIG_STAR,
    };
    private float dashCooldownTimerCountdown = 20f;
    private int CurrentPhase => canRevive || reviving ? 1 : 2;

    private bool IsDashing => DashCoroutine != null || dashAttackCoroutine != null;
    private bool CanUseDash 
        => SpottedPlayer 
            && dashCooldownTimerCountdown <= 0f 
            && !IsAttackLocked 
            && CanAttack 
            && !IsDashing 
            && IsAlive();

    private short DashScanCnt = 0;
    CanvasGroup canvasGroup;
    SpriteRenderer shadowSpriteRend;
    Color shadowInitColor;

    short ToggleAttackUpEffectCnt = 5;
    public override void FixedUpdate()
    {
        base.FixedUpdate();

        ToggleAttackUpEffectCnt++;
        if (ToggleAttackUpEffectCnt >= 5)
        {
            ToggleAttackUpEffectCnt = 0;
            atkUpEffect.SetActive(
                AtkBuffs.ContainsKey(SilencerAtkBuffKey) && AtkBuffs[SilencerAtkBuffKey].Value >= MaxAtkBuff * 100f / 2
                );
        }

        if (dashCooldownTimerCountdown > 0)
        {
            float reduce = Time.fixedDeltaTime;
            if (TowardDeathTriggered) reduce *= 3f;
            else if (CurrentPhase == 2) reduce *= (1 + Mathf.Lerp(2f, 0f, health * 1.0f / mHealth));
            dashCooldownTimerCountdown -= reduce;
        }

        ProcessFeudBond();

        CheckForTowardDeath();
        if (TowardDeathTriggered) return;

        DashScanCnt++;
        if (DashScanCnt >= 25 && CanUseDash && IsAlive() && !reviving)
        {
            DashScanCnt = 0;
            if (SpottedPlayer && dashAttackCoroutine == null) dashAttackCoroutine = StartCoroutine(DashAttack());
        }
    }

    public override bool IsMoving()
    {
        return base.IsMoving() || IsDashing;
    }

    public override void InitializeComponents()
    {
        GameObject feudBondObj = Instantiate(feudBond, transform.position + new Vector3(0, 100, 0), Quaternion.identity);
        feudBondScript = feudBondObj.GetComponent<AssassinFeudBondObject>();

        base.InitializeComponents();

        canvasGroup = GetComponent<CanvasGroup>();
        shadowSpriteRend = ShadowSprite.GetComponent<SpriteRenderer>();
        shadowInitColor = shadowSpriteRend.color;
    }

    private PlayerBase PrevSpottedPlayer = null;
    private int FeudLevel = 0;
    void ProcessFeudBond()
    {
        if (!SpottedPlayer)
        {
            return;
        }

        if (SpottedPlayer != PrevSpottedPlayer) FeudLevel = 0;
        PrevSpottedPlayer = SpottedPlayer;

        if (feudBondScript)
        {
            feudBondScript.transform.position = SpottedPlayer.transform.position + new Vector3(0, 100, 0);
            feudBondScript.SetFeud(FeudLevel, MaxFeudLevel);
        }

        if (FeudLevel >= MaxFeudLevel)
        {
            ApplyEffect(Effect.AffectedStat.MSPD, "ASSASSIN_MARK_MSPD_BUFF", MaxMspdBuff * 100, 0.2f, true);
            if (CurrentPhase > 1) 
                SpottedPlayer.ApplyEffect(Effect.AffectedStat.MSPD, "ASSASSIN_MARKED_DEBUFF", -20f, 0.2f, true); 
        }
    }

    void AddFeud(EntityBase source)
    {
        if (!source || source != SpottedPlayer || FeudLevel >= MaxFeudLevel) return;
        FeudLevel++;

        ProcessFeudBond();
    }

    public int MaxFeudLevel = 20;
    public float MaxDamageReduction = 1f, MaxAtkBuff = 0.5f, MaxMspdBuff = 0.3f;
    public float AtkBuff_Jump = 0.05f;
    readonly string SilencerAtkBuffKey = "SILENCER_ATK_BUFF";
    public override void TakeDamage(DamageInstance damage, EntityBase source, ProjectileScript projectileInfo = null, bool IgnoreInvulnerability = false)
    {
        if (source && source == SpottedPlayer)
        {
            float ratio = Mathf.Lerp(0f, 1f, FeudLevel * 1.00f / MaxFeudLevel);
            float reduction = 1 - ratio * MaxDamageReduction;
            damage.Multiply(reduction);

            AddFeud(source);
        }
        else if (damage.TotalDamage > 0) damage.SetTotal(1);

        if (damage.TotalDamage > 0)
        {
            if (AtkBuffs.ContainsKey(SilencerAtkBuffKey))
                ApplyEffect(Effect.AffectedStat.ATK, SilencerAtkBuffKey, Mathf.Min(AtkBuffs[SilencerAtkBuffKey].Value + AtkBuff_Jump * 100f, MaxAtkBuff * 100f), 9999f, true);
            else
                ApplyEffect(Effect.AffectedStat.ATK, SilencerAtkBuffKey, AtkBuff_Jump * 100f, 9999f, true);

            dashCooldownTimerCountdown--;
        }

        base.TakeDamage(damage, source, projectileInfo, IgnoreInvulnerability);
    }

    public override void Move()
    {
        if (IsDashing || reviving || TowardDeathTriggered) return;
        base.Move();
    }

    public override IEnumerator Attack()
    {
        if (TowardDeathTriggered || IsDashing || reviving || !CanAttack || IsAttackLocked) yield break;
        yield return StartCoroutine(base.Attack());
        if (sfxs[0]) sfxs[0].Play();
    }

    private bool dashDoesDamage = true;
    private List<GameObject> IllusionsTransforms = new();

    Coroutine dashAttackCoroutine = null;
    IEnumerator DashAttack(IllusionSpawnShape shape = IllusionSpawnShape.RANDOM)
    {
        if (TowardDeathTriggered)
        {
            foreach (var item in colliders)
            {
                item.enabled = true;
            }

            spriteRenderer.color = InitSpriteColor;
            canvasGroup.alpha = 1;
        }

        IsShiftImmune = true;
        EndFreeze();
        EndStun();
        StopMovement();
        CancelAttack();

        if (shape == IllusionSpawnShape.RANDOM)
        {
            if (CurrentPhase == 1)
            {
                yield return DashCoroutine = StartCoroutine(UseDash(IllusionSpawnShape.LINE));
                yield return new WaitForSeconds(0.3f);
                yield return DashCoroutine = StartCoroutine(UseDash(IllusionSpawnShape.LINE_EXACT));
            }
            else if (TowardDeathTriggered)
            {
                if (Vector2.Distance(transform.position, SpottedPlayer.transform.position) >= 300f)
                {
                    yield return DashCoroutine = StartCoroutine(UseDash(IllusionSpawnShape.LINE));
                    yield return new WaitForSeconds(0.3f);
                    yield return DashCoroutine = StartCoroutine(UseDash(IllusionSpawnShape.RANDOM));
                }
                else
                {
                    IllusionSpawnShape[] shapesFirst = 
                    { 
                        IllusionSpawnShape.STAR, IllusionSpawnShape.FIREWORK, IllusionSpawnShape.INF, IllusionSpawnShape.BIG_STAR 
                    };

                    IllusionSpawnShape[] shapesAfter = 
                    { 
                        IllusionSpawnShape.Z, IllusionSpawnShape.LINE
                    };

                    yield return DashCoroutine = StartCoroutine(UseDash(shapesFirst[Random.Range(0, 1000) % shapesFirst.Length]));
                    yield return new WaitForSeconds(0.3f);
                    yield return DashCoroutine = StartCoroutine(UseDash(shapesAfter[Random.Range(0, 1000) % shapesAfter.Length]));
                }
            }
            else
            {
                IllusionSpawnShape thisShape = Vector2.Distance(transform.position, SpottedPlayer.transform.position) >= dashDistance * 0.7f ? 
                    IllusionSpawnShape.LINE : 
                    IllusionSpawnShape.RANDOM;
                yield return DashCoroutine = StartCoroutine(UseDash(thisShape));
                yield return new WaitForSeconds(0.3f);
                yield return DashCoroutine = StartCoroutine(UseDash(IllusionSpawnShape.LINE_EXACT));
            }
        }
        else
        {
            yield return DashCoroutine = StartCoroutine(UseDash(shape));
            yield return new WaitForSeconds(0.3f);
            yield return DashCoroutine = StartCoroutine(UseDash(IllusionSpawnShape.LINE_EXACT));
        }

        yield return new WaitForSeconds(0.1f);
        RemoveEffect(SilencerAtkBuffKey);

        dashCooldownTimerCountdown = dashCooldown;
        IsShiftImmune = false;
        DashCoroutine = dashAttackCoroutine = null;
    }

    Coroutine DashCoroutine = null;
    [SerializeField] float DashExpectedDistance = 500f; 
    IEnumerator UseDash(IllusionSpawnShape shape = IllusionSpawnShape.RANDOM)
    {
        if (!SpottedPlayer) yield break;
        if (IsFrozen || IsStunned)
        {
            OnDashInterrupted();
        }

        bool useRandomShape = shape == IllusionSpawnShape.RANDOM;

        if (useRandomShape) 
            shape = (IllusionSpawnShape) Random.Range(1, Enum.GetValues(typeof(IllusionSpawnShape)).Length);

        float healthRatio = CurrentPhase == 1 ? 1 : health * 1.0f / mHealth, 
              minThreshold = TowardDeathHpTriggerThreshold;

        if (TowardDeathTriggered) healthRatio = 0f;
        else healthRatio = Mathf.InverseLerp(minThreshold, 1f, healthRatio);

        float dashScaleDuration = Mathf.Lerp(dashDuration * 0.4f, dashDuration, healthRatio),
              dashScaleInt = Mathf.Lerp(dashInterval * 0.3f, dashInterval, healthRatio),
              prepTime = Mathf.Lerp(0.5f, 1f, healthRatio);

        if (FeudLevel >= MaxFeudLevel)
        {
            dashScaleDuration *= 0.75f;
            dashScaleInt *= 0.75f;
        }

        if (shape == IllusionSpawnShape.FIREWORK || shape == IllusionSpawnShape.BIG_STAR || PostReviveTrack)
        {
            dashScaleDuration /= 2;
            if (PostReviveTrack) dashScaleInt = 0f;
            else dashScaleInt /= 2;
        }

        if (!PostReviveTrack) IllusionsTransforms.Clear();

        Vector3 selfPos = transform.position;
        if (!PostReviveTrack)
        {
            SpawnIllusionShape(shape, selfPos);
        }
        PostReviveTrack = false;

        if (IllusionsTransforms.Count > 0) FaceToward(IllusionsTransforms.First().transform.position);
        animator.SetTrigger("skill_prep");
        if (sfxs[1]) sfxs[1].Play();

        CreateIllusionsTrails(prepTime + 0.3f);

        yield return new WaitForSeconds(prepTime);
        animator.SetTrigger("skill_cast");
        yield return new WaitForSeconds(0.32f);

        float c = 0;
        for (int i = 0; i < IllusionsTransforms.Count; ++i)
        {
            dashDoesDamage = true;
            GameObject illusion = IllusionsTransforms[i];
            Vector3 destination = illusion.transform.position;

            selfPos = transform.position;

            float distance = Vector3.Distance(selfPos, destination);
            float expectedDuration = Mathf.Max(dashScaleDuration / 2, distance / DashExpectedDistance * dashScaleDuration);

            c = 0;
            while (c < expectedDuration)
            {
                TryDamagingPlayer();
                transform.position = Vector3.Lerp(selfPos, destination, c * 1.0f / expectedDuration);

                if (Time.deltaTime > 0) SpawnIllusion();

                c += Time.deltaTime;
                yield return null;
            }

            TryDamagingPlayer();
            transform.position = illusion.transform.position;
            Destroy(illusion);

            if (i + 1 < IllusionsTransforms.Count) FaceToward(IllusionsTransforms[i + 1].transform.position);
            yield return new WaitForSeconds(dashScaleInt);
        }

        if (SpottedPlayer) FaceToward(SpottedPlayer.transform.position);

        EndDash();
    }

    void TryDamagingPlayer()
    {
        if (!dashDoesDamage) return;
        var player = SearchForNearestEntityAroundCertainPoint(typeof(PlayerBase), transform.position, 60f, true);
        if (player)
        {
            dashDoesDamage = false;
            DealDamage(player, (int)(atk * dashAtkScale));
        }
    }

    void SpawnIllusionShape(IllusionSpawnShape shape, Vector3 selfPos)
    {
        float range = Mathf.Clamp(Vector3.Distance(transform.position, SpottedPlayer.transform.position) * 2, dashDistance * 0.9f, dashDistance);
        Vector3 playerPos = SpottedPlayer.transform.position;
        switch (shape)
        {
            case IllusionSpawnShape.STAR:
                if (playerPos.x >= transform.position.x && playerPos.y <= transform.position.y)
                {
                    CreateIllusion(selfPos + new Vector3(range, 0));                          // o1
                    CreateIllusion(selfPos + new Vector3(range / 4, range / 2 * -1));          // o2
                    CreateIllusion(selfPos + new Vector3(range / 2, range / 2));               // o4
                    CreateIllusion(selfPos + new Vector3(range * 3 / 4, range / 2 * -1));      // o3
                }
                else if (playerPos.x < transform.position.x && playerPos.y <= transform.position.y)
                {
                    CreateIllusion(selfPos + new Vector3(range * -1, 0));                      // o1
                    CreateIllusion(selfPos + new Vector3(range / 4 * -1, range / 2 * -1));     // o2
                    CreateIllusion(selfPos + new Vector3(range / 2 * -1, range / 2));          // o4
                    CreateIllusion(selfPos + new Vector3(range * 3 / 4 * -1, range / 2 * -1)); // o3
                }
                else if (playerPos.x >= transform.position.x && playerPos.y > transform.position.y)
                {
                    CreateIllusion(selfPos + new Vector3(range * 3 / 4, range / 2));           // o2
                    CreateIllusion(selfPos + new Vector3(range / 4 * -1, range / 2));          // o1
                    CreateIllusion(selfPos + new Vector3(range / 2, 0));                       // o4
                    CreateIllusion(selfPos + new Vector3(range / 4, range));                   // o3
                }
                else
                {
                    CreateIllusion(selfPos + new Vector3(range * 3 / 4 * -1, range / 2));      // o1
                    CreateIllusion(selfPos + new Vector3(range / 4, range / 2));               // o2
                    CreateIllusion(selfPos + new Vector3(range / 2 * -1, 0));                  // o4
                    CreateIllusion(selfPos + new Vector3(range / 4 * -1, range));              // o3
                }

                CreateIllusion(selfPos); // o5
                break;

            case IllusionSpawnShape.TRIANGLE:
                float Dist = Vector2.Distance(selfPos, playerPos);

                if (playerPos.y > selfPos.y)
                {
                    if (Dist >= range / 2)
                    {
                        if (playerPos.x > selfPos.x)
                        {
                            CreateIllusion(selfPos + new Vector3(range, 0));          // o1
                            CreateIllusion(selfPos + new Vector3(range / 2, range));  // o2
                        }
                        else
                        {
                            CreateIllusion(selfPos + new Vector3(range / 2 * -1, range));
                            CreateIllusion(selfPos + new Vector3(range * -1, 0));
                        }
                    }
                    else
                    {
                        CreateIllusion(selfPos + new Vector3(range / 2, range));
                        CreateIllusion(selfPos + new Vector3(range / 2 * -1, range));
                    }
                }
                else
                {
                    if (Dist >= range / 2)
                    {
                        if (playerPos.x > selfPos.x)
                        {
                            CreateIllusion(selfPos + new Vector3(range * -1, 0));
                            CreateIllusion(selfPos + new Vector3(range / 2 * -1, range * -1));
                        }
                        else
                        {
                            CreateIllusion(selfPos + new Vector3(range / 2, range * -1));
                            CreateIllusion(selfPos + new Vector3(range, 0));
                        }
                    }
                    else
                    {
                        CreateIllusion(selfPos + new Vector3(range / 2 * -1, range * -1));
                        CreateIllusion(selfPos + new Vector3(range / 2, range * -1));
                    }
                }

                CreateIllusion(selfPos); // o3
                break;

            case IllusionSpawnShape.FIREWORK:
                CreateIllusion(selfPos + new Vector3(-range, 0));
                CreateIllusion(selfPos);
                CreateIllusion(selfPos + new Vector3(-range / 2, range / 2));
                CreateIllusion(selfPos);
                CreateIllusion(selfPos + new Vector3(0, range));
                CreateIllusion(selfPos);
                CreateIllusion(selfPos + new Vector3(range / 2, range / 2));
                CreateIllusion(selfPos);
                CreateIllusion(selfPos + new Vector3(range, 0));
                CreateIllusion(selfPos);
                CreateIllusion(selfPos + new Vector3(range / 2, -range / 2));
                CreateIllusion(selfPos);
                CreateIllusion(selfPos + new Vector3(0, -range));
                CreateIllusion(selfPos);
                CreateIllusion(selfPos + new Vector3(-range / 2, -range / 2));
                CreateIllusion(selfPos);
                break;

            case IllusionSpawnShape.BIG_STAR:
                CreateIllusion(selfPos + new Vector3(-range, 0));
                CreateIllusion(selfPos + new Vector3(-range / 4, range / 4));
                CreateIllusion(selfPos + new Vector3(0, range));
                CreateIllusion(selfPos + new Vector3(range / 4, range / 4));
                CreateIllusion(selfPos + new Vector3(range, 0));
                CreateIllusion(selfPos + new Vector3(range / 4, -range / 4));
                CreateIllusion(selfPos + new Vector3(0, -range));
                CreateIllusion(selfPos + new Vector3(-range / 4, -range / 4));
                CreateIllusion(selfPos + new Vector3(-range, 0));
                CreateIllusion(selfPos);
                break;

            case IllusionSpawnShape.INF:
                CreateIllusion(selfPos + new Vector3(range / 2 * -1, range / 2));
                CreateIllusion(selfPos + new Vector3(range * -1, 0));
                CreateIllusion(selfPos + new Vector3(range / 2 * -1, range / 2 * -1));
                CreateIllusion(selfPos + new Vector3(range / 2, range / 2));
                CreateIllusion(selfPos + new Vector3(range, 0));
                CreateIllusion(selfPos + new Vector3(range / 2, range / 2 * -1));
                CreateIllusion(selfPos);
                break;

            case IllusionSpawnShape.Z:
                if (playerPos.y < selfPos.y)
                {
                    if (playerPos.x <= selfPos.x)
                    {
                        CreateIllusion(selfPos + new Vector3(range * -1, range / 4 * -1));
                        CreateIllusion(selfPos + new Vector3(0, range / 2 * -1));
                        CreateIllusion(selfPos + new Vector3(range * -1, range * 3 / 4 * -1));
                        if (Random.Range(0, 100) % 2 == 0) CreateIllusion(selfPos + new Vector3(0, range * -1));
                    }
                    else
                    {
                        CreateIllusion(selfPos + new Vector3(range, range / 4 * -1));
                        CreateIllusion(selfPos + new Vector3(0, range / 2 * -1));
                        CreateIllusion(selfPos + new Vector3(range, range * 3 / 4 * -1));
                        if (Random.Range(0, 100) % 2 == 0) CreateIllusion(selfPos + new Vector3(0, range * -1));
                    }
                }
                else
                {
                    if (playerPos.x <= selfPos.x)
                    {
                        CreateIllusion(selfPos + new Vector3(range * -1, range / 4));
                        CreateIllusion(selfPos + new Vector3(0, range / 2));
                        CreateIllusion(selfPos + new Vector3(range * -1, range * 3 / 4));
                        if (Random.Range(0, 100) % 2 == 0) CreateIllusion(selfPos + new Vector3(0, range));
                    }
                    else
                    {
                        CreateIllusion(selfPos + new Vector3(range, range / 4));
                        CreateIllusion(selfPos + new Vector3(0, range / 2));
                        CreateIllusion(selfPos + new Vector3(range, range * 3 / 4));
                        if (Random.Range(0, 100) % 2 == 0) CreateIllusion(selfPos + new Vector3(0, range));
                    }
                }
                break;

            case IllusionSpawnShape.LINE:
                CreateIllusion(playerPos + (transform.position - playerPos).normalized * -250f);
                break;

            case IllusionSpawnShape.LINE_EXACT:
                CreateIllusion(playerPos);
                break;
        }
    }

    void EndDash(bool interrupted = false)
    {
        if (!IsDashing) return;

        dashCooldownTimerCountdown = dashCooldown;
        PostReviveTrack = false;

        if (interrupted)
        {
            foreach (var illusion in IllusionsTransforms)
            {
                if (illusion) Destroy(illusion);
            }
        }
        else
        {
            animator.SetTrigger("skill_end");
        }

        IllusionsTransforms.Clear();

        dashDoesDamage = false;
        DashCoroutine = null;
    }

    public override void OnFreezeEnter()
    {
        base.OnFreezeEnter();

        if (IsDashing || DashCoroutine != null || dashAttackCoroutine != null)
        {
            OnDashInterrupted();
        }
    }

    public override void OnStunEnter()
    {
        base.OnStunEnter(); 
        
        if (IsDashing || DashCoroutine != null || dashAttackCoroutine != null)
        {
            OnDashInterrupted();
        }
    }

    void OnDashInterrupted()
    {
        if (IsDashing)
        {
            if (dashAttackCoroutine != null) StopCoroutine(dashAttackCoroutine);
            if (DashCoroutine != null)
            {
                StopCoroutine(DashCoroutine);
                animator.SetTrigger("interrupt");
            }

            EndDash(true);
        }

        if (!TowardDeathTriggered) dashCooldownTimerCountdown = 0;

        DashCoroutine = dashAttackCoroutine = null;
        IsShiftImmune = false;

        if (TowardDeathTriggered)
        {
            EndFreeze();
            EndStun();

            IsFreezeImmune = IsStunImmune = true;
        }
    }

    void CreateIllusion(Vector3 position)
    {
        GameObject illusion = Instantiate(shadowClonePrefab, position, Quaternion.identity);
        IllusionsTransforms.Add(illusion);
    }

    [HideInInspector] public bool TowardDeathTriggered = false;
    IEnumerator ActivateVanishAndDashTowardPlayer()
    {
        if (TowardDeathTriggered) yield break;

        StopMovement();
        EndFreeze();
        EndStun();

        TowardDeathTriggered = true;
        
        dashCooldownTimerCountdown = dashCooldown;
        dashAtkScale += 0.15f;
        defPen += 33;

        if (sfxs[2]) sfxs[2].Play();

        yield return StartCoroutine(Vanish(3f));
        dashCooldownTimerCountdown = dashCooldown - 3f;

        yield return new WaitForSeconds(2f);

        float ReappearTime = 0.5f, minReappearTime = 0.35f;
        while (true)
        {
            ApplyEffect(Effect.AffectedStat.ATK, SilencerAtkBuffKey, MaxAtkBuff * 100f, 9999f, true);
            yield return new WaitUntil(() => dashCooldownTimerCountdown <= 0 && SpottedPlayer);

            float minOffset = Random.Range(100f, 400f);
            Vector3 randomDirection = new Vector3(Random.Range(-1000, 1000), Random.Range(-1000, 1000)).normalized;
            Vector3 appearSpot = SpottedPlayer.transform.position
                    + randomDirection * minOffset;
            transform.position = appearSpot;
            if (SpottedPlayer) FaceToward(SpottedPlayer.transform.position);

            Color appearColor = new(InitSpriteColor.r, InitSpriteColor.g, InitSpriteColor.b, 0.25f);
            
            float c = 0, d = Mathf.Max(ReappearTime, minReappearTime);
            ReappearTime -= 0.05f;

            while (c < d)
            {
                shadowSpriteRend.color = Color.Lerp(Color.clear, shadowInitColor, c * 1.0f / d);
                spriteRenderer.color = Color.Lerp(spriteRenderer.color, appearColor, c * 1.0f / d);
                c += Time.deltaTime;
                yield return null;
            }

            shadowSpriteRend.color = shadowInitColor;

            if (SpottedPlayer)
            {
                IsFreezeImmune = IsStunImmune = false;
                dashAttackCoroutine = StartCoroutine(DashAttack());
                while (dashAttackCoroutine != null) yield return null;
            }
            else dashCooldownTimerCountdown = dashCooldown;

            yield return StartCoroutine(Vanish());
        }
    }

    [SerializeReference] GameObject trailPrefab;
    void CreateIllusionsTrails(float persist)
    {
        List<GameObject> TargetPoints = new(IllusionsTransforms);
        TargetPoints.Insert(0, this.gameObject);

        for (int i = 0; i < TargetPoints.Count - 1; ++i)
        {
            Vector3 currentIllusionPos = TargetPoints[i].transform.position;
            Vector3 nextIllusionPos = TargetPoints[i + 1].transform.position;

            // Correct midpoint
            Vector3 trailSpawnPos = (currentIllusionPos + nextIllusionPos) / 2f;

            // Correct rotation (for UI, aligning up-axis to the direction)
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, nextIllusionPos - currentIllusionPos);

            GameObject trail = Instantiate(trailPrefab, trailSpawnPos, rotation, TargetPoints[i].transform);
            float scale = 1.0f / TargetPoints[i].transform.localScale.x;
            trail.transform.localScale = new(scale, scale);

            Image trailImg = trail.GetComponentInChildren<Image>();

            // Resize trail length
            float distance = Vector3.Distance(currentIllusionPos, nextIllusionPos);
            trailImg.GetComponent<RectTransform>().sizeDelta = new Vector2(85, distance);

            if (trail) Destroy(trail, persist);
        }
    }

    public override void OnDeath()
    {
        foreach (var illusion in IllusionsTransforms)
        {
            if (illusion) Destroy(illusion);
        }
        IllusionsTransforms.Clear();
        base.OnDeath();
    }

    public float Revive_PlayerPosStoreInterval = 1.5f;
    bool PostReviveTrack = false;
    public override IEnumerator Revive()
    {
        EndDash(true);
        EndFreeze();
        EndStun();
        ClearAllEffects();
        canRevive = false;
        reviving = true;

        foreach (var item in colliders)
        {
            item.enabled = false;
        }

        dashCooldown = 11f;
        dashCooldownTimerCountdown = 0f;
        animator.SetBool("attack", false);

        float lockoutDuration = reviveDuration + postReviveDuration + 2f;
        animator.SetTrigger("die");
        health = 1;
        StartCoroutine(StartMovementLockout(lockoutDuration));
        StartCoroutine(StartAttackLockout(lockoutDuration));
        SetInvulnerable(lockoutDuration);

        PlayerBase currentPlayer = SpottedPlayer;

        yield return new WaitForSeconds(1f);

        float c = 0, fillInterval = 0, revive_StoreCount = 0;
        while (c < reviveDuration)
        {
            health = (int)Mathf.Lerp(1, mHealth, c * 1.0f / reviveDuration);
            SetHealth(health);
            c += Time.deltaTime;
            fillInterval += Time.deltaTime;
            revive_StoreCount += Time.deltaTime;

            if (fillInterval >= 0.15f && currentPlayer && currentPlayer == SpottedPlayer)
            {
                fillInterval = 0;
                AddFeud(currentPlayer);
            }

            if (revive_StoreCount >= Revive_PlayerPosStoreInterval && SpottedPlayer)
            {
                revive_StoreCount = 0;
                SpawnIllusionFollowsPlayerMovement();
            }

            yield return null;
        }

        SpawnIllusionFollowsPlayerMovement();

        reviving = false;
        animator.SetTrigger("revive");
        health = mHealth;
        SetHealth(health);

        yield return new WaitForSeconds(0.5f);

        StartCoroutine(AutoBuildupFeud());

        PostReviveTrack = true;
        StartCoroutine(DashAttack());

        yield return new WaitForSeconds(0.5f);

        foreach (var item in colliders)
        {
            item.enabled = true;
        }
    }

    void SpawnIllusionFollowsPlayerMovement()
    {
        if (!SpottedPlayer) return;

        Vector3 playerMvm = SpottedPlayer.GetMovementDirection();
        if (playerMvm == Vector3.zero) playerMvm = SpottedPlayer.GetSpriteRenderer().flipX ? Vector3.left : Vector3.right;

        Vector3 position = SpottedPlayer.transform.position + playerMvm * 200f;
        CreateIllusion(position);
    }

    void CheckForTowardDeath()
    {
        if (TowardDeathTriggered) return;
        if (IsDashing || attacking || CurrentPhase != 2 || health >= mHealth * TowardDeathHpTriggerThreshold) return;

        StartCoroutine(ActivateVanishAndDashTowardPlayer());
    }

    IEnumerator Vanish(float d = 1f)
    {
        EndFreeze();
        EndStun();
        StopMovement();
        foreach (var cld in colliders) cld.enabled = false;
        IsShiftImmune = true;

        Color end = new(InitSpriteColor.r, InitSpriteColor.g, InitSpriteColor.b, 0);
        float c = 0;

        while (c < d)
        {
            spriteRenderer.color = Color.Lerp(InitSpriteColor, end, c * 1.0f / d);
            shadowSpriteRend.color = Color.Lerp(shadowInitColor, Color.clear, c * 1.0f / d);

            canvasGroup.alpha = Mathf.Lerp(1, 0, c * 1.0f / d);
            c += Time.deltaTime;
            yield return null;
        }

        spriteRenderer.color = end;
        shadowSpriteRend.color = Color.clear;
        canvasGroup.alpha = 0;

        if (TowardDeathRestPort) transform.position = TowardDeathRestPort.position;
    }

    IEnumerator AutoBuildupFeud()
    {
        float interval = 1f;
        while (isActiveAndEnabled && IsAlive())
        {
            yield return new WaitForSeconds(interval);
            if (SpottedPlayer && CurrentPhase == 2)
            {
                AddFeud(SpottedPlayer);
            }
        }
    }

    void SpawnIllusion()
    {
        GameObject Illusion = Instantiate(shadowClonePrefab, transform.position, Quaternion.identity);
        SpriteRenderer IllusionSpriteRenderer = Illusion.GetComponentInChildren<SpriteRenderer>();
        IllusionSpriteRenderer.sprite = spriteRenderer.sprite;
        IllusionSpriteRenderer.flipX = spriteRenderer.flipX;
        IllusionSpriteRenderer.color = new Color(1, 1, 1, 0.5f);
        Destroy(Illusion, 0.2f);
    }

    public override void WriteStats()
    {
        Description = "Assassin who has abandoned his name and covered his face. Behind that pall is a burning fanaticism and a destined fate.";
        
        Skillset =
            $"• Any damage received that is not from the target for [Feud Bonding] is greatly reduced.\n\n" +
            $"• [Feud Bonding] Receiving damage from the player unit builds up \"Feud\" on them (has a limit). Self takes reduced damage from the attacker based on how much \"Feud\" they have built up, up to {MaxDamageReduction * 100}%. " +
            "Gains greatly increased MSPD when the target has their \"Feud\" maxed out.\n\n" +
            "• [Silencer] Receving damage increases ATK (stacks to a limit) and shortens the cool-down of the next [Pack Up] and [Wrapped Shroud] use. " +
            "After using [Pack Up] or [Wrapped Shroud], follows with a dash toward the player position and resets the ATK buff.\n\n" +
            
            "<b><color=red>FIRST PHASE</color></b>\n\n" +
            "• [Pack Up] Stops moving, creates an illusion behind the player then dashes toward it, " +
            "dealing physical damage if collides. Cool-down is refunded if interrupted.\n\n" +
            "• [Held Breath] When HP reaches 0, enters a revival state: Gradually recovers HP to full " +
            "while continuously builds up \"Feud\" and spawns illusion at the player position throughout the duration. " +
            "When finished, cast [Pack Up] on them. Enters the second phase afterward.\n\n" +
            
            "<b><color=red>SECOND PHASE</color></b>\n\n" +
            "• [Overflowing Hatred] \"Feud\" is now gradually builds up overtime, and 'slows' the player at max stacks.\n\n" +
            "• [Wrapped Shroud] Stops moving, creates multiple illusion of self then dashes toward them in quick succession, " +
            "damages the player if collides. Dash speed increases as HP decreases. Cool-down is refunded if interrupted.\n\n" +
            $"• [Toward Death] When HP falls below {TowardDeathHpTriggerThreshold * 100}% for the first time: Stops attacking and becomes 'Invisible'. " +
            "Periodically reappears to cast [Wrapped Shroud], disables 'Invisibility' during the process and resumes them after finished. " +
            "When inflicted by stun or freeze while casting [Wrapped Shroud], both the skill and that effect will be prematurely ended.";
        
        TooltipsDescription = "Assassin who has abandoned his name and covered his face. Behind that pall is a burning fanaticism and a destined fate.";
        base.WriteStats();
    }
}
