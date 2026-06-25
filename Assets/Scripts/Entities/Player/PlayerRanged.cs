using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static LevelDifficultyModifier;
using static PlayerManager;

public class PlayerRanged : PlayerBase
{
    [SerializeField] private GameObject AttackRangeIndicator, Warning, EffectsParent, SkillEffect, FreezeEffect, FreezeRing, FreezeMaintRing, JuggEffect;

    [SerializeField] private Transform SkillPosition;
    [SerializeField] private float SkillCooldown = 30f;
    [SerializeField] private float SkillDuration = 7f;
    [SerializeField] private float Skill_DamageMulitplier = 0.25f;
    [SerializeField] private float Skill_AtkInterval = 0.25f;
    [SerializeField] private float Skill_ProjLifeSpan = 1.8f;
    [SerializeField] private GameObject SkillBarObj;
    private Slider SkillBar;

    [SerializeField] private float FreezeRange = 800f;
    [SerializeField] private float FreezeDurationMin = 1f, FreezeDurationMax = 4f, MinDistanceForFreezeDuration = 150f;
    [SerializeField] private float FreezeCooldown = 11.5f;
    [SerializeField] private float FreezeCastDuration = 0.25f;

    [SerializeField] private GameObject IllusionPrefab;

    [SerializeField] private AudioSource SkillSfx;
    AudioSource ProjectileEnviEnhanceSfx => sfxs[4];
    AudioSource ProjectileBounceSfx => sfxs[5];

    private bool CanUseSkill = true, IsSkillActive = false, CanUseFreeze = true;

    private RectTransform AttackRangeIndicatorRect;

    private EntityBase target;
    private float skillCurrentDuration;

    public override void Start()
    {
        base.Start();
        AttackRangeIndicatorRect = AttackRangeIndicator.GetComponent<RectTransform>();
    }

    short fixedUpdateCnt = 0;
    public override void FixedUpdate()
    {
        BounceSfxTimeLockout -= Time.fixedUnscaledDeltaTime;
        EnhanceSfxTimeLockout -= Time.fixedUnscaledDeltaTime;

        if (Time.timeScale <= 0) return;
        base.FixedUpdate();

        fixedUpdateCnt++;
        if (fixedUpdateCnt < 5) return;
        fixedUpdateCnt = 0;

        if (!IsAlive())
        {
            AttackRangeIndicator.SetActive(false);
        }

        if (AttackRangeIndicatorRect)
        {
            AttackRangeIndicatorRect.sizeDelta = new Vector2(
                attackRange * 2,
                attackRange * 2
            );
        }

        LockIn.gameObject.SetActive(IsSkillActive && lockInTarget && lockInTarget.IsAlive());
        if (LockIn.gameObject.activeSelf) LockIn.transform.position = lockInTarget.transform.position;

        bool alive = IsAlive();
        bool SkillActive = IsSkillActive && alive, FreezeActive = IsFreezeActive && alive;
        SkillEffect.SetActive(SkillActive);
        FreezeEffect.SetActive(FreezeActive);
        SkillBarObj.SetActive(SkillActive);

        SkillBarObj.transform.localPosition =
            WindanthemBar.activeSelf ? new Vector3(-0.08f, -3.2f, 0) : new Vector3(-0.08f, -2.3f, 0);

        ccBar.transform.localPosition =
            SkillBarObj.activeSelf ? SkillBarObj.transform.localPosition + new Vector3(0, -0.9f, 0) : new Vector3(-0.08f, -2.3f, 0);
    }

    public override PlayerManager.PlayerType GetPlayerType()
    {
        return PlayerManager.PlayerType.RANGED;
    }

    public override void InitializeComponents()
    {
        SkillBar = SkillBarObj.GetComponentInChildren<Slider>();
        base.InitializeComponents();
    }

    public override void OnFieldSwapOut(PlayerBase swapInPlayer)
    {
        base.OnFieldSwapOut(swapInPlayer);
        SpawnIllusion();
        if (freezeMaintRing) Destroy(freezeMaintRing);
    }

    [SerializeField] float PhantomAtkInherit = 0.5f;
    void SpawnIllusion()
    {
        if (!Skills.Contains(SkillTree_Manager.SkillName.SPIRAL_SHADOW) || !IsSkillActive) return;

        // inherits stats and skill duration and start casting skill on time for remaining duration
        GameObject o = Instantiate(IllusionPrefab, transform.position, Quaternion.identity);
        PlayerCasterIllusion playerCasterIllusion = o.GetComponent<PlayerCasterIllusion>();
        playerCasterIllusion.InitializeComponents();
        playerCasterIllusion.SetInherit(
            ATK: (short)(atk * PhantomAtkInherit),
            maxDuration: SkillDuration,
            duration: skillCurrentDuration,
            Skill_DamageMulitplier,
            GetSkillFiringInterval(),
            Skills.Contains(SkillTree_Manager.SkillName.SPIRAL_FIELD_EXPERT),
            playerManager,
            Skill_ProjLifeSpan,
            flipX: spriteRenderer.flipX);
    }

    public override void FlipAttackPosition()
    {
        base.FlipAttackPosition();
        SkillPosition.localPosition = new Vector3(
            -SkillPosition.localPosition.x,
            SkillPosition.localPosition.y,
            SkillPosition.localPosition.z
        );

        EffectsParent.transform.localPosition = new Vector3(
            -EffectsParent.transform.localPosition.x,
            EffectsParent.transform.localPosition.y,
            EffectsParent.transform.localPosition.z
        );
    }

    public override void GetSkillTreeEffects()
    {
        short diff = (short)(CharacterPrefabsStorage.DifficultyLevel - 1);
        if (diff + 1 == (int) DiffType.Observer)
        {
            FreezeCooldown *= 0.6f;
            SkillCooldown *= 0.6f;
        }
        else if (diff == (int) DiffType.PlayerCD_1)
        {
            FreezeCooldown *= 1.5f;
            SkillCooldown *= 1.25f;
        }
        else if (diff >= (int) DiffType.PlayerCD_2)
        {
            FreezeCooldown *= 2f;
            SkillCooldown *= 1.5f;
        }

        base.GetSkillTreeEffects();
        if (Skills.Contains(SkillTree_Manager.SkillName.WINGED_STEPS_C))
        {
            FreezeCooldown *= 0.85f;
            SkillCooldown *= 0.85f;
        }

        if (Skills.Contains(SkillTree_Manager.SkillName.FREEZE_BLOOM))
        {
            FreezeDurationMin += 0.5f;
        }

        if (Skills.Contains(SkillTree_Manager.SkillName.SPIRAL_MORE))
            Skill_DamageMulitplier *= 0.75f;

        if (Skills.Contains(SkillTree_Manager.SkillName.HAIR_RIBBON) && CharacterPrefabsStorage.startingPlayer == PlayerManager.PlayerType.RANGED)
        {
            SkillCooldown *= 0.9f;
        }
    }

    public override void OnFieldEnter()
    {
        base.OnFieldEnter();

        if (Skills.Contains(SkillTree_Manager.SkillName.SWAP_START_SPECIAL))
        {
            Debut = true;
            UseSpecial();
        }
    }

    public override void Action_Skill()
    {
        if (!IsAlive()) return;

        if (Skills.Contains(SkillTree_Manager.SkillName.KNOTS) && !playerManager.hasVowed)
        {
            MakeVow(PlayerManager.SkillType.ULTIMATE);
        }

        if (SkillCoroutine == null) UseSkill();
        else CancelSkill();
    }

    protected override void GetVow()
    {
        var skill = playerManager.GetVowSkill(this);
        if (skill == SkillType.NONE) return;

        if (skill == SkillType.SPECIAL)
        {
            FreezeCooldown *= 0.8f;
            FreezeDurationMax *= 1.4f;
            FreezeDurationMin += 0.5f;
            FreezeRange += 150f;
            MinDistanceForFreezeDuration += 75f;

            ApplyEffect(Effect.AffectedStat.ATK, "VOW_ATK_BUFF", 15f, 9999f, true, EffectPersistType.PERSIST, false);
            ApplyEffect(Effect.AffectedStat.ASPD, "VOW_ASPD_BUFF", 15f, 9999f, false, EffectPersistType.PERSIST, false);
            ApplyEffect(Effect.AffectedStat.ARNG, "VOW_ARNG_BUFF", 50f, 9999f, false, EffectPersistType.PERSIST, false);

            FreezeChargeCDRefund += 0.03f;
            FreezeConductDebuff += 20f;
            FreezeHoldMax += 1f;
        }
        else
        {
            SkillCooldown *= 0.67f;
            SkillDuration += 1;
            Skill_AtkInterval *= 0.8f;
            Skill_DamageMulitplier *= 1.2f;
            Skill_ProjLifeSpan += 0.5f;

            ApplyEffect(Effect.AffectedStat.MSPD, "VOW_MSPD_BUFF", 12f, 9999f, true, EffectPersistType.PERSIST, false);
            ApplyEffect(Effect.AffectedStat.RES, "VOW_RES_BUFF", 15f, 9999f, false, EffectPersistType.PERSIST, false);
            ApplyEffect(Effect.AffectedStat.DEF, "VOW_DEF_BUFF", 10f, 9999f, false, EffectPersistType.PERSIST, false);

            SP_SkillMaxRefund = 0.98f;
            MaxRefund = 0.9f;

            MaxExtraFlowers++;
            FlowerMaxDuration += 1f;

            lockInDamageFalloff -= 0.003f;
            lockInDamageMulMin += 0.1f;

            PhantomAtkInherit += 0.2f;
        }
    }

    public override void UseSkill()
    {
        if (!CanUseSkill || IsFreezeActive || playerManager.RangedSealSkill == PlayerManager.SkillType.ULTIMATE) return;

        base.UseSkill();
        SkillCoroutine = StartCoroutine(CastSkill());
    }

    void CancelSkill()
    {
        if (!IsSkillActive || SkillCoroutine == null) return;

        if (Skills.Contains(SkillTree_Manager.SkillName.SPIRAL_SHADOW)) SpawnIllusion();
        else if (Skills.Contains(SkillTree_Manager.SkillName.SPIRAL_READ)) RefundSkill();

        if (SkillSfx && SkillSfx.isPlaying) SkillSfx.Stop();
        
        StopCoroutine(SkillCoroutine);
        SkillCoroutine = null;

        animator.SetTrigger("skill_end");
        IsSkillActive = false;
    }

    [SerializeField] float 
                            SP_SkillMaxRefund = 0.9f, 
                            SP_MinWinForRefundMax = 0.25f,
                            MaxRefund = 0.8f, 
                            MinWindowForMaxRefund = 0.5f;
    void RefundSkill()
    {
        float refundPercentage;
        
        float currentDuration = skillCurrentDuration;
        if (currentDuration < SP_MinWinForRefundMax) refundPercentage = SP_SkillMaxRefund;
        else if (currentDuration <= MinWindowForMaxRefund) refundPercentage = MaxRefund;
        else
        {
            refundPercentage = Mathf.Lerp(MaxRefund, 0f, currentDuration * 1.0f / SkillDuration);
        }

        float refundTime = SkillCooldown * refundPercentage;
        ReduceUltimateCooldown(refundTime);
    }

    public override void ReduceSpecialCooldown(float amount, CooldownReductionType reductionType = CooldownReductionType.FLAT)
    {
        if (!IsAlive()) return;
        if (freezeCooldownTimer >= FreezeCooldown) return;

        float reductionAmount = reductionType switch
        {
            CooldownReductionType.FLAT => amount,
            CooldownReductionType.PERCENTAGE_FULL => FreezeCooldown * amount,
            CooldownReductionType.PERCENTAGE_CURRENT => (FreezeCooldown - freezeCooldownTimer) * amount,
            _ => amount,
        };

        freezeCooldownTimer += reductionAmount;

        if (freezeCooldownTimer > FreezeCooldown) freezeCooldownTimer = FreezeCooldown;
        StartCoroutine(playerManager.SpecialCooldown(FreezeCooldown, freezeCooldownTimer));
    }

    public override void ReduceUltimateCooldown(float amount, CooldownReductionType reductionType = CooldownReductionType.FLAT)
    {
        if (!IsAlive()) return;
        if (skillCooldownTimer >= SkillCooldown) return;

        float reductionAmount = reductionType switch
        {
            CooldownReductionType.FLAT => amount,
            CooldownReductionType.PERCENTAGE_FULL => SkillCooldown * amount,
            CooldownReductionType.PERCENTAGE_CURRENT => (SkillCooldown - skillCooldownTimer) * amount,
            _ => amount,
        };

        skillCooldownTimer += reductionAmount;

        if (skillCooldownTimer > SkillCooldown) skillCooldownTimer = SkillCooldown;
        StartCoroutine(playerManager.SkillCooldown(SkillCooldown, skillCooldownTimer));
    }

    public override void UseSpecial()
    {
        if (!CanUseFreeze || playerManager.RangedSealSkill == PlayerManager.SkillType.SPECIAL) return; 
        if (IsSkillActive) CancelSkill();

        base.UseSpecial();
        StartCoroutine(CastFreeze());
    }

    public override void Move()
    {
        if (IsMovementLocked || IsFreezeActive) return;
        if (IsSkillActive && InputManager.Instance.GetMovementInput().magnitude > 0) CancelSkill();

        base.Move();
    }

    float skillCooldownTimer = 9999f;
    protected override IEnumerator UltimateLockout()
    {
        StartCoroutine(base.UltimateLockout());

        CanUseSkill = false;
        skillCooldownTimer = 0f;

        StartCoroutine(playerManager.SkillCooldown(SkillCooldown, skillCooldownTimer));
        while (skillCooldownTimer < SkillCooldown)
        {
            skillCooldownTimer += Time.deltaTime;
            yield return null;
        }
        CanUseSkill = true;
    }

    float freezeCooldownTimer = 9999f;
    protected override IEnumerator SpecialLockout()
    {
        StartCoroutine(base.SpecialLockout());

        freezeCooldownTimer = Debut ? FreezeCooldown : 0f;

        CanUseFreeze = false;
        StartCoroutine(playerManager.SpecialCooldown(FreezeCooldown, freezeCooldownTimer));

        while (freezeCooldownTimer < FreezeCooldown)
        {
            freezeCooldownTimer += Time.deltaTime;
            yield return null;
        }
        CanUseFreeze = true;
    }

    public override IEnumerator Attack()
    {
        if (!CanAttack || IsAttackLocked || IsFreezeActive) yield break;
        if (IsSkillActive) CancelSkill();

        AttackRangeIndicator.SetActive(true);
        target = SearchForNearestEntityAroundCertainPoint(typeof(EnemyBase), transform.position, attackRange);
        if (!target)
        {
            Warning.SetActive(true);
            yield return new WaitForSeconds(1f);
            Warning.SetActive(false);
            AttackRangeIndicator.SetActive(false);
            yield break;
        }

        MovementLockout = Mathf.Max(MovementLockout, GetWindupTime() * 1.5f);
        animator.SetBool("attack", true);
        LockoutMovementOnAttackCoroutine = StartCoroutine(LockoutMovementsOnAttack());

        yield return new WaitForSeconds(GetWindupTime());

        AttackRangeIndicator.SetActive(false);
        yield return null;
    }

    public override IEnumerator OnAttackComplete()
    {
        if (!target) yield break;

        if (sfxs[0]) sfxs[0].Play();

        int currentAtk = atk;
        float atkPostBonus = atk;
        if (Skills.Contains(SkillTree_Manager.SkillName.HEAVY_HITTER))
        {
            atkPostBonus *= GetHeavyHitterMultiplier();
            timerSinceLastAttack = 0f;
        }

        atk = (short)atkPostBonus;
        CreateProjectileAndShootToward(target, ProjectileType, ProjectileSpeed);
        target = null;

        atk = (short)currentAtk;

        if (Skills.Contains(SkillTree_Manager.SkillName.WINGED_STEPS_A))
        {
            GetWingedStepMspdBuff();
        }
    }

    bool IsFreezeActive = false;
    GameObject freezeMaintRing = null;
    [SerializeField] float FreezeChargeCDRefund = 0.15f, 
                           FreezeConductDebuff = 50f,
                           FreezeHoldMax = 5f;
    [SerializeField] GameObject SuperConnductUI;
    public IEnumerator CastFreeze()
    {
        if (!IsAlive() || !CanUseFreeze || IsFrozen || IsStunned) yield break;

        IsFreezeActive = true;

        StartCoroutine(SpecialLockout());
        Debut = false;

        StartCoroutine(StartMovementLockout(FreezeCastDuration));
        StartCoroutine(StartAttackLockout(FreezeCastDuration));

        animator.SetBool("attack", false);
        animator.SetTrigger("skill");

        if (sfxs[1]) sfxs[1].Play();

        GameObject o = Instantiate(FreezeRing, SkillPosition.position, Quaternion.identity);
        o.GetComponent<PlayerRangedFreezeObj>().TargetScale *= GetFreezeRingScale();

        yield return new WaitForSeconds(FreezeCastDuration - Time.fixedDeltaTime);

        Dictionary<EntityBase, float> InitialHitDictionary = new();
        var hitEnemies = SearchForEntitiesAroundCertainPoint(typeof(EnemyBase), SkillPosition.position, FreezeRange, true);
        
        foreach (EntityBase e in hitEnemies)
        {
            EnemyBase enemy = e as EnemyBase;
            float distance = Vector3.Distance(SkillPosition.position, enemy.transform.position);
            float freezeDuration = distance >= FreezeRange * 0.8f
                ?
                FreezeDurationMin
                : 
                Mathf.Lerp(FreezeDurationMin, FreezeDurationMax, MinDistanceForFreezeDuration * 1.0f / distance);
            
            ApplyFreeze(enemy, freezeDuration);

            if (Skills.Contains(SkillTree_Manager.SkillName.FREEZE_SUPERCONDUCT))
            {
                ApplySuperConduct(enemy);
            }

            if (Skills.Contains(SkillTree_Manager.SkillName.WINDBLOW_NORTH))
            {
                float pushDuration = distance >= FreezeRange * 0.8f
                    ?
                    0.1f
                    :
                    Mathf.Lerp(0.12f, 0.23f, MinDistanceForFreezeDuration * 1.0f / distance);

                PushEntityFrom(enemy, AttackPosition.transform, 2.3f, pushDuration);
            }
            else if (Skills.Contains(SkillTree_Manager.SkillName.WINDBLOW_SOUTH))
                PullEntityTowards(enemy, AttackPosition.transform, 2.6f, 0.2f);

            InitialHitDictionary.Add(e, freezeDuration);
        }

        if (Skills.Contains(SkillTree_Manager.SkillName.FREEZE_CHARGE))
        {
            for (int i = 1; i <= hitEnemies.Count; i++) ReduceSpecialCooldown(FreezeChargeCDRefund, CooldownReductionType.PERCENTAGE_CURRENT);
        }

        if (Skills.Contains(SkillTree_Manager.SkillName.FREEZE_HOLD))
        {
            float bonusDuration = 0, maxBonus = FreezeHoldMax;
            freezeMaintRing = null;
            bool initiatedRing = false;

            var currentHitEnemies = SearchForEntitiesAroundCertainPoint(typeof(EnemyBase), SkillPosition.position, FreezeRange, true);
            short frameCount = 0;
            while (bonusDuration < maxBonus && Input.GetKey(InputManager.Instance.SpecialKey))
            {
                if (frameCount >= 3)
                {
                    currentHitEnemies = SearchForEntitiesAroundCertainPoint(typeof(EnemyBase), SkillPosition.position, FreezeRange, true);
                    frameCount = 0;
                }

                if (!freezeMaintRing && !initiatedRing)
                {
                    freezeMaintRing = Instantiate(FreezeMaintRing, SkillPosition.position + new Vector3(0, 15, 0), Quaternion.identity, SkillPosition);
                    freezeMaintRing.transform.localScale *= GetFreezeRingScale();

                    initiatedRing = true;
                }

                foreach (var enemy in currentHitEnemies)
                {
                    if (InitialHitDictionary.ContainsKey(enemy)) 
                        ApplyFreeze(enemy, InitialHitDictionary[enemy]);
                    else
                        ApplyFreeze(enemy, FreezeDurationMin);
                }

                bonusDuration += Time.deltaTime;
                frameCount++;
                yield return null;
            }

            if (freezeMaintRing) Destroy(freezeMaintRing);
        }

        IsFreezeActive = false;

        animator.SetTrigger("skill_end");
        if (Skills.Contains(SkillTree_Manager.SkillName.FREEZE_BLOOM))
            playerManager.ChainFreeze(InitialHitDictionary, FreezeRange, FreezeDurationMin, FreezeDurationMax, MinDistanceForFreezeDuration);
    }

    void ApplySuperConduct(EntityBase enemy)
        => StartCoroutine(ApplySuperConductCoroutine(enemy));

    IEnumerator ApplySuperConductCoroutine(EntityBase enemy)
    {
        yield return null;

        float bonusDuration = Mathf.Min(enemy.FreezeTimer * 0.5f, 2f);
        float debuffDuration = enemy.FreezeTimer + bonusDuration;
        enemy.ApplyEffect(Effect.AffectedStat.DEF, "FREEZE_SUPERCONDUCT_DEF_DEBUFF", -FreezeConductDebuff, debuffDuration, true);
        enemy.ApplyEffect(Effect.AffectedStat.RES, "FREEZE_SUPERCONDUCT_RES_DEBUFF", -FreezeConductDebuff, debuffDuration, true);

        if (enemy.IsFrozen)
        {
            GameObject sp = Instantiate(SuperConnductUI, enemy.transform.position + new Vector3(50, 70), Quaternion.identity, enemy.transform);
            sp.GetComponent<UI_SuperConduct>().Inititialize(enemy, "FREEZE_SUPERCONDUCT_DEF_DEBUFF");
        }
    }

    float GetFreezeRingScale()
    {
        return FreezeRange / 450f;
    }

    public float GetSkillFiringInterval()
    {
        float ASPD_Dif = ASPD - 100;
        float ScaleFactor = 100 / (100 + ASPD_Dif * 0.33f);
        return Skill_AtkInterval * ScaleFactor;
    }

    [SerializeField] Transform LockIn;
    [SerializeField] private float lockInDamageFalloff = 0.01f, lockInDamageMulMin = 0.2f;
    EntityBase lockInTarget;
    public IEnumerator CastSkill()
    {
        if (!IsAlive()) yield break;

        StartCoroutine(StartMovementLockout(0.15f));
        StartCoroutine(UltimateLockout());

        animator.SetTrigger("skill");
        IsSkillActive = true;
        skillCurrentDuration = 0;
        flowerCount = 0;
        float angleOffset = 0;

        if (SkillSfx) SkillSfx.Play();

        bool shootAdditionalBullets = Skills.Contains(SkillTree_Manager.SkillName.SPIRAL_MORE),
             homingOnFirstTarget = Skills.Contains(SkillTree_Manager.SkillName.SPIRAL_TRAVEL);

        lockInTarget = homingOnFirstTarget 
            ? SearchForNearestEntityAroundSelf(typeof(EnemyBase))
            : null;

        if (!lockInTarget) homingOnFirstTarget = false;

        SkillBar.maxValue = SkillBar.value = SkillDuration;
        float intervalCounter = GetSkillFiringInterval();

        float lockInTargetDamageMul = 1.0f;
        int yellowBulletAngle = 90;

        GameObject o2 = null, o3 = null;

        while (skillCurrentDuration < SkillDuration)
        {
            SkillBar.value = SkillDuration - skillCurrentDuration;

            if (homingOnFirstTarget && (!lockInTarget || !lockInTarget.IsAlive()))
            {
                lockInTarget = homingOnFirstTarget
                        ? SearchForNearestEntityAroundSelf(typeof(EnemyBase))
                        : null;

                if (!lockInTarget) homingOnFirstTarget = false;
            }

            if (intervalCounter >= GetSkillFiringInterval())
            {
                Vector3 sourcePosition = SkillPosition.position;
                intervalCounter = 0;
                if (sfxs[2]) sfxs[2].Play();

                float speed = ProjectileSpeed * 0.25f;
                for (int i = 0; i < 360; i += 30)
                {
                    float currentAngle = i + angleOffset;

                    float angleInRadians = currentAngle * Mathf.Deg2Rad;

                    float circleRadius = 30f + (skillCurrentDuration * 5f);
                    Vector3 targetPosition = new Vector3(
                        sourcePosition.x + Mathf.Cos(angleInRadians) * circleRadius,
                        sourcePosition.y + Mathf.Sin(angleInRadians) * circleRadius,
                        sourcePosition.z
                    );

                    if (lockInTarget && lockInTarget.IsAlive())
                    {
                        CreateProjectileAndShootToward(
                            lockInTarget,
                            new DamageInstance(0, (int)(atk * Skill_DamageMulitplier * lockInTargetDamageMul), 0),
                            sourcePosition,
                            projectileType: ProjectileScript.ProjectileType.HOMING_TO_SPECIFIC_TARGET
                            );

                        if (lockInTargetDamageMul > lockInDamageMulMin) lockInTargetDamageMul -= lockInDamageFalloff;
                    }
                    else
                    {
                        CreateProjectileAndShootToward(
                            ProjectilePrefab,
                            new DamageInstance(0, (int)(atk * Skill_DamageMulitplier), 0),
                            sourcePosition,
                            targetPosition,
                            projectileType: ProjectileScript.ProjectileType.CATCH_FIRST_TARGET_OF_TYPE,
                            travelSpeed: speed,
                            acceleration: speed,
                            lifeSpan: Skill_ProjLifeSpan,
                            targetType: typeof(EnemyBase));
                    }

                    if (shootAdditionalBullets)
                    {
                        currentAngle = i - angleOffset - 15;
                        angleInRadians = currentAngle * Mathf.Deg2Rad;

                        targetPosition = new Vector3(
                            sourcePosition.x + Mathf.Cos(angleInRadians) * circleRadius,
                            sourcePosition.y + Mathf.Sin(angleInRadians) * circleRadius,
                            sourcePosition.z
                        );

                        o2 = CreateProjectileAndShootToward(
                            ProjectilePrefab,
                            new DamageInstance(0, (int)(atk * Skill_DamageMulitplier), 0),
                            sourcePosition,
                            targetPosition,
                            projectileType: ProjectileScript.ProjectileType.CATCH_FIRST_TARGET_OF_TYPE,
                            travelSpeed: speed,
                            acceleration: speed,
                            lifeSpan: Skill_ProjLifeSpan,
                            targetType: typeof(EnemyBase));

                        o2.GetComponent<SpriteRenderer>().color = new(0, 0.76f, 1f);
                    }
                }
                angleOffset += 6;

                if (shootAdditionalBullets)
                {
                    int angle = yellowBulletAngle;
                    float angleInRadians = angle * Mathf.Deg2Rad;
                    float circleRadius = 30f + (skillCurrentDuration * 5f);
                    Vector3 targetPosition = new Vector3(
                        sourcePosition.x + Mathf.Cos(angleInRadians) * circleRadius,
                        sourcePosition.y + Mathf.Sin(angleInRadians) * circleRadius,
                        sourcePosition.z
                    );

                    o3 = CreateProjectileAndShootToward(
                        ProjectilePrefab,
                        new DamageInstance(0, (int)(atk * Skill_DamageMulitplier * 2), 0),
                        sourcePosition,
                        targetPosition,
                        projectileType: ProjectileScript.ProjectileType.CATCH_FIRST_TARGET_OF_TYPE,
                        travelSpeed: speed * 2,
                        acceleration: speed * 2,
                        lifeSpan: Skill_ProjLifeSpan * 0.6f,
                        targetType: typeof(EnemyBase));
                    o3.GetComponent<SpriteRenderer>().color = Color.yellow;

                    angle *= -1;
                    angleInRadians = angle * Mathf.Deg2Rad;
                    targetPosition = new Vector3(
                            sourcePosition.x + Mathf.Cos(angleInRadians) * circleRadius,
                            sourcePosition.y + Mathf.Sin(angleInRadians) * circleRadius,
                            sourcePosition.z
                        );

                    o3 = CreateProjectileAndShootToward(
                        ProjectilePrefab,
                        new DamageInstance(0, (int)(atk * Skill_DamageMulitplier * 2), 0),
                        sourcePosition,
                        targetPosition,
                        projectileType: ProjectileScript.ProjectileType.CATCH_FIRST_TARGET_OF_TYPE,
                        travelSpeed: speed * 2,
                        acceleration: speed * 2,
                        lifeSpan: Skill_ProjLifeSpan * 0.6f,
                        targetType: typeof(EnemyBase));
                    o3.GetComponent<SpriteRenderer>().color = Color.yellow;

                    angle += 180;
                    angleInRadians = angle * Mathf.Deg2Rad;
                    targetPosition = new Vector3(
                        sourcePosition.x + Mathf.Cos(angleInRadians) * circleRadius,
                        sourcePosition.y + Mathf.Sin(angleInRadians) * circleRadius,
                        sourcePosition.z
                    );

                    o3 = CreateProjectileAndShootToward(
                        ProjectilePrefab,
                        new DamageInstance(0, (int)(atk * Skill_DamageMulitplier * 2), 0),
                        sourcePosition,
                        targetPosition,
                        projectileType: ProjectileScript.ProjectileType.CATCH_FIRST_TARGET_OF_TYPE,
                        travelSpeed: speed * 2,
                        acceleration: speed * 2,
                        lifeSpan: Skill_ProjLifeSpan * 0.6f,
                        targetType: typeof(EnemyBase));
                    o3.GetComponent<SpriteRenderer>().color = Color.yellow;

                    angle *= -1;
                    angleInRadians = angle * Mathf.Deg2Rad;
                    targetPosition = new Vector3(
                            sourcePosition.x + Mathf.Cos(angleInRadians) * circleRadius,
                            sourcePosition.y + Mathf.Sin(angleInRadians) * circleRadius,
                            sourcePosition.z
                        );

                    o3 = CreateProjectileAndShootToward(
                        ProjectilePrefab,
                        new DamageInstance(0, (int)(atk * Skill_DamageMulitplier * 2), 0),
                        sourcePosition,
                        targetPosition,
                        projectileType: ProjectileScript.ProjectileType.CATCH_FIRST_TARGET_OF_TYPE,
                        travelSpeed: speed * 2,
                        acceleration: speed * 2,
                        lifeSpan: Skill_ProjLifeSpan * 0.6f,
                        targetType: typeof(EnemyBase));
                    o3.GetComponent<SpriteRenderer>().color = Color.yellow;

                    yellowBulletAngle += 12;
                }
            }
            
            yield return null;
            skillCurrentDuration += Time.deltaTime;
            intervalCounter += Time.deltaTime;
        }

        if (SkillSfx && SkillSfx.isPlaying) SkillSfx.Stop();
        animator.SetTrigger("skill_end");
        IsSkillActive = false;
        skillCurrentDuration = SkillDuration;
        yield return null;

        SkillCoroutine = null;
    }

    short flowerCount = 0;
    [SerializeField] short MaxExtraFlowers = 3;
    [SerializeField] float FlowerMaxDuration = 1.5f;
    public override void OnEnemyDefeat(EntityBase enemy)
    {
        base.OnEnemyDefeat(enemy);

        if (IsSkillActive
            && Skills.Contains(SkillTree_Manager.SkillName.SPIRAL_PHANTOM)
            && gameObject.activeSelf
            && !enemy.IsAlive()
            && flowerCount < MaxExtraFlowers)
        {
            StartCoroutine(CreateExtraFlowers(enemy.transform.position, FlowerMaxDuration));
            flowerCount++;
        }
    }

    public IEnumerator CreateExtraFlowers(Vector3 position, float duration)
    {
        float angleOffset = 0;

        float intervalCounter = Skill_AtkInterval;
        float count = 0;

        GameObject E_SkillEffect = Instantiate(SkillEffect, position, Quaternion.identity);
        E_SkillEffect.GetComponent<SpriteRenderer>().color = Color.green;
        E_SkillEffect.transform.localScale = new(30, 30, 30);
        E_SkillEffect.SetActive(true);
        Destroy(E_SkillEffect, duration + 0.1f);

        yield return new WaitForSeconds(0.2f);

        GameObject o;
        while (count < duration)
        {
            if (intervalCounter >= Skill_AtkInterval)
            {
                Vector3 sourcePosition = position;
                intervalCounter = 0;
                if (sfxs[2]) sfxs[2].Play();

                float lifeSpan = Skill_ProjLifeSpan;
                float speed = ProjectileSpeed * 0.25f;
                for (int i = 0; i < 360; i += 30)
                {
                    float currentAngle = (i + angleOffset) * -1;

                    float angleInRadians = currentAngle * Mathf.Deg2Rad;

                    float circleRadius = 30f + (count * 5f);
                    Vector3 targetPosition = new Vector3(
                        sourcePosition.x + Mathf.Cos(angleInRadians) * circleRadius,
                        sourcePosition.y + Mathf.Sin(angleInRadians) * circleRadius,
                        sourcePosition.z
                    );

                    o = CreateProjectileAndShootToward(
                        ProjectilePrefab,
                        new DamageInstance(0, (int)(atk * Skill_DamageMulitplier), 0),
                        sourcePosition,
                        targetPosition,
                        projectileType: ProjectileScript.ProjectileType.CATCH_FIRST_TARGET_OF_TYPE,
                        travelSpeed: speed,
                        acceleration: speed,
                        lifeSpan: lifeSpan,
                        targetType: typeof(EnemyBase));
                    o.GetComponent<SpriteRenderer>().color = new(0, 1f, 0.52f);
                }
                angleOffset += 6;
            }

            yield return null;
            count += Time.deltaTime;
            intervalCounter += Time.deltaTime;
        }
    }

    private float BurstHeal_HpPercentage;
    private float HealPerSecond_HpPercentage;
    private float DefBoost;
    private float ResBoost;
    private float AtkBoost;
    private float SpeedBoost;
    public void SetJuggernauntInherit(float duration, float BurstHeal_HpPercentage, float HPS_Percentage, float DefBoost, float ResBoost, float AtkBoost, float SpeedBoost)
    {
        this.BurstHeal_HpPercentage = BurstHeal_HpPercentage;
        this.HealPerSecond_HpPercentage = HPS_Percentage;
        this.DefBoost = DefBoost;
        this.ResBoost = ResBoost;
        this.AtkBoost = AtkBoost;
        this.SpeedBoost = SpeedBoost;
        StartCoroutine(ActivateJuggernaunt(duration));
    }

    float juggernauntCurrentDuration = 0;
    protected IEnumerator ActivateJuggernaunt(float duration)
    {
        yield return new WaitUntil(() => IsComponentsInitialized);
        
        JuggEffect.SetActive(true);
        juggernauntCurrentDuration = 0;

        yield return null;

        Heal(mHealth * BurstHeal_HpPercentage);

        ApplyEffect(Effect.AffectedStat.ATK, "JUGGERNAUNT_SKILL_ATK_BUFF", AtkBoost * 100, 999f, true, EffectPersistType.PERSIST, false);
        ApplyEffect(Effect.AffectedStat.DEF, "JUGGERNAUNT_SKILL_DEF_BUFF", DefBoost * 100, 999f, true, EffectPersistType.PERSIST, false);
        ApplyEffect(Effect.AffectedStat.RES, "JUGGERNAUNT_SKILL_RES_BUFF", ResBoost, 999f, false, EffectPersistType.PERSIST, false);
        ApplyEffect(Effect.AffectedStat.MSPD, "JUGGERNAUNT_SKILL_MSPD_BUFF", SpeedBoost * 100, 999f, true, EffectPersistType.PERSIST, false);

        float t = 1.0f, d = duration;

        while (juggernauntCurrentDuration < d)
        {
            juggernauntCurrentDuration += Time.deltaTime;
            t += Time.deltaTime;

            if (t >= 1.0f)
            {
                Heal(mHealth * HealPerSecond_HpPercentage);
                t = 0;
            }

            yield return null;
        }

        Heal(mHealth * HealPerSecond_HpPercentage);
        RemoveEffect("JUGGERNAUNT_SKILL_ATK_BUFF");
        RemoveEffect("JUGGERNAUNT_SKILL_DEF_BUFF");
        RemoveEffect("JUGGERNAUNT_SKILL_RES_BUFF");
        RemoveEffect("JUGGERNAUNT_SKILL_MSPD_BUFF");
        JuggEffect.SetActive(false);
    }

    public override void OnDeath()
    {
        base.OnDeath();
        if (!IsAlive() && freezeMaintRing) Destroy(freezeMaintRing);
    }

    protected override void MintRevive()
    {
        freezeCooldownTimer = FreezeCooldown;
        skillCooldownTimer = SkillCooldown;
        base.MintRevive();
    }

    private float EnhanceSfxTimeLockout = 0f;
    public void ProjectileEnvironmentEnhanceCallback()
    {
        if (EnhanceSfxTimeLockout > 0) return;
        
        ProjectileEnviEnhanceSfx.Play();
        EnhanceSfxTimeLockout = 0.5f;
    }

    private float BounceSfxTimeLockout = 0f;
    public void ProjectileBounceCallback()
    {
        if (BounceSfxTimeLockout > 0) return;
        
        ProjectileBounceSfx.Play();
        BounceSfxTimeLockout = 0.5f;
    }

    public override PlayerTooltipsInfo GetPlayerTooltipsInfo()
    {
        var info = base.GetPlayerTooltipsInfo();

        info.AttackText = $"Lauches a projectile toward the nearest enemy within range, " +
            $"which deals {atk} {damageType.ToString().ToLower()} damage upon contact.";
        
        if (Skills.Contains(SkillTree_Manager.SkillName.SPIRAL_FIELD_EXPERT))
        {
            info.AttackText = $"Lauches a projectile toward the nearest enemy within range, " +
            $"which deals {atk} {damageType.ToString().ToLower()} damage upon contact and ricochets (up to 5 times). " +
            $"Projectiles can touch an environmental tile to gain additional effect.";
        }

        info.SkillName = "Der Tag neight Sich - ";
        if (Skills.Contains(SkillTree_Manager.SkillName.SPIRAL_MORE))
        {
            info.SkillName += CharacterPrefabsStorage.GetSkillName(SkillTree_Manager.SkillName.SPIRAL_MORE);
            info.SkillText =
                $"Continuously unleashes multiple waves of projectiles spreading in all direction around self, lasts up to {SkillDuration} seconds (can be cancelled via recast or perform other action). " +
                $"Each projectile hits the first enemy it comes into contact with, dealing {Skill_DamageMulitplier * 100}% ATK damage each, firing interval scales with ASPD.";
        }
        else if (Skills.Contains(SkillTree_Manager.SkillName.SPIRAL_TRAVEL))
        {
            info.SkillName += CharacterPrefabsStorage.GetSkillName(SkillTree_Manager.SkillName.SPIRAL_TRAVEL);
            info.SkillText =
                $"Locks-on to the nearest enemy within range and continuously unleashes waves of projectiles, lasts up to {SkillDuration} seconds (can be cancelled via recast or perform other action). " +
                $". Each projectile hits the first enemy it comes into contact with (if there is a locked enemy, all projectiles homing toward them instead), dealing {Skill_DamageMulitplier * 100}% ATK damage each, firing interval scales with ASPD.";
        }
        else if (Skills.Contains(SkillTree_Manager.SkillName.SPIRAL_PHANTOM))
        {
            info.SkillName += CharacterPrefabsStorage.GetSkillName(SkillTree_Manager.SkillName.SPIRAL_PHANTOM);
            info.SkillText =
                $"Continuously unleashes waves of projectiles spreading in all direction around self, lasts up to {SkillDuration} seconds (can be cancelled via recast or perform other action). " +
                $"Each projectile hits the first enemy it comes into contact with, dealing {Skill_DamageMulitplier * 100}% ATK damage each, firing interval scales with ASPD. " +
                $"If an enemy is defeated while the skill is active, another wave of projectiles will be created " +
                $"at their position, lasting up to {FlowerMaxDuration} seconds (triggers up to {MaxExtraFlowers} times).";
        }
        else if (Skills.Contains(SkillTree_Manager.SkillName.SPIRAL_SHADOW))
        {
            info.SkillName += CharacterPrefabsStorage.GetSkillName(SkillTree_Manager.SkillName.SPIRAL_SHADOW);
            info.SkillText =
                $"Continuously unleashes waves of projectiles spreading in all direction around self, lasts up to {SkillDuration} seconds (can be cancelled via recast or perform other action). " +
                $"Each projectile hits the first enemy it comes into contact with, dealing {Skill_DamageMulitplier * 100}% ATK damage each, firing interval scales with ASPD. " +
                $"Cancelling or swapping during the skill leaves behind a phantom that maintains the same effect for the remaining duration. " +
                $"The phantom has {PhantomAtkInherit * 100}% of self ATK.";
        }
        else if (Skills.Contains(SkillTree_Manager.SkillName.SPIRAL_READ))
        {
            info.SkillName += CharacterPrefabsStorage.GetSkillName(SkillTree_Manager.SkillName.SPIRAL_READ);
            info.SkillText =
                $"Continuously unleashes waves of projectiles spreading in all direction around self, lasts up to {SkillDuration} seconds (can be cancelled via recast or perform other action, " +
                $"and refunds upon doing so, up to {SP_SkillMaxRefund * 100}% CD). " +
                $"Each projectile hits the first enemy it comes into contact with, " +
                $"dealing {Skill_DamageMulitplier * 100}% ATK damage each, firing interval scales with ASPD.";
        }
        else
        {
            info.SkillName = "Der Tag neigt Sich";
            info.SkillText =
                $"Continuously unleashes waves of projectiles spreading in all direction around self, lasts up to {SkillDuration} seconds (can be cancelled via recast or perform other action). " +
                $"Each projectile hits the first enemy it comes into contact with, " +
                $"dealing {Skill_DamageMulitplier * 100}% ATK damage each, firing interval scales with ASPD.";
        }

        info.SkillText += $" {Math.Round(SkillCooldown, 1)}s cooldown.";

        info.SpecialName = "Zeropoint Burst - ";
        if (Skills.Contains(SkillTree_Manager.SkillName.FREEZE_BLOOM))
        {
            info.SpecialName += CharacterPrefabsStorage.GetSkillName(SkillTree_Manager.SkillName.FREEZE_BLOOM);
            info.SpecialText =
                $"After a short delay, inflicts freeze to all enemies within attack range for {FreezeDurationMin} - {FreezeDurationMax} seconds, inversely based on distance. Frozen enemies continues to " +
                $"create an extra freeze ring around their position (trigger once per enemy)";
        }
        else if (Skills.Contains(SkillTree_Manager.SkillName.FREEZE_HOLD))
        {
            info.SpecialName += CharacterPrefabsStorage.GetSkillName(SkillTree_Manager.SkillName.FREEZE_HOLD);
            info.SpecialText =
                $"After a short delay, inflicts freeze to all enemies within attack range for {FreezeDurationMin} - {FreezeDurationMax} seconds, " +
                $"inversely based on distance. The freeze can be maintained by holding the skill's key for up to an additional " +
                $"{FreezeHoldMax} seconds (can not act while maintaining, release the key to end its effect)";
        }
        else if (Skills.Contains(SkillTree_Manager.SkillName.FREEZE_CHARGE))
        {
            info.SpecialName += CharacterPrefabsStorage.GetSkillName(SkillTree_Manager.SkillName.FREEZE_CHARGE);
            info.SpecialText =
                $"After a short delay, inflicts freeze to all enemies within attack range for {FreezeDurationMin} - {FreezeDurationMax} seconds, inversely based on distance. Every enemy hit " +
                $"shortens the cool-down of the next usage by {FreezeChargeCDRefund * 100}%";
        }
        else if (Skills.Contains(SkillTree_Manager.SkillName.FREEZE_SUPERCONDUCT))
        {
            info.SpecialName += CharacterPrefabsStorage.GetSkillName(SkillTree_Manager.SkillName.FREEZE_SUPERCONDUCT);
            info.SpecialText =
                $"After a short delay, inflicts freeze to all enemies within attack range for {FreezeDurationMin} - {FreezeDurationMax} seconds, inversely based on distance " +
                $"and reduce their DEF and RES by {FreezeConductDebuff}% for equivalent duration";
        }
        else
        {
            info.SpecialName = "Zeropoint Burst";
            info.SpecialText =
                $"After a short delay, inflicts freeze to all enemies within attack range for {FreezeDurationMin} - {FreezeDurationMax} seconds (closer enemies are frozen for longer)";
        }

        if (Skills.Contains(SkillTree_Manager.SkillName.WINDBLOW_NORTH))
        {
            info.SpecialText += " and pushes them away from self";
        }
        else if (Skills.Contains(SkillTree_Manager.SkillName.WINDBLOW_SOUTH))
        {
            info.SpecialText += " and pulls them towards self";
        }

        info.SpecialText += $". {Math.Round(FreezeCooldown, 1)}s cool-down.";

        return info;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}