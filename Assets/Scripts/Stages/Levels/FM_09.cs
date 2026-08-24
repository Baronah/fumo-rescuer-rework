using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class FM_09 : StageManager
{
    [SerializeField] private GameObject laterSpawn;
    private List<EndlessEnemySpawn> EndlessSpawns;

    [SerializeField] private TMP_Text timerText, completeCond;
    [SerializeField] private float targetTimer = 240f;
    [SerializeField] private float laterSpawnsActivateTimegate = 120f, modifySpawnInterval = 45f;
    float stageTimer = 0;

    FumoScript fumo;

    public override SkillTree_Manager.SkillName[] GetExtraSkills()
    {
        if (!fumo) fumo = FindFirstObjectByType<FumoScript>();
        if (fumo) return new[] { SkillTree_Manager.SkillName.VICTORY_ATK };
        return base.GetExtraSkills();
    }

    public override void Start()
    {
        if (!fumo) fumo = FindFirstObjectByType<FumoScript>();
        EndlessSpawns = new List<EndlessEnemySpawn>(FindObjectsOfType<EndlessEnemySpawn>(true));
        base.Start();
    }

    public override void EnableChallengeMode()
    {
        base.EnableChallengeMode();
        if (CharacterPrefabsStorage.EnableChallengeMode)
        {
            StageCompleteConditionType = StageCompleteCondition.PROTECT_FUMO;
            EndlessSpawns.ForEach(s => s.spotPlayerUponSpawn = false);
            fumo.GetComponent<Collider2D>().enabled = true;
        }

        completeCond.text = StageCompleteConditionType == StageCompleteCondition.PROTECT_FUMO ?
            "Goal: <color=yellow>Protect the Fumo</color></b>" 
            : "Goal: <color=yellow>Survive until time runs out</color></b>";

        float countTimer = targetTimer - stageTimer;
        timerText.text = $"{Mathf.FloorToInt(countTimer / 60):00}:{Mathf.FloorToInt(countTimer % 60):00}";
    }

    public override void OnEnemySpawn(EnemyBase enemy)
    {
        base.OnEnemySpawn(enemy);

        if (enemy.attackPattern == EntityBase.AttackPattern.MELEE)
            enemy.detectionRange += 70f;
    }

    public override void OnPlayerSpawn(PlayerBase player)
    {
        base.OnPlayerSpawn(player);
        player.mHealth *= 2;
        player.bDef += 15;
        player.bRes += 10;
        player.bAtk = (short)(player.bAtk * 1.5f);
        player.ASPD += 25;
        player.defPen += 10;
        player.resPen += 15;
    }

    float modifySpawnTimer = 0;
    bool enabledLaterSpawn = false;
    public override void Update()
    {
        if (stageTimer >= targetTimer) return;
        base.Update();

        if (!IsStageStarted) return;

        stageTimer += Time.deltaTime;

        float countTimer = targetTimer - stageTimer;
        timerText.text = $"{Mathf.FloorToInt(countTimer / 60):00}:{Mathf.FloorToInt(countTimer % 60):00}";

        if (fumo && stageTimer >= targetTimer)
        {
            stageTimer = targetTimer;
            OnPlayerFumoProtected(fumo);
        }

        modifySpawnTimer += Time.deltaTime;

        if (stageTimer >= laterSpawnsActivateTimegate && !enabledLaterSpawn)
        {
            EnableLaterSpawn();
        }

        if (modifySpawnTimer >= modifySpawnInterval)
        {
            ModifySpawnRate();
        }
    }

    void EnableLaterSpawn()
    {
        if (enabledLaterSpawn) return;
        
        enabledLaterSpawn = true;
        var list = laterSpawn.GetComponentsInChildren<EndlessEnemySpawn>();
        foreach (EndlessEnemySpawn e in list) e.OnValueSetToTrue_Spawn = true;
    }

    short modifySpawnCount = 0;
    void ModifySpawnRate()
    {
        modifySpawnTimer = 0;
        switch (modifySpawnCount)
        {
            case 0:
                foreach (var spawn in EndlessSpawns)
                {
                    spawn.enemyPrefabs.Clear();
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.HOUND);
                }
                break;
            case 1:
            case 2:
                foreach (var spawn in EndlessSpawns)
                {
                    spawn.enemyPrefabs.Clear();
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.HEIR);
                }
                break;
            case 3:
                foreach (var spawn in EndlessSpawns)
                {
                    spawn.enemyPrefabs.Clear();
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.ORIGINIUM_SPIDER);
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.ORIGINIUM_SPIDER);
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.ORIGINIUM_SPIDER);
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.ORIGINIUM_SPIDER_ALPHA);
                }
                break;
            case 4:
                foreach (var spawn in EndlessSpawns)
                {
                    spawn.enemyPrefabs.Clear();
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.ORIGINIUM_SPIDER);
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.ORIGINIUTANT);
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.ZEALOT);
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.MATTERLLURGIST);
                }
                break;
            case 5:
                foreach (var spawn in EndlessSpawns)
                {
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.ORIGINIUM_SPIDER_ALPHA);
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.BLOODBOIL_KNIGHT);
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.BLOODBOIL_KNIGHT);
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.ORIGINIUTANT);
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.SUDARAM);
                }
                break;
            case 6:
                foreach (var spawn in EndlessSpawns)
                {
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.SUDARAM);
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.SUDARAM);
                }
                break;
        }
        modifySpawnCount++;
    }
}