using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using static EnemyBase;

public class EnemySpawnpointScript : MonoBehaviour
{
    public enum ActionType
    {
        NONE,
        DESTROY,
        ACTIVATE,
        DEACTIVATE,
    };

    [SerializeField] private bool spotPlayerUponSpawn = false, immediateSpawn = false, showTooltips;
    [SerializeField] private short InitTooltipsPriority = 0;
    [SerializeField] public List<EnemyCheckpointScript> enemyCheckpoints;
    [SerializeField] private float InitWaittime;
    [SerializeField] private EnemyCode enemyPrefab;
    [SerializeField] private short Quantity = 1;
    [SerializeField] private float OffsetRadius = 5f;

    [SerializeField] private GameObject[] TargetObjectsToInteract;
    [SerializeField] private ActionType OnEnemySpawn_Action = ActionType.NONE;
    [SerializeField] private ActionType OnEnemyDeath_Action = ActionType.NONE;

    private List<EnemyBase> SpawnEnemies = new();
    private float extraWaittime = 0;

    private static int TooltipsPriority = 0;
    public static void OnStageRetry() => TooltipsPriority = 0;

    private Transform SpawnPosition;

    private bool Spawned = false;
    public bool IsSpawnpointSpawned => Spawned;

    private void Start()
    {
        SpawnPosition = transform.Find("Spawnposition");
        if (immediateSpawn)
            StartCoroutine(SpawnEnemy());
    }

    public void OnStageStart(float extraWaittime = 0)
    {
        this.extraWaittime += extraWaittime;
        enabled = true;
    }

    public IEnumerator SpawnEnemy()
    {
        if (Spawned) yield break;

        for (int i = 0; i < Quantity; i++) 
        { 
            GameObject o = Instantiate(
                CharacterPrefabsStorage.EnemyPrefabs[(int) enemyPrefab], 
                SpawnPosition.position + new Vector3(Random.Range(-OffsetRadius, OffsetRadius), Random.Range(-OffsetRadius, OffsetRadius)), 
                Quaternion.identity);

            EnemyBase enemy = o.GetComponent<EnemyBase>();

            enemyCheckpoints.Insert(0, new EnemyCheckpointScript { Checkpoint = SpawnPosition, WaitTime = InitWaittime });
            enemy.SetCheckpoints(InitWaittime, enemyCheckpoints, showTooltips, TooltipsPriority + InitTooltipsPriority);
            TooltipsPriority++;
            enemy.enabled = true;
            Spawned = true;

            yield return null;

            if (spotPlayerUponSpawn)
            {
                enemy.ForceSpotPlayer();
            }
            else
            {
                StartCoroutine(enemy.StartMovementLockout(extraWaittime));
            }

            SpawnEnemies.Add(enemy);
        }

        foreach (var obj in TargetObjectsToInteract)
        {
            switch (OnEnemySpawn_Action)
            {
                case ActionType.NONE:
                    break;
                case ActionType.DESTROY:
                    Destroy(obj);
                    break;
                case ActionType.ACTIVATE:
                    obj.SetActive(true);
                    break;
                case ActionType.DEACTIVATE:
                    obj.SetActive(false);
                    break;
            }
        }
    }

    private void FixedUpdate()
    {
        OnSpawnedEnemyDeath();
    }

    private void OnSpawnedEnemyDeath()
    {
        if (SpawnEnemies.Count <= 0 || SpawnEnemies.Any(e => e.IsAlive())) return;

        SpawnEnemies.Clear();
        foreach (var obj in TargetObjectsToInteract)
        {
            switch (OnEnemyDeath_Action)
            {
                case ActionType.NONE:
                    break;
                case ActionType.DESTROY:
                    Destroy(obj);
                    break;
                case ActionType.ACTIVATE:
                    obj.SetActive(true);
                    break;
                case ActionType.DEACTIVATE:
                    obj.SetActive(false);
                    break;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (immediateSpawn || Spawned) return;

        if (other.CompareTag("Player"))
        {
            StartCoroutine(SpawnEnemy());
        }
    }
}

