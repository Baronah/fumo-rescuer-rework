
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

[Singleton]
public class EntityManager : MonoBehaviour
{
    public static EntityManager _instance { get; private set; }
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            _instance = this;
        }
    }

    private short EnemyDefeatedCount = 0;
    public short GetEnemyDefeatedCount() => EnemyDefeatedCount;

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

    private float SFXValue;

    public void OnEntitySpawn(GameObject e)
    {
        var spriteRenderers = e.GetComponentsInChildren<SpriteRenderer>().Where(o => o.sortingLayerName == "Entities");
        if (spriteRenderers.Count() <= 0) return;
        SpriteRenderers.AddRange(spriteRenderers.Where(s => !SpriteRenderers.Contains(s)));

        var sfxs = e.GetComponent<EntityBase>().sfxs;
        foreach (var item in sfxs)
        {
            item.volume = SFXValue;
        }
    }

    public void OnEntityDeath(GameObject e)
    {
        if (e.GetComponent<EnemyBase>()) EnemyDefeatedCount++;
    }

    private void Start()
    {
        SFXValue = SaveDataManager.GetSFXVolume();
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