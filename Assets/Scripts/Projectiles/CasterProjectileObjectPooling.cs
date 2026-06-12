using System.Collections.Generic;
using UnityEngine;

public class CasterProjectileObjectPooling : MonoBehaviour
{
    public static CasterProjectileObjectPooling Instance { get; private set; }

    [SerializeField] private GameObject projectileInfo;
    [SerializeField] private int poolSize = 10;
    [SerializeField] private int poolExpandSize = 5;

    private Queue<GameObject> projectilePool = new Queue<GameObject>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            for (int i = 0; i < poolSize; i++)
            {
                GameObject obj = Instantiate(projectileInfo);
                obj.SetActive(false);
                projectilePool.Enqueue(obj);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public GameObject GetProjectile(Vector3 position)
    {
        if (projectilePool.Count > 0)
        {
            GameObject obj = projectilePool.Dequeue();
            obj.transform.position = position;
            obj.SetActive(true);
            return obj;
        }
        else
        {
            for (int i = 0; i < poolExpandSize; ++i)
            {
                GameObject ex = Instantiate(projectileInfo);
                ex.SetActive(false);
                projectilePool.Enqueue(ex);
            }

            GameObject obj = Instantiate(projectileInfo, position, Quaternion.identity);
            return obj;
        }
    }

    public void ReturnProjectile(GameObject obj)
    {
        if (projectilePool.Contains(obj)) return;

        obj.SetActive(false);
        projectilePool.Enqueue(obj);
    }
}