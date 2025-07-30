
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EntityManager : MonoBehaviour
{
    public static List<SpriteRenderer> SpriteRenderers = new();

    public static List<EntityBase> Entities => SpriteRenderers
        .Where(e => e && e.transform.parent.GetComponent<EntityBase>())
        .Select(e => e.transform.parent.GetComponent<EntityBase>())
        .ToList();

    public static List<EnemyBase> Enemies => SpriteRenderers
        .Where(e => e && e.transform.parent.GetComponent<EnemyBase>())
        .Select(e => e.transform.parent.GetComponent<EnemyBase>())
        .ToList();

    public static List<PlayerBase> Players => SpriteRenderers
        .Where(e => e && e.transform.parent.GetComponent<PlayerBase>())
        .Select(e => e.transform.parent.GetComponent<PlayerBase>())
        .ToList();

    public static void OnStageStart(float extraWaittime)
    {
        var spawnPoints = FindObjectsOfType<EnemySpawnpointScript>();
        foreach (var item in spawnPoints)
        {
            item.OnStageStart(extraWaittime);
        }
    }

    public void OnEntitySpawn(GameObject e)
    {
        var spriteRenderers = e.GetComponentsInChildren<SpriteRenderer>().Where(o => o.sortingLayerName == "Entities");
        if (spriteRenderers.Count() <= 0) return;
        SpriteRenderers.AddRange(spriteRenderers.Where(s => !SpriteRenderers.Contains(s)));
    }

    public void OnEntityDeath(GameObject e)
    {
    }

    private void Start()
    {
        SpriteRenderers = FindObjectsOfType<SpriteRenderer>().Where(o => o.sortingLayerName == "Entities").ToList();
        SortLayerIndex();
    }

    private short frameCounter = 0;

    private void FixedUpdate()
    {
        frameCounter++;
        if (frameCounter >= 20)
        {
            SortLayerIndex();
            frameCounter = 0;
        }
    }

    private void SortLayerIndex()
    {
        SpriteRenderers.RemoveAll(e => e == null || e.transform == null || e.transform.parent == null);

        SpriteRenderers.ToList().OrderBy(e => e.transform.parent.GetComponent<EntityBase>() ? e.transform.parent.position.y : e.transform.position.y)
            .ToList().ForEach(e => e.sortingOrder = Mathf.RoundToInt(e.transform.position.y * -1));
    }
}