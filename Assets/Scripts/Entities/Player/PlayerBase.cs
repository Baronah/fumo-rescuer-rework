using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static LevelDifficultyModifier;
using static PlayerManager;
using static SkillTree_Manager;
using SkillType = PlayerManager.SkillType;

public class PlayerBase : EntityBase
{
    public bool SettleSwappedInPlayer = false;
    public LayerMask ObstacleLayers;
    public Sprite AttackSprite, SkillSprite, SpecialSprite;
    public string AttackDes, SkillName, SkillDes, SpecialName, SpecialDes;
    
    protected static PlayerManager playerManager => PlayerManager._instance;

    [SerializeField] protected GameObject HH_Effect_parent;
    [SerializeField] private Material HH_Fill_Material;
    [SerializeField] protected Image HH_Effect_fill;

    [SerializeField] protected GameObject RockEffect, VowEffect;
    
    [SerializeField] protected GameObject WindanthemBar, WindanthemMaxEffect;
    protected Slider WindanthemSlider;
    protected TMP_Text WindanthemCounter;

    public List<SkillName> Skills = new();

    protected Coroutine SkillCoroutine = null;

    protected string WindAnthemKey = "WIND_ANTHEM_BUFF";
    [SerializeField] protected float WindAnthemAspdBuffAmount = 15f, WindAnthemAspdBuffDuration = 15f, WindAnthemAspdBuffCap = 75f;
    protected bool IsWindAnthemMaxed => AspdBuffs.ContainsKey(WindAnthemKey) && AspdBuffs[WindAnthemKey].IsInEffect && AspdBuffs[WindAnthemKey].Value >= WindAnthemAspdBuffCap;

    public virtual PlayerType GetPlayerType()
    {
        return playerManager.PlayerStartType;
    }

    public override System.Type GetGenericType() => typeof(PlayerBase);

    private void Update()
    {
        GetControlInputs();
    }

    public override void InitializeComponents()
    {
        if (IsComponentsInitialized) return;
        ObstacleLayers = LayerMask.GetMask("Obstacle", "OnedirectionalPassage", "Border");

        StageManager.OnPlayerSpawn(this);

        playerManager.Register(this);

        GetSkillTreeEffects();
        StageManager.ProcessPlayerSkillTree(this);

        HH_Effect_parent.SetActive(Skills.Contains(SkillTree_Manager.SkillName.HEAVY_HITTER));
        WindanthemSlider = WindanthemBar.GetComponentInChildren<Slider>();
        WindanthemCounter = WindanthemBar.GetComponentInChildren<TMP_Text>();

        base.InitializeComponents();
        FeetPosition = transform.Find("Feetposition");

        SetInvulnerable(1f);

        IsComponentsInitialized = true;
    }

    [SerializeField] GameObject AllowVow;
    public override void FixedUpdate()
    {
        if (Time.timeScale <= 0) return;
        base.FixedUpdate();

        bool alive = IsAlive();

        if (alive)
        {
            IdeasBuff();
            AttentionBuff();
            WindBladeBuff();
        }

        WindanthemBar.SetActive(alive && AspdBuffs.ContainsKey(WindAnthemKey) && AspdBuffs[WindAnthemKey].IsInEffect);
        if (WindanthemBar.activeSelf)
        {
            WindanthemSlider.maxValue = WindAnthemAspdBuffDuration;
            WindanthemSlider.value = AspdBuffs[WindAnthemKey].Duration;

            WindanthemCounter.text = ((int)(AspdBuffs[WindAnthemKey].Value / WindAnthemAspdBuffAmount)).ToString();
        }

        WindanthemMaxEffect.SetActive(alive && IsWindAnthemMaxed);

        AllowVow.SetActive(alive && canVow && !playerManager.hasVowed);
    }

    protected override bool TriggerHunger()
    {
        return base.TriggerHunger() && IsMoving() && !Skills.Contains(SkillTree_Manager.SkillName.EQUIPMENT_PROVISIONS);
    }

    float w_countUp = 0;
    void WindBladeBuff()
    {
        w_countUp += Time.fixedDeltaTime;
        if (w_countUp < 0.15f) return;
        w_countUp = 0;

        if (!Skills.Contains(SkillTree_Manager.SkillName.WINGED_STEPS_B)) return;

        float amount = b_moveSpeed == 0
            ? 0 
            : (moveSpeed - b_moveSpeed) / (b_moveSpeed * 0.01f);

        if (amount > 0) ApplyEffect(Effect.AffectedStat.ASPD, "WIND_BLADE_BUFF", amount * 0.7f, 0.15f, false);
    }

    float i_countUp = 0;
    readonly float i_UltimateRefundBase = 0.03f, i_SpecialRefundBase = 0.05f;
    bool previouslyMoving = true;
    short i_ContiuousCounter = 0;
    void IdeasBuff()
    {
        i_countUp += Time.fixedDeltaTime;
        if (i_countUp < 1f) return;
        float interval = i_countUp;
        i_countUp = 0;

        if (!Skills.Contains(SkillTree_Manager.SkillName.WINGED_STEPS_C)) return;
        
        bool isMoving = rb2d.velocity.magnitude > 0.1f;
        if (isMoving == previouslyMoving) i_ContiuousCounter = (short) Mathf.Min(i_ContiuousCounter + 1, 3);
        else i_ContiuousCounter = 0;

        if (isMoving)
        {
            float reduceAmount = interval * (i_UltimateRefundBase * (1f + 0.5f * i_ContiuousCounter));
            ReduceUltimateCooldown(reduceAmount, CooldownReductionType.PERCENTAGE_CURRENT);
        }
        else
        {
            float reduceAmount = interval * (i_SpecialRefundBase * (1f + 0.5f * i_ContiuousCounter));
            ReduceSpecialCooldown(reduceAmount, CooldownReductionType.PERCENTAGE_CURRENT);
        }

        previouslyMoving = isMoving;
    }

    float a_countUp = 0;
    short attentionMaxReduction = 80;
    float attentionMinLifesteal = 0.35f;
    bool hasDamageReductionBuffFromAttentionMaxed = false,
         hasLifestealBuffFromAttentionMin = false;
    void AttentionBuff()
    {
        a_countUp += Time.fixedDeltaTime;
        if (a_countUp < 0.2f) return;
        a_countUp = 0;

        if (health < mHealth && hasDamageReductionBuffFromAttentionMaxed)
        {
            hasDamageReductionBuffFromAttentionMaxed = false;
            damageReduction -= attentionMaxReduction;
        }

        if (health > mHealth * 0.3f && hasLifestealBuffFromAttentionMin)
        {
            hasLifestealBuffFromAttentionMin = false;
            lifeSteal -= attentionMinLifesteal;
        }

        if (health >= mHealth * 0.85f && Skills.Contains(SkillTree_Manager.SkillName.ATTENTION_BOOK))
        {
            ApplyEffect(Effect.AffectedStat.ATK, "ATTENTION_BUFF", 30, 0.22f, true);
            ApplyEffect(Effect.AffectedStat.ASPD, "ATTENTION_BUFF", 20, 0.22f, true);

            if (health >= mHealth && !hasDamageReductionBuffFromAttentionMaxed)
            {
                hasDamageReductionBuffFromAttentionMaxed = true;
                damageReduction += attentionMaxReduction;
            }
        }
        
        if (health <= mHealth * 0.6f && Skills.Contains(SkillTree_Manager.SkillName.ATTENTION_DEVICE))
        {
            ApplyEffect(Effect.AffectedStat.DEF, "ATTENTION_BUFF", 30, 0.22f, false);
            ApplyEffect(Effect.AffectedStat.RES, "ATTENTION_BUFF", 20, 0.22f, false);

            if (health <= mHealth * 0.3f && !hasLifestealBuffFromAttentionMin)
            {
                hasLifestealBuffFromAttentionMin = true;
                lifeSteal += attentionMinLifesteal;
            }
        }
    }

    public override void UpdateCooldowns()
    {
        base.UpdateCooldowns();
        if (Skills.Contains(SkillTree_Manager.SkillName.HEAVY_HITTER))
        {
            timerSinceLastAttack += Time.fixedDeltaTime;
            HH_Effect_fill.fillAmount = Mathf.Lerp(0, 1f, timerSinceLastAttack / heavyHitterMaxTimer);
            HH_Effect_fill.color = IsHeavyHitterMaxed ? Color.white : new(0.81f, 0.12f, 0.12f);
            HH_Effect_fill.material = IsHeavyHitterMaxed ? null : HH_Fill_Material;
        }

        leverCDTimer += Time.fixedDeltaTime;
    }

    readonly string StartMspdBuffKey = "SWAP_START_MSPDBUFF";
    public virtual void OnFieldEnter()
    {
        short diff = (short)(CharacterPrefabsStorage.DifficultyLevel - 1);
        if (diff >= (int) DiffType.PlayerFieldDebuff_1) StartCoroutine(DifficultModifierNegativeStatus(diff));

        if (diff >= (int) DiffType.Player1HP && playerManager.IsFirstTimeStageEnter)
        {
            SetHealth(1);
            StartCoroutine(HealingEffectivenessDebuff());
            playerManager.IsFirstTimeStageEnter = false;
        }

        GetVow();
        if (Skills.Contains(SkillTree_Manager.SkillName.SWAP_START_ATK))
        {
            Heal((mHealth - health) * 0.3f);
            ApplyEffect(Effect.AffectedStat.ATK, "SWAP_START_ATKBUFF", 75f, 5f, true, EffectPersistType.PERSIST);
        }

        if (Skills.Contains(SkillTree_Manager.SkillName.SWAP_START_MSPD))
        {
            ApplyEffect(Effect.AffectedStat.MSPD, StartMspdBuffKey, 50f, 9999f, true, EffectPersistType.PERSIST, false);
        }
    }

    IEnumerator HealingEffectivenessDebuff()
    {
        HealingEffectiveness -= 0.9f;
        
        int loop = 80;
        float add = 0.9f / loop;

        for (int i = 0; i < loop; i++)
        {
            yield return new WaitForSeconds(0.5f);
            HealingEffectiveness += add;
        }
    }

    IEnumerator DifficultModifierNegativeStatus(short diff)
    {
        if (diff < (int)DiffType.PlayerFieldDebuff_1) yield break;

        if (diff == (int)DiffType.PlayerFieldDebuff_1) yield return new WaitForSeconds(10f);
        else yield return new WaitForSeconds(5f);

        float c = 0, d = 40f;
        while (c < d)
        {
            float Strength_Scale80To0 = Mathf.Lerp(0, 80f, c * 1.0f / d),
                  Strength_Scale50To0 = Mathf.Lerp(0, 50f, c * 1.0f / d);

            ApplyEffect(Effect.AffectedStat.ATK, "DIFFICULT_HANDICAP_ATK", Strength_Scale80To0 * -1, 9999f, true);

            if (diff >= (int) DiffType.PlayerFieldDebuff_2)
                ApplyEffect(Effect.AffectedStat.MSPD, "DIFFICULT_HANDICAP_MSPD", Strength_Scale50To0 * -1, 9999f, true);

            c += 1;
            yield return new WaitForSeconds(1f);
        }
    }

    protected bool FireWorkStarted = false;
    protected IEnumerator FireWork_Special()
    {
        FireWorkStarted = true;

        float duration = 5f, c = 0f, intervalCount = 0, interval = 0.5f;
        while (c < duration)
        {
            if (intervalCount >= interval)
            {
                ReduceSpecialCooldown(interval * 2f, CooldownReductionType.FLAT);
                intervalCount = 0;
            }

            intervalCount += Time.deltaTime;
            c += Time.deltaTime;
            yield return null;
        }
    }

    protected bool Debut = false;
    protected virtual IEnumerator SpecialLockout()
    {
        if (!Debut && !FireWorkStarted && Skills.Contains(SkillTree_Manager.SkillName.SWAP_START_SPECIAL))
            StartCoroutine(FireWork_Special());
        yield return null;
    }

    protected virtual IEnumerator UltimateLockout()
    {
        yield return null;
    }

    protected bool canVow = false;
    private List<SkillTree_Manager.SkillName> RockBonusSkill = new()
    {
        SkillTree_Manager.SkillName.WINGED_STEPS_A,
        SkillTree_Manager.SkillName.WINGED_STEPS_B,
        SkillTree_Manager.SkillName.WINGED_STEPS_C,
        SkillTree_Manager.SkillName.SWAP_START_ATK,
        SkillTree_Manager.SkillName.SWAP_START_SPECIAL,
        SkillTree_Manager.SkillName.SWAP_START_MSPD,
        SkillTree_Manager.SkillName.BREAK_THE_ICE,
        SkillTree_Manager.SkillName.CERTAIN_FATES,
        SkillTree_Manager.SkillName.BUBBLE_ARTS,
        SkillTree_Manager.SkillName.HEAVY_HITTER,
        SkillTree_Manager.SkillName.SPECIAL_MSPD,
        SkillTree_Manager.SkillName.ULTIMATE_BUFF,
        SkillTree_Manager.SkillName.EQUIPMENT_SCOPE,
        SkillTree_Manager.SkillName.EQUIPMENT_PROVISIONS,
        SkillTree_Manager.SkillName.EQUIPMENT_BLADE,
        SkillTree_Manager.SkillName.ATTENTION_BOOK,
        SkillTree_Manager.SkillName.ATTENTION_DEVICE,
        SkillTree_Manager.SkillName.VICTORY_ATK,
        SkillTree_Manager.SkillName.VICTORY_REFRESH,
    };

    [SerializeField] private GameObject RockPickEffect;

    public virtual void GetSkillTreeEffects()
    {
        var SelectedSkills = CharacterPrefabsStorage.Skills.Keys.ToList();

        RockGachaSkill rockGacha = RockPickEffect.GetComponent<RockGachaSkill>();

        if (SelectedSkills.Contains(SkillTree_Manager.SkillName.A_NICE_LOOKING_ROCK))
        {
            RockBonusSkill.RemoveAll(s => SelectedSkills.Contains(s));

            if (RockBonusSkill.Count > 0)
            {
                SkillName bonusSkill = RockBonusSkill[Random.Range(0, RockBonusSkill.Count)];
                SelectedSkills.Add(bonusSkill);

                GameObject o = Instantiate(RockPickEffect, transform.position + new Vector3(0, 100), Quaternion.identity, transform);
                RockGachaSkill thisRock = o.GetComponent<RockGachaSkill>();
                thisRock.SetSkill(bonusSkill);

                playerManager.PlayerInvoke_SetSkillUI(
                    bonusSkill,
                    thisRock.GetSkillImage(bonusSkill),
                    new Color(0f, 1f, 0.67f));
            }
        }

        SelectedSkills = SelectedSkills.OrderBy(s => s).ToList();
        foreach (var skill in SelectedSkills)
        {
            Skills.Add(skill);

            switch (skill)
            {
                case SkillTree_Manager.SkillName.WINGED_STEPS_A:
                    ASPD += 20;
                    break;

                case SkillTree_Manager.SkillName.WINGED_STEPS_B:
                    ApplyEffect(Effect.AffectedStat.MSPD, "WINGED_STEPS_B_BUFF", 10f, 9999f, true, EffectPersistType.PERSIST);
                    break;

                case SkillTree_Manager.SkillName.EQUIPMENT_BLADE:
                    defPen += 15;
                    break;

                case SkillTree_Manager.SkillName.EQUIPMENT_SCOPE:
                    b_attackRange *= 1.2f;
                    break;

                case SkillTree_Manager.SkillName.EQUIPMENT_PROVISIONS:
                    HealingEffectiveness += 0.25f;
                    break;

                case SkillTree_Manager.SkillName.HEAVY_HITTER:
                    ASPD -= 40;
                    bAtk += (short)(bAtk * 0.2f);
                    break;

                case SkillTree_Manager.SkillName.A_NICE_LOOKING_ROCK:
                    mHealth += (mHealth * 0.052f);
                    bAtk = (short)(bAtk * 1.052f);
                    b_moveSpeed += b_moveSpeed * 0.052f;
                    break;

                case SkillTree_Manager.SkillName.HAIR_RIBBON:
                    PlayerType playerType = GetPlayerType();

                    if (CharacterPrefabsStorage.startingPlayer == playerType)
                    {
                        bAtk = (short)(bAtk * 1.3f);

                        if (playerType == PlayerType.MELEE)
                        {
                            mHealth += (int)(mHealth * 0.15f);
                        }
                        else if (playerType == PlayerType.RANGED)
                        {
                            b_moveSpeed += b_moveSpeed * 0.2f;
                        }
                    }
                    break;

                case SkillTree_Manager.SkillName.CERTAIN_FATES:
                    weight++;
                    break;
            }
        }

        canVow = Skills.Contains(SkillTree_Manager.SkillName.KNOTS);

        if (playerManager && playerManager.MintBlessing)
        {
            playerManager.PlayerInvoke_SetSkillUI(
                SkillTree_Manager.SkillName.AMULET, 
                rockGacha.GetSkillImage(SkillTree_Manager.SkillName.AMULET),
                new Color(0, 0.8f, 1f));
        }
    }

    public virtual void OnFieldSwapOut(PlayerBase swapInPlayer)
    {
        swapInPlayer.timerSinceLastAttack = timerSinceLastAttack;
        swapInPlayer.environmentalTilesStandingOn = new(this.environmentalTilesStandingOn);

        List<Dictionary<string, Effect>> allBuffs = AllBuffs();
        foreach (var dictionary in allBuffs)
        {
            foreach (var kvp in dictionary)
            {
                Effect buff = kvp.Value;
                if (!buff.TransferOnSwap) continue;
                swapInPlayer.ApplyEffect(buff.affectedStat, kvp.Key, buff.Value, buff.Duration, buff.IsPercentage, buff.DecayOverDuration ? EffectPersistType.DECAY : EffectPersistType.PERSIST);
            }
        }

        swapInPlayer.enemyDefeatCount = enemyDefeatCount;
        swapInPlayer.specialCastCount = specialCastCount;

        swapInPlayer.SettleSwappedInPlayer = true;

        foreach (var skill in Skills)
        {
            playerManager.PlayerInvoke_RemoveSkillUI(skill);
        }
    }

    protected override float GetRegenAmount()
    {
        float regenAmount = base.GetRegenAmount();
        float provisionAdd = 0;
        if (Skills.Contains(SkillTree_Manager.SkillName.EQUIPMENT_PROVISIONS))
        {
            provisionAdd = mHealth * 0.01f + (mHealth - health) * 0.02f;
        }

        return regenAmount + provisionAdd;
    }

    protected void MakeVow(PlayerManager.SkillType skillType)
    {
        if (!Skills.Contains(SkillTree_Manager.SkillName.KNOTS) || playerManager.hasVowed || skillType == SkillType.NONE) return;

        SkillType seal;

        if (skillType == SkillType.SPECIAL)
            seal = SkillType.ULTIMATE;
        else
            seal = SkillType.SPECIAL;

        playerManager.SetSealSkill(this, seal);
        GameObject vowEffect = Instantiate(VowEffect, transform.position + new Vector3(0, 100), Quaternion.identity, transform);
        SpriteRenderer vowEffectSp = vowEffect.GetComponent<SpriteRenderer>();

        Color vowColor = GetVowEffectColor(skillType);
        vowEffectSp.color = vowColor;
        vowEffect.GetComponentInChildren<Image>().color = new Color(vowColor.r, vowColor.g, vowColor.b, 0.7f);

        playerManager.PlayerInvoke_SetSkillUI(SkillTree_Manager.SkillName.KNOTS, vowEffectSp.sprite, vowColor); 

        GetVow();
    }

    Color GetVowEffectColor(PlayerManager.SkillType skillType)
    {
        if (GetPlayerType() == PlayerType.MELEE)
        {
            return skillType switch
            {
                PlayerManager.SkillType.SPECIAL => new Color(0.83f, 0.1f, 0.1f),
                PlayerManager.SkillType.ULTIMATE => new Color(0.112f, 0.79f, 0.42f),
                _ => Color.white,
            };
        }
        else
        {
            return skillType switch
            {
                PlayerManager.SkillType.SPECIAL => new Color(0.13f, 0.52f, 1f),
                PlayerManager.SkillType.ULTIMATE => new Color(0.79f, 0.12f, 1f),
                _ => Color.white,
            };
        }
    }

    protected virtual void GetVow()
    {

    }

    protected virtual void GetControlInputs()
    {
        if (!IsAlive()) return;

        if (Input.GetKeyDown(GlobalStageManager.AttackKey))
        {
            Action_Attack();
        }
        else if (Input.GetKeyDown(GlobalStageManager.SkillKey))
        {
            Action_Skill();
        }
        else if (Input.GetKeyDown(GlobalStageManager.SpecialKey))
        {
            Action_Special();
        }
        else
        {
            Move();
        }
    }

    public virtual void Action_Attack()
    {
        if (!IsAlive()) return;
        AttackCoroutine = StartCoroutine(Attack());
    }

    public virtual void Action_Skill()
    {
        if (!IsAlive()) return;

        if (Skills.Contains(SkillTree_Manager.SkillName.KNOTS) && !playerManager.hasVowed)
        {
            MakeVow(PlayerManager.SkillType.ULTIMATE);
        }

        UseSkill();
    }

    public virtual void Action_Special()
    {
        if (!IsAlive()) return;

        if (Skills.Contains(SkillTree_Manager.SkillName.KNOTS) && !playerManager.hasVowed)
        {
            MakeVow(PlayerManager.SkillType.SPECIAL);
        }

        UseSpecial();
    }

    short specialCastCount = 0;
    public virtual void UseSpecial()
    {
        if (Skills.Contains(SkillTree_Manager.SkillName.SPECIAL_MSPD))
        {
            float duration = Mathf.Min(3f, 1f + specialCastCount * 0.5f);
            ApplyEffect(Effect.AffectedStat.MSPD, "SPECIAL_MSPD_BUFF", 100f, duration, true, EffectPersistType.PERSIST);
            specialCastCount++;
        }
    }

    [SerializeField] GameObject UltimateShockwaveEffect;
    public virtual void UseSkill()
    {
        if (Skills.Contains(SkillTree_Manager.SkillName.ULTIMATE_BUFF))
        {
            ActivateLever();
        }
    }

    readonly float leverCD = 5f;
    float leverCDTimer = 9999f;
    void ActivateLever()
    {
        ApplyEffect(Effect.AffectedStat.ATK, "ULTIMATE_ATK_BUFF", 25f, 5f, true, EffectPersistType.DECAY);
        ApplyEffect(Effect.AffectedStat.ASPD, "ULTIMATE_ASPD_BUFF", 25f, 5f, true, EffectPersistType.DECAY);
        ApplyEffect(Effect.AffectedStat.MSPD, "ULTIMATE_MSPD_BUFF", 25f, 5f, true, EffectPersistType.DECAY);

        if (leverCDTimer < leverCD) return;

        float range = Mathf.Max(attackRange, 300f);

        var enemies = SearchForEntitiesAroundSelf(typeof(EnemyBase), range, true);
        foreach (var enemy in enemies)
        {
            if (!enemy || !enemy.IsAlive()) continue;
            enemy.ApplyEffect(Effect.AffectedStat.MSPD, "ULTIMATE_ENEMY_DEBUFF", -90f, 2f, true, EffectPersistType.DECAY);
            PushEntityFrom(enemy, transform.position, 3.6f, 0.15f, true);
        }

        GameObject vfx = Instantiate(UltimateShockwaveEffect, transform.position, Quaternion.identity, transform);
        vfx.transform.localScale *= range / 300f;
        Destroy(vfx, 0.5f);

        leverCDTimer = 0f;
    }

    public override void Move()
    {
        if (IsMovementLocked || IsBound) return;

        Vector2 movementInputs = InputManager.Instance.GetMovementInput();
        rb2d.velocity = CalculateMovement(movementInputs);

        // Calculate movement magnitude for animator
        float moveMagnitude = Mathf.Abs(movementInputs.x) + Mathf.Abs(movementInputs.y);
        animator.SetFloat("move", moveMagnitude);

        base.Move();
    }

    public override IEnumerator Attack()
    {
        if (!CanAttack || IsAttackLocked) yield break;

        MovementLockout = Mathf.Max(MovementLockout, GetWindupTime() * 1.5f);

        animator.SetBool("attack", true);
        LockoutMovementOnAttackCoroutine = StartCoroutine(LockoutMovementsOnAttack());
    }

    public override IEnumerator OnAttackComplete()
    {
        var targets = SearchForEntitiesAroundCertainPoint(typeof(EnemyBase), AttackPosition.position, attackRange);
        foreach (var target in targets)
        {
            if (!target || !target.IsAlive()) continue;
            DealDamage(target, atk);
        }

        yield return null;
    }

    public override IEnumerator LockoutMovementsOnAttack()
    {
        StartCoroutine(base.LockoutMovementsOnAttack());
        StartCoroutine(playerManager.AttackCooldown(GetAttackLockoutTime()));
        yield return null;
    }

    public void ClearAllAggro()
    {
        var enemies = EntityManager.Enemies;
        foreach (var enemy in enemies)
        {
            enemy.ChangeAggro(null);
        }
    }

    public void SetInvisible(float duration)
    {
        ClearAllAggro();
        StartCoroutine(SetInvisibleCoroutine(duration));
    }

    IEnumerator SetInvisibleCoroutine(float duration)
    {
        isInvisible = true;
        float c = 0;
        while (c < duration)
        {
            spriteRenderer.color = new Color(1, 1, 1, 0.3f);
            c += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
        spriteRenderer.color = Color.white;
        isInvisible = false;
    }

    public virtual PlayerTooltipsInfo GetPlayerTooltipsInfo()
    {
        return new PlayerTooltipsInfo
        {
            Icon = Icon,
            AttackSprite = AttackSprite,
            SkillSprite = SkillSprite,
            SpecialSprite = SpecialSprite,
            attackRange = attackRange,
            attackSpeed = GetWindupTime(),
            attackInterval = attackInterval,
            atk = atk,
            bAtk = bAtk,
            bDef = bDef,
            def = def,
            bRes = bRes,
            res = res,
            attackPattern = attackPattern,
            damageType = damageType,
            mHealth = Mathf.FloorToInt(mHealth),
            health = Mathf.FloorToInt(health),
            moveSpeed = moveSpeed,
            SkillName = SkillName,
            SkillText = SkillDes,
            SpecialName = SpecialName,
            SpecialText = SpecialDes,
            AttackText = "Perform an attack that deals",
        };
    }

    public override void TakeDamage(DamageInstance damage, EntityBase source, ProjectileScript projectileInfo = null, bool IgnoreInvulnerability = false, bool CalculateDamage = false)
    {
        base.TakeDamage(damage, source, projectileInfo, IgnoreInvulnerability, CalculateDamage);

        if (damage.TotalDamage > 0 && !isInvulnerable) RemoveEffect(StartMspdBuffKey);

        if (source && !isInvulnerable && !IgnoreInvulnerability)
        {
            float strengthLevel = damage.TotalDamage * 1.0f / (mHealth * 0.5f);
            playerManager.OnPlayerAttacked(strengthLevel);
        }
    }

    public float heavyHitterMaxTimer = 10f;
    protected float timerSinceLastAttack = 0f;
    protected float GetHeavyHitterMultiplier()
    {
        if (!Skills.Contains(SkillTree_Manager.SkillName.HEAVY_HITTER)) return 1f;
        float multiplier = 1f + Mathf.Lerp(0f, 2.5f, timerSinceLastAttack / heavyHitterMaxTimer);
        return multiplier;
    }

    protected void GetWingedStepMspdBuff()
    {
        float baseValue = 25f, jumpValue = 20f;
        string key = "WINGED_STEPS_A_MSPD_BUFF";
        if (MspdBuffs.ContainsKey(key) && MspdBuffs[key].IsInEffect)
        {
            float newValue = MspdBuffs[key].Value + jumpValue;

            ApplyEffect(
                Effect.AffectedStat.MSPD, 
                key,
                Mathf.Max(baseValue, newValue), 
                3, 
                true, 
                EffectPersistType.DECAY);
        }
        else
        {
            ApplyEffect(Effect.AffectedStat.MSPD, key, baseValue, 3, true, EffectPersistType.DECAY);
        }
    }

    protected bool IsHeavyHitterMaxed =>
        Skills.Contains(SkillTree_Manager.SkillName.HEAVY_HITTER)
        &&
        timerSinceLastAttack >= heavyHitterMaxTimer;

    readonly HashSet<EntityBase> Levitated = new();
    public override void DealDamage(EntityBase target, float pDmg, float mDmg, float tDmg, bool allowWhenDisabled = false, ProjectileScript projectileInfo = null)
    {
        if (Skills.Contains(SkillTree_Manager.SkillName.BUBBLE_ARTS) && !Levitated.Contains(target))
        {
            mDmg += (int)(atk * 0.1f);
            ApplyLevitate(target, 2.5f);
            Levitated.Add(target);
        }

        if (Skills.Contains(SkillTree_Manager.SkillName.BREAK_THE_ICE) && target.IsFrozen)
        {
            float freezeDuration = target.FreezeTimer;
            target.EndFreeze();

            int bonusDmg = (int)(target.mHealth * 0.1f + Mathf.Min(atk * 0.5f * freezeDuration, bAtk * 4f));
            tDmg += bonusDmg;
        }

        base.DealDamage(target, pDmg, mDmg, tDmg, allowWhenDisabled);
        if (!target.IsAlive())
        {
            OnEnemyDefeat(target);
        }
    }

    int enemyDefeatCount = 0;
    public virtual void OnEnemyDefeat(EntityBase enemy)
    {
        if (Skills.Contains(SkillTree_Manager.SkillName.VICTORY_ATK))
        {
            float strength = Mathf.Min(100f, 50f + 5f * enemyDefeatCount), duration = 5f;
            ApplyEffect(Effect.AffectedStat.ATK, "VICTORY_ATK_BUFF", strength, duration, true, EffectPersistType.DECAY);
            ApplyEffect(Effect.AffectedStat.MSPD, "VICTORY_MSPD_BUFF", strength, duration, true, EffectPersistType.DECAY);
        }
        else if (Skills.Contains(SkillTree_Manager.SkillName.VICTORY_REFRESH))
            ReduceSpecialCooldown(Mathf.Min(1.0f, 0.3f + 0.07f * enemyDefeatCount), CooldownReductionType.PERCENTAGE_FULL);

        enemyDefeatCount++;
    }

    public enum CooldownReductionType
    {
        FLAT,
        PERCENTAGE_FULL,
        PERCENTAGE_CURRENT,
    }

    public virtual void ReduceUltimateCooldown(float amount, CooldownReductionType reductionType = CooldownReductionType.FLAT)
    {
    }

    public virtual void ReduceSpecialCooldown(float amount, CooldownReductionType reductionType = CooldownReductionType.FLAT)
    {
    }

    public override void OnDeath()
    {
        if (!gameObject.activeSelf) return;

        if (!IsAlive() && playerManager.MintBlessing)
        {
            MintRevive();
            return;
        }

        base.OnDeath();
        playerManager.OnPlayerDeath();
    }

    protected virtual void MintRevive()
    {
        Heal(mHealth * 0.52f, healThroughDead: true);

        playerManager.MintBlessingRevival();
        playerManager.PlayerInvoke_RemoveSkillUI(SkillTree_Manager.SkillName.AMULET);
        SetInvulnerable(1.52f);

        Instantiate(RockEffect, transform.position, Quaternion.identity);
    }

    public void ResumeStageBGM()
    {
        StageManager.StageBGM.Play();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision || !collision.gameObject) return;

        FumoScript fumoScript = collision.gameObject.GetComponent<FumoScript>();
        if (fumoScript && fumoScript.ObjectiveType == FumoScript.FumoObjectiveType.PICK_UP && collision.gameObject.CompareTag("Fumo"))
        {
            StageManager.OnPlayerFumoPickup(this, collision);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(AttackPosition ? AttackPosition.position : transform.position, attackRange);
    }

    private void OnDisable()
    {
        StopMovement();
    }
}