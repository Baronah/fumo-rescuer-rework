using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static EnemyBase;

public class FM_14 : StageManager
{
    [SerializeField] private EnemyCode CM_ReplaceFrom = EnemyCode.GLOOMPINCER, CM_ReplaceTo = EnemyCode.TOY;

    public override void EnableChallengeMode()
    {
        base.EnableChallengeMode();

        if (CharacterPrefabsStorage.EnableChallengeMode)
        {
            var spawnpoints = FindObjectsOfType<EnemySpawnpointScript>();
            foreach (var spawnpoint in spawnpoints)
            {
                if (spawnpoint.enemyPrefab == CM_ReplaceFrom)
                {
                    spawnpoint.enemyPrefab = CM_ReplaceTo;
                }
            }
        }
    }
}
