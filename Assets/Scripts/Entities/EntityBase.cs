using DamageCalculation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using static ProjectileScript;
using static UnityEngine.GraphicsBuffer;

public class EntityBase : MonoBehaviour
{
    [SerializeField] protected string Name;
    [SerializeField] protected Sprite Icon;

    [SerializeField] protected int mHealth;
    [SerializeField] protected short bAtk, bDef, bRes;
    [SerializeField] protected short defPen, defIgn, resPen, resIgn;
    [SerializeField] protected float lifeSteal, b_moveSpeed, b_attackRange, b_attackWindupTime, b_attackInterval;
    public float MIN_PHYSICAL_DMG = 0.05F, MIN_MAGICAL_DMG = 0.1F;

    public int health;
    public short atk, def, res;
    public float ASPD = 100;
    public float moveSpeed, attackRange, attackWindupTime, attackInterval;

    public int GetMaxHealth() => mHealth; 
    public short GetHealthPercentage() => (short) Mathf.Max(1, health * 100 / mHealth);
    public short GetMissingealthPercentage() => (short) Mathf.Max(1, (mHealth - health) * 100 / mHealth);

    public bool canRevive = false, isInvulnerable = false, isInvisible = false;

    public enum DamageType { PHYSICAL, MAGICAL, TRUE }
    public DamageType damageType;

    public enum AttackPattern { MELEE, RANGED, NONE }
    public AttackPattern attackPattern;
    [SerializeField] protected GameObject ProjectilePrefab;
    [SerializeField] protected ProjectileType ProjectileType = ProjectileType.CATCH_FIRST_TARGET_OF_TYPE;   

    [SerializeField] private GameObject DamagePopup;
    
    protected HealthBar healthBar;

    protected Transform AttackPosition;
    protected SpriteRenderer spriteRenderer;
    protected Animator animator;
    protected Rigidbody2D rb2d;
    protected Collider2D[] colliders;
    protected AudioSource[] sfxs;

    private GameObject ShadowSprite;

    public SpriteRenderer GetSpriteRenderer() => spriteRenderer;

    protected bool useTransformAsAttackPosition = false;
    protected Vector3 PrevPosition;
    protected Color InitSpriteColor;

    protected float MovementLockout = 0, AttackLockout = 0;
    public bool IsMovementLocked => MovementLockout > 0 || IsFrozen || IsStunned;
    public bool IsAttackLocked => AttackLockout > 0 || IsFrozen || IsStunned;

    private bool TriggeredOnDeath = false;

    protected float FreezeTimer = 0f, StunTimer = 0f;

    public bool IsFrozen => FreezeTimer > 0f;
    public bool IsStunned => StunTimer > 0f;

    [SerializeField] protected float preferredMoveAnimationPlaySpeed = 1.0f, preferredAttackAnimationSpeed = 1.0f;

    private short UpdateCounter = 0;
    protected EntityManager EntityManager;

    protected Coroutine AttackCoroutine = null, LockoutMovementOnAttackCoroutine = null;
    protected Animation attackAnimation;

    public Vector3 GetAttackPosition()
    {
        if (useTransformAsAttackPosition) return transform.position;
        return AttackPosition ? AttackPosition.position : transform.position;
    }

    public virtual void Start()
    {
        InitializeComponents();
    }

    public virtual void InitializeComponents()
    {
        Transform Sprite = transform.Find("Sprite");
        spriteRenderer = Sprite.GetComponent<SpriteRenderer>();
        animator = Sprite.GetComponent<Animator>();
        rb2d = GetComponent<Rigidbody2D>();
        colliders = GetComponents<Collider2D>();
        sfxs = GetComponents<AudioSource>();

        ShadowSprite = spriteRenderer.transform.Find("Shadow").gameObject;

        InitSpriteColor = spriteRenderer.color;
        PrevPosition = transform.position;

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

        EntityManager = FindObjectOfType<EntityManager>();
        if (EntityManager)
        {
            EntityManager.OnEntitySpawn(this.gameObject);
        }

        StartCoroutine(OnStartCoroutine());
    }

    IEnumerator OnStartCoroutine()
    {
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

    public virtual void FixedUpdate()
    {
        if (!IsAlive() && !TriggeredOnDeath) OnDeath();
        UpdateCooldowns();
        HandleSpriteFlipping();
        HandleAnimationSpeed();
    }

    public virtual void UpdateCooldowns()
    {
        bool PrevFrozen = FreezeTimer > 0f; 
        FreezeTimer -= Time.deltaTime;
        if (FreezeTimer > 0f) OnFreezeMaintain();
        else if (PrevFrozen && FreezeTimer <= 0f) OnFreezeExit();

        StunTimer -= Time.deltaTime;
        
        AttackLockout -= Time.deltaTime;
        MovementLockout -= Time.deltaTime;
    }

    public virtual void HandleSpriteFlipping()
    {
        Vector3 CurrentPos = transform.position;
        float deltaX = CurrentPos.x - PrevPosition.x;

        bool PrevFlipX = spriteRenderer.flipX;
        if (Mathf.Abs(deltaX) > 0.1f)
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
        StopMovement();
        MovementLockout = Mathf.Max(MovementLockout, m);
        yield return null;
    }

    public virtual IEnumerator StartAttackLockout(float m)
    {
        AttackLockout = Mathf.Max(AttackLockout, m);
        yield return null;
    }

    public virtual DamageInstance DamageOutput(EntityBase target, int pDmg, int mDmg, int tDmg)
    {
        DamagePipeline pipeline = new DamagePipeline
        {
            attacker = this,
            target = target,
            instance = new DamageInstance(pDmg, mDmg, tDmg)
        };

        pipeline.Add(new CalculateDefense());
        pipeline.Calculate();

        return pipeline.instance;
    }

    public DamageInstance GetInstanceBasedOnDamagetype()
    {
        return GetInstanceBasedOnDamagetype(atk);
    }

    public DamageInstance GetInstanceBasedOnDamagetype(int atk)
    {
        DamageInstance instance = new DamageInstance();
        if (damageType == DamageType.PHYSICAL) instance.PhysicalDamage = atk;
        else if (damageType == DamageType.MAGICAL) instance.MagicalDamage = atk;
        else instance.TrueDamage = atk;
        return instance;
    }

    public virtual void DealDamage(EntityBase target, int damage)
    {
        if (damageType == DamageType.PHYSICAL) DealDamage(target, atk, 0, 0);
        else if (damageType == DamageType.MAGICAL) DealDamage(target, 0, atk, 0);
        else DealDamage(target, 0, 0, atk);
    }

    public virtual void DealDamage(EntityBase target, DamageInstance damage)
    {
        DealDamage(target, damage.PhysicalDamage, damage.MagicalDamage, damage.TrueDamage);
    }

    public virtual void DealDamage(EntityBase target, int pDmg, int mDmg, int tDmg, bool allowWhenDisabled = false)
    {
        if ((!allowWhenDisabled && (IsFrozen || IsStunned)) || !target || !target.IsAlive() || target.isInvulnerable) return;

        var calcDamage = DamageOutput(target, pDmg, mDmg, tDmg);
        if (calcDamage.TotalDamage <= 0) return;
        
        target.TakeDamage(calcDamage, this);
        OnSuccessfulAttack(target, calcDamage);
    }

    public virtual void OnSuccessfulAttack(EntityBase target, DamageInstance damage)
    {
        if (lifeSteal > 0)
        {
            Heal(damage.TotalDamage * lifeSteal);
        }
    }

    public virtual void TakeDamage(DamageInstance damage, EntityBase source)
    {
        if (!this || !this.IsAlive() || this.isInvulnerable) return;

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
        string dmgTxt = "<color=" + (damage.PhysicalDamage > damage.MagicalDamage ? "red" : "#ff00ff") + ">" + damage.TotalDamage + "</color>";
        DisplayDamage(dmgTxt, new(0, 55));
    }

    public void AdjustHealthOnDamageReceive(DamageInstance damage)
    {
        health -= damage.TotalDamage;
        if (health < 0) health = 0;
        healthBar?.SetHealth(health); 
        
        if (health <= 0) OnDeath();
    }

    public void SetHealth(int health)
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
        if (!DamagePopup) return;

        GameObject popup = Instantiate(DamagePopup, transform.position + offset, Quaternion.identity);
        popup.GetComponent<DamagePopup>().text.text = msg;
    }

    public virtual bool IsAlive() => health > 0;

    public virtual void Move()
    {
        // base example
        if (IsMovementLocked) return;

    }

    public virtual void StopMovement()
    {
        rb2d.velocity = Vector2.zero;
        animator.SetFloat("move", 0);
    }

    public virtual void ApplyFreeze(EntityBase target, float duration)
    {
        target.animator.speed = 0f;
        target.animator.StartPlayback();
        target.FreezeTimer = Mathf.Max(target.FreezeTimer, duration);
        target.StopMovement();
        target.CancelAttack();
    }

    public virtual void ApplyStun(EntityBase target, float duration)
    {
        target.animator.speed = 0f;
        target.StunTimer = Mathf.Max(target.StunTimer, duration);
        target.StopMovement();
        target.CancelAttack();
    }

    public virtual void OnFreezeMaintain()
    {
        spriteRenderer.color = Color.blue;
        StopMovement();
    }

    public virtual void OnFreezeExit()
    {
        if (FreezeTimer > 0f) return;
        FreezeTimer = 0f;
        spriteRenderer.color = InitSpriteColor;
        animator.speed = 1f;
    }

    public virtual Vector2 CalculateMovement(Vector2 normalizedMovementVector) => CalculateMovement(normalizedMovementVector, moveSpeed);
    public virtual Vector2 CalculateMovement(Vector2 normalizedMovementVector, float speed) => normalizedMovementVector * speed;

    public virtual void OnDeath()
    {
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

    public virtual IEnumerator Attack()
    {
        if (IsAttackLocked) yield break;

        animator.SetTrigger("attack");
        LockoutMovementOnAttackCoroutine = StartCoroutine(LockoutMovementsOnAttack());
        yield break;
    }

    public virtual IEnumerator LockoutMovementsOnAttack()
    {
        // base example
        if (IsAttackLocked) yield break;

        StartCoroutine(StartMovementLockout(GetWindupTime() * 1.5f));
        StartCoroutine(StartAttackLockout(GetAttackLockoutTime()));

        // as enemy and player unit have different attack method,
        // overrides will handle this

        yield return null;
    }

    public float GetWindupTime() => attackWindupTime * (100 / Mathf.Max(20, ASPD));

    public float GetAttackInterval() => attackInterval * (100 / Mathf.Max(20, ASPD));

    public float GetAttackLockoutTime() 
        => Mathf.Max(
                GetWindupTime(),
                GetAttackInterval(),
                animator.GetCurrentAnimatorClipInfo(0).Length / preferredAttackAnimationSpeed / animator.GetFloat("a_speed_value")
            );

    public virtual void CancelAttack()
    {
        animator.ResetTrigger("attack");

        if (AttackCoroutine != null)
        {
            StopCoroutine(AttackCoroutine);
            AttackCoroutine = null;
            AttackLockout = (short)Mathf.Max(AttackLockout - 1, 0);
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

    public virtual void Heal(float amount, EntityBase target, bool healThroughDead = false)
    {
        if (amount <= 0 || (!target.IsAlive() && !healThroughDead)) return;
        target.DisplayDamage("<color=green>+" + (int)amount + "</color>", new Vector3(0, 55));
        target.health += (int)amount;
        if (target.health > target.mHealth) target.health = target.mHealth;
        target.healthBar.SetHealth(target.health);
    }

    public virtual IEnumerator Revive()
    {
        float duration = 5;

        StartCoroutine(StartMovementLockout(duration));
        StartCoroutine(StartAttackLockout(duration));
        animator.SetTrigger("revive");

        float c = 0;
        while (c < duration)
        {
            health = (int) Mathf.Lerp(0, mHealth, c * 1.0f / duration);
            c += Time.deltaTime;
            yield return null;
        }

        yield return null;
    }

    public void CreateProjectileAndShootToward(EntityBase target, ProjectileScript.ProjectileType projectileType, float travelSpeed = 1000, float acceleration = 0)
    {
        CreateProjectileAndShootToward(ProjectilePrefab, GetInstanceBasedOnDamagetype(), target, AttackPosition.position, target.transform.position, projectileType, travelSpeed, acceleration);
    }

    public void CreateProjectileAndShootToward(EntityBase target, Vector3 targetPosition, ProjectileScript.ProjectileType projectileType, float travelSpeed = 1000, float acceleration = 0)
    {
        CreateProjectileAndShootToward(ProjectilePrefab, GetInstanceBasedOnDamagetype(), target, AttackPosition.position, targetPosition, projectileType, travelSpeed, acceleration);
    }

    public void CreateProjectileAndShootToward(EntityBase target, Vector3 spawnPosition, Vector3 targetPosition, ProjectileScript.ProjectileType projectileType, float travelSpeed = 1000, float acceleration = 0)
    {
        CreateProjectileAndShootToward(ProjectilePrefab, GetInstanceBasedOnDamagetype(), target, spawnPosition, targetPosition, projectileType, travelSpeed, acceleration);
    }

    public void CreateProjectileAndShootToward(EntityBase target, DamageInstance damageInstance, ProjectileScript.ProjectileType projectileType, float travelSpeed = 1000, float acceleration = 0)
    {
        CreateProjectileAndShootToward(ProjectilePrefab, damageInstance, target, AttackPosition.position,target.transform.position, projectileType, travelSpeed, acceleration);
    }

    public void CreateProjectileAndShootToward(EntityBase target, DamageInstance damageInstance, Vector3 targetPosition, ProjectileScript.ProjectileType projectileType, float travelSpeed = 1000, float acceleration = 0)
    {
        CreateProjectileAndShootToward(ProjectilePrefab, damageInstance, target, AttackPosition.position, targetPosition, projectileType, travelSpeed, acceleration);
    }

    public void CreateProjectileAndShootToward(GameObject ProjectilePref, DamageInstance damageInstance, EntityBase target, Vector3 spawnPosition, Vector3 preferPosition, ProjectileScript.ProjectileType projectileType, float travelSpeed = 1000, float acceleration = 0, float lifeSpan = 8)
    {
        if (!ProjectilePref) return;

        GameObject projectile = Instantiate(ProjectilePref, spawnPosition, Quaternion.identity);
        ProjectileScript projectileScript = projectile.GetComponent<ProjectileScript>();
        if (!projectileScript) return;

        projectileScript.ProjectileFirer = this;
        projectileScript.DamageInstance = damageInstance;
        projectileScript.TravelSpeed = travelSpeed;
        projectileScript.Acceleration = acceleration;
        projectileScript.ShootTowards(preferPosition, target, projectileType, lifeSpan);
    }

    public void CreateProjectileAndShootToward(GameObject ProjectilePref, DamageInstance damageInstance, Type targetType, Vector3 spawnPosition, Vector3 preferPosition, ProjectileScript.ProjectileType projectileType, float travelSpeed = 1000, float acceleration = 0, float lifeSpan = 8)
    {
        if (!ProjectilePref) return;

        GameObject projectile = Instantiate(ProjectilePref, spawnPosition, Quaternion.identity);
        ProjectileScript projectileScript = projectile.GetComponent<ProjectileScript>();
        if (!projectileScript) return;

        projectileScript.ProjectileFirer = this;
        projectileScript.DamageInstance = damageInstance;
        projectileScript.TravelSpeed = travelSpeed;
        projectileScript.Acceleration = acceleration;
        projectileScript.ShootTowards(preferPosition, targetType, projectileType, lifeSpan);
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

    public virtual List<EntityBase> SearchForEntitiesAroundCertainPoint(Type type, Vector2 pos, float r, bool catchInvisibles = false, short take = -1)
    {
        Collider2D[] collider2Ds = Physics2D.OverlapCircleAll(pos, r);
        List<EntityBase> entityBases = new List<EntityBase>();

        foreach (Collider2D collider in collider2Ds)
        {
            EntityBase entity = collider.GetComponent<EntityBase>();
            if (!entity 
                || !entity.IsAlive() 
                    || (entity.isInvisible && !catchInvisibles) 
                        || entityBases.Contains(entity)
                            || (type != null && !type.IsAssignableFrom(entity.GetType()))) 
                continue;
            
            entityBases.Add(entity);
        }

        if (entityBases.Count == 1 || entityBases.Count <= take) return entityBases;

        entityBases = entityBases.OrderBy(e => Vector2.Distance(e.transform.position, pos)).ToList();
        return take == -1 ? entityBases : entityBases.Take(take).ToList();
    }

    public virtual EntityBase SearchForNearestEntityAroundSelf(Type type = null, bool catchInvisible = false)
    {
        return SearchForNearestEntityAroundCertainPoint(type, AttackPosition.position, attackRange, catchInvisible);
    }

    public virtual EntityBase SearchForNearestEntityAroundCertainPoint(Type type, Vector2 pos, float r, bool catchInvisibles = false)
    {
        var targets = SearchForEntitiesAroundCertainPoint(type, pos, r, catchInvisibles, 1);
        if (targets == null || targets.Count <= 0) return null;

        return targets[0];
    }
}
