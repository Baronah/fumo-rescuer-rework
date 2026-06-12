using UnityEngine;

public class ProjectilesCleanup : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        ProjectileScript projectile = collision.GetComponent<ProjectileScript>();
        if (!projectile) return;
        
        projectile.ReturnToPool();
    }
}