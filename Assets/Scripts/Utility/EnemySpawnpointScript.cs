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

    [SerializeField] public float InitDelay = 0f;

    [SerializeField] public bool spotPlayerUponSpawn = false, immediateSpawn = false, isBound = false, showTooltips, isInsignificant = false;
    [SerializeField] protected short InitTooltipsPriority = 0;
    [SerializeField] public List<EnemyCheckpointScript> enemyCheckpoints;
    [SerializeField] protected float InitWaittime;
    [SerializeField] public EnemyCode enemyPrefab;
    [SerializeField] private bool doSpawnEnemy = true;
    [SerializeField] protected short Quantity = 1;
    [SerializeField] protected float OffsetRadius = 5f;

    [SerializeField] private bool RepeatedSpawn = false;
    [ShowIf("RepeatedSpawn", true)]
    [SerializeField] protected float WaittimeBeforeNextSpawn = 5f;

    [SerializeField] private GameObject[] TargetObjectsToInteract;

    [SerializeField] private ActionType OnEnemySpawn_Action = ActionType.NONE;

    [ShowIf("RepeatedSpawn", false)]
    [SerializeField] private ActionType OnEnemyDeath_Action = ActionType.NONE;

    protected List<EnemyBase> SpawnEnemies = new();
    protected float extraWaittime = 0;

    protected static int TooltipsPriority = 0;
    public static void OnStageRetry() => TooltipsPriority = 0;

    protected Transform[] SpawnPositions;

    protected StageManager stageManager;

    protected bool Spawned = false;
    public bool IsSpawnpointSpawned => Spawned;

    public bool ShowSpawnpointIndicator = true;
    [SerializeField] GameObject SpawnpointIndicatorGraphicPrefab;
    List<GameObject> SpawnpointIndicatorGraphics = new();

    bool CanSpawn => !Spawned || (RepeatedSpawn && doSpawnEnemy);

    private void Awake()
    {
        stageManager = FindObjectOfType<StageManager>(true);
        if (stageManager.DoNotShowSpawnsGraphic) ShowSpawnpointIndicator = false;

        GetSpawnPositions();
    }

    public bool OnValueSetToTrue_Spawn = true;
    private void Start()
    {
        StartCoroutine(WaitUntilSpawnValueIsTrue_ThenSpawn());
    }

    IEnumerator WaitUntilSpawnValueIsTrue_ThenSpawn()
    {
        yield return new WaitUntil(() => OnValueSetToTrue_Spawn);
        if (immediateSpawn)
            StartCoroutine(SpawnEnemy());
    }

    private void Update()
    {
        bool activeStatus = ShowSpawnpointIndicator && CanSpawn;
        foreach (var sp in SpawnpointIndicatorGraphics)
        {
            if (sp) sp.SetActive(activeStatus);
        }
    }

    public void OnStageStart(float extraWaittime = 0)
    {
        if (!this) return;
        this.extraWaittime += extraWaittime;
        enabled = true;
    }

    public virtual IEnumerator SpawnEnemy()
    {
        if (immediateSpawn)
        {
            yield return new WaitForSeconds(InitDelay);
        }

        if (Spawned) yield break;

        if (doSpawnEnemy)
        {
            yield return StartCoroutine(CreateEnemySpawn());
            if (RepeatedSpawn)
            {
                StartCoroutine(DoRepeatedSpawn());
            }
        }

        foreach (var obj in TargetObjectsToInteract)
        {
            if (!obj) continue;

            switch (OnEnemySpawn_Action)
            {
                case ActionType.NONE:
                    break;
                case ActionType.DESTROY:
                    EntityBase en = obj.GetComponent<EntityBase>();
                    if (en)
                    {
                        en.InstaKill();
                    }
                    else if (obj == this) Destroy(obj, 0.5f);
                    else Destroy(obj);
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

    IEnumerator CreateEnemySpawn()
    {
        short maxSpawnPositions = (short)SpawnPositions.Length;

        for (int i = 0; i < maxSpawnPositions; i++)
        {
            for (int j = 0; j < Quantity; j++)
            {
                List<EnemyCheckpointScript> enemyCheckpointsClone = new(enemyCheckpoints);
                Transform spawnTransform = SpawnPositions[Mathf.Min(i, maxSpawnPositions - 1)];
                Vector3 spawnPos = spawnTransform.position + new Vector3(Random.Range(-OffsetRadius, OffsetRadius), Random.Range(-OffsetRadius, OffsetRadius));

                spawnTransform.position = spawnPos;

                GameObject o = Instantiate(
                    CharacterPrefabsStorage.EnemyPrefabs[(int)enemyPrefab],
                    spawnPos,
                    Quaternion.identity);

                EnemyBase enemy = o.GetComponent<EnemyBase>();

                stageManager.OnEnemySpawn(enemy);

                enemyCheckpointsClone.Insert(0, new EnemyCheckpointScript { Checkpoint = spawnTransform, WaitTime = InitWaittime });
                enemy.SetCheckpoints(InitWaittime, enemyCheckpointsClone, showTooltips, TooltipsPriority + InitTooltipsPriority);
                if (showTooltips) TooltipsPriority++;
                enemy.enabled = true;
                Spawned = true;

                showTooltips = false;
                yield return null;

                if (spotPlayerUponSpawn)
                {
                    enemy.ForceSpotPlayer();
                }

                if (isBound) enemy.BoundTimer = 9999f;
                if (extraWaittime > 0) StartCoroutine(enemy.StartMovementLockout(extraWaittime));

                SpawnEnemies.Add(enemy);

                yield return null;
            }
        }
    }

    IEnumerator DoRepeatedSpawn()
    {
        if (!RepeatedSpawn) yield break;

        while (true)
        {
            yield return new WaitForSeconds(WaittimeBeforeNextSpawn);
            yield return StartCoroutine(CreateEnemySpawn());
        }
    }

    private void FixedUpdate()
    {
        OnSpawnedEnemyDeath();
    }

    bool onDeathTriggered = false;
    public virtual void OnSpawnedEnemyDeath()
    {
        if (onDeathTriggered) return;
        if (!doSpawnEnemy || RepeatedSpawn) return;
        if (SpawnEnemies.Count <= 0 || SpawnEnemies.Any(e => !e.IsComponentsInitialized || e.IsConsideredActive())) return;

        onDeathTriggered = true;
        foreach (var obj in TargetObjectsToInteract)
        {
            if (!obj) continue;

            switch (OnEnemyDeath_Action)
            {
                case ActionType.NONE:
                    break;
                case ActionType.DESTROY:
                    EntityBase en = obj.GetComponent<EntityBase>();
                    if (en)
                    {
                        en.InstaKill();
                    }
                    else if (obj == this) Destroy(obj, 0.5f);
                    else Destroy(obj);
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
        if (immediateSpawn || Spawned || !OnValueSetToTrue_Spawn) return;

        if (other.CompareTag("Player"))
        {
            StartCoroutine(SpawnEnemy());
        }
    }

    public virtual int GetEnemiesCount(bool countInfiniteSpawns = false) 
    {
        if (!this.gameObject) return 0;

        if (!stageManager) stageManager = FindObjectOfType<StageManager>(true);
        GetSpawnPositions();

        int activeCount = SpawnEnemies.Count(e => e.IsConsideredActive() && !e.IsInsignificant);
        bool IsNotAccountForCounter = isInsignificant || (Spawned && activeCount == 0);

        if (!doSpawnEnemy || SpawnPositions == null || IsNotAccountForCounter) return 0;
        
        if (RepeatedSpawn) return countInfiniteSpawns ? 1 : 0;

        if (Spawned) return activeCount;
        return SpawnPositions.Length * Quantity;
    }

    void GetSpawnPositions()
    {
        if (SpawnPositions != null) return;

        Transform SpawnPosition = transform.Find("Spawnposition");
        SpawnPositions = SpawnPosition.GetComponentsInChildren<Transform>();

        if (!doSpawnEnemy || !ShowSpawnpointIndicator) return;
        foreach (var sp in SpawnPositions)
        {
            GameObject graphic = Instantiate(SpawnpointIndicatorGraphicPrefab, sp.position, Quaternion.identity, sp);
            SpawnpointIndicatorGraphics.Add(graphic);
        }
    }
}


