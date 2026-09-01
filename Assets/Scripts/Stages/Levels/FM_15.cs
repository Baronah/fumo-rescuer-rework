using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Tilemaps;

public class FM_15 : StageManager
{
    [SerializeField] private GameObject[] laterSpawns; 
    private List<EndlessEnemySpawn> EndlessSpawns;

    [SerializeField] private TMP_Text timerText;
    [SerializeField] private float targetTimer = 240f;
    [SerializeField] private float laterSpawnsActivateTimegate = 120f, modifySpawnInterval = 45f, modifyTileInterval = 25f;
    float stageTimer = 0;

    [SerializeField] private float CM_StatueASPDBuff = 12f, CM_StatueMSPDBuff = 10f;

    [SerializeField] private AudioSource darkTileToggleWarning;

    FumoScript fumo;

    Tilemap darkTile;
    Color darkTileInitColor, darkTileClearColor;

    public override SkillTree_Manager.SkillName[] GetExtraSkills()
    {
        if (!fumo) fumo = FindFirstObjectByType<FumoScript>();
        if (fumo) return new[] { SkillTree_Manager.SkillName.GEOLOGIST_EXPLORE };
        return base.GetExtraSkills();
    }

    public override void Start()
    {
        if (!fumo) fumo = FindFirstObjectByType<FumoScript>();
        darkTile = FindFirstObjectByType<DarkTile>().GetComponent<Tilemap>();
        darkTileInitColor = darkTile.color;
        darkTileClearColor = ColorUtil.GetClearColorOf(darkTileInitColor);

        EndlessSpawns = new List<EndlessEnemySpawn>(FindObjectsOfType<EndlessEnemySpawn>(true));
        base.Start();
        UpdateTimer();
    }

    void ToggleDarkTile()
    {
        modifyTileTimer = 0f;
        warningPlayed = false;
        bool makeAppear = !darkTile.gameObject.activeSelf;
        StartCoroutine(DarkTileToggleCoroutine(makeAppear));
    }

    IEnumerator DarkTileToggleCoroutine(bool makeAppear)
    {
        Color from = makeAppear ? darkTileClearColor : darkTileInitColor;
        Color to = makeAppear ? darkTileInitColor : darkTileClearColor;
        
        if (makeAppear) darkTile.gameObject.SetActive(true);

        float c = 0, d = 1.2f;
        while (c < d)
        {
            darkTile.color = Color.Lerp(from, to, c * 1.0f / d);
            c += Time.deltaTime;
            yield return null;
        }

        darkTile.color = to;
        darkTile.gameObject.SetActive(makeAppear);
    }

    public override void EnableChallengeMode()
    {
        base.EnableChallengeMode();
    }

    void UpdateTimer()
    {
        if (IsStageStarted) stageTimer += Time.deltaTime;
        float countTimer = targetTimer - stageTimer;
        timerText.text = $"{Mathf.FloorToInt(countTimer / 60):00}:{Mathf.FloorToInt(countTimer % 60):00}";
    }

    public override void OnPlayerSpawn(PlayerBase player)
    {
        base.OnPlayerSpawn(player);
        player.mHealth *= 2;
        player.bDef += 15;
        player.bAtk = (short)(player.bAtk * 1.5f);
        player.ASPD += 20;
        player.defPen += 10;
        player.resPen += 15;
        player.b_moveSpeed *= 1.2f;
    }

    public override void OnEnemySpawn(EnemyBase enemy)
    {
        base.OnEnemySpawn(enemy);
        enemy.b_moveSpeed *= 1.15f;
    }

    public override void OnEnemySpecialAbilityActivate(EnemyBase enemy)
    {
        if (CharacterPrefabsStorage.EnableChallengeMode && enemy as SaintStatue)
        {
            string ASPDBuffKey = "STATUE_ASPD_BUFF_" + enemy.GetInstanceID();
            string MSPDBuffKey = "STATUE_MSPD_BUFF_" + enemy.GetInstanceID();
            EntityManager.Enemies.ForEach(enemy =>
            {
                if (!enemy || !enemy.IsAlive()) return;
                enemy.ApplyEffect(Effect.AffectedStat.ASPD, ASPDBuffKey, CM_StatueASPDBuff, 9999f, false);
                enemy.ApplyEffect(Effect.AffectedStat.MSPD, MSPDBuffKey, CM_StatueMSPDBuff, 9999f, true);
            });
        }
    }

    public override void OnEnemySpecialAbilityStop(EnemyBase enemy)
    {
        if (CharacterPrefabsStorage.EnableChallengeMode && enemy as SaintStatue)
        {
            string ASPDBuffKey = "STATUE_ASPD_BUFF_" + enemy.GetInstanceID();
            string MSPDBuffKey = "STATUE_MSPD_BUFF_" + enemy.GetInstanceID();
            EntityManager.Enemies.ForEach(enemy =>
            {
                if (!enemy || !enemy.IsAlive()) return;
                enemy.RemoveEffect(ASPDBuffKey);
                enemy.RemoveEffect(MSPDBuffKey);
            });
        }
    }

    [SerializeField] float modifySpawnTimer = 0, modifyTileTimer = 0f, enableSpawnTimer = 0f;
    bool warningPlayed = false;
    
    public override void Update()
    {
        if (stageTimer >= targetTimer) return;
        base.Update();

        if (!IsStageStarted) return;

        UpdateTimer();

        if (fumo && stageTimer >= targetTimer)
        {
            stageTimer = targetTimer;
            OnPlayerFumoProtected(fumo);
        }

        enableSpawnTimer += Time.deltaTime;
        if (enableSpawnTimer >= laterSpawnsActivateTimegate)
        {
            EnableLaterSpawn();
        }

        modifySpawnTimer += Time.deltaTime;
        if (modifySpawnTimer >= modifySpawnInterval)
        {
            ModifySpawnRate();
        }

        modifyTileTimer += Time.deltaTime;
        if (!warningPlayed && modifyTileTimer >= modifyTileInterval - 3f)
        {
            warningPlayed = true;
            darkTileToggleWarning.Play();
        }
        
        if (modifyTileTimer >= modifyTileInterval)
        {
            ToggleDarkTile();
        }
    }

    int enbCount = 0;
    void EnableLaterSpawn()
    {
        if (enbCount >= laterSpawns.Length) return;

        var list = laterSpawns[enbCount].GetComponentsInChildren<EnemySpawnpointScript>();
        foreach (EnemySpawnpointScript e in list) e.OnValueSetToTrue_Spawn = true;
        enbCount++;
        enableSpawnTimer = 0;
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
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.MATTERLLURGIST);
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.MATTERLLURGIST);
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.HIBERNATOR_KNIGHT);
                }
                break;
            case 1:
                foreach (var spawn in EndlessSpawns)
                {
                    spawn.enemyPrefabs.Clear();
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.MATTERLLURGIST);
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.HIBERNATOR_KNIGHT);
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.HIBERNATOR_KNIGHT);
                }
                break;
            case 2:
                foreach (var spawn in EndlessSpawns)
                {
                    spawn.enemyPrefabs.Clear();
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.MATTERLLURGIST);
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.GLOOMPINCER);
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.HIBERNATOR_KNIGHT);
                }
                break;
            case 3:
                foreach (var spawn in EndlessSpawns)
                {
                    spawn.enemyPrefabs.Clear();
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.TOY);
                }
                break;
            case 4:
                foreach (var spawn in EndlessSpawns)
                {
                    spawn.enemyPrefabs.Clear();
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.TOY);
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.TOY);
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.GLOOMPINCER);
                }
                break;
            case 5:
                foreach (var spawn in EndlessSpawns)
                {
                    spawn.enemyPrefabs.Clear();
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.TOY);
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.TOY);
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.GLOOMPINCER);
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.HIBERNATOR_KNIGHT);
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.HIBERNATOR_KNIGHT);
                    spawn.enemyPrefabs.Add(EnemyBase.EnemyCode.MATTERLLURGIST);
                }
                break;
        }
        modifySpawnCount++;
    }
}
