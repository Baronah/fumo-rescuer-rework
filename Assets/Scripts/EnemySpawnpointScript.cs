using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using static EnemyBase;

public class EnemySpawnpointScript : MonoBehaviour
{
    [SerializeField] private bool spotPlayerUponSpawn = false, immediateSpawn = false, showTooltips;
    [SerializeField] public List<EnemyCheckpointScript> enemyCheckpoints;
    [SerializeField] private float InitWaittime;
    [SerializeField] private EnemyCode enemyPrefab;
    [SerializeField] private short Quantity = 1;
    [SerializeField] private float OffsetRadius = 5f;

    private static int TooltipsPriority = 0;
    public static void OnStageRetry() => TooltipsPriority = 0;

    private Transform SpawnPosition;

    private bool Spawned = false;

    private void Start()
    {
        SpawnPosition = transform.Find("Spawnposition");
        if (immediateSpawn) SpawnEnemy();
    }

    public void SpawnEnemy()
    {
        if (Spawned) return;

        for (int i = 0; i < Quantity; i++) 
        { 
            GameObject o = Instantiate(
                CharacterPrefabsStorage.EnemyPrefabs[(int) enemyPrefab], 
                SpawnPosition.position + new Vector3(Random.Range(-OffsetRadius, OffsetRadius), Random.Range(-OffsetRadius, OffsetRadius)), 
                Quaternion.identity);

            EnemyBase enemy = o.GetComponent<EnemyBase>();

            enemyCheckpoints.Insert(0, new EnemyCheckpointScript { Checkpoint = SpawnPosition, WaitTime = InitWaittime });
            enemy.SetCheckpoints(InitWaittime, enemyCheckpoints, showTooltips, TooltipsPriority);
            TooltipsPriority++;

            if (spotPlayerUponSpawn)
            {
                enemy.ForceSpotPlayer();
            }

            enemy.enabled = true;
            Spawned = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (immediateSpawn || Spawned) return;

        if (other.CompareTag("Player"))
        {
            SpawnEnemy();
        }
    }
}

