using DamageCalculation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using static Cinemachine.CinemachineTargetGroup;
using static Effect;
using static ProjectileScript;
using static StageManager;
using static UnityEngine.EventSystems.EventTrigger;
using static UnityEngine.GraphicsBuffer;

public class EntityBase : MonoBehaviour
{
    [SerializeField] public string Name;
    [SerializeField] public Sprite Icon;

    [SerializeField] public float mHealth;
    [SerializeField] public short bAtk, bDef, bRes;
    [SerializeField] public short defPen, defIgn, resPen, resIgn;
    [SerializeField] public float lifeSteal, b_moveSpeed, b_attackRange, b_attackWindupTime, b_attackInterval;
    public float MIN_PHYSICAL_DMG = 0.05F, MIN_MAGICAL_DMG = 0.1F;

    public float health;
    public short atk, def, res;
    public float ASPD = 100;
    public float moveSpeed, attackRange, attackWindupTime, attackInterval;

    public float hpRegenFlat = 0, hpRegenPercentage = 0;

    public short weight = 0;
    public short damageReduction = 0, damageAmplify = 0;

    public bool IsShiftImmune = false;

    public float BoundTimer = 0f;
    public bool IsBound => BoundTimer > 0f;

    public float GetMaxHealth() => mHealth;
    public short GetHealthPercentage() => (short)Mathf.Max(1, health * 100 / mHealth);
    public short GetMissinghealthPercentage() => (short)((mHealth - health) * 100 / mHealth);

    public bool IsFreezeImmune = false, IsStunImmune = false, IsPhysicalImmune = false, IsMagicalImmune = false, canRevive = false, isInvisible = false;

    public float InvulnerableTimer = 0f;
    public bool isInvulnerable => InvulnerableTimer > 0f;

    public virtual Type GetGenericType() => typeof(EntityBase);

    public void SetInvulnerable(float duration, bool stack = false)
    {
        if (stack)
        {
            InvulnerableTimer += duration;
            return;
        }

        InvulnerableTimer = Mathf.Max(InvulnerableTimer, duration);
    }

    public bool CanBeHitByProjectiles = true;

    public enum DamageType { PHYSICAL, MAGICAL, TRUE }
    public DamageType damageType;

    public HashSet<EnvironmentType> environmentalTilesStandingOn;
    
    public bool IsStandingOnEnvironmentalTile(EnvironmentType environmentType)
    {
        return environmentalTilesStandingOn.Contains(environmentType);
    }

    public void AddEnvironmentalTilesThisUnitStandingOn(EnvironmentType environmentType)
    {
        if (environmentalTilesStandingOn.Contains(environmentType)) return;
        environmentalTilesStandingOn.Add(environmentType);
        OnEnvironmentalTileEnter(environmentType);
    }

    public virtual void OnEnvironmentalTileEnter(EnvironmentType environmentType)
    {

    }

    public void RemoveEnvironmentalTilesThisUnitStandingOn(EnvironmentType environmentType)
    {
        if (!environmentalTilesStandingOn.Contains(environmentType)) return;
        environmentalTilesStandingOn.Remove(environmentType);
        OnEnvironmentalTileExit(environmentType);
    }

    public virtual void OnEnvironmentalTileExit(EnvironmentType environmentType)
    {

    }

    public enum AttackPattern { MELEE, RANGED, NONE }
    public AttackPattern attackPattern;
    [SerializeField] protected GameObject ProjectilePrefab;
    [SerializeField] protected ProjectileType ProjectileType = ProjectileType.CATCH_FIRST_TARGET_OF_TYPE;
    [SerializeField] public float ProjectileSpeed = 1000;
    [SerializeField] private GameObject DamagePopup;

    protected HealthBar healthBar;
    [SerializeField] protected GameObject ccBar;
    protected Slider ccSlider;

    [SerializeField] protected AnimationClip AttackAnimation;
    protected Transform AttackPosition;
    protected SpriteRenderer spriteRenderer;
    protected Animator animator;

    protected Rigidbody2D rb2d;
    public Rigidbody2D GetRigidbody2D => rb2d;

    protected Collider2D[] colliders;
    public Collider2D[] selfColliders => colliders;

    public AudioSource[] sfxs;

    protected GameObject ShadowSprite;

    public SpriteRenderer GetSpriteRenderer()
    {
        if (!spriteRenderer) 
        {
            Transform Sprite = transform.Find("Sprite");
            spriteRenderer = Sprite.GetComponent<SpriteRenderer>();
        } 
        
        return spriteRenderer;
    }

    protected bool useTransformAsAttackPosition = false;
    protected Vector3 PrevPosition;
    protected Color InitSpriteColor;

    protected float MovementLockout = 0, AttackLockout = 0;
    public bool IsMovementLocked => MovementLockout > 0 || IsFrozen || IsStunned;
    public bool IsAttackLocked => AttackLockout > 0 || IsFrozen || IsStunned;

    public short BeingShiftState = 0;
    public bool IsBeingShifted => BeingShiftState > 0;

    private bool TriggeredOnDeath = false;

    public float FreezeTimer = 0f, StunTimer = 0f;

    public float StatusResistTimer = 0f;
    public bool HasStatusResistant => StatusResistTimer > 0f;

    public float PhysicalDodgeChance = 0, MagicalDodgeChance = 0;

    public bool IsFrozen => FreezeTimer > 0f;
    public bool IsStunned => StunTimer > 0f;

    [SerializeField] protected float preferredMoveAnimationPlaySpeed = 1.0f, preferredAttackAnimationSpeed = 1.0f;

    protected short UpdateCounter = 0;
    protected EntityManager EntityManager;

    protected Coroutine AttackCoroutine = null, LockoutMovementOnAttackCoroutine = null;
    protected Animation attackAnimation;

    public bool IsComponentsInitialized = false;

    public Dictionary<string, Effect>
                    AtkBuffs = new(),
                    AtkDebuffs = new(),
                    DefBuffs = new(),
                    DefDebuffs = new(),
                    ResBuffs = new(),
                    ResDebuffs = new(),
                    MspdBuffs = new(),
                    MspdDebuffs = new(),
                    AspdBuffs = new(),
                    AspdDebuffs = new(),
                    ArngBuffs = new(),
                    ArngDebuffs = new();

    protected List<Effect> AllEffects()
    {
        return AtkBuffs.Values
            .Concat(AtkDebuffs.Values)
            .Concat(DefBuffs.Values)
            .Concat(DefDebuffs.Values)
            .Concat(ResBuffs.Values)
            .Concat(ResDebuffs.Values)
            .Concat(MspdBuffs.Values)
            .Concat(MspdDebuffs.Values)
            .Concat(AspdBuffs.Values)
            .Concat(AspdDebuffs.Values)
            .Concat(ArngBuffs.Values)
            .Concat(ArngDebuffs.Values)
            .ToList();
    }

    protected List<Dictionary<string, Effect>> AllBuffs()
    {
        return new()
        {
            AtkBuffs,
            DefBuffs,
            ResBuffs,
            MspdBuffs,
            AspdBuffs,
            ArngBuffs
        };
    }

    protected List<Dictionary<string, Effect>> AllDebuffs()
    {
        return new()
        {
            AtkDebuffs,
            DefDebuffs,
            ResDebuffs,
            MspdDebuffs,
            AspdDebuffs,
            ArngDebuffs
        };
    }

    public virtual bool CanAttack =>
        attackPattern != AttackPattern.NONE &&
        !IsFrozen &&
        !IsStunned &&
        IsAlive();

    public virtual bool CanFinishAttack => CanAttack && attacking;

    public bool ViewOnlyMode => FindAnyObjectByType<StageManager>() == null;

    public Vector3 GetMovementDirection()
    {
        return rb2d.velocity.normalized;
    }

    public Vector3 GetAttackPosition()
    {
        if (useTransformAsAttackPosition) return transform.position;
        return AttackPosition ? AttackPosition.position : transform.position;
    }

    public virtual void Start()
    {
        InitializeComponents();
    }

    GameObject BlindnessEffect;
    RectTransform GravityCircle;
    readonly bool absolutism = CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.ABSOLUTISM);
    readonly bool statis = CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.STATIS);
    readonly bool gravity = CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.GRAVITY);
    readonly bool acceleration = CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.ACCELERATION);
    public virtual void InitializeComponents()
    {
        Transform Sprite = transform.Find("Sprite");
        spriteRenderer = Sprite.GetComponent<SpriteRenderer>();
        animator = Sprite.GetComponent<Animator>();
        rb2d = GetComponent<Rigidbody2D>();
        colliders = GetComponents<Collider2D>();
        sfxs = GetComponents<AudioSource>();

        LevitationEffect = spriteRenderer.transform.Find("Bubble").GetComponent<SpriteRenderer>();
        LevitationEffect.color = Color.clear;

        ShadowSprite = spriteRenderer.transform.Find("Shadow").gameObject;

        InitSpriteColor = Color.white;
        PrevPosition = transform.position;

        environmentalTilesStandingOn ??= new HashSet<EnvironmentType>();

        GravityTimerCount = UnityEngine.Random.Range(0f, PullTick);

        Transform circleFind = transform.Find("GravityCircle/Radius");
        if (circleFind)
        {
            GravityCircle = circleFind.GetComponent<RectTransform>();
            GravityCircle.gameObject.SetActive(gravity);
        }

        Transform StatisTransform = transform.Find("Statis");
        if (StatisTransform)
        { 
            StatisObj = StatisTransform.gameObject;
            StatisObj.SetActive(statis);
        }

        BlindnessEffect = transform.Find("Blindness").gameObject;

        health = mHealth;
        atk = bAtk;
        def = bDef;
        res = bRes;
        moveSpeed = b_moveSpeed;
        attackRange = b_attackRange;
        attackWindupTime = b_attackWindupTime;
        attackInterval = b_attackInterval;

        AttackPosition = transform.Find("AttackPosition");
        if (!AttackPosition)
        {
            AttackPosition = transform;
            useTransformAsAttackPosition = true;
        }
        if (spriteRenderer.flipX) FlipAttackPosition();

        healthBar = GetComponentInChildren<HealthBar>();
        healthBar.SetMaxHealth(mHealth);

        ccSlider = ccBar.GetComponentInChildren<Slider>();

        if (ViewOnlyMode) return;

        EntityManager = FindObjectOfType<EntityManager>();
        if (EntityManager)
        {
            EntityManager.OnEntitySpawn(this.gameObject);
        }

        OnInitialize_GetSkillTreeAttributes();

        StartCoroutine(OnStartCoroutine());
        CalculateBuffsAndDebuffs();
    }

    protected virtual void OnInitialize_GetSkillTreeAttributes()
    {
        if (this as PlayerBase)
        {
            if (CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.WINGED_STEPS_A)
            || CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.WINGED_STEPS_B))
                AccelerationBuffPerSec *= 1.5f;

            if (CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.ATTENTION_DEVICE))
                StatisRequiredTimer = 3f;
        }
    }

    [SerializeField] protected bool HasOnStartCoroutine = true;
    IEnumerator OnStartCoroutine()
    {
        if (!HasOnStartCoroutine)
        {
            spriteRenderer.color = InitSpriteColor;
            yield break;
        }

        Color transparentBlack = new Color(0, 0, 0, 0);
        spriteRenderer.color = transparentBlack;

        float c = 0, d = 0.25f;
        while (c < d)
        {
            spriteRenderer.color = Color.Lerp(transparentBlack, Color.black, c * 1.0f / d);
            c += Time.deltaTime;
            yield return null;
        }

        c = 0; d = 0.5f;
        while (c < d)
        {
            spriteRenderer.color = Color.Lerp(Color.black, InitSpriteColor, c * 1.0f / d);
            c += Time.deltaTime;
            yield return null;
        }

        spriteRenderer.color = InitSpriteColor;
        yield return null;
    }

    short BDB_Cnt = 0;
    public virtual void FixedUpdate()
    {
        if (!IsAlive() && !TriggeredOnDeath && !canRevive) OnDeath();

        RegenCount();
        UpdateCooldowns();
        UpdateEffectDurations();
        UpdateUI();

        BDB_Cnt++;
        if (BDB_Cnt >= 10) CalculateBuffsAndDebuffs();
        
        HandleSpriteFlipping();
        HandleAnimationSpeed();
        ProcessSkillTree();

        if (!IsStunned && IsBeingLevitated && EndLevitateCoroutine == null)
            EndLevitateCoroutine = StartCoroutine(EndLevitate());
    }

    short BlindnessUIUpdateCnt = 7;
    public virtual void UpdateUI()
    {
        BlindnessUIUpdateCnt++;

        if (BlindnessUIUpdateCnt >= 7)
        {
            BlindnessUIUpdateCnt = 0;

            string BlindKey = "BLINDNESS";
            BlindnessEffect.SetActive(IsAlive() && ArngDebuffs.ContainsKey(BlindKey) && ArngDebuffs[BlindKey].IsInEffect);
        }
    }

    // use negative values for debuffs
    public enum EffectPersistType
    {
        PERSIST,
        DECAY
    };

    public void ApplyEffect(AffectedStat affectedStat, string Key, float Value, float Duration, bool IsPercentageBased, EffectPersistType persistType = EffectPersistType.PERSIST, bool transferOnSwap = true)
    {
        bool IsDebuff = Value < 0;
        bool DecayOverDuration = persistType == EffectPersistType.DECAY;

        if (IsDebuff)
        {
            Value *= -1;

            switch (affectedStat)
            {
                case AffectedStat.ATK:
                    if (AtkDebuffs.ContainsKey(Key))
                        AtkDebuffs[Key].Instantiate(this, affectedStat, Value, Duration, IsPercentageBased, DecayOverDuration, transferOnSwap);
                    else
                        AtkDebuffs.Add(Key, new(this, affectedStat, Value, Duration, IsPercentageBased, DecayOverDuration, transferOnSwap)); 
                    break;

                case AffectedStat.DEF:
                    if (DefDebuffs.ContainsKey(Key))
                        DefDebuffs[Key].Instantiate(this, affectedStat, Value, Duration, IsPercentageBased, DecayOverDuration, transferOnSwap);
                    else
                        DefDebuffs.Add(Key, new(this, affectedStat, Value, Duration, IsPercentageBased, DecayOverDuration, transferOnSwap));
                    break;

                case AffectedStat.RES:
                    if (ResDebuffs.ContainsKey(Key))
                        ResDebuffs[Key].Instantiate(this, affectedStat, Value, Duration, IsPercentageBased, DecayOverDuration, transferOnSwap);
                    else
                        ResDebuffs.Add(Key, new(this, affectedStat, Value, Duration, IsPercentageBased, DecayOverDuration, transferOnSwap));
                    break;

                case AffectedStat.MSPD:
                    if (MspdDebuffs.ContainsKey(Key))
                        MspdDebuffs[Key].Instantiate(this, affectedStat, Value, Duration, IsPercentageBased, DecayOverDuration, transferOnSwap);
                    else
                        MspdDebuffs.Add(Key, new(this, affectedStat, Value, Duration, IsPercentageBased, DecayOverDuration, transferOnSwap));
                    break;

                case AffectedStat.ASPD:
                    if (AspdDebuffs.ContainsKey(Key))
                        AspdDebuffs[Key].Instantiate(this, affectedStat, Value, Duration, IsPercentageBased, DecayOverDuration, transferOnSwap);
                    else
                        AspdDebuffs.Add(Key, new(this, affectedStat, Value, Duration, IsPercentageBased, DecayOverDuration, transferOnSwap));
                    break;

                case AffectedStat.ARNG:
                    if (ArngDebuffs.ContainsKey(Key))
                        ArngDebuffs[Key].Instantiate(this, affectedStat, Value, Duration, IsPercentageBased, DecayOverDuration, transferOnSwap);
                    else
                        ArngDebuffs.Add(Key, new(this, affectedStat, Value, Duration, IsPercentageBased, DecayOverDuration, transferOnSwap));
                    break;
            }
        }
        else
        {
            switch (affectedStat)
            {
                case AffectedStat.ATK:
                    if (AtkBuffs.ContainsKey(Key))
                        AtkBuffs[Key].Instantiate(this, affectedStat, Value, Duration, IsPercentageBased, DecayOverDuration, transferOnSwap);
                    else
                        AtkBuffs.Add(Key, new(this, affectedStat, Value, Duration, IsPercentageBased, DecayOverDuration, transferOnSwap));
                    break;

                case AffectedStat.DEF:
                    if (DefBuffs.ContainsKey(Key))
                        DefBuffs[Key].Instantiate(this, affectedStat, Value, Duration, IsPercentageBased, DecayOverDuration, transferOnSwap);
                    else
                        DefBuffs.Add(Key, new(this, affectedStat, Value, Duration, IsPercentageBased, DecayOverDuration, transferOnSwap));
                    break;

                case AffectedStat.RES:
                    if (ResBuffs.ContainsKey(Key))
                        ResBuffs[Key].Instantiate(this, affectedStat, Value, Duration, IsPercentageBased, DecayOverDuration, transferOnSwap);
                    else
                        ResBuffs.Add(Key, new(this, affectedStat, Value, Duration, IsPercentageBased, DecayOverDuration, transferOnSwap));
                    break;

                case AffectedStat.MSPD:
                    if (MspdBuffs.ContainsKey(Key))
                        MspdBuffs[Key].Instantiate(this, affectedStat, Value, Duration, IsPercentageBased, DecayOverDuration, transferOnSwap);
                    else
                        MspdBuffs.Add(Key, new(this, affectedStat, Value, Duration, IsPercentageBased, DecayOverDuration, transferOnSwap));
                    break;

                case AffectedStat.ASPD:
                    if (AspdBuffs.ContainsKey(Key))
                        AspdBuffs[Key].Instantiate(this, affectedStat, Value, Duration, IsPercentageBased, DecayOverDuration, transferOnSwap);
                    else
                        AspdBuffs.Add(Key, new(this, affectedStat, Value, Duration, IsPercentageBased, DecayOverDuration, transferOnSwap));
                    break;

                case AffectedStat.ARNG:
                    if (ArngBuffs.ContainsKey(Key))
                        ArngBuffs[Key].Instantiate(this, affectedStat, Value, Duration, IsPercentageBased, DecayOverDuration, transferOnSwap);
                    else
                        ArngBuffs.Add(Key, new(this, affectedStat, Value, Duration, IsPercentageBased, DecayOverDuration, transferOnSwap));
                    break;
            }
        }

        if (IsComponentsInitialized) CalculateBuffsAndDebuffs();
    }

    public void RemoveEffect(params string[] Keys)
    {
        bool hasChanged = false;
        foreach (var Key in Keys)
        {
            if (AtkBuffs.ContainsKey(Key))
            {
                AtkBuffs.Remove(Key);
                hasChanged = true;
            }

            if (AtkDebuffs.ContainsKey(Key))
            {
                AtkDebuffs.Remove(Key);
                hasChanged = true;
            }

            if (DefBuffs.ContainsKey(Key))
            {
                DefBuffs.Remove(Key);
                hasChanged = true;
            }

            if (DefDebuffs.ContainsKey(Key))
            {
                DefDebuffs.Remove(Key);
                hasChanged = true;
            }

            if (ResBuffs.ContainsKey(Key))
            {
                ResBuffs.Remove(Key);
                hasChanged = true;
            }

            if (ResDebuffs.ContainsKey(Key))
            {
                ResDebuffs.Remove(Key);
                hasChanged = true;
            }

            if (MspdBuffs.ContainsKey(Key))
            {
                MspdBuffs.Remove(Key);
                hasChanged = true;
            }

            if (MspdDebuffs.ContainsKey(Key))
            {
                MspdDebuffs.Remove(Key);
                hasChanged = true;
            }

            if (AspdBuffs.ContainsKey(Key))
            {
                AspdBuffs.Remove(Key);
                hasChanged = true;
            }

            if (AspdDebuffs.ContainsKey(Key))
            {
                AspdDebuffs.Remove(Key);
                hasChanged = true;
            }

            if (ArngBuffs.ContainsKey(Key))
            {
                ArngBuffs.Remove(Key);
                hasChanged = true;
            }

            if (ArngDebuffs.ContainsKey(Key))
            {
                ArngDebuffs.Remove(Key);
                hasChanged = true;
            }
        }
        
        if (hasChanged) CalculateBuffsAndDebuffs();
    }

    public void ClearAllEffects()
    {
        AtkBuffs.Clear();
        AtkDebuffs.Clear();
        DefBuffs.Clear();
        DefDebuffs.Clear();
        ResBuffs.Clear();
        ResDebuffs.Clear();
        MspdBuffs.Clear();
        MspdDebuffs.Clear();
        AspdBuffs.Clear();
        AspdDebuffs.Clear();
        ArngBuffs.Clear();
        ArngDebuffs.Clear();
        CalculateBuffsAndDebuffs();
    }

    public void UpdateEffectDurations()
    {
        var effects = AllEffects();
        foreach (var effect in effects)
        {
            if (!effect.IsInEffect) continue;
            effect.Duration -= Time.deltaTime;
            if (effect.DecayOverDuration) effect.Decay();

            if (!effect.IsInEffect) effect.EndEffect();
        }
    }

    float prevAtkAdd = 0, prevDefAdd = 0, prevResAdd = 0;
    float prevMspdAdd = 0, prevAspdAdd = 0, prevArngAdd = 0, prevDrngAdd = 0;
    public void CalculateBuffsAndDebuffs()
    {
        BDB_Cnt = 0;
        
        EnemyBase enemyBaseCheck = this as EnemyBase;

        atk -= (short) prevAtkAdd;
        def -= (short) prevDefAdd;
        res -= (short) prevResAdd;
        moveSpeed -= prevMspdAdd;
        ASPD -= prevAspdAdd;

        attackRange -= prevArngAdd; 
        if (enemyBaseCheck)
            enemyBaseCheck.detectionRange -= prevDrngAdd;

        prevAtkAdd = prevDefAdd = prevResAdd = 0;
        prevMspdAdd = prevAspdAdd = prevArngAdd = prevDrngAdd = 0;

        // Buffs
        List<Effect> atkBuffsList = new(AtkBuffs.Values.Where(a => a.IsInEffect).ToList());
        atkBuffsList.ForEach(a =>
        {
            if (a.IsPercentage)
            {
                prevAtkAdd += (bAtk * a.Value / 100);
            }
            else
            {
                prevAtkAdd += a.Value;
            }
        });

        List<Effect> defBuffsList = new(DefBuffs.Values.Where(a => a.IsInEffect).ToList());
        defBuffsList.ForEach(a =>
        {
            if (a.IsPercentage)
            {
                prevDefAdd += (bDef * a.Value / 100);
            }
            else
            {
                prevDefAdd += a.Value;
            }
        });

        List<Effect> resBuffsList = new(ResBuffs.Values.Where(a => a.IsInEffect).ToList());
        resBuffsList.ForEach(a =>
        {
            if (a.IsPercentage)
            {
                prevResAdd += (bRes * a.Value / 100);
            }
            else
            {
                prevResAdd += a.Value;
            }
        });

        List<Effect> mspdBuffsList = new(MspdBuffs.Values.Where(a => a.IsInEffect).ToList());
        mspdBuffsList.ForEach(a =>
        {
            if (a.IsPercentage)
            {
                prevMspdAdd += b_moveSpeed * a.Value / 100;
            }
            else
            {
                prevMspdAdd += a.Value;
            }
        });

        List<Effect> aspdBuffsList = new(AspdBuffs.Values.Where(a => a.IsInEffect).ToList());
        aspdBuffsList.ForEach(a =>
        {
            prevAspdAdd += a.Value;
        });


        List<Effect> arngBuffsList = new(ArngBuffs.Values.Where(a => a.IsInEffect).ToList());
        arngBuffsList.ForEach(a =>
        {
            if (a.IsPercentage)
            {
                prevArngAdd += b_attackRange * a.Value / 100f;
                if (enemyBaseCheck)
                    prevDrngAdd += enemyBaseCheck.b_detectionRange * a.Value / 100f;
            }
            else
            {
                prevArngAdd += a.Value;
                prevDrngAdd += a.Value;
            }
        });

        float simAtk = (atk + prevAtkAdd),
             simDef = (def + prevDefAdd),
             simRes = (res + prevResAdd),
             simMspd = moveSpeed + prevMspdAdd,
             simAspd = ASPD + prevAspdAdd,
             simArng = attackRange + prevArngAdd,
             simDrng = 0;

        if (enemyBaseCheck)
            simDrng = enemyBaseCheck.detectionRange + prevDrngAdd;

        // Debuffs
        List<Effect> sortedAtkDebuffs = new(AtkDebuffs.Values.Where(a => a.IsInEffect).ToList());
        sortedAtkDebuffs.Sort((a1, a2) => (int) (a2.Value - a1.Value));
        sortedAtkDebuffs.ForEach(a =>
        {
            if (a.IsPercentage)
            {
                prevAtkAdd -= (simAtk * a.Value / 100);

                simAtk -= (simAtk * a.Value / 100);
            }
            else
            {
                prevAtkAdd -= a.Value;

                simAtk -= a.Value;
            }
        });

        List<Effect> sortedDefDebuffs = new(DefDebuffs.Values.Where(a => a.IsInEffect).ToList());
        sortedDefDebuffs.Sort((a1, a2) => (int)(a2.Value - a1.Value));
        sortedDefDebuffs.ForEach(a =>
        {
            if (a.IsPercentage)
            {
                prevDefAdd -= (simDef * a.Value / 100);

                simDef -= (simDef * a.Value / 100);
            }
            else
            {
                prevDefAdd -= a.Value;
                simDef -= a.Value;
            }
        });

        List<Effect> sortedResDebuffs = new(ResDebuffs.Values.Where(a => a.IsInEffect).ToList());
        sortedResDebuffs.Sort((a1, a2) => (int)(a2.Value - a1.Value));
        sortedResDebuffs.ForEach(a =>
        {
            if (a.IsPercentage)
            {
                prevResAdd -= (simRes * a.Value / 100);
                simRes -= (simRes * a.Value / 100);
            }
            else
            {
                prevResAdd -= a.Value;
                simRes -= a.Value;
            }
        });

        List<Effect> sortedMspdDebuffs = new(MspdDebuffs.Values.Where(a => a.IsInEffect).ToList());
        sortedMspdDebuffs.Sort((a1, a2) => (int)(a2.Value - a1.Value));
        sortedMspdDebuffs.ForEach(a =>
        {
            if (a.IsPercentage)
            {
                prevMspdAdd -= (simMspd * a.Value / 100);
                simMspd -= (simMspd * a.Value / 100);
            }
            else
            {
                prevMspdAdd -= a.Value;
                simMspd -= a.Value;
            }
        });

        List<Effect> sortedAspdDebuffs = new(AspdDebuffs.Values.Where(a => a.IsInEffect).ToList());
        sortedAspdDebuffs.ForEach(a =>
        {
            if (a.IsPercentage)
            {
                prevAspdAdd -= (simAspd * a.Value / 100);
                simAspd -= (simAspd * a.Value / 100);
            }
            else
            {
                prevAspdAdd -= a.Value;
                simAspd -= a.Value;
            }
        });

        List<Effect> sortedArngDebuffs = new(ArngDebuffs.Values.Where(a => a.IsInEffect).ToList());
        sortedArngDebuffs.Sort((a1, a2) => (int)(a2.Value - a1.Value));

        sortedArngDebuffs.ForEach(a =>
        {
            if (a.IsPercentage)
            {
                float reduction = simArng * a.Value / 100f;
                prevArngAdd -= reduction;
                simArng -= reduction;

                if (enemyBaseCheck)
                {
                    float drngReduction = simDrng * a.Value / 100f;
                    prevDrngAdd -= drngReduction;
                    simDrng -= drngReduction;
                }
            }
            else
            {
                prevArngAdd -= a.Value;
                simArng -= a.Value;
                if (enemyBaseCheck)
                {
                    prevDrngAdd -= a.Value;
                    simDrng -= a.Value;
                }
            }
        });

        // avoid negative amount and force it to be 0 instead
        // prevAdd * -1 >= stat means the debuff is greater than or equal to the current stat,
        // so we set the debuff to be equal to the current stat, effectively making the final stat 0
        if (prevMspdAdd * -1 >= moveSpeed) prevMspdAdd = moveSpeed * -1;
        if (prevAtkAdd * -1 >= atk) prevAtkAdd = atk * -1;
        if (prevDefAdd * -1 >= def) prevDefAdd = def * -1;
        if (prevResAdd * -1 >= res) prevResAdd = res * -1;
        if (simAspd < 20) prevAspdAdd = ASPD - 20;

        if (prevArngAdd * -1 >= attackRange)
            prevArngAdd = attackRange * -1;

        if (enemyBaseCheck)
        {
            if (prevDrngAdd * -1 >= enemyBaseCheck.detectionRange)
                prevDrngAdd = enemyBaseCheck.detectionRange * -1;
        }

        atk += (short) prevAtkAdd;
        def += (short) prevDefAdd;
        res += (short) prevResAdd;
        moveSpeed += prevMspdAdd;
        ASPD += prevAspdAdd;

        attackRange += prevArngAdd;
        if (enemyBaseCheck) enemyBaseCheck.detectionRange += prevDrngAdd;
    }

    private float regenTimer = 0;
    public void RegenCount()
    {
        regenTimer += Time.deltaTime;
        if (regenTimer < 1.0f) return;
        regenTimer = 0;

        Regen();
    }

    protected virtual void Regen()
    {
        if (!IsAlive()) return;

        float regenAmount = GetRegenAmount();
        if (regenAmount <= 0) return;

        Heal(regenAmount, this, false, true);
    }

    protected virtual float GetRegenAmount()
    {
        return hpRegenFlat + mHealth * hpRegenPercentage;
    }

    public virtual void UpdateCooldowns()
    {
        bool PrevFrozen = FreezeTimer > 0f;
        FreezeTimer -= Time.deltaTime;
        if (FreezeTimer > 0f) OnFreezeMaintain();
        else if (PrevFrozen && FreezeTimer <= 0f) OnFreezeExit();

        bool PrevStunned = StunTimer > 0f;
        StunTimer -= Time.deltaTime;
        if (StunTimer > 0f) OnStunMaintain();
        else if (PrevStunned && StunTimer <= 0f) OnStunExit();

        AttackLockout -= Time.deltaTime;
        MovementLockout -= Time.deltaTime;
        BoundTimer -= Time.deltaTime;

        InvulnerableTimer -= Time.deltaTime;

        StatusResistTimer -= Time.deltaTime;
    }

    public virtual void HandleSpriteFlipping()
    {
        Vector3 CurrentPos = transform.position;
        float deltaX = CurrentPos.x - PrevPosition.x;

        bool PrevFlipX = spriteRenderer.flipX;
        if (Mathf.Abs(deltaX) > 0.25f)
        {
            spriteRenderer.flipX = deltaX <= 0;
        }

        if (PrevFlipX != spriteRenderer.flipX)
            FlipAttackPosition();

        PrevPosition = CurrentPos;
    }

    public virtual void FlipAttackPosition()
    {
        if (useTransformAsAttackPosition) return;

        AttackPosition.localPosition = new Vector3(
            -AttackPosition.localPosition.x,
            AttackPosition.localPosition.y,
            AttackPosition.localPosition.z
        );
    }

    public virtual void FaceToward(Vector2 position)
    {
        if (IsFrozen || IsStunned) return;

        float deltaX = position.x - transform.position.x;

        bool PrevFlipX = spriteRenderer.flipX;
        if (Mathf.Abs(deltaX) > Mathf.Epsilon)
        {
            spriteRenderer.flipX = deltaX <= 0;
        }

        if (PrevFlipX != spriteRenderer.flipX)
            FlipAttackPosition();
    }

    public virtual void HandleAnimationSpeed()
    {
        if (!animator || !animator.runtimeAnimatorController) return;

        float MIN_MSPEED = preferredMoveAnimationPlaySpeed * 0.2f,
               MAX_MSPEED = preferredMoveAnimationPlaySpeed * 2,
               X_MSPD_MULTIPLIER = preferredMoveAnimationPlaySpeed - MIN_MSPEED;
        animator.SetFloat("speed_value", Mathf.Lerp(MIN_MSPEED, MAX_MSPEED, moveSpeed * (X_MSPD_MULTIPLIER / (MAX_MSPEED - MIN_MSPEED)) / b_moveSpeed));

        float MIN_ASPEED = preferredAttackAnimationSpeed * 0.2f,
           MAX_ASPEED = preferredAttackAnimationSpeed * 5,
           X_ASPD_MULTIPLIER = preferredAttackAnimationSpeed - MIN_MSPEED;
        animator.SetFloat("a_speed_value", Mathf.Lerp(MIN_ASPEED, MAX_ASPEED, ASPD * (X_ASPD_MULTIPLIER / (MAX_ASPEED - MIN_ASPEED)) / 100));
    }

    public virtual IEnumerator StartMovementLockout(float m)
    {
        MovementLockout = Mathf.Max(MovementLockout, m);
        StopMovement();
        yield return null;
    }

    public virtual IEnumerator StartAttackLockout(float m)
    {
        yield return null;
        AttackLockout = Mathf.Max(AttackLockout, m);
        yield return null;
    }

    public virtual DamageInstance DamageOutput(EntityBase target, float pDmg, float mDmg, float tDmg)
    {
        DamagePipeline pipeline = new DamagePipeline
        {
            attacker = this,
            target = target,
            instance = new DamageInstance(pDmg, mDmg, tDmg)
        };

        pipeline.Add(new ModifyRawDamage());
        pipeline.Add(new AccountSkillTreeEffects());
        pipeline.Add(new CalculateDefense());
        pipeline.Add(new AccountDodges());
        pipeline.Calculate();

        return pipeline.instance;
    }

    public DamageInstance GetInstanceBasedOnDamagetype()
    {
        return GetInstanceBasedOnDamagetype(atk);
    }

    public DamageInstance GetInstanceBasedOnDamagetype(int atk)
    {
        DamageInstance instance = new();
        if (damageType == DamageType.PHYSICAL) instance.Set(atk, 0, 0);
        else if (damageType == DamageType.MAGICAL) instance.Set(0, atk, 0);
        else instance.Set(0, 0, atk);
        return instance;
    }

    public virtual void DealDamage(EntityBase target, int damage, ProjectileScript projectileInfo = null)
    {
        if (damageType == DamageType.PHYSICAL) DealDamage(target, damage, 0, 0, projectileInfo);
        else if (damageType == DamageType.MAGICAL) DealDamage(target, 0, damage, 0, projectileInfo);
        else DealDamage(target, 0, 0, damage, projectileInfo);
    }

    public virtual void DealDamage(EntityBase target, DamageInstance damage, ProjectileScript projectileInfo = null)
    {
        DealDamage(target, damage.PhysicalDamage, damage.MagicalDamage, damage.TrueDamage, projectileInfo);
    }

    public virtual void DealDamage(EntityBase target, float pDmg, float mDmg, float tDmg, bool allowWhenDisabled = false, ProjectileScript projectileInfo = null)
    {
        if ((!allowWhenDisabled && (IsFrozen || IsStunned)) || !target || !target.IsAlive()) return;

        var calcDamage = DamageOutput(target, pDmg, mDmg, tDmg);

        target.TakeDamage(calcDamage, this, projectileInfo);

        if (calcDamage.TotalDamage <= 0) return;
        
        OnSuccessfulAttack(target, calcDamage);
    }

    public virtual void OnSuccessfulAttack(EntityBase target, DamageInstance damage)
    {
        if (lifeSteal > 0)
        {
            Heal(damage.TotalDamage * lifeSteal);
        }
    }

    public virtual void TakeDamage(DamageInstance damage, EntityBase source, ProjectileScript projectileInfo = null, bool IgnoreInvulnerability = false)
    {
        if (!this || !this.IsAlive() || (this.isInvulnerable && !IgnoreInvulnerability)) return;

        OnAttackReceive(source);
        ShowDamageDealt(damage);
        AdjustHealthOnDamageReceive(damage);
        if (damage.TotalDamage > 0) StartCoroutine(PulseSprite());
    }

    public void InstaKill()
    {
        if (!IsAlive()) return;
        canRevive = false;
        health = 0;
        healthBar.SetHealth(0);
        OnDeath();
    }

    public virtual void OnAttackReceive(EntityBase source)
    {

    }

    public void ShowDamageDealt(DamageInstance damage)
    {
        if (damage.TotalDamage == 0 && !damage.IsDodged) return;

        string dmgTxt = string.Empty;

        if (damage.IsDodged)
        {
            dmgTxt = "<size=52><color=#EDED6D>MISS</color></color>";
        }
        else
        {
            bool hasMoreThanOneDamageType = false;
            if (damage.PhysicalDamage > 0)
            {
                dmgTxt += $"<color=red>{Mathf.FloorToInt(damage.PhysicalDamage)}";
                hasMoreThanOneDamageType = true;
            }

            if (damage.MagicalDamage > 0)
            {
                if (hasMoreThanOneDamageType) dmgTxt += '\n';
                dmgTxt += $"<color=#ff00ff>{Mathf.FloorToInt(damage.MagicalDamage)}</color>";
                hasMoreThanOneDamageType = true;
            }

            if (damage.TrueDamage > 0)
            {
                if (hasMoreThanOneDamageType) dmgTxt += '\n';
                dmgTxt += $"<color=#b1b1b1>{Mathf.FloorToInt(damage.TrueDamage)}</color>";
            }
        }

        DisplayDamage(dmgTxt, new(0, 55));
    }

    public void AdjustHealthOnDamageReceive(DamageInstance damage)
    {
        if (damage.TotalDamage <= 0) return;
        health -= damage.TotalDamage;
        if (enteredStatis && health <= 0) health = 1; 
        else if (health < 0) health = 0;
        healthBar.SetHealth(health);

        if (health <= 0)
        {
            if (!canRevive) OnDeath();
            else
            {
                StopAllCoroutines();
                StartCoroutine(Revive());
            }
        }
    }

    public void SetHealth(float health)
    {
        this.health = health;
        if (healthBar) healthBar.SetHealth(health);
    }

    public IEnumerator PulseSprite()
    {
        spriteRenderer.color = Color.red;

        float c = 0, d = 0.5f;
        while (c < d)
        {
            spriteRenderer.color = Color.Lerp(Color.red, InitSpriteColor, c * 1.0f / d);
            c += Time.deltaTime;

            yield return null;
        }

        spriteRenderer.color = InitSpriteColor;
    }

    public void DisplayDamage(string msg)
    {
        DisplayDamage(msg, Vector3.zero);
    }

    public void DisplayDamage(string msg, Vector3 offset)
    {
        if (!gameObject || !DamagePopup) return;

        GameObject popup = Instantiate(DamagePopup, transform.position + offset, Quaternion.identity);
        popup.GetComponent<DamagePopup>().text.text = msg;
    }

    public virtual bool IsAlive() => health > 0;

    public virtual void Move()
    {
        // base example
        if (rb2d.velocity.magnitude != 0) animator.SetBool("attack", false);
    }

    protected virtual void ProcessSkillTree()
    {
        if (!IsAlive()) return;

        ProcessAccelaration();
        ProcessGravity();
        ProcessStatis();
    }

    private float GravityTimerCount = 0f;
    private readonly float PullTick = 0.3f;
    private readonly float BaseRange = 150f;
    private readonly float GrowthRange = 80f;
    private readonly float PullForce_Base = 0.8f, PullForce_Growth = 0.5f;
    protected virtual void ProcessGravity()
    {
        if (!gravity || weight <= 0) return;
        GravityTimerCount += Time.fixedDeltaTime;
        if (GravityTimerCount < PullTick) return;

        GravityTimerCount = 0f;

        float searchRange = BaseRange + GrowthRange * (weight - 1);
        var hits = SearchForEntitiesAroundCertainPoint(typeof(EntityBase), transform.position, searchRange, true)
            .Where(e => e.weight < weight);
        foreach (var hit in hits)
        { 
            float force = PullForce_Base + PullForce_Growth * (weight - hit.weight - 1);
            PullEntityTowards(hit, transform.position, force, 0.09f, true, false);
        }

        ApplyEffect(AffectedStat.MSPD, "GRAVITY_WEIGHT_PENALTY",
            Mathf.Min(50f, GetMultiplicativeValue(100f, 0.1f, weight)) * -1f,
            PullTick,
            true);

        if (GravityCircle && GravityCircle.gameObject.activeSelf) 
            GravityCircle.sizeDelta = new Vector2(searchRange * 2.05f, searchRange * 2.05f);
    }

    public static float GetMultiplicativeValue(float BaseValue, float Jump, short Step)
    {
        float originalBaseValue = BaseValue;
        for (short i = 0; i < Step; i++)
        {
            BaseValue *= (1.0f - Jump);
        }

        return originalBaseValue - BaseValue;
    }

    private Vector3 prevDirection = Vector3.zero;
    private readonly float minDirection = 0.15f;
    private bool prevSpriteFlipX = false;
    private float AcceTimerCount = 0f;
    protected float AccelerationBuffPerSec = 25f, AccelerationBuffMax = 200f;
    private readonly float AcceTick = 0.25f;
    protected virtual void ProcessAccelaration()
    {
        if (!acceleration) return;
        AcceTimerCount += Time.fixedDeltaTime;
        if (AcceTimerCount < AcceTick) return;

        AcceTimerCount = 0f;
        Vector3 direction = rb2d.velocity.normalized;
        Vector3 compareDirection = new (Mathf.Abs(direction.x - prevDirection.x), Mathf.Abs(direction.y - prevDirection.y));
        string effectKey = "ACCELERATION_BUFF";
        float AccelerationBuffPerTick = AccelerationBuffPerSec * AcceTick;

        if (compareDirection.x <= minDirection && compareDirection.y <= minDirection && direction != Vector3.zero && prevSpriteFlipX == spriteRenderer.flipX)
        {
            if (MspdBuffs.ContainsKey(effectKey))
            {
                ApplyEffect(
                    AffectedStat.MSPD,
                    effectKey,
                    Mathf.Min(AccelerationBuffMax, MspdBuffs[effectKey].Value + AccelerationBuffPerTick),
                    9999f,
                    true);
            }
            else
            {
                ApplyEffect(
                    AffectedStat.MSPD,
                    effectKey,
                    AccelerationBuffPerTick,
                    9999f,
                    true);
            }
        }
        else
        {
            float currentBuff = !MspdBuffs.ContainsKey(effectKey) ? 0 : MspdBuffs[effectKey].Value;
            if (currentBuff > 0) 
                ApplyEffect(
                    AffectedStat.MSPD,
                    "ACCELERATE_DIR_CHANGE_PUNISH",
                    Mathf.Lerp(0, 90f, currentBuff / AccelerationBuffMax) * -1,
                    1.5f,
                    true,
                    EffectPersistType.DECAY);

            RemoveEffect(effectKey);
        }

        prevDirection = direction;
        prevSpriteFlipX = spriteRenderer.flipX;
    }

    protected float AtkDebuffPercentage = 50f, RegenBuffPercentage = 0.04f;
    protected float StatisRequiredTimer = 5f;
    private float StatisTimer = 0f;
    private bool enteredStatis = false;
    private GameObject StatisObj;

    public virtual bool IsMoving()
    {
        return rb2d.velocity != Vector2.zero || PrevPosition != transform.position;
    }

    protected virtual void ProcessStatis()
    {
        if (!statis) return;

        string keyAtk = "STATIS_DEBUFF_ATK";
        if (IsMoving())
        {
            StatisTimer += Time.fixedDeltaTime;
            if (StatisTimer >= StatisRequiredTimer && !enteredStatis)
            {
                enteredStatis = true;
                hpRegenPercentage += RegenBuffPercentage;
                ApplyEffect(AffectedStat.ATK, keyAtk, AtkDebuffPercentage * -1f, 9999f, true);
            }
        }
        else
        {
            if (enteredStatis)
            {
                hpRegenPercentage -= RegenBuffPercentage;
                RemoveEffect(keyAtk);
            }
            enteredStatis = false;
            StatisTimer = 0f;
        }

        StatisObj.SetActive(enteredStatis);
    }

    public virtual void StopMovement()
    {
        rb2d.velocity = Vector2.zero;
        animator.SetFloat("move", 0);
    }

    public virtual void ApplyFreeze(EntityBase target, float duration)
    {
        if (!gameObject || !gameObject.activeSelf) return;
        StartCoroutine(ApplyFreezeCoroutine(target, duration));
    }

    IEnumerator ApplyFreezeCoroutine(EntityBase target, float duration)
    {
        yield return new WaitUntil(() => target.IsComponentsInitialized);

        if (target.IsFreezeImmune || !target.IsAlive()) yield break;

        if (target.HasStatusResistant) duration /= 2f;

        target.FreezeTimer = Mathf.Max(target.FreezeTimer, duration);
        target.OnFreezeEnter();
    }

    public virtual void OnFreezeEnter()
    {
        if (IsFreezeImmune || !IsAlive()) return;

        animator.speed = 0f;
        
        StopMovement();
        CancelAttack();

        ccBar.SetActive(true);
        ccSlider.value = ccSlider.maxValue = FreezeTimer;
    }

    public virtual void ApplyStun(EntityBase target, float duration)
    {
        if (!gameObject || !gameObject.activeSelf) return;
        StartCoroutine(ApplyStunCoroutine(target, duration));
    }

    IEnumerator ApplyStunCoroutine(EntityBase target, float duration)
    {
        yield return new WaitUntil(() => target.IsComponentsInitialized);

        if (target.IsStunImmune || !target.IsAlive()) yield break;
        
        if (target.HasStatusResistant) duration /= 2f;

        target.StunTimer = Mathf.Max(target.StunTimer, duration);
        target.OnStunEnter();
    }

    public virtual void OnStunEnter()
    {
        if (IsStunImmune || !IsAlive()) return;

        animator.speed = 0f;
        StopMovement();
        CancelAttack();
        ccBar.SetActive(true);
        ccSlider.value = ccSlider.maxValue = StunTimer;
    }

    public virtual void ApplyLevitate(EntityBase target, float duration)
    {
        ApplyStun(target, duration);
        target.StartCoroutine(target.Levitate(duration));
    }

    public bool IsBeingLevitated = false;
    public SpriteRenderer LevitationEffect;
    Vector2 spriteInitPos;
    float LevitationHeight = 2.1f;

    float floatUpDuration = 0.2f;
    float floatDownDuration = 0.1f;

    IEnumerator Levitate(float duration)
    {
        if (IsBeingLevitated) yield break;

        yield return null; // ensure this runs after stun is applied
        if (!IsStunned) yield break;

        spriteInitPos = spriteRenderer.transform.localPosition;
        LevitationEffect.color = new Color(1, 1, 1, 0.3f);
        IsBeingLevitated = true;

        floatDownDuration = Mathf.Min(0.2f, duration * 0.15f);
        floatDownDuration = floatUpDuration / 2;

        Vector2 targetPos = spriteInitPos + new Vector2(0, LevitationHeight);
        
        float c = 0, d = floatUpDuration;
        while (c < d)
        {
            spriteRenderer.transform.localPosition = Vector2.Lerp(spriteRenderer.transform.localPosition, targetPos, c * 1.0f / d);
            c += Time.deltaTime;
            yield return null;
        }

        spriteRenderer.transform.localPosition = targetPos;
        
        yield return new WaitForSeconds(duration - floatUpDuration - floatDownDuration);

        EndLevitateCoroutine = StartCoroutine(EndLevitate());
    }

    Coroutine EndLevitateCoroutine;
    IEnumerator EndLevitate()
    {
        if (!IsBeingLevitated) yield break;
        
        float c = 0; 
        float d = floatDownDuration;
        while (c < d)
        {
            spriteRenderer.transform.localPosition = Vector2.Lerp(spriteRenderer.transform.localPosition, spriteInitPos, c * 1.0f / d);
            c += Time.deltaTime;
            yield return null;
        }
        spriteRenderer.transform.localPosition = spriteInitPos;
        IsBeingLevitated = false;
        LevitationEffect.color = Color.clear;

        EndLevitateCoroutine = null;
    }

    public virtual void OnFreezeMaintain()
    {
        spriteRenderer.color = Color.blue;
        ccSlider.value = FreezeTimer;
    }

    public virtual void OnFreezeExit()
    {
        spriteRenderer.color = InitSpriteColor;
        if (FreezeTimer > 0f || IsStunned || IsFrozen) return;

        animator.SetBool("attack", false);
        FreezeTimer = 0f;
        animator.speed = 1f;
        ccSlider.value = 0;   
        ccBar.SetActive(false);
    }

    public void EndFreeze()
    {
        FreezeTimer = 0f;
        OnFreezeExit();
    }

    public virtual void OnStunMaintain()
    {
        ccSlider.value = StunTimer;
    }

    public virtual void OnStunExit()
    {
        if (StunTimer > 0f || IsStunned || IsFrozen) return;

        animator.SetBool("attack", false);
        StunTimer = 0f;
        animator.speed = 1f;
        ccSlider.value = 0;
        ccBar.SetActive(false);
    }

    public void EndStun()
    {
        StartCoroutine(EndLevitate());
        StunTimer = 0f;
        OnStunExit();
    }

    public virtual Vector2 CalculateMovement(Vector2 normalizedMovementVector) => CalculateMovement(normalizedMovementVector, moveSpeed);
    public virtual Vector2 CalculateMovement(Vector2 normalizedMovementVector, float speed) => normalizedMovementVector * speed;

    public virtual void OnDeath()
    {
        EndFreeze();
        EndStun();
        LevitationEffect.color = Color.clear;
        ccBar.SetActive(false);
        ShadowSprite.SetActive(false);
        animator.speed = 1f;

        TriggeredOnDeath = true;
        animator.SetTrigger("die");
        healthBar.SetHealth(0);
        healthBar.gameObject.SetActive(false);
        foreach (var c in colliders)
        {
            c.enabled = false;
        }
        rb2d.velocity = Vector2.zero;
        StopAllCoroutines();
        StartCoroutine(StartMovementLockout(999));
        StartCoroutine(StartAttackLockout(999));
        StartCoroutine(EndLevitate());

        foreach (var entity in currentlyShiftedEntities)
        {
            if (!entity) continue;

            entity.BeingShiftState--;
            if (entity.rb2d) entity.rb2d.velocity = Vector2.zero;
        }

        if (EntityManager)
        {
            EntityManager.OnEntityDeath(this.gameObject);
        }
        Destroy(this.gameObject, 4);
        StartCoroutine(SpriteFadeOutOnDeath());
    }

    IEnumerator SpriteFadeOutOnDeath()
    {
        yield return new WaitForSeconds(0.8f);
        float c = 0, d = 0.25f;
        while (c < d)
        {
            spriteRenderer.color = Color.Lerp(InitSpriteColor, Color.black, c * 1.0f / d);
            c += Time.deltaTime;
            yield return null;
        }
        spriteRenderer.color = Color.black;

        c = 0; d = 0.5f;
        while (c < d)
        {
            spriteRenderer.color = Color.Lerp(Color.black, new Color(0, 0, 0, 0), c * 1.0f / d);
            c += Time.deltaTime;
            yield return null;
        }
        spriteRenderer.color = new Color(0, 0, 0, 0);
    }

    protected bool attacking = false;
    public virtual IEnumerator Attack()
    {
        if (!CanAttack || IsAttackLocked)
        {
            AttackCoroutine = null;
            yield break;
        }

        attacking = true;
        animator.SetBool("attack", true);
        LockoutMovementOnAttackCoroutine = StartCoroutine(LockoutMovementsOnAttack());
    }

    // Called by the animation event
    public virtual IEnumerator OnAttackComplete()
    {
        attacking = false;
        yield break;
    }

    public virtual IEnumerator LockoutMovementsOnAttack()
    {
        // base example
        if (IsAttackLocked)
        {
            LockoutMovementOnAttackCoroutine = AttackCoroutine = null;
            yield break;
        }

        StartCoroutine(StartAttackLockout(GetAttackLockoutTime()));

        StartCoroutine(StartMovementLockout(GetWindupTime() * 1.5f));

        yield return new WaitForSeconds(GetAttackAnimationLength());
        if (!IsFrozen && !IsStunned) animator.SetBool("attack", false);

        LockoutMovementOnAttackCoroutine = null;
        AttackCoroutine = null;
        attacking = false;
    }

    public float GetWindupTime() => attackWindupTime * (100 / Mathf.Max(20, ASPD));

    public float GetAttackInterval() => attackInterval * (100 / Mathf.Max(20, ASPD));

    public float GetAttackAnimationLength() => 
        AttackAnimation 
            ? AttackAnimation.length / preferredAttackAnimationSpeed / animator.GetFloat("a_speed_value")
            : 0;

    public float GetAttackLockoutTime() 
        => Mathf.Max(
                GetWindupTime(),
                GetAttackInterval(),
                GetAttackAnimationLength()
            );

    public virtual void CancelAttack()
    {
        if (!IsFrozen && !IsStunned) animator.SetBool("attack", false);

        attacking = false;

        if (AttackCoroutine != null)
        {
            StopCoroutine(AttackCoroutine);
            AttackCoroutine = null;
            AttackLockout = (short)Mathf.Max(AttackLockout, 0);
        }

        if (LockoutMovementOnAttackCoroutine != null)
        {
            StopCoroutine(LockoutMovementOnAttackCoroutine);
            LockoutMovementOnAttackCoroutine = null;
        }
    }

    public virtual void Heal(float amount, bool healThroughDead = false)
    {
        Heal(amount, this, healThroughDead);
    }

    [SerializeField] protected float HealingEffectiveness = 1f;
    public virtual void Heal(float amount, EntityBase target, bool healThroughDead = false, bool displayMsg = true)
    {
        if (!target || amount <= 0 || (!target.IsAlive() && !healThroughDead)) return;
        
        amount *= HealingEffectiveness;

        if (displayMsg) target.DisplayDamage("<color=green>+" + Mathf.CeilToInt(amount) + "</color>", new Vector3(0, 55));
        target.health += amount;
        if (target.health > target.mHealth) target.health = target.mHealth;
        target.healthBar.SetHealth(target.health);
    }

    [SerializeField] protected float reviveDuration = 5;
    [SerializeField] protected float postReviveDuration = 0;
    public virtual IEnumerator Revive()
    {
        if (!canRevive) yield break;

        float lockoutDuration = reviveDuration + postReviveDuration + 1f;
        animator.SetTrigger("die");
        health = 1;
        StartCoroutine(StartMovementLockout(lockoutDuration));
        StartCoroutine(StartAttackLockout(lockoutDuration));
        SetInvulnerable(lockoutDuration);

        yield return new WaitForSeconds(1f);

        float c = 0;
        while (c < reviveDuration)
        {
            health = (int) Mathf.Lerp(1, mHealth, c * 1.0f / reviveDuration);
            SetHealth(health);
            c += Time.deltaTime;
            yield return null;
        }

        animator.SetTrigger("revive");
        health = mHealth;
        SetHealth(health);
        canRevive = false;
    }

    public void PullEntityTowards(EntityBase targetEntity, Transform targetPosition, float pullForce, float duration, bool hasReferencePosition = true, bool cancelAction = true)
        => StartCoroutine(ApplyForceCoroutine(targetEntity, targetPosition.position, pullForce, duration, true, hasReferencePosition, cancelAction));

    public void PushEntityFrom(EntityBase targetEntity, Transform sourcePosition, float pushForce, float duration, bool hasReferencePosition = true, bool cancelAction = true)
        => StartCoroutine(ApplyForceCoroutine(targetEntity, sourcePosition.position, pushForce, duration, false, hasReferencePosition, cancelAction));

    public void PullEntityTowards(EntityBase targetEntity, Vector3 targetPosition, float pullForce, float duration, bool hasReferencePosition = true, bool cancelAction = true)
        => StartCoroutine(ApplyForceCoroutine(targetEntity, targetPosition, pullForce, duration, true, hasReferencePosition, cancelAction));

    public void PushEntityFrom(EntityBase targetEntity, Vector3 sourcePosition, float pushForce, float duration, bool hasReferencePosition = true, bool cancelAction = true)
        => StartCoroutine(ApplyForceCoroutine(targetEntity, sourcePosition, pushForce, duration, false, hasReferencePosition, cancelAction));

    public static int GetConversionWeight(short inWeight)
    {
        return inWeight switch
        {
            0 => 0,
            1 => 1,
            2 => 3,
            3 => 5,
            4 => 7,
            5 => 10,
            6 => 14,
            _ => inWeight * 7 / 3 + 1,
        };
    }
    
    private List<EntityBase> currentlyShiftedEntities = new();
    private IEnumerator ApplyForceCoroutine(EntityBase targetEntity, Vector3 referencePosition, float force, float duration, bool isPull, bool hasReferencePosition = true, bool cancelAction = true)
    {
        if (targetEntity == null || targetEntity.rb2d == null || !targetEntity.IsAlive() || targetEntity.IsBeingLevitated || targetEntity.IsShiftImmune) yield break;

        float ForceValue = force * duration / 0.03f;
        
        float ForceValueAfterWeight = ForceValue - GetConversionWeight(targetEntity.weight);
        if (!cancelAction) ForceValueAfterWeight = ForceValue;

        if (ForceValueAfterWeight <= 0.4f) yield break;

        if (targetEntity is EnemyBase e)
        {
            e.StopObstacleIgnore();
        }

        bool ShiftDoesDamage = this is PlayerBase b && targetEntity != this && b.Skills.Contains(SkillTree_Manager.SkillName.CERTAIN_FATES);

        float multiplier = ForceValueAfterWeight / ForceValue;
        force *= multiplier;

        if (cancelAction)
        {
            targetEntity.StopMovement();
            targetEntity.CancelAttack();
            StartCoroutine(targetEntity.StartMovementLockout(duration + 0.1f));
        }

        targetEntity.BeingShiftState++;
        currentlyShiftedEntities.Add(targetEntity);

        float elapsedTime = 0f;
        float initialDistance = Vector3.Distance(targetEntity.transform.position, referencePosition);
        float minDistanceForPull = 20f;

        if (hasReferencePosition && initialDistance <= minDistanceForPull && isPull)
        {
            yield break;
        }

        float lastDistance = initialDistance;

        while (elapsedTime < duration)
        {
            if (!targetEntity || !targetEntity.rb2d) yield break;
            else if (!targetEntity.IsAlive())
            {
                targetEntity.rb2d.velocity = Vector2.zero;
                yield break;
            }

            float currentDistance = Vector3.Distance(targetEntity.transform.position, referencePosition);

            if (isPull)
            {
                // Stop if reached minimum distance
                if (hasReferencePosition && currentDistance <= minDistanceForPull)
                {
                    targetEntity.rb2d.velocity = Vector2.zero;
                    break;
                }

                // Stop if distance is increasing (overshot the target)
                if (currentDistance > lastDistance && elapsedTime > 0.05f) // Small grace period
                {
                    targetEntity.rb2d.velocity = Vector2.zero;
                    break;
                }
            }

            Vector3 directionVector;
            if (hasReferencePosition)
            {
                directionVector = isPull
                            ? (referencePosition - targetEntity.transform.position).normalized
                            : (targetEntity.transform.position - referencePosition).normalized;
            }
            else directionVector = referencePosition;

            ForceMode2D mode =ForceMode2D.Force;
            if (isPull)
            {
                float progress = 1f - (currentDistance / initialDistance);
                float easedForce = force * (1f - Mathf.Pow(1f - progress, 3f));
                float finalForce = Mathf.Max(0.3f * force, easedForce);
                targetEntity.rb2d.AddForce(directionVector * finalForce, mode);
            }
            else
            {
                float timeProgress = elapsedTime / duration;
                float decayedForce = force * (1f - timeProgress * 0.5f);
                targetEntity.rb2d.AddForce(directionVector * decayedForce, mode);
            }

            if (ShiftDoesDamage)
            {
                float damage = ForceValueAfterWeight * 0.85f;
                if (isPull) damage *= 0.7f;

                DealDamage(targetEntity, new DamageInstance(0, (int)damage, 0));
            }

            lastDistance = currentDistance;
            elapsedTime += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        // Ensure entity stops at the end
        if (targetEntity.rb2d) targetEntity.rb2d.velocity = Vector2.zero;
        targetEntity.BeingShiftState--;
        currentlyShiftedEntities.Remove(targetEntity);
    }

    private void OnDestroy()
    {
        foreach (var entity in currentlyShiftedEntities)
        {
            if (!entity) continue;

            entity.BeingShiftState--;
            if (entity.rb2d) entity.rb2d.velocity = Vector2.zero;
        }
    }

    public void CreateProjectileAndShootToward(EntityBase target, ProjectileScript.ProjectileType projectileType, float acceleration = 0)
    {
        CreateProjectileAndShootToward(ProjectilePrefab, GetInstanceBasedOnDamagetype(), target, AttackPosition.position, target.transform.position, projectileType, ProjectileSpeed, acceleration);
    }

    public void CreateProjectileAndShootToward(EntityBase target, ProjectileScript.ProjectileType projectileType, float travelSpeed, float acceleration)
    {
        CreateProjectileAndShootToward(ProjectilePrefab, GetInstanceBasedOnDamagetype(), target, AttackPosition.position, target.transform.position, projectileType, travelSpeed, acceleration);
    }

    public void CreateProjectileAndShootToward(EntityBase target, Vector3 targetPosition, ProjectileScript.ProjectileType projectileType, float acceleration = 0)
    {
        CreateProjectileAndShootToward(ProjectilePrefab, GetInstanceBasedOnDamagetype(), target, AttackPosition.position, targetPosition, projectileType, ProjectileSpeed, acceleration);
    }

    public void CreateProjectileAndShootToward(EntityBase target, Vector3 spawnPosition, Vector3 targetPosition, ProjectileScript.ProjectileType projectileType, float acceleration = 0)
    {
        CreateProjectileAndShootToward(ProjectilePrefab, GetInstanceBasedOnDamagetype(), target, spawnPosition, targetPosition, projectileType, ProjectileSpeed, acceleration);
    }

    public void CreateProjectileAndShootToward(EntityBase target, Vector3 spawnPosition, Vector3 targetPosition, ProjectileScript.ProjectileType projectileType, float travelSpeed, float acceleration = 0)
    {
        CreateProjectileAndShootToward(ProjectilePrefab, GetInstanceBasedOnDamagetype(), target, spawnPosition, targetPosition, projectileType, travelSpeed, acceleration);
    }

    public void CreateProjectileAndShootToward(EntityBase target, DamageInstance damageInstance, ProjectileScript.ProjectileType projectileType, float acceleration = 0)
    {
        CreateProjectileAndShootToward(ProjectilePrefab, damageInstance, target, AttackPosition.position,target.transform.position, projectileType, ProjectileSpeed, acceleration);
    }

    public void CreateProjectileAndShootToward(EntityBase target, DamageInstance damageInstance, Vector3 targetPosition, ProjectileScript.ProjectileType projectileType, float acceleration = 0)
    {
        CreateProjectileAndShootToward(ProjectilePrefab, damageInstance, target, AttackPosition.position, targetPosition, projectileType, ProjectileSpeed, acceleration);
    }

    public void CreateProjectileAndShootToward(GameObject ProjectilePref, DamageInstance damageInstance, EntityBase target, Vector3 spawnPosition, Vector3 preferPosition, ProjectileScript.ProjectileType projectileType, float travelSpeed = 1000, float acceleration = 0, float lifeSpan = 8)
    {
        if (!ProjectilePref) return;

        GameObject projectile = Instantiate(ProjectilePref, spawnPosition, Quaternion.identity);
        ProjectileScript projectileScript = projectile.GetComponent<ProjectileScript>();
        if (!projectileScript) return;

        if (absolutism)
        {
            lifeSpan = Mathf.Min(15f, lifeSpan * 1.5f);
        }

        projectileScript.ProjectileFirer = this;
        projectileScript.DamageInstance = damageInstance;
        projectileScript.TravelSpeed = travelSpeed;
        projectileScript.Acceleration = acceleration;
        projectileScript.ShootTowards(preferPosition, target, projectileType, lifeSpan);
    }

    public void CreateProjectileAndShootToward(GameObject ProjectilePref, DamageInstance damageInstance, Vector3 spawnPosition, Vector3 preferPosition, ProjectileScript.ProjectileType projectileType, float travelSpeed = 1000, float acceleration = 0, float lifeSpan = 8, params Type[] targetType)
    {
        if (!ProjectilePref) return;

        GameObject projectile = Instantiate(ProjectilePref, spawnPosition, Quaternion.identity);
        ProjectileScript projectileScript = projectile.GetComponent<ProjectileScript>();
        if (!projectileScript) return;

        if (absolutism)
        {
            lifeSpan = Mathf.Min(15f, lifeSpan * 1.5f);
        }

        projectileScript.ProjectileFirer = this;
        projectileScript.DamageInstance = damageInstance;
        projectileScript.TravelSpeed = travelSpeed;
        projectileScript.Acceleration = acceleration;
        projectileScript.ShootTowards(preferPosition, projectileType, lifeSpan, false, targetType);
    }

    public virtual List<EntityBase> SearchForEntitiesAroundSelf(Type type = null, bool catchInvisibles = false, short take = -1)
    {
        return SearchForEntitiesAroundCertainPoint(type, transform.position, attackRange, catchInvisibles, take);
    }

    public virtual List<EntityBase> SearchForEntitiesAroundSelf(Type type, float range, bool catchInvisibles = false, short take = -1)
    {
        return SearchForEntitiesAroundCertainPoint(type, transform.position, range, catchInvisibles, take);
    }

    public virtual List<EntityBase> SearchForEntitiesAroundSelf(float r, Type type = null, bool catchInvisibles = false, short take = -1)
    {
        return SearchForEntitiesAroundCertainPoint(type, transform.position, r, catchInvisibles, take);
    }

    public List<EntityBase> SearchForEntitiesAroundCertainPoint(Type type, Vector2 pos, float r, bool catchInvisibles = false, short take = -1)
    {
        var result = Base_SearchForEntitiesAroundCertainPoint(type, pos, r, catchInvisibles, take);
        if (!catchInvisibles && !IsStandingOnEnvironmentalTile(EnvironmentType.DARK_ZONE))
        {
            result = result.Where(e => e && !e.IsStandingOnEnvironmentalTile(EnvironmentType.DARK_ZONE)).ToList();
        }
        
        return take < 0 ? result : result.Take(take).ToList();
    }

    public static List<EntityBase> Base_SearchForEntitiesAroundCertainPoint(Type type, Vector2 pos, float r, bool catchInvisibles = false, short take = -1)
    {
        List<EntityBase> entityBases = new List<EntityBase>();

        if (r >= 9999f)
        {
            entityBases = EntityManager.Entities
                .Where(entity => 
                    entity 
                    && entity.colliders.Any(c => c.enabled) 
                    && entity.IsComponentsInitialized 
                    && entity.IsAlive() 
                    && (!entity.isInvisible || catchInvisibles) 
                    && type != null 
                    && type.IsAssignableFrom(entity.GetType()))
                .ToList();
        }
        else
        {
            Collider2D[] collider2Ds = Physics2D.OverlapCircleAll(pos, r);
            foreach (Collider2D collider in collider2Ds)
            {
                EntityBase entity = collider.GetComponent<EntityBase>();
                if (!entity
                    || !entity.IsComponentsInitialized
                        || !entity.IsAlive()
                            || (entity.isInvisible && !catchInvisibles)
                                || entityBases.Contains(entity)
                                    || (type != null && !type.IsAssignableFrom(entity.GetType())))
                    continue;

                PlayerBase pb = entity as PlayerBase;
                if (pb && !pb.SettleSwappedInPlayer) continue;

                entityBases.Add(entity);
            }
        }

        if (entityBases.Count == 1 || entityBases.Count <= take) return entityBases;
        if (take > 0) entityBases = entityBases.OrderBy(e => Vector2.Distance(e.transform.position, pos)).ToList();
        
        return entityBases;
    }

    public virtual EntityBase SearchForNearestEntityAroundSelf(Type type = null, bool catchInvisible = false)
    {
        return SearchForNearestEntityAroundCertainPoint(type, AttackPosition.position, attackRange, catchInvisible);
    }

    public static EntityBase Base_SearchForNearestEntityAroundCertainPoint(Type type, Vector2 pos, float r, bool catchInvisibles = false)
    {
        var result = Base_SearchForEntitiesAroundCertainPoint(type, pos, r, catchInvisibles, 1);
        if (result == null || result.Count <= 0) return null;

        return result[0];
    }

    public virtual EntityBase SearchForNearestEntityAroundCertainPoint(Type type, Vector2 pos, float r, bool catchInvisibles = false)
    {
        var result = Base_SearchForEntitiesAroundCertainPoint(type, pos, r, catchInvisibles, 1);
        if (result == null || result.Count <= 0) return null;

        if (!catchInvisibles && !IsStandingOnEnvironmentalTile(EnvironmentType.DARK_ZONE))
        {
            result = result.Where(e => e && !e.IsStandingOnEnvironmentalTile(EnvironmentType.DARK_ZONE)).ToList();
        }

        if (result == null || result.Count <= 0) return null;
        return result.Count<= 0 ? null : result[0];
    }
}
