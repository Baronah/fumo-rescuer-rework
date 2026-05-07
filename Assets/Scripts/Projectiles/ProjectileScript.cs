using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

public class ProjectileScript : MonoBehaviour
{
	[HideInInspector] public EntityBase ProjectileFirer;

	protected EntityBase ProjectileDestination = null;
	protected List<Type> ProjectileTargetedTypes = new();
    protected float ProjectileLifespan = 8f;

    public DamageInstance DamageInstance;

	[HideInInspector] public string displayMsg = string.Empty;
	[HideInInspector] public Vector3 msgDisplayOffset = Vector3.zero;
	public bool doesDamage = true;

	public float TravelSpeed = 25f;
    public float Acceleration = 0f;

    protected Vector3 targetDirection;
    protected Collider2D Target = null;
	protected Rigidbody2D rb2d;
    protected bool allowingUpdate = false;

    protected EntityBase excludedTarget = null;

    private void Start()
    {
        rb2d = GetComponent<Rigidbody2D>();
    }

    public enum ProjectileType
	{
		HOMING_TO_SPECIFIC_TARGET,
		CATCH_FIRST_TARGET_OF_TYPE,
    }

	public ProjectileType projectileType;
    bool DamageScaleWithDistance = false;

	public void ShootTowards(EntityBase enemy, ProjectileType projectileType, bool useAbsoluteDirection = false)
	{
		ShootTowards(enemy.transform.position, enemy, projectileType, ProjectileLifespan, useAbsoluteDirection);
    }

    public virtual void ShootTowards(Vector3 targetPosition, EntityBase enemy, ProjectileType projectileType, float ProjectileLifespan, bool useAbsoluteDirection = false)
    {
        this.projectileType = projectileType;

        if (enemy)
        {
            ProjectileDestination = enemy;
            Target = enemy.GetComponent<Collider2D>();
        }

        ProjectileTargetedTypes.Add(enemy.GetType());
        targetDirection = useAbsoluteDirection ? targetPosition : (targetPosition - transform.position).normalized;

        Lifespan = ProjectileLifespan;

        float desiredZRotation = Mathf.Atan2(targetDirection.y, targetDirection.x) * Mathf.Rad2Deg - 90;

        transform.rotation = Quaternion.Euler(0f, 0f, desiredZRotation);

        DamageScaleWithDistance = CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.ABSOLUTISM);

        allowingUpdate = true;
    }

    public virtual void ShootTowards(Vector3 targetPosition, ProjectileType projectileType, float ProjectileLifespan, bool useAbsoluteDirection = false, params Type[] enemy)
    {
        this.projectileType = projectileType;

        ProjectileTargetedTypes.AddRange(enemy);
        targetDirection = useAbsoluteDirection ? targetPosition : (targetPosition - transform.position).normalized;

        Lifespan = ProjectileLifespan;

        float desiredZRotation = Mathf.Atan2(targetDirection.y, targetDirection.x) * Mathf.Rad2Deg - 90;

        transform.rotation = Quaternion.Euler(0f, 0f, desiredZRotation);

        DamageScaleWithDistance = CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.ABSOLUTISM);

        allowingUpdate = true;
    }

    protected float Lifespan = 3f;
    float TravelTimeCount = 0f;
    protected virtual void FixedUpdate()
	{
		if (!allowingUpdate) return;

        Lifespan -= Time.fixedDeltaTime;
        if (projectileType == ProjectileType.HOMING_TO_SPECIFIC_TARGET && ProjectileDestination && !ProjectileDestination.IsAlive()) Lifespan = 0f;
        if (Lifespan <= 0f)
        {
            allowingUpdate = false;
            gameObject.SetActive(false);
            Destroy(this.gameObject, 0.1f);
            return;
        }

        float angle = Mathf.Atan2(rb2d.velocity.y, rb2d.velocity.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        
		if (projectileType == ProjectileType.HOMING_TO_SPECIFIC_TARGET && ProjectileDestination != null)
        {
            Vector3 direction = (ProjectileDestination.transform.position - transform.position).normalized;
            rb2d.velocity = direction * TravelSpeed;
        }
        else
        {
            rb2d.velocity = targetDirection * TravelSpeed;
        }

        TravelSpeed += Acceleration * Time.fixedDeltaTime;

        TravelTimeCount += Time.fixedDeltaTime;
        if (DamageScaleWithDistance && TravelTimeCount >= 0.25f)
        {
            float upPerSecond = 0.5f;
            DamageInstance.AddByPercentage(upPerSecond * TravelTimeCount);

            TravelTimeCount = 0;
        }
    }

	public virtual void OnHitEvent(EntityBase target)
	{
		if (!target || target == excludedTarget) return;

		if (doesDamage)
		{
			ProjectileFirer.DealDamage(target, DamageInstance.PhysicalDamage, DamageInstance.MagicalDamage, DamageInstance.TrueDamage, true, this);
			allowingUpdate = false;
		}

		if (displayMsg != string.Empty) ProjectileFirer.DisplayDamage(displayMsg, msgDisplayOffset);
        gameObject.SetActive(false);
        Destroy(this.gameObject, 0.5f);
    }

    public virtual void HandleHit(GameObject other)
    {
        if (!allowingUpdate) return;

        EntityBase entity = other.GetComponent<EntityBase>();
        if (entity == null || !entity.CanBeHitByProjectiles) return;

        if (
            (projectileType == ProjectileType.HOMING_TO_SPECIFIC_TARGET && entity == ProjectileDestination) ||
            (projectileType == ProjectileType.CATCH_FIRST_TARGET_OF_TYPE && ProjectileTargetedTypes.Any(tt => tt.IsAssignableFrom(entity.GetType())))
           )
        {
            OnHitEvent(entity);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision) => HandleHit(collision.gameObject);
    private void OnCollisionEnter2D(Collision2D collision) => HandleHit(collision.gameObject);
}