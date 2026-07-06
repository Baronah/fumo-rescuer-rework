 using Assets.Scripts;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using static EnemyBase;
using static LevelDifficultyModifier;
using static SkillTree_Manager;
using Image = UnityEngine.UI.Image;

[Singleton]
public class StageManager : MonoBehaviour
{
    public static StageManager _instance { get; private set; }
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

    public int LevelIndex = 0;
    protected bool PressedAnyKey = false;
    protected bool IsStageStarted = false;
    protected bool IsStagePaused = false;
    public bool ReadIsStagePaused => IsStagePaused;

    protected static bool IsFirstTimeStageEnter = true;

    [SerializeField] private GameObject pauseOverlay, titleOverlay, mapOverview, mapCamera;
    [SerializeField] private EnemyCode[] appearingEnemies;
    [SerializeField] private TMP_Text RemainingEnemiesTxt;
    protected GameObject RemainingEnemiesGO => RemainingEnemiesTxt.transform.parent.gameObject;

    [SerializeField] private float extraEnemyWaittime = 0f, extraPlayerWaittime = 1.5f;
    [SerializeField] private TMP_Text Title, LoadingState;
    [SerializeField] private CharacterPrefabsStorage prefabStorage;

    protected CameraMovement mainCamera;
    [SerializeField] private float ShowcaseSize;
    [SerializeField] private Transform[] CameraShowcases;
    [SerializeField] private float[] Waittimes;

    [SerializeField] private Image PauseButton, SlowImg;
    [SerializeField] private Button o_QuitBtn, o_RetryBtn;
    [SerializeField] private Sprite PausedSprite, UnpausedSprite;

    CharacterPrefabsStorage characterPrefabsStorage;

    protected string LevelName;
    protected AudioSource BGM;
    public AudioSource StageBGM => BGM;

    protected EnemySpawnpointScript[] enemySpawnpoints;

    [SerializeField] protected float ChallengeModeStatsModifier = 1.2f;

    protected float timeScaleSlow = 0.4f;
    protected bool isSlowing = false;

    protected bool IsEnemyAlive => EntityManager.Enemies.Any(e => e && e.IsAlive()) || enemySpawnpoints.Any(e => !e.IsSpawnpointSpawned);

    public bool DoNotShowSpawnsGraphic = false;

    public bool EnableLowDetail = false;

    public enum StageCompleteCondition
    {
        ELIMINATE_ALL_ENEMIES,
        RETRIEVE_FUMO,
        PROTECT_FUMO,
        SURVIVE_FOR_GIVEN_TIME,
    };

    public StageCompleteCondition StageCompleteConditionType = StageCompleteCondition.ELIMINATE_ALL_ENEMIES;

    public enum EnvironmentType
    {
        KEYS,
        ORIGINIUM_TILE,
        ONE_WAY_PASSAGE,
        HEAT_PUMP_VENT,
        MEDICAL_TILE,
        DARK_ZONE,
    };

    public EnvironmentType[] StageEvironmentTypes;

    private PlayerManager playerManager => PlayerManager._instance;

    bool IsStageReady = false,
        IsStageEnd = false,
        IsStageEndOverlayActive = false,
        IsResultVictory = false;

    GameObject TopOverlay;

    public Collider2D[] ObstacleCollider;

    // Theoria
    public bool Absolutism, Statis, Gravity, Acceleration, Hunger, Adaption;
    //

    [SerializeField] GameObject ExtraEnemySpawn;
    public virtual void Start()
    {
        GlobalStageManager.OnStageStart();

        Absolutism = CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.ABSOLUTISM);
        Statis = CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.STATIS);
        Gravity = CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.GRAVITY);
        Acceleration = CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.ACCELERATION);
        Hunger = CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.HUNGER);
        Adaption = CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.ADAPTION);

        LevelName = SceneManager.GetActiveScene().name;
        Title.text = $"<b><size=120>{LevelName}</size></b>\n{prefabStorage.LevelTitles[LevelIndex]}";

        o_QuitBtn.onClick.AddListener(QuitStage);
        o_RetryBtn.onClick.AddListener(RetryStage);
        PauseButton.GetComponent<Button>().onClick.AddListener(TogglePauseStage);

        SetGoal();
        SetTips();

        StartCoroutine(OnStartOverlayFadeout());

        TopOverlay = GameObject.Find("OnTopOverlay");
        TopOverlay.SetActive(false);

        ObstacleCollider = FindObjectsOfType<TilemapCollider2D>().Where(t => t.gameObject.layer == 8).ToArray();

        GameObject TheoryWorld = GameObject.Find("TheoryWorld");
        TheoryWorld.SetActive(CharacterPrefabsStorage.Skills.Any(s => s.Value.skillType == SkillType.THEORIA));
        if (!TheoryWorld.activeSelf) Destroy(TheoryWorld);

        mainCamera = GetComponentInChildren<CameraMovement>(true);
        BGM = GetComponent<AudioSource>();
        BGM.volume = GlobalStageManager.GetBGM();

        timeScaleSlow = Mathf.Clamp(PlayerPrefs.GetFloat("TimeScaleSlow", 0.4f), 0.1f, 0.9f);

        var sfxs = FindObjectsOfType<AudioSource>(true).Where(a => a != BGM);
        float sfxValue = GlobalStageManager.GetSFX();
        foreach (var item in sfxs)
        {
            item.volume = sfxValue;
        }

        if (!ExtraEnemySpawn) ExtraEnemySpawn = GameObject.Find("Researcher_ExtraEnemies");
        if (ExtraEnemySpawn && CharacterPrefabsStorage.DifficultyLevel - 1 < (int) DiffType.ExtraEnemies)
        {
            Destroy(ExtraEnemySpawn);
        }

        timeDilation.SetActive(CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.TIME_DILATION));
        if (StageCompleteConditionType == StageCompleteCondition.RETRIEVE_FUMO) timeDilation.transform.position += new Vector3(0, 100);

        RemainingEnemiesGO.SetActive(StageCompleteConditionType == StageCompleteCondition.ELIMINATE_ALL_ENEMIES);

        EnableChallengeMode();

        enemySpawnpoints = FindObjectsOfType<EnemySpawnpointScript>(true).ToArray();

        if (CharacterPrefabsStorage.EnableChallengeMode) LoadingState.transform.position += new Vector3(0, -50);
        LoadingState.text = "<i>Loading stage, please wait...</i>";

        StartCoroutine(LoadRequiredPrefabs());
        EnemyTooltipsScript.isAnyTooltipsShowing = false;
        Time.timeScale = 0f;
    }

    void SetGoal()
    {
        TMP_Text TxtGoal = GameObject.Find("Goal").GetComponent<TMP_Text>();
        if (CharacterPrefabsStorage.EnableChallengeMode) TxtGoal.transform.position += new Vector3(0, -50);

        TxtGoal.text = GetExplorationMode() + "\n";
        TxtGoal.text += StageCompleteConditionType switch
        {
            StageCompleteCondition.RETRIEVE_FUMO => "Goal: <color=#00ffff>Retrieve the Mint Fumo</color></b>",
            StageCompleteCondition.PROTECT_FUMO => "Goal: <color=yellow>Protect the Mint Fumo</color></b>",
            StageCompleteCondition.SURVIVE_FOR_GIVEN_TIME => "Goal: <color=yellow>Survive until time runs out</color></b>",
            _ => "Goal: <color=red>Eliminate all enemies</color></b>",
        };
    }

    #region Loading Screen Tips
    private TMP_Text TxtTips;
    private readonly List<string> BaseTips = new()
    {
        "Overflowing swap CD will be counted toward charges (up to 2, indicated by the green diamond on top of your swap character). " +
            "When available, the next swap will consume a charge, but have no CD.",
        "Swap refreshes all CDs of your character,\nand gives them a small window of invulnerability.",
        "Make good use of terrains and map layout to gain advantages in combat!",
        $"Different characters also have different stats and skills.\nYou can check them anytime by pressing '{AttributeKey}'.",
        "Gates with keyhole symbol can be removed by picking up the key with its corresponding color, while gates with sword symbol requires you to eliminate certain enemies in its area in order to be removed.",
        "Obstacles can block the enemy's sight of view,\npreventing them from spotting you.",
        "Ranged enemies tend to have larger detection range than melee.",
        "Enemies that spotted you can alert their nearby allies.",
        "Enemies that are under the effect of crowd-control\ncan not attack nor detect you.",
        "Long-ranged enemies will try to keep distance in combat.",
        "When a patrolling enemy is attacked, they and their nearby allies will try to rush toward where the attack came from (represented by the '?' symbol above them).",
        "Melee attack can be dodged by quickly moving out of the attacker's range.\nFor ranged attack, just dodge their projectiles.",
        "Enemies who have spotted you have a '!' symbol above them. If it turns red, it means you are inside their attack range.",
        "DEF and RES reduce the respective damage type a unit receives.\n" +
        "Every 1 DEF/RES reduces the amount of physical/magical damage by 1%, up to 95% and 90%, respectively.",
    };

    private readonly List<string> MintLabTips = new()
    {
        "Borrowing the right inventions can help a ton,\njust remember to return them to her after completing a stage :)",
        $"Forgot which inventions you borrored?\nPress '{AttributeKey}' to open view menu, then '{SwapAttribute}' to see them!",
        "Please pay her a visit after this.",
    };

    private readonly List<string> AdvancedTips = new()
    {
        $"Need time to pull off a difficult move? Press '{SlowKey}' to toggle slow-mo!",
        "'Der Tag neight Sich' (Ranged character's ultimate) can be cancelled by recast, move, attack or use special during channel time.",
        "'Zeropoint Burst' (Ranged character's special) has a delay of 0.1 seconds",
        "'Evasion' (Melee character's special) gives you invincibility during dash.",
        "Enemies with higher weight are more resistant to push/pull effects.",
        "Push/pull will also cancel attacks.",
        "Buffs are carried across swaps, debuffs are not.",
    };

    private readonly List<string> ResearcherTips = new()
    {
        "Clear challenge mode of a level and complete Researcher 1 or higher to trim your medal (it's very valuable trust).",
        "Clear challenge mode of a level at Researcher 6 or higher to trim your trimmed medal(?).",
    };

    const string SlowKey = "SLOWKEYREPLACE",
                AttributeKey = "ATTRIBUTEKEY",
                SwapAttribute = "SKILLVIEWTOGGLE";

    private readonly string[] Trolls =
    {
        "Everyone likes Mint Arknights.",
        ":minthype:",
        "Surf Bungaku Kamakura",
        "Tsukiyoi refers to the yoizuki in Japanese, the early evening moon of August which is also called the yuzuki.",
        "ASIAN KUNG-FU GENERATION my beloved.",
        "I never troll.",
        "noodles.",
        "expect reading :)",
        "Post this sheep at random interval.",
        "In infinite tries everything is possible.",
        "Welcome back Jonny",
        "Oh nyo you lost the game",
        "You and I we're gonna live forever.",
    };

    int[] luckyNumbers = new int[] { 52, 39, 2, 27, 4 };

    void SetTips()
    {
        int tipCount = PlayerPrefs.GetInt("TipsCounter", 0);
        List<string> Tips = new(BaseTips);
        if (CharacterPrefabsStorage.Skills.Count > 0) Tips.AddRange(MintLabTips);
        if (tipCount >= 10) Tips.AddRange(AdvancedTips);
        if (SaveDataManager.AllResearchesUnlocked) Tips.AddRange(ResearcherTips);

        TxtTips = GameObject.Find("Tips").GetComponent<TMP_Text>();

        bool IsTroll = tipCount >= 25 && luckyNumbers.Contains(Random.Range(0, 100));
        TxtTips.text = "<b>TIPS:</b> " +
            (IsTroll ? Trolls[Random.Range(0, Trolls.Length)] : Tips[Random.Range(0, Tips.Count)]);
        TxtTips.text = TxtTips.text
            .Replace(SlowKey, KeybindButton.GetDisplayNameForKey(InputManager.Instance.SlowKey))
            .Replace(AttributeKey, KeybindButton.GetDisplayNameForKey(InputManager.Instance.ViewInfoKey))
            .Replace(SwapAttribute, KeybindButton.GetDisplayNameForKey(InputManager.Instance.SwapInfoKey));

        PlayerPrefs.SetInt("TipsCounter", tipCount + 1);
        PlayerPrefs.Save();
    }
    #endregion

    private int GetEnemyCount(bool countInfiniteSpawns = false)
    {
        int count = 0;
        foreach (var enemy in enemySpawnpoints)
        {
            if (!enemy) continue;
            count += enemy.GetEnemiesCount(countInfiniteSpawns);
        }

        return count;
    }

    GameObject StageTransitionOverlay;
    IEnumerator OnStartOverlayFadeout()
    {
        StageTransitionOverlay = GameObject.Find("StageOverlayTransition");
        if (!StageTransitionOverlay) yield break;

        Image image = StageTransitionOverlay.GetComponentInChildren<Image>();
        float fadeOutTime = 1f, c = 0, cJump = 0.02f;
        while (c < fadeOutTime)
        {
            image.color = Color.Lerp(Color.black, Color.clear, c * 1.0f / fadeOutTime);
            c += cJump;
            yield return new WaitForSecondsRealtime(cJump);
        }

        image.color = Color.clear;

        Destroy(StageTransitionOverlay);
    }

    #region Time Dilation Upgrade
    [SerializeField] GameObject timeDilation;
    IEnumerator TimeDilationCoroutine()
    {
        TimeDilationScript timeDilationScript = timeDilation.GetComponent<TimeDilationScript>();

        Slider timeDilationSlider = timeDilationScript.Slider;
        Image fillRect = timeDilationScript.Fill, 
              icon = timeDilationScript.Icon;

        while (!IsStageEnd)
        {
            // slow phase
            timeDilationScript.SetUI_TimeSlow();

            // debuff
            EntityManager.Enemies.ForEach(e =>
            {
                if (!e || !e.IsAlive()) return;
                e.ApplyEffect(Effect.AffectedStat.MSPD, "TIME_DILATION_MSPD_DEBUFF", -timeDilationScript.speedDebuff, timeDilationScript.cycleSwap, true);
                e.ApplyEffect(Effect.AffectedStat.ASPD, "TIME_DILATION_ASPD_DEBUFF", -timeDilationScript.speedDebuff, timeDilationScript.cycleSwap, true);
            });
            float count = 0;
            while (count < timeDilationScript.cycleSwap)
            {
                count += Time.fixedDeltaTime;
                timeDilationSlider.value = count;

                yield return new WaitForFixedUpdate();
            }

            // recover phase
            timeDilationScript.SetUI_Recover();
            count = 0;

            while (count < timeDilationScript.cooldown)
            {
                count += Time.fixedDeltaTime;
                timeDilationSlider.value = timeDilationScript.cooldown - count;
                yield return new WaitForFixedUpdate();
            }

            // fast phase
            // buff
            timeDilationScript.SetUI_TimeFast();
            EntityManager.Enemies.ForEach(e =>
            {
                if (!e || !e.IsAlive()) return;
                e.ApplyEffect(Effect.AffectedStat.MSPD, "TIME_DILATION_MSPD_BUFF", timeDilationScript.speedBuff, timeDilationScript.cycleSwap, true);
                e.ApplyEffect(Effect.AffectedStat.ASPD, "TIME_DILATION_ASPD_BUFF", timeDilationScript.speedBuff, timeDilationScript.cycleSwap, true);
            });

            count = 0f;
            while (count < timeDilationScript.cycleSwap)
            {
                count += Time.fixedDeltaTime;
                timeDilationSlider.value = count;
                yield return new WaitForFixedUpdate();
            }

            count = 0f;
            timeDilationScript.SetUI_Recover();

            while (count < timeDilationScript.cooldown)
            {
                count += Time.fixedDeltaTime;
                timeDilationSlider.value = timeDilationScript.cooldown - count;
                yield return new WaitForFixedUpdate();
            }
        }
    }
    #endregion

    public virtual void EnableChallengeMode()
    {
        if (CharacterPrefabsStorage.EnableChallengeMode) Title.text += "\n<color=red><size=52>[CHALLENGE MODE]</size></color>";
    }

    public virtual void OnEnemySpawn(EnemyBase enemy)
    {
        EnemyStatsLookup.GetStats(enemy, LevelIndex, out bool hasChanged);
        EnemyStatsLookup.ProcessEnemyDifficultyScaling(enemy, StageCompleteConditionType);

        if (CharacterPrefabsStorage.EnableChallengeMode)
        {
            enemy.bAtk = (short)(enemy.bAtk * ChallengeModeStatsModifier);
            enemy.mHealth = (int)(enemy.mHealth * ChallengeModeStatsModifier);
        }

        foreach (var skillData in CharacterPrefabsStorage.Skills)
        {
            SkillName skill = skillData.Key;
            switch (skill)
            {
                case SkillName.OBSCURE_VISION:
                    enemy.b_attackRange *= 0.8f;
                    enemy.detectionRange *= 0.8f;
                    break;

                case SkillName.HUNGER:
                    enemy.bAtk = (short)(enemy.bAtk * 1.5f);
                    enemy.ASPD += 30;
                    enemy.lifeSteal += 0.3f;
                    break;

                case SkillName.ABSOLUTISM:
                    if (enemy.attackPattern == EntityBase.AttackPattern.MELEE || enemy.attackPattern == EntityBase.AttackPattern.NONE)
                    {
                        enemy.mHealth *= 2;
                        enemy.b_moveSpeed *= 1.35f;
                        enemy.weight += 2;
                    }
                    else if (enemy.attackPattern == EntityBase.AttackPattern.RANGED)
                    {
                        enemy.ASPD += enemy.b_attackRange * 0.15f;
                        enemy.b_attackRange = enemy.detectionRange = 15000f;
                    }
                    break;

                case SkillName.HIBERNATE:
                    float duration = 40f;
                    enemy.mHealth *= 1.5f;
                    enemy.ApplyFreeze(enemy, duration);

                    if (!enemy.IsFreezeImmune)
                    {
                        enemy.ApplyEffect(Effect.AffectedStat.DEF, "ICEAGE_DEF_BUFF", 70f, duration, false);
                        enemy.ApplyEffect(Effect.AffectedStat.RES, "ICEAGE_RES_BUFF", 25f, duration, false);
                    }
                    break;

                case SkillName.ADAPTION:
                    enemy.mHealth += (enemy.bDef + enemy.bRes) / 2;
                    enemy.bDef = enemy.bRes = 0;
                    break;
            }
        }
    }

    public virtual void OnPlayerSpawn(PlayerBase player)
    {
        if (CharacterPrefabsStorage.DifficultyLevel == (int) DiffType.Observer)
        {
            player.mHealth += player.mHealth * 0.5f;
            player.bAtk = (short)(player.bAtk * 1.3f);
        }
    }

    public void ProcessPlayerSkillTree(PlayerBase player)
    {
        var Skills = player.Skills;
        foreach (SkillName skill in Skills)
        {
            switch (skill)
            {
                case SkillName.OBSCURE_VISION:
                    player.b_attackRange *= 0.8f;
                    break;
                case SkillName.HUNGER:
                    if (!CharacterPrefabsStorage.Skills.ContainsKey(SkillName.EQUIPMENT_PROVISIONS))
                    {
                        player.bAtk = (short)(player.bAtk * 1.5f);
                        player.ASPD += 30;
                        player.lifeSteal += 0.3f;
                    }
                    break;
                case SkillName.ABSOLUTISM:
                    if (player.attackPattern == EntityBase.AttackPattern.MELEE)
                    {
                        player.mHealth *= 2;
                        player.b_moveSpeed *= 1.35f;
                        player.weight += 2;
                    }
                    else if (player.attackPattern == EntityBase.AttackPattern.RANGED)
                    {
                        player.ASPD += player.b_attackRange * 0.15f;
                        player.b_attackRange = 15000f;
                    }
                    break;
                case SkillName.HEAT_DEATH:
                    player.mHealth *= 0.55f;
                    break;
            }
        }
    }

    #region Prefab Loading
    private IEnumerator LoadRequiredPrefabs()
    {
        // Load all player prefabs
        CharacterPrefabsStorage.PlayerPrefabs = new();
        int i = 0;
        foreach (var reference in prefabStorage.PlayerAssetReferences)
        {
            var handle = DataHandler.Instance.LoadAddressable<GameObject>(reference);
            yield return handle;
            CharacterPrefabsStorage.PlayerPrefabs[i] = handle.Result;
            i++;
        }

        // Load only required enemy prefabs
        CharacterPrefabsStorage.EnemyPrefabs = new();
        HashSet<int> uniqueIndices = new(); // prevent duplicate loads

        foreach (var code in appearingEnemies)
        {
            if (uniqueIndices.Add((int) code)) // only process unique ones
            {
                var reference = prefabStorage.EnemyAssetReferences[(int)code];
                var handle = DataHandler.Instance.LoadAddressable<GameObject>(reference);
                yield return handle;
                CharacterPrefabsStorage.EnemyPrefabs[(int)code] = handle.Result;
            }
        }

        IsStageReady = true;
        LoadingState.text = 
            (CharacterPrefabsStorage.DifficultyLevel > 1
            ? "<color=green><i>---Goodluck---</i></color>"
            : "<color=green><i>---Press any key to start---</i></color>");
    }
    #endregion

    string GetExplorationMode()
    {
        string result = "<color=white>Exploring as: </color>";
        int diff = CharacterPrefabsStorage.DifficultyLevel;
        if (diff <= 0) return result + "<color=#00ffbf>Observer</color>";
        if (diff == 1) return result + "<color=yellow>Explorer</color>";
        return result + $"<color=#ff4545>Researcher LV.{diff - 1}</color>";
    }

    #region Stage Procession
    IEnumerator TitleFadeOut()
    {
        BGM.Play();
        CanvasGroup canvasGroup = titleOverlay.GetComponent<CanvasGroup>();

        float c = 0, d = 1.25f, cJump = 0.02f;
        while (c < d)
        {
            canvasGroup.alpha = Mathf.Lerp(1, 0, c * 1.0f / d);
            c += cJump;
            yield return new WaitForSecondsRealtime(cJump);
        }

        canvasGroup.alpha = 0;
        Destroy(titleOverlay);

        Time.timeScale = 1f;
        if (IsFirstTimeStageEnter)
        {
            yield return StartCoroutine(mainCamera.MoveShowcases(ShowcaseSize, CameraShowcases, Waittimes));
            IsFirstTimeStageEnter = false;
        }
        else if (extraPlayerWaittime > 0) extraPlayerWaittime = 0.5f;

        foreach (var item in enemySpawnpoints)
        {
            item.OnStageStart(extraEnemyWaittime);
        }

        yield return new WaitForSeconds(extraPlayerWaittime);
        playerManager.enabled = true;
        yield return null;

        OnStageReady();
        IsStageStarted = true;
        StartCoroutine(CheckStageStatus());
        if (CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.TIME_DILATION))
        {
            StartCoroutine(TimeDilationCoroutine());
        }
    }

    bool IsTimeScaleZero => IsStagePaused || mainCamera.TriggerStopHit || playerManager.IsReadingSkillView || mapOverview.activeSelf || IsStageEndOverlayActive;
    public void UpdateTimeScale()
    {
        Time.timeScale =
            IsTimeScaleZero
            ? 0f
                : isSlowing
                ? timeScaleSlow
                    : 1f;
    }

    public virtual void Update()
    {
        if (!IsStageReady) return;

        UpdateTimeScale();

        if (!PressedAnyKey && !IsStageStarted && Input.anyKeyDown)
        {
            PressedAnyKey = true;
            StartCoroutine(TitleFadeOut());
        }

        if (!PressedAnyKey) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePauseStage();
        }

        if (!IsStageStarted) return;

        OnStageUpdate();

        if (Input.GetKeyDown(InputManager.Instance.SlowKey))
        {
            ToggleSlowmo();
        }
        else if (Input.GetKeyDown(InputManager.Instance.ViewMapKey))
        {
            ViewMap();
        }
    }

    void ToggleSlowmo()
    {
        isSlowing = !isSlowing;
        SlowImg.color = isSlowing ? Color.white : new(0.15f, 0.15f, 0.15f);
        UpdateTimeScale();
    }

    private void ViewMap()
    {
        if (mapOverview.activeSelf)
        {
            mapOverview.SetActive(false);
            mapCamera.SetActive(false);
        }
        else
        {
            mapOverview.SetActive(true);
            mapCamera.SetActive(true);
        }

        UpdateTimeScale();
    }

    protected virtual void OnStageReady() { }

    private short SearchCnt = 0;
    protected virtual void OnStageUpdate() 
    { 
        if (IsStagePaused) return;

        SearchCnt++;
        if (SearchCnt >= 10 && RemainingEnemiesGO.activeSelf)
        {
            UpdateEnemyCountUI();
            SearchCnt = 0;
        }
    }

    void UpdateEnemyCountUI()
    {
        int remainingEnemies = GetEnemyCount();
        RemainingEnemiesTxt.text = $"Enemies: {remainingEnemies}";
    }

    protected virtual IEnumerator CheckStageStatus()
    {
        while (IsStageStarted)
        {
            yield return new WaitForSeconds(2f);
            if (IsStageEnd) yield break;

            if (!playerManager.IsPlayerAlive)
            {
                IsStageStarted = false;
                OnStageEnd(ResultType.PLAYER_DEFEATED);
                FadeInResult();
                yield break; // Exit the coroutine if player is dead
            }

            if (StageCompleteConditionType == StageCompleteCondition.ELIMINATE_ALL_ENEMIES 
                && GetEnemyCount() <= 0)
            {
                IsStageStarted = false;
                OnStageEnd(playerManager.IsPlayerAlive ? ResultType.ENEMIES_DEFEATED : ResultType.PLAYER_DEFEATED);
                FadeInResult();
            }
        }
    }

    Coroutine HDCoroutine = null;
    public void CheckWinWithHeathDeath()
    {
        HDCoroutine = StartCoroutine(ShowOverlayFadeInToColorThenFadeOut(Color.white, 0.15f, 0.75f, 1f));
        
        UpdateEnemyCountUI();
        RemainingEnemiesGO.SetActive(true);

        if (GetEnemyCount(countInfiniteSpawns: true) > 0 
            || FindObjectsOfType<EnemyBase>().Any(e => e && e.IsAlive() && !e.IsInsignificant)) return;

        IsStageStarted = false;
        OnStageEnd(ResultType.HEATH_DEATH);
        FadeInResult();
    }

    IEnumerator ShowOverlayFadeInToColorThenFadeOut(Color toColor, float inDuration, float holdDuration, float outDuration)
    {
        Color clearColor = new Color(toColor.r, toColor.g, toColor.b, 0);
        Image screen = TopOverlay.GetComponentInChildren<Image>();
        screen.color = clearColor;

        TopOverlay.SetActive(true);
        float c = 0, d = inDuration, cJump = 0.02f;
        while (c < d)
        {
            screen.color = Color.Lerp(clearColor, toColor, c * 1.0f / d);
            c += cJump;
            yield return new WaitForSecondsRealtime(cJump);
        }

        screen.color = toColor;
        yield return new WaitForSecondsRealtime(holdDuration);

        c = 0;
        d = outDuration;
        while (c < d)
        {
            screen.color = Color.Lerp(toColor, clearColor, c * 1.0f / d);
            c += cJump;
            yield return new WaitForSecondsRealtime(cJump);
        }

        TopOverlay.SetActive(false);
        HDCoroutine = null;
    }

    public enum ResultType
    {
        ENEMIES_DEFEATED,
        PLAYER_DEFEATED,
        FUMO_RETRIEVED,
        FUMO_PROTECTED,
        FUMO_LOST,
        PLAYER_SURVIVED,
        HEATH_DEATH,
    }
    public virtual void OnStageEnd(ResultType resultType)
    {
        if (HDCoroutine == null && resultType == ResultType.ENEMIES_DEFEATED) 
            HDCoroutine = StartCoroutine(ShowOverlayFadeInToColorThenFadeOut(new Color(0, 1f, 0.63f, 0.8f), 0.5f, 0.5f, 1f));

        Cursor.visible = true;

        UpdateEnemyCountUI();

        PauseButton.gameObject.SetActive(false);
        IsStageEnd = true;
        FindObjectsOfType<EnemySpawnpointScript>().ToList().ForEach(e => e.enabled = false);

        ResultType[] VictoryConds = {
            ResultType.ENEMIES_DEFEATED,
            ResultType.FUMO_RETRIEVED,
            ResultType.FUMO_PROTECTED,
            ResultType.PLAYER_SURVIVED,
            ResultType.HEATH_DEATH
        };

        IsResultVictory = VictoryConds.Contains(resultType);

        TMP_Text text = pauseOverlay.GetComponentInChildren<TMP_Text>();
        text.text = resultType switch
        {
            ResultType.ENEMIES_DEFEATED => 
                CharacterPrefabsStorage.EnableChallengeMode
                    ? "<color=#ff3b3b>Challenge Completed!</color>"
                    : "<color=green>Enemies Eliminated!</color>",
            ResultType.FUMO_RETRIEVED => 
                CharacterPrefabsStorage.EnableChallengeMode
                    ? "<color=#ff3b3b>Challenge Completed!</color>"
                    : "<color=green>Fumo Retrieved!</color>",
            ResultType.PLAYER_SURVIVED =>
                    CharacterPrefabsStorage.EnableChallengeMode
                    ? "<color=#ff3b3b>Challenge Completed!</color>"
                    : "<color=green>Stage Completed!</color>",
            ResultType.HEATH_DEATH => "<color=yellow>T.E. reached!</color>",
            ResultType.FUMO_PROTECTED => 
                CharacterPrefabsStorage.EnableChallengeMode
                    ? "<color=#ff3b3b>Challenge Completed!</color>"
                    : "<color=green>Fumo Protected!</color>",
            ResultType.PLAYER_DEFEATED => "<color=red>Defeated!</color>",
            ResultType.FUMO_LOST => "<color=red>Fumo Stolen!</color>",
        };

        if (IsResultVictory) ProceedAsVictory(text, resultType);
    }

    void ProceedAsVictory(TMP_Text text, ResultType resultType)
    {
        foreach (var item in enemySpawnpoints)
        {
            Destroy(item);
        }

        SaveDataManager.SaveLevelCompletion(
            LevelName, 
            out bool IsFirsttime, 
            out SaveDataManager.CompletionType CompletionType,
            out bool IsDifficultyHigher);

        if (resultType != ResultType.HEATH_DEATH && CharacterPrefabsStorage.DifficultyLevel > 1)
        {
            text.text = "<color=#00ffb7>Research Conducted!</color>";
        }

        if (IsFirsttime) text.text += "\n<size=36>+1 Mint fumo</size>";

        if (resultType == ResultType.HEATH_DEATH)
        {
            text.text += "\n<color=#00ffb7><size=36>But, let's not do that again...</size></color>";
        }
        else
        {
            switch (CompletionType)
            {
                case SaveDataManager.CompletionType.OBSERVER_NORMAL:
                    if (IsFirsttime) text.text += "\n<size=36>Complete this stage in Explorer mode and above to unlock CM!</size>";
                    break;

                case SaveDataManager.CompletionType.NORMAL:
                    if (IsDifficultyHigher) text.text += "\n<size=36>Challenge mode has been unlocked!</size>";
                    else text.text += "\n<size=36>It's very hype.</size>";
                    break;

                case SaveDataManager.CompletionType.NORMAL_DIFF:
                    if (IsDifficultyHigher) text.text += "\n<size=36>It's a new rekky!</size>";
                    else text.text += "\n<size=36>Challenge mode has been unlocked!</size>";
                    break;

                case SaveDataManager.CompletionType.CHALLENGE_MODE:
                    text.text += "\n<size=36>It's just so peak...</size>";
                    break;

                case SaveDataManager.CompletionType.CHALLENGE_MODE_DIFF:
                    if (IsDifficultyHigher) text.text += "\n<size=36>Oh my fricking shit...</size>";
                    else text.text += "\n<size=36>It's just so peak...</size>";
                    break;
            }
        }

        if (IsFirsttime) AddFumo();

        o_QuitBtn.transform.localPosition = new Vector3(0, o_QuitBtn.transform.localPosition.y);
        o_RetryBtn.gameObject.SetActive(false);

        CharacterPrefabsStorage.EnableChallengeMode = false;
    }

    void AddFumo()
    {
        int fumoCount = PlayerPrefs.GetInt("Fumo", 0);
        int aFumoCount = PlayerPrefs.GetInt("AllTimeFumo", fumoCount);
        PlayerPrefs.SetInt("Fumo", fumoCount + 1);
        PlayerPrefs.SetInt("AllTimeFumo", aFumoCount + 1);
    }

    void FadeInResult()
    {
        StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        CanvasGroup canvasGroup = pauseOverlay.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0;
        pauseOverlay.SetActive(true);

        yield return new WaitUntil(() => HDCoroutine == null);
        float c = 0, d = 1.25f, cJump = 0.02f;
        while (c < d)
        {
            canvasGroup.alpha = Mathf.Lerp(0, 1, c * 1.0f / d);
            c += cJump;
            yield return new WaitForSecondsRealtime(cJump);
        }

        canvasGroup.alpha = 1;
        IsStageEndOverlayActive = true;
    }

    public void TogglePauseStage()
    {
        if (IsStageEnd) return;

        IsStagePaused = !IsStagePaused;
        pauseOverlay.SetActive(IsStagePaused);

        PauseButton.sprite = IsStagePaused ? PausedSprite : UnpausedSprite;

        Cursor.visible = IsStagePaused;

        UpdateTimeScale();
    }

    public virtual void RetryStage()
    {
        Time.timeScale = 1f;
        EnemySpawnpointScript.OnStageRetry();
        string currentSceneName = SceneManager.GetActiveScene().name;
        Addressables.LoadSceneAsync(currentSceneName, LoadSceneMode.Single, true);
        
        if (StageTransitionOverlay) Destroy(StageTransitionOverlay);
    }

    public virtual void QuitStage()
    {
        EnemySpawnpointScript.OnStageRetry();
        CharacterPrefabsStorage.EnemyPrefabs.Clear();
        CharacterPrefabsStorage.PlayerPrefabs.Clear();
        CharacterPrefabsStorage.EnableChallengeMode = false;

        if (IsResultVictory) CharacterPrefabsStorage.ClearBattleData();

        IsFirstTimeStageEnter = true;
        Time.timeScale = 1f;

        SceneManager.LoadSceneAsync(CharacterPrefabsStorage.LevelSelectionKey);
        // Addressables.LoadSceneAsync(CharacterPrefabsStorage.LevelSelectionKey, LoadSceneMode.Single, true);

        if (StageTransitionOverlay) Destroy(StageTransitionOverlay);
    }

    public void OnPlayerFumoPickup(PlayerBase player, Collider2D FumoObj)
    {
        if (StageCompleteConditionType != StageCompleteCondition.RETRIEVE_FUMO) return;

        playerManager.enabled = playerManager.activePlayer.enabled = FumoObj.enabled = false;

        player.SetInvulnerable(9999f);
        EntityManager.Enemies.ForEach(e => { if (e) e.InstaKill(); });
        IsStageStarted = false; 
        OnStageEnd(ResultType.FUMO_RETRIEVED);
        StartCoroutine(ZoomInFumo(FumoObj.gameObject));
    }

    public void OnPlayerFumoProtected(FumoScript FumoObj)
    {
        if (StageCompleteConditionType != StageCompleteCondition.PROTECT_FUMO 
            && StageCompleteConditionType != StageCompleteCondition.SURVIVE_FOR_GIVEN_TIME) return;

        playerManager.enabled = playerManager.activePlayer.enabled = FumoObj.enabled = false;

        EntityManager.Enemies.ForEach(e => { if (e) e.InstaKill(); });
        IsStageStarted = false;
        OnStageEnd(ResultType.FUMO_PROTECTED);
        StartCoroutine(ZoomInFumo(FumoObj.gameObject));
    }

    public void OnEnemyFumoPickup(EnemyBase enemy, Collider2D FumoObj)
    {
        if (StageCompleteConditionType != StageCompleteCondition.PROTECT_FUMO) return;

        playerManager.enabled = playerManager.activePlayer.enabled = FumoObj.enabled = false;

        IsStageStarted = false;
        Destroy(FumoObj.gameObject);
        OnStageEnd(ResultType.FUMO_LOST);
        FadeInResult();
    }

    IEnumerator ZoomInFumo(GameObject fumoObj)
    {
        var stageCompletedText = pauseOverlay.GetComponentInChildren<TMP_Text>();
        stageCompletedText.transform.localPosition = new Vector3(0, 360.0729f);

        o_QuitBtn.transform.localScale *= 0.8f;
        o_QuitBtn.transform.localPosition = new Vector3(0, -370);

        FumoScript fumoScript = fumoObj.GetComponent<FumoScript>();
        var fumo = fumoScript.Fumo;

        BGM.clip = fumoScript.f_WinBGM;
        BGM.loop = false;
        BGM.Play();

        Vector3 fumoCurrentPostition = fumoScript.OnFumoPickUp();
        RawImage fumoGlowImg = fumoObj.GetComponentInChildren<RawImage>(true);

        mainCamera.enabled = false;
        yield return new WaitForSecondsRealtime(1.5f);

        Transform fumoSpriteTransform = fumo.transform;
        Vector3
            fumoInitScale = fumo.transform.localScale,
            fumoTargetScale = new(5.5f, 5.5f);

        Vector3 fumoGlowImgScale = fumoGlowImg.transform.localScale,
                fumoGlowImgTargetScale = fumoGlowImgScale * 50f;

        float c = 0, d = 4.2f, cJump = 0.01f;
        float rotateTimer = d * 0.75f;
        float rotateDegreePerLoop = 360 * 3 / rotateTimer * cJump;

        // Get screen center for lerping to center
        Vector3 screenCenter = new Vector3(Screen.width / 2f, Screen.height / 2f, 0);

        while (c < d)
        {
            /*
            if (c < rotateTimer)
                fumoSpriteTransform.Rotate(0, rotateDegreePerLoop, 0);
            else
                fumoSpriteTransform.rotation = Quaternion.Euler(Vector3.zero);
            */

            // Lerp from current screen position to screen center
            Vector3 currentScreenPos = Vector3.Lerp(fumoCurrentPostition, screenCenter, c * 1.0f / d);
            fumoGlowImg.transform.position = fumo.transform.position = currentScreenPos;

            fumo.transform.localScale = Vector3.Lerp(fumoInitScale, fumoTargetScale, c * 1.0f / d);
            fumoGlowImg.transform.localScale = Vector3.Lerp(fumoGlowImgScale, fumoGlowImgTargetScale, c * 1.0f / d);

            c += cJump;
            yield return new WaitForSecondsRealtime(cJump);
        }

        fumo.transform.localPosition = Vector3.zero;
        fumo.transform.localScale = fumoTargetScale;
        fumoGlowImg.transform.localScale = fumoGlowImgTargetScale;

        fumoScript.FumoZoomInComplete();

        yield return new WaitForSecondsRealtime(2f);
        FadeInResult();
    }
    #endregion
}