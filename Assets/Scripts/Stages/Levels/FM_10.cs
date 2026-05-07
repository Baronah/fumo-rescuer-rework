using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

public class FM_10 : StageManager
{
    [SerializeField] private GameObject activateSudaram, activateStatue;
    [SerializeField] private Transform AssassinRestPort;

    bool spawnSudaram = false;
    ShroudedAssassin shroudedAssassin;
    bool assassinReviveTriggered = false;

    public override void Update()
    {
        if (IsStageStarted && shroudedAssassin != null 
            && 
            !assassinReviveTriggered 
            && (!shroudedAssassin.IsAlive() || shroudedAssassin.TowardDeathTriggered)
            )
        {
            OnAssassinSkillTrigger();
            assassinReviveTriggered = true;
        }

        base.Update();
    }

    [SerializeField] Tilemap bg1, bg2;
    void OnAssassinSkillTrigger()
    {
        activateStatue.GetComponentInChildren<EnemySpawnpointScript>().OnValueSetToTrue_Spawn = true;
        if (spawnSudaram)
        {
            var sudarams = activateSudaram.GetComponentsInChildren<EnemySpawnpointScript>();
            foreach (var s in sudarams)
            {
                s.OnValueSetToTrue_Spawn = true;
            }
        }
        StartCoroutine(TilemapDarkens());
    }

    [SerializeField] Color darkenColor;
    IEnumerator TilemapDarkens()
    {
        float c = 0, d = 5;
        while (c < d)
        {
            bg1.color = Color.Lerp(bg1.color, darkenColor, c * 1.0f / d);
            bg2.color = Color.Lerp(bg2.color, darkenColor, c * 1.0f / d);
            c += Time.deltaTime;
            yield return null;
        }

        bg1.color = bg2.color = darkenColor;
    }

    public override void EnableChallengeMode()
    {
        base.EnableChallengeMode();

        spawnSudaram = CharacterPrefabsStorage.EnableChallengeMode;

        if (!CharacterPrefabsStorage.EnableChallengeMode)
        {
            Destroy(activateSudaram);
        }
    }

    public override void OnEnemySpawn(EnemyBase enemy)
    {
        enemy.detectionRange = 5000f;

        if (enemy is ShroudedAssassin a)
        {
            shroudedAssassin = a;
            a.TowardDeathRestPort = AssassinRestPort;
        }

        base.OnEnemySpawn(enemy);
    }
}