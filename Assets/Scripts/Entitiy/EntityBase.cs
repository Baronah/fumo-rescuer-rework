using DamageCalculation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class EntityBase : MonoBehaviour
{
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

    [SerializeField] private GameObject DamagePopup;

    protected Transform AttackPosition;
    protected SpriteRenderer spriteRenderer;
    protected Animator animator;
    protected Rigidbody2D rb2d;

    private bool SpriteInitialFlipX = false;
    private Vector3 PrevPosition;
    private Color InitSpriteColor;

    protected short MovementLockout = 0, AttackLockout = 0;
    public bool IsMovementLocked => MovementLockout > 0;
    public bool IsAttackLocked => AttackLockout > 0;

    public virtual void Start()
    {
        InitializeComponents();
    }

    public virtual void InitializeComponents()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        rb2d = GetComponent<Rigidbody2D>();

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

        AttackPosition = transform;
    }

    public virtual void FixedUpdate()
    {
        if (health <= 0) OnDeath();
        HandleSpriteFlipping();
    }

    public virtual void HandleSpriteFlipping()
    {
        Vector3 CurrentPos = transform.position;
        float deltaX = CurrentPos.x - PrevPosition.x;

        if (Mathf.Abs(deltaX) > Mathf.Epsilon)
        {
            if (SpriteInitialFlipX) spriteRenderer.flipX = deltaX < 0 ? SpriteInitialFlipX : !SpriteInitialFlipX;
            else spriteRenderer.flipX = deltaX < 0 ? !SpriteInitialFlipX : SpriteInitialFlipX;
        }

        PrevPosition = CurrentPos;
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
            instance = new DamageInstance
            {
                PhysicalDamage = pDmg,
                MagicalDamage = mDmg,
                TrueDamage = tDmg
            },
        };

        pipeline.Add(new CalculateDefense());
        pipeline.Calculate();

        return pipeline.instance;
    }

    public virtual void DealDamage(EntityBase target, int damage)
    {
        if (!target || !target.IsAlive()) return;

        if (damageType == DamageType.PHYSICAL) DealDamage(target, atk, 0, 0);
        else if (damageType == DamageType.MAGICAL) DealDamage(target, 0, atk, 0);
        else DealDamage(target, 0, 0, atk);
    }

    public virtual void DealDamage(EntityBase target, int pDmg, int mDmg, int tDmg)
    {
        var calcDamage = DamageOutput(target, pDmg, mDmg, tDmg);
        if (calcDamage.TotalDamage <= 0) return;
        
        target.TakeDamage(calcDamage, this);
    }

    public virtual void TakeDamage(DamageInstance damage, EntityBase source)
    {
        // to be added
        // DisplayDamage(damage);

        health -= damage.TotalDamage;
        if (health < 0) health = 0;
        StartCoroutine(PulseSprite());

        if (health <= 0) OnDeath();
    }

    IEnumerator PulseSprite()
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

    public void DisplayDamage()
    {
        // to be added
    }

    public virtual bool IsAlive() => health > 0;

    public virtual void Move()
    {
        // base example
        if (IsMovementLocked) return;
        
        // as enemy and player unit have different movement set,
        // overrides will handle this
    }

    public virtual void StopMovement()
    {
        rb2d.velocity = Vector2.zero;
    }

    public Vector2 CalculateMovement(Vector2 normalizedMovementVector) => normalizedMovementVector * mSpd;

    public virtual void OnDeath()
    {
        animator.SetTrigger("die");
        StopAllCoroutines();
        StartCoroutine(StartMovementLockout(999));
        StartCoroutine(StartAttackLockout(999));
        Destroy(this.gameObject, 5);
    }

    public virtual IEnumerator Attack()
    {
        // base example
        if (IsAttackLocked) yield break;

        StartCoroutine(StartMovementLockout(aInt * 1.2f));
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
                        || (type != null && entity.GetType() != type)) 
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
