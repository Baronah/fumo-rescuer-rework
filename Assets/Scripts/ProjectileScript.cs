using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProjectileScript : MonoBehaviour
{
	[HideInInspector] public EntityBase ProjectileFirer;
	protected EntityBase ProjectileDestination = null;
	protected Type ProjectileTargetedType = null;

	public DamageInstance DamageInstance;
	[HideInInspector] public string displayMsg = string.Empty;
	[HideInInspector] public Vector3 msgDisplayOffset = Vector3.zero;
	public bool doesDamage = true;

	public float travelSpeed = 25f;

    Vector3 targetDirection;
    protected Collider2D Target = null;
	private Rigidbody2D rb2d;
    protected bool allowingUpdate = false;

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

	public void ShootTowards(EntityBase enemy, ProjectileType projectileType)
	{
		ShootTowards(enemy.transform.position, enemy, projectileType);
    }

    public void ShootTowards(Vector3 targetPosition, EntityBase enemy, ProjectileType projectileType)
    {
        this.projectileType = projectileType;

        ProjectileDestination = enemy;
        Target = enemy.GetComponent<Collider2D>();

        ProjectileTargetedType = enemy.GetType();
        targetDirection = (targetPosition - transform.position).normalized;

        Destroy(gameObject, 8); 

        float desiredZRotation = Mathf.Atan2(targetDirection.y, targetDirection.x) * Mathf.Rad2Deg - 90;

        transform.rotation = Quaternion.Euler(0f, 0f, desiredZRotation);

        allowingUpdate = true;
    }

    private void FixedUpdate()
	{
		if (!allowingUpdate) return;

        float angle = Mathf.Atan2(rb2d.velocity.y, rb2d.velocity.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        
		if (projectileType == ProjectileType.HOMING_TO_SPECIFIC_TARGET && ProjectileDestination != null)
        {
            Vector3 direction = (ProjectileDestination.transform.position - transform.position).normalized;
            rb2d.velocity = direction * travelSpeed;
        }
        else
        {
            rb2d.velocity = targetDirection * travelSpeed;
        }
    }

	public virtual void OnHitEvent(EntityBase target)
	{
		if (!target)
		{
            Destroy(this.gameObject);
			return;
        }

		if (doesDamage)
		{
			ProjectileFirer.DealDamage(target, DamageInstance);
			allowingUpdate = false;
		}


		if (displayMsg != string.Empty) ProjectileFirer.DisplayDamage(displayMsg, msgDisplayOffset);
		Destroy(this.gameObject);
	}

    private void HandleHit(GameObject other)
    {
        if (!allowingUpdate) return;

        EntityBase entity = other.GetComponent<EntityBase>();
        if (entity == null) return;

        if (
            (projectileType == ProjectileType.HOMING_TO_SPECIFIC_TARGET && entity == ProjectileDestination) ||
            (projectileType == ProjectileType.CATCH_FIRST_TARGET_OF_TYPE && ProjectileTargetedType.IsAssignableFrom(entity.GetType()))
           )
        {
            OnHitEvent(entity);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision) => HandleHit(collision.gameObject);
    private void OnCollisionEnter2D(Collision2D collision) => HandleHit(collision.gameObject);
}
