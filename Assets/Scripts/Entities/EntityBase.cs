using DamageCalculation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using static ProjectileScript;

public class EntityBase : MonoBehaviour
{
    [SerializeField] protected string Name;
    [SerializeField] protected Sprite Icon;

    [SerializeField] protected int mHealth;
    [SerializeField] protected short bAtk, bDef, bRes;
    [SerializeField] protected short defPen, defIgn, resPen, resIgn, lifeSteal;
    [SerializeField] protected float bmSpd, baRng, baInt, baSpd;
    public float MIN_PHYSICAL_DMG = 0.05F, MIN_MAGICAL_DMG = 0.1F;

    [HideInInspector] public int health;
    [HideInInspector] public short atk, def, res;
    [HideInInspector] public float mSpd, aRng, aInt, aSpd;

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
    protected Collider2D collider;
    protected AudioSource[] sfxs;

    protected bool SpriteInitialFlipX = false, useTransformAsAttackPosition = false;
    protected Vector3 PrevPosition;
    protected Color InitSpriteColor;

    protected short MovementLockout = 0, AttackLockout = 0;
    public bool IsMovementLocked => MovementLockout > 0;
    public bool IsAttackLocked => AttackLockout > 0;

    private bool TriggeredOnDeath = false;

    [SerializeField] private float preferredMoveAnimationPlaySpeed = 1.0f;

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
        collider = GetComponent<Collider2D>();
        sfxs = GetComponents<AudioSource>();

        InitSpriteColor = spriteRenderer.color;
        PrevPosition = transform.position;
        SpriteInitialFlipX = spriteRenderer.flipX;

        health = mHealth;
        atk = bAtk; 
        def = bDef; 
        res = bRes;
        mSpd = bmSpd;
        aRng = baRng;
        aInt = baInt;
        aSpd = baSpd;

        AttackPosition = transform.Find("AttackPosition");
        if (!AttackPosition)
        {
            AttackPosition = transform;
            useTransformAsAttackPosition = true;
        }
        healthBar = GetComponentInChildren<HealthBar>();
        healthBar.SetMaxHealth(mHealth);
    }

    public virtual void FixedUpdate()
    {
        if (!IsAlive() && !TriggeredOnDeath) OnDeath();
        HandleSpriteFlipping();
        HandleAnimationSpeed();
    }

    public virtual void HandleSpriteFlipping()
    {
        Vector3 CurrentPos = transform.position;
        float deltaX = CurrentPos.x - PrevPosition.x;

        bool PrevFlipX = spriteRenderer.flipX;
        if (Mathf.Abs(deltaX) > Mathf.Epsilon)
        {
            if (SpriteInitialFlipX)
            {
                spriteRenderer.flipX = deltaX < 0 ? SpriteInitialFlipX : !SpriteInitialFlipX;
            }
            else
            {
                spriteRenderer.flipX = deltaX < 0 ? !SpriteInitialFlipX : SpriteInitialFlipX;
            }
        }

        if (PrevFlipX != spriteRenderer.flipX && !useTransformAsAttackPosition)
            AttackPosition.localPosition = new Vector3(
                -AttackPosition.localPosition.x, 
                AttackPosition.localPosition.y, 
                AttackPosition.localPosition.z
            );
        PrevPosition = CurrentPos;
    }

    public virtual void HandleAnimationSpeed()
    {
         float MIN_SPEED = preferredMoveAnimationPlaySpeed * 0.2f, 
                MAX_SPEED = preferredMoveAnimationPlaySpeed * 2, 
                X_MULTIPLIER = preferredMoveAnimationPlaySpeed - MIN_SPEED;
        animator.SetFloat("speed_value", Mathf.Lerp(MIN_SPEED, MAX_SPEED, mSpd * (X_MULTIPLIER / (MAX_SPEED - MIN_SPEED)) / bmSpd)); 
    }

    public virtual IEnumerator StartMovementLockout(float m)
    {
        StopMovement();
        MovementLockout++;
        yield return new WaitForSeconds(m);
        MovementLockout--;
    }

    public virtual IEnumerator StartAttackLockout(float m)
    {
        AttackLockout++;
        yield return new WaitForSeconds(m);
        AttackLockout--;
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

    public virtual void DealDamage(EntityBase target, int pDmg, int mDmg, int tDmg)
    {
        if (!target || !target.IsAlive()) return;

        var calcDamage = DamageOutput(target, pDmg, mDmg, tDmg);
        if (calcDamage.TotalDamage <= 0) return;
        
        target.TakeDamage(calcDamage, this);
    }

    public virtual void TakeDamage(DamageInstance damage, EntityBase source)
    {
        ShowDamageDealt(damage);
        AdjustHealthOnDamageReceive(damage);
        if (damage.TotalDamage > 0) StartCoroutine(PulseSprite());
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
        if (DamagePopup)
        {
            GameObject popup = Instantiate(DamagePopup, transform.position + offset, Quaternion.identity);
            popup.GetComponent<DamagePopup>().text.text = msg;
        }
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

    public Vector2 CalculateMovement(Vector2 normalizedMovementVector) => normalizedMovementVector * mSpd;

    public virtual void OnDeath()
    {
        TriggeredOnDeath = true;
        animator.SetTrigger("die");
        healthBar.SetHealth(0);
        healthBar.gameObject.SetActive(false);
        collider.enabled = false;
        rb2d.velocity = Vector2.zero;
        StopAllCoroutines();
        StartCoroutine(StartMovementLockout(999));
        StartCoroutine(StartAttackLockout(999));
        Destroy(this.gameObject, 5);
    }

    public virtual IEnumerator Attack()
    {
        // base example
        if (IsAttackLocked) yield break;

        StartCoroutine(StartMovementLockout(aInt * 1.5f));
        StartCoroutine(StartAttackLockout(aSpd));

        // as enemy and player unit have different attack method,
        // overrides will handle this

        yield return null;
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

    public void CreateProjectileAndShootToward(EntityBase target, ProjectileScript.ProjectileType projectileType)
    {
        CreateProjectileAndShootToward(ProjectilePrefab, GetInstanceBasedOnDamagetype(), target, target.transform.position, projectileType);
    }

    public void CreateProjectileAndShootToward(EntityBase target, Vector3 position, ProjectileScript.ProjectileType projectileType)
    {
        CreateProjectileAndShootToward(ProjectilePrefab, GetInstanceBasedOnDamagetype(), target, position, projectileType);
    }

    public void CreateProjectileAndShootToward(EntityBase target, DamageInstance damageInstance, ProjectileScript.ProjectileType projectileType)
    {
        CreateProjectileAndShootToward(ProjectilePrefab, damageInstance, target, target.transform.position, projectileType);
    }

    public void CreateProjectileAndShootToward(EntityBase target, DamageInstance damageInstance, Vector3 position, ProjectileScript.ProjectileType projectileType)
    {
        CreateProjectileAndShootToward(ProjectilePrefab, damageInstance, target, position, projectileType);
    }

    public void CreateProjectileAndShootToward(GameObject ProjectilePref, DamageInstance damageInstance, EntityBase target, Vector3 preferPosition, ProjectileScript.ProjectileType projectileType)
    {
        if (!ProjectilePref) return;

        GameObject projectile = Instantiate(ProjectilePref, AttackPosition.position, Quaternion.identity);
        ProjectileScript projectileScript = projectile.GetComponent<ProjectileScript>();
        if (!projectileScript) return;

        projectileScript.ProjectileFirer = this;
        projectileScript.DamageInstance = damageInstance;
        projectileScript.ShootTowards(preferPosition, target, projectileType);
    }

    public virtual List<EntityBase> SearchForEntitiesAroundSelf(Type type = null, bool catchInvisibles = false)
    {
        return SearchForEntitiesAroundCertainPoint(type, transform.position, aRng, catchInvisibles);
    }

    public virtual List<EntityBase> SearchForEntitiesAroundCertainPoint(Type type, Vector2 pos, float r, bool catchInvisibles)
    {
        Collider2D[] collider2Ds = Physics2D.OverlapCircleAll(pos, r);
        List<EntityBase> entityBases = new List<EntityBase>();

        foreach (Collider2D collider in collider2Ds)
        {
            EntityBase entity = collider.GetComponent<EntityBase>();
            if (!entity 
                || !entity.IsAlive() 
                    || (entity.isInvisible && !catchInvisibles) 
                        || (type != null && !type.IsAssignableFrom(entity.GetType()))) 
                continue;
            
            entityBases.Add(entity);
        }

        return entityBases;
    }

    public virtual EntityBase SearchForNearestEntityAroundSelf(Type type = null, bool catchInvisible = false)
    {
        return SearchForNearestEntityAroundCertainPoint(type, AttackPosition.position, aRng, catchInvisible);
    }

    public virtual EntityBase SearchForNearestEntityAroundCertainPoint(Type type, Vector2 pos, float r, bool catchInvisibles)
    {
        var entities = SearchForEntitiesAroundCertainPoint(type, pos, r, catchInvisibles);
        if (entities.Count <= 0) return null;
        if (entities.Count == 1) return entities[0];

        return entities.OrderBy(e => Vector2.Distance(e.transform.position, transform.position)).First();
    }
}
