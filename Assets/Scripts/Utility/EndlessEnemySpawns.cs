using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using static EnemyBase;
using Random = UnityEngine.Random;

public class EndlessEnemySpawn : EnemySpawnpointScript
{
    [SerializeField] public List<EnemyCode> enemyPrefabs;

    [SerializeField] private float GraduallyDecreaseSpawnInterval = 0.5f;

    public override IEnumerator SpawnEnemy()
    {
        if (immediateSpawn)
        {
            yield return new WaitForSeconds(InitDelay);
        }

        if (Spawned) yield break;

        yield return StartCoroutine(CreateEnemySpawn());
        StartCoroutine(DoRepeatedSpawn());
    }

    IEnumerator CreateEnemySpawn()
    {
        short maxSpawnPositions = (short)SpawnPositions.Length;

        for (int i = 0; i < maxSpawnPositions; i++)
        {
            int enemyCode = enemyPrefabs.Select(enemyPrefabs => (int)enemyPrefabs).ToArray()[Random.Range(0, enemyPrefabs.Count)];

            for (int j = 0; j < Quantity; j++)
            {
                Transform spawnTransform = SpawnPositions[Mathf.Min(i, maxSpawnPositions - 1)];

                GameObject o = Instantiate(
                    CharacterPrefabsStorage.EnemyPrefabs[enemyCode],
                    spawnTransform.position + new Vector3(Random.Range(-OffsetRadius, OffsetRadius), Random.Range(-OffsetRadius, OffsetRadius)),
                    Quaternion.identity);

                EnemyBase enemy = o.GetComponent<EnemyBase>();

                stageManager.OnEnemySpawn(enemy);

                enemyCheckpoints.Insert(0, new EnemyCheckpointScript { Checkpoint = spawnTransform, WaitTime = InitWaittime });
                enemy.SetCheckpoints(InitWaittime, enemyCheckpoints, showTooltips, TooltipsPriority + InitTooltipsPriority);
                if (showTooltips) TooltipsPriority++;
                enemy.enabled = true;
                Spawned = true;

                showTooltips = false;
                yield return null;

                if (spotPlayerUponSpawn)
                {
                    enemy.ForceSpotPlayer();
                }

                if (extraWaittime > 0) StartCoroutine(enemy.StartMovementLockout(extraWaittime));

                SpawnEnemies.Add(enemy);

                yield return null;
            }
        }
    }

    IEnumerator DoRepeatedSpawn()
    {
        while (true)
        {
            yield return new WaitForSeconds(WaittimeBeforeNextSpawn);
            yield return StartCoroutine(CreateEnemySpawn());

            WaittimeBeforeNextSpawn = Mathf.Max(5f, WaittimeBeforeNextSpawn - GraduallyDecreaseSpawnInterval);
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

    public override int GetEnemiesCount(bool countRepeatedSpawn = false)
    {
        return countRepeatedSpawn ? 1 : 0;
    }
}

