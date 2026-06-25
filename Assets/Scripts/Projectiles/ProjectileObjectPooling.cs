using System.Collections.Generic;
using UnityEngine;

public class ProjectileObjectPooling : MonoBehaviour
{
    public static ProjectileObjectPooling Instance { get; private set; }

    [SerializeField] private GameObject projectileBase;
    [SerializeField] private int poolInitSize = 30;
    [SerializeField] private int poolExpandSize = 5;

    private readonly Queue<GameObject> projectilePool = new Queue<GameObject>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            for (int i = 0; i < poolInitSize; i++)
            {
                GameObject obj = Instantiate(projectileBase);
                obj.SetActive(false);
                projectilePool.Enqueue(obj);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public GameObject GetProjectile(GameObject prefab, Vector3 position)
    {
        if (projectilePool.Count > 0)
        {
            GameObject obj = projectilePool.Dequeue();
            obj.transform.position = position;

            SpriteRenderer objRenderer = obj.GetComponent<SpriteRenderer>(), 
                            prefabRenderer = prefab.GetComponent<SpriteRenderer>();
            objRenderer.sprite = prefabRenderer.sprite;
            objRenderer.color = prefabRenderer.color;
            objRenderer.material = prefabRenderer.sharedMaterial;
            obj.transform.localScale = prefab.transform.localScale;

            BoxCollider2D collider = obj.GetComponent<BoxCollider2D>();
            if (collider)
            {
                BoxCollider2D prefabCollider = prefab.GetComponent<BoxCollider2D>();
                collider.size = prefabCollider.size;
                collider.offset = prefabCollider.offset;
            }

            TrailRenderer trail = obj.GetComponent<TrailRenderer>();
            if (trail)
            {
                TrailRenderer prefabTrail = prefab.GetComponent<TrailRenderer>();
                trail.enabled = prefabTrail && prefabTrail.enabled;

                if (trail.enabled)
                {
                    trail.startColor = prefabTrail.startColor;
                    trail.endColor = prefabTrail.endColor;
                    trail.time = prefabTrail.time;
                    trail.startWidth = prefabTrail.startWidth;
                    trail.endWidth = prefabTrail.endWidth;
                    trail.widthCurve = prefabTrail.widthCurve;
                    trail.widthMultiplier = prefabTrail.widthMultiplier;
                    trail.material = prefabTrail.sharedMaterial;
                }
            }

            obj.SetActive(true);
            return obj;
        }
        else
        {
            for (int i = 0; i < poolExpandSize; i++)
            {
                GameObject obj = Instantiate(projectileBase);
                obj.SetActive(false);
                projectilePool.Enqueue(obj);
            }

            GameObject objReturn = Instantiate(prefab, position, Quaternion.identity);
            return objReturn;
        }
    }

    public void ReturnProjectile(GameObject obj)
    {
        if (projectilePool.Contains(obj)) return;

        obj.GetComponent<TrailRenderer>().enabled = false;
        obj.SetActive(false);
        projectilePool.Enqueue(obj);
    }
}