using Assets.Scripts;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using Unity.Loading;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static EnemyBase;
using static PlayerManager;
using static SaveDataManager;
using static StageManager;

public class LevelSelectionScript : MonoBehaviour
{
    [SerializeField] private GameObject LevelPrefabTemplate, LevelsPoint, Overlay, LevelSelectionConfirm, Nav, LevelEnemyView, EnemyViewPrefab, EnemyViewContent;
    [SerializeField] private CharacterPrefabsStorage characterPrefabsStorage;
    [SerializeField] private Transform[] Containers;
    [SerializeField] private Sprite Incompleted, Completed, CompletedCM, CompletedCM_H_DF, CompletedCM_DF;

    [SerializeField] private Sprite NMSprite, CMSprite, LockedSprite;
    [SerializeField] private Button CMToggleButton;
    private Image CMToggleImg => CMToggleButton.GetComponent<Image>();

    [SerializeField] private TMP_Text SelectedLvlName, SelectedLvlDescription;
    [SerializeField] private string[] Descriptions, ChallengeModes, SecretDescriptions;
    [SerializeField] private StageCompleteCondition[] CompleteCondition;
    [SerializeField] private StageEnvironment[] Environments;
    [SerializeField] private AppearingEnemies[] AppearingEnemies;

    [SerializeField] private GameObject MapPreviewObj;
    [SerializeField] private Image MapPreviewImg, MapPreviewImgOverlay;
    [SerializeField] private Sprite[] Map_SSs;

    [SerializeField] private GameObject FetchingData_1, FetchingData_2;
    [SerializeField] private Sprite DefaultEnemyIcon;
    [SerializeField] private GameObject EnemyDetail, SelectedBorder;
    [SerializeField] private TMP_Text 
        EnemyName,
        EnemyPattern,
        EnemyDescription,
        Weight,
        Rating_HP, 
        Rating_DEF, 
        Rating_RES, 
        Rating_ATK, 
        Rating_ASPD, 
        Rating_ARNG, 
        Rating_MSPD, 
        Rating_DRNG,
        Rating_CALV;

    [SerializeField] private GameObject SP_Text;
    [SerializeField] private Image EnemyIcon;

    private bool IsLoadingEnemyPrefabs = false;
    private List<GameObject> LevelPrefabs = new();
    private int CurrentPageIndex = 0;
    private int MaxPageSize => Containers.Length;
    private int TotalLevels => characterPrefabsStorage.SceneAssetReferences.Length;
    private int TotalPages => Mathf.CeilToInt((float)TotalLevels / MaxPageSize);    

    private string selectedKey = null;
    private int selectedIndex = -1;
    private bool enableCM = false, isViewingMap = false;

    private List<bool> IsMapCleared = new();

    private List<GameObject> CreatedEnemyViewPrefabs = new();

    private List<EnemyCode> EncounteredEnemies = new();
    private bool IsEnemyEncoutered(EnemyCode enemyCode) => EncounteredEnemies.Contains(enemyCode);

    private AudioSource[] sfxs;

    [SerializeField] Image StartingPlayerIconImg, StartingPlayerGlowImg;
    [SerializeField] Sprite PlayerMeleIcon, PlayerRangedIcon;

    [SerializeField] TMP_Text FumoCount;

    public AudioSource BGMSource()
    {
        SetSfxs();
        return sfxs[0];
    }

    private void SetSfxs()
    {
        if (sfxs != null) return;
        sfxs = GetComponents<AudioSource>();
        sfxs[0].volume = PlayerPrefs.GetFloat("BGM", 1f);
        for (int i = 1; i < sfxs.Length; ++i)
        {
            sfxs[i].volume = PlayerPrefs.GetFloat("SFX", 1f);
        }
    }

    private void Start()
    {
        GlobalStageManager.ResumeNormalVariables();
        SaveDataManager.GetAllCompletedLevels();

        FindAnyObjectByType<SkillTree_Manager>(FindObjectsInactive.Include).GetPlayerProgress();

        SetSfxs();

        string[] encounteredEnemies = PlayerPrefs.GetString("EncounteredEnemies", string.Empty).Split(" ").ToArray();
        foreach (var enemy in encounteredEnemies)
        {
            if (Enum.TryParse(enemy, out EnemyCode code))
            {
                EncounteredEnemies.Add(code);
            }
        }

        AssignLevels();
        UpdateStartingPlayerIcon();

        StartCoroutine(OverlayFadeOut());

        FetchingData_1.SetActive(!SaveDataManager.IsResearchUnlocked);
        FetchingData_2.SetActive(SaveDataManager.IsResearchUnlocked);
    }

    short cnt = 60;
    private void Update()
    {
        if (cnt++ < 60) return;
        FumoCount.text = "x " + PlayerPrefs.GetInt("Fumo", 0).ToString();
        cnt = 0;
    }

    void AssignLevels()
    {
        foreach (var l in LevelPrefabs)
        {
            if (l != null) Destroy(l);
        }
        LevelPrefabs.Clear();
        Nav.SetActive(TotalPages > 1);
        if (Nav.activeSelf)
        {
            Nav.transform.Find("Next").gameObject.SetActive(CurrentPageIndex < TotalPages - 1);
            Nav.transform.Find("Prev").gameObject.SetActive(CurrentPageIndex > 0);
        }

        // Updated regex: now allows optional _CM and required _<difficulty> suffix
        var regex = new Regex(@"^FM-(\d+)(_CM)?_(-?\d+)$");

        int startLevelIndex = CurrentPageIndex * MaxPageSize;
        List<string> CompletedLevels = PlayerPrefs.GetString("CompletedLevels", string.Empty)
                                        .Split(" ", StringSplitOptions.RemoveEmptyEntries)
                                        .Where(p => regex.IsMatch(p))
                                        .ToList();

        int highestLevelCompleted = 0;
        if (CompletedLevels.Count > 0)
        {
            var numbers = CompletedLevels
                .Select(p => int.Parse(regex.Match(p).Groups[1].Value));
            highestLevelCompleted = numbers.Max() + 1;
        }

        for (int i = 0; i < MaxPageSize; ++i)
        {
            int levelIndex = startLevelIndex + i;
            if (levelIndex >= TotalLevels) break;
            bool levelUnlocked = levelIndex < highestLevelCompleted + 1;
            var targetLevel = characterPrefabsStorage.SceneAssetReferences[levelIndex];
            var runtimeKey = targetLevel.RuntimeKey.ToString();
            GameObject level = Instantiate(LevelPrefabTemplate, Containers[i].position, Quaternion.identity, LevelsPoint.transform);
            TMP_Text nameText = level.GetComponentInChildren<TMP_Text>();
            Image completionStatus = level.transform.Find("CompletionStatus").GetComponent<Image>();
            string displayName = GetLevelNameByIndex(levelIndex);

            nameText.text = displayName;

            // Use GetLevelCompletionType instead of raw string matching
            CompletionType completion = SaveDataManager.GetLevelCompletionType(displayName);

            completionStatus.sprite = completion switch
            {
                CompletionType.CHALLENGE_MODE => CompletedCM,
                CompletionType.CHALLENGE_MODE_DIFF => CompletedCM_DF,
                CompletionType.CHALLENGE_MODE_DIFF_HIGH => CompletedCM_H_DF,
                CompletionType.OBSERVER_NORMAL or CompletionType.NORMAL or CompletionType.NORMAL_DIFF => Completed,
                _ => Incompleted,
            };

            if (completion == CompletionType.CHALLENGE_MODE)
            {
                var (NM_Difficulty, CM_Difficulty) = GetLevelHighestDifficulty(displayName);
                if (NM_Difficulty > 1 || CM_Difficulty > 1) completionStatus.sprite = CompletedCM_DF;
            }

            IsMapCleared.Add(completion != CompletionType.UNCLEARED);

            string capturedKey = runtimeKey;
            Button levelButton = level.GetComponent<Button>();
            levelButton.onClick.AddListener(() => SelectLevel(levelIndex, capturedKey));
            levelButton.interactable = levelUnlocked;
            Transform Lock = level.transform.Find("Lock");
            Lock.gameObject.SetActive(!levelUnlocked);
            Image[] imgs = level.GetComponentsInChildren<Image>();
            foreach (var item in imgs)
            {
                if (item.transform == Lock || item.transform.parent == Lock) continue;
                item.color = levelUnlocked ? Color.white : new(0.18f, 0.18f, 0.18f);
            }
            LevelPrefabs.Add(level);
        }
    }

    string GetLevelNameByIndex(int levelIndex)
    {
        return "FM-" + (levelIndex < 10 ? $"0{levelIndex}" : levelIndex);
    }

    bool CanUnlockCM(int index) => IsMapCleared[index] && SaveDataManager.GetLevelCompletionType(GetLevelNameByIndex(index)) != CompletionType.OBSERVER_NORMAL;

    [SerializeField] LevelDifficultyModifier levelDifficultyModifier;
    void SelectLevel(int index, string runtimeKey)
    {
        if (isViewingMap) return;

        levelDifficultyModifier.SetRecordText(GetLevelNameByIndex(index));

        LevelDescriptionScrollbar.verticalNormalizedPosition = 1;

        sfxs[1].Play();

        selectedIndex = index;
        selectedKey = runtimeKey;
        SelectedLvlName.text = GetLevelNameByIndex(selectedIndex) + ": " + characterPrefabsStorage.LevelTitles[selectedIndex];
        SelectedLvlDescription.text = GetLevelDescription(selectedIndex);

        bool unlockCM = CanUnlockCM(index);
        if (!unlockCM) CMToggleImg.sprite = LockedSprite;

        MapPreviewImgOverlay.sprite = MapPreviewImg.sprite = Map_SSs[selectedIndex];

        StartCoroutine(ScaleLevelSelection(true));
    }

    public void SwapStartingPlayerType()
    {
        PlayerType current = CharacterPrefabsStorage.startingPlayer;
        if (current == PlayerType.MELEE) CharacterPrefabsStorage.startingPlayer = PlayerType.RANGED;
        else CharacterPrefabsStorage.startingPlayer = PlayerType.MELEE;

        UpdateStartingPlayerIcon();
    }

    private void UpdateStartingPlayerIcon()
    {
        PlayerType current = CharacterPrefabsStorage.startingPlayer;
        if (current == PlayerType.MELEE)
        {
            StartingPlayerIconImg.sprite = PlayerMeleIcon;
            StartingPlayerGlowImg.color = new(0, 0.48f, 1f);
        }
        else
        {
            StartingPlayerIconImg.sprite = PlayerRangedIcon;
            StartingPlayerGlowImg.color = new(0.95f, 0, 1f);
        }
    }

    string GetLevelDescription(int index)
    {
        string description = Descriptions[index];
        if (enableCM)
        {
            description = $"<size=30><color=red><b>Conditions:</size></b>\n{ChallengeModes[index]}</color>";
        }
        else
        {
            bool secret = SaveDataManager.GetLevelCompletionType(GetLevelNameByIndex(index)) == CompletionType.CHALLENGE_MODE_DIFF_HIGH;
            if (secret)
            {
                description = $"<color=#00FFD5>{SecretDescriptions[index]}</color>";
            }
            else
            {
                string stageCompleteCondition = CompleteCondition[index] switch
                {
                    StageCompleteCondition.ELIMINATE_ALL_ENEMIES => "<color=red><Annihilation></color> Eliminate all enemies to complete the stage.",
                    StageCompleteCondition.RETRIEVE_FUMO => "<color=#00ffb7><Rescue></color> Reach the location of the Fumo to complete the stage.",
                    StageCompleteCondition.SURVIVE_FOR_GIVEN_TIME => "<color=yellow><Survive></color> Survive until the time runs out to complete the stage.",
                    StageCompleteCondition.PROTECT_FUMO => "<color=yellow><Protect></color> Protect the Fumo from enemies until the time runs out to complete the stage.",
                    _ => "Unknown condition"
                };

                string environmentDescription = string.Empty;
                foreach (var env in Environments[index].Environments)
                {
                    string envDes = env switch
                    {
                        EnvironmentType.KEYS => "<color=purple><Key></color> Collect to remove the terrains with corresponding color.",
                        EnvironmentType.ONE_WAY_PASSAGE => "<color=#d6d930><One-directional Passage></color> Can only be passed through when appoarched from a certain direction.",
                        EnvironmentType.ORIGINIUM_TILE => "<color=#C40000><Originium Pollution></color> Continuously deals true damage to the player and enemy units standing on it.",
                        EnvironmentType.HEAT_PUMP_VENT => "<color=#ff9a03><Heatpump Vent></color> Periodically pushes the player and enemies within range toward a certain direction.",
                        EnvironmentType.MEDICAL_TILE => "<color=green><Medical Tile></color> Continuously heals the player and enemies standing on it.",
                        EnvironmentType.DARK_ZONE => "<color=black><Shrouded Zone></color> Some areas of the map is covered in darkness. Units standing on those areas have reduced attack and detection range, but can not be detected and targeted by units standing on brighter areas.",
                        _ => "Unknown environment"
                    };
                    environmentDescription += $"{envDes}\n";
                }

                description += $"\n\n<color=#E5E5E5>{stageCompleteCondition}\n{environmentDescription}</color>";
            }
        }
        return description.Replace(@"\n", "\n");
    }

    IEnumerator ScaleLevelSelection(bool toggleIn)
    {
        isViewingMap = true;

        Vector3 fullScale = Vector3.one, 
                hideScale = new(0.03f, 1, 1),
                fullPosition = Vector3.zero,
                hidePosition = new(0, -1000);

        Transform targetTransform = LevelSelectionConfirm.transform.Find("Body");

        float c, d;
        if (toggleIn)
        {
            LevelSelectionConfirm.SetActive(true);
            targetTransform.localScale = hideScale;
            targetTransform.localPosition = hidePosition;

            c = 0;
            d = 0.35f;
            while (c < d)
            {
                targetTransform.localPosition = Vector3.Lerp(hidePosition, fullPosition, c * 1.0f / d);

                c += Time.deltaTime;
                yield return null;
            }

            targetTransform.localPosition = fullPosition;
            yield return new WaitForSeconds(0.05f);

            c = 0;
            d = 0.3f;
            while (c < d)
            {
                targetTransform.localScale = Vector3.Lerp(hideScale, fullScale, c * 1.0f / d);

                c += Time.deltaTime;
                yield return null;
            }

            targetTransform.localScale = fullScale;
        }
        else
        {
            targetTransform.localScale = fullScale;
            targetTransform.localPosition = fullPosition;

            c = 0;
            d = 0.35f;
            while (c < d)
            {
                targetTransform.localScale = Vector3.Lerp(fullScale, hideScale, c * 1.0f / d);

                c += Time.deltaTime;
                yield return null;
            }

            targetTransform.localScale = hideScale;
            yield return new WaitForSeconds(0.05f);

            c = 0;
            d = 0.3f;
            while (c < d)
            {
                targetTransform.localPosition = Vector3.Lerp(fullPosition, hidePosition, c * 1.0f / d);

                c += Time.deltaTime;
                yield return null;
            }
            targetTransform.localPosition = hidePosition;

            LevelSelectionConfirm.SetActive(false); 
            CMToggleImg.sprite = NMSprite;
        }

        isViewingMap = false;
    }

    [SerializeField] private GameObject CMUnlockMessageBox;
    public void ToggleChallengeMode()
    {
        if (sfxs[2]) sfxs[2].Play();

        if (!CanUnlockCM(selectedIndex))
        {
            CMUnlockMessageBox.SetActive(true);
            return;
        }

        if (!enableCM)
        {
            enableCM = true;
            CMToggleImg.sprite = CMSprite;
        }
        else
        {
            enableCM = false;
            CMToggleImg.sprite = NMSprite;
        }

        SelectedLvlDescription.text = GetLevelDescription(selectedIndex);
        levelDifficultyModifier.AdjustMaxDiffOnCMSelect(enableCM);
    }

    IEnumerator ConfirmLevelSelection()
    {
        if (sfxs[2]) sfxs[2].Play();
        yield return StartCoroutine(OverlayFadeIn());

        CharacterPrefabsStorage.EnableChallengeMode = enableCM;
        Addressables.LoadSceneAsync(selectedKey, LoadSceneMode.Single, true);
    }

    public void ViewMap()
    {
        MapPreviewObj.SetActive(true);
        MapPreviewImgOverlay.sprite = MapPreviewImg.sprite = Map_SSs[selectedIndex];
    }

    public void ViewEnemy()
    {
        if (IsLoadingEnemyPrefabs) return;
        StartCoroutine(LoadEnemyPrefabs());
    }

    public void CloseEnemyView()
    {
        SelectedBorder.SetActive(false);
        LevelEnemyView.SetActive(false);
        foreach (var go in CreatedEnemyViewPrefabs)
        {
            Destroy(go);
        }
        CreatedEnemyViewPrefabs.Clear();
        ResetInformation();
    }

    void ResetInformation()
    {
        SetEnemyDefaultUI();
        EnemyDescription.text = 
            "Select an enemy to view their information.\nHover over a statline to see its description.";
    }

    void SetUnknownEnemyInfo()
    {
        SetEnemyDefaultUI();
        EnemyDescription.text = 
            "You haven't encountered this enemy yet.";
    }

    void SetEnemyDefaultUI()
    {
        EnemyIcon.sprite = DefaultEnemyIcon;
        Weight.text = Rating_CALV.text = Rating_DRNG.text = Rating_MSPD.text = Rating_ASPD.text =
            Rating_ARNG.text = Rating_DEF.text = Rating_RES.text = Rating_ATK.text =
            Rating_HP.text = EnemyPattern.text = EnemyName.text = "N/A";

        SP_Text.SetActive(false);
    }

    [SerializeField] ScrollRect LevelDescriptionScrollbar, EnemyInfoScrollbar; 
    
    readonly Dictionary<EnemyStatKey, EnemyBase> InstantiatedEnemyGOsForThisLevel = new();
    public IEnumerator GetEnemyInformation(EnemyCode enemyCode, Vector3 position)
    {
        EnemyInfoScrollbar.verticalNormalizedPosition = 1;

        sfxs[1].Play();

        SelectedBorder.transform.localPosition = position;
        SelectedBorder.SetActive(true);

        if (!EncounteredEnemies.Contains(enemyCode))
        {
            SetUnknownEnemyInfo();
            yield break;
        }

        if (IsLoadingEnemyPrefabs) yield return new WaitUntil(() => !IsLoadingEnemyPrefabs);

        bool hasStatsChanged = EnemyStatsLookup.HasStatsChange(enemyCode, selectedIndex);
        EnemyStatKey enemyStatKey = new() 
        { 
            Code = enemyCode, 
            LevelIndex = hasStatsChanged ? selectedIndex : -1, 
            hasChanged = EnemyStatsLookup.HasStatsChange(enemyCode, selectedIndex) 
        };

        EnemyBase enemy;

        if (InstantiatedEnemyGOsForThisLevel.ContainsKey(enemyStatKey))
        {
            enemy = InstantiatedEnemyGOsForThisLevel[enemyStatKey];
        }
        else
        {
            GameObject enemyGO = CharacterPrefabsStorage.EnemyPrefabs[(int)enemyCode];
            if (hasStatsChanged)
            {
                GameObject instantiate = GameObject.Instantiate(enemyGO);
                enemy = instantiate.GetComponent<EnemyBase>();
                EnemyStatsLookup.GetStats(enemy, selectedIndex, out bool hasChanged);
                enemyStatKey.hasChanged = hasChanged;
            }
            else
            {
                enemy = enemyGO.GetComponent<EnemyBase>();
            }
            
            enemy.InitializeComponents();
            InstantiatedEnemyGOsForThisLevel.Add(enemyStatKey, enemy);
        }

        EnemyIcon.sprite = enemy.Icon;

        EnemyName.text = enemy.Name;

        EnemyPattern.text = enemy.attackPattern == EntityBase.AttackPattern.NONE
            ? $"{enemy.attackPattern}"
            : $"{enemy.attackPattern} {enemy.damageType}";
        
        Weight.text = enemy.weight.ToString();

        SP_Text.SetActive(hasStatsChanged);

        // hp
        if (enemy.mHealth <= 30) 
            Rating_HP.text = "E";
        else if (enemy.mHealth <= 60)
            Rating_HP.text = "D";
        else if (enemy.mHealth <= 100)
            Rating_HP.text = "C";
        else if (enemy.mHealth <= 150)
            Rating_HP.text = "C+";
        else if (enemy.mHealth <= 200)
            Rating_HP.text = "B";
        else if (enemy.mHealth <= 260)
            Rating_HP.text = "B+";
        else if (enemy.mHealth <= 400)
            Rating_HP.text = "A";
        else if (enemy.mHealth <= 550)
            Rating_HP.text = "A+";
        else if (enemy.mHealth <= 1000)
            Rating_HP.text = "S";
        else
            Rating_HP.text = "S+";

        // atk
        if (enemy.atk <= 0) 
            Rating_ATK.text = "E";
        else if (enemy.atk <= 15)
            Rating_ATK.text = $"D";
        else if (enemy.atk <= 20)
            Rating_ATK.text = "C";
        else if (enemy.atk <= 25)
            Rating_ATK.text = "C+";
        else if (enemy.atk <= 40)
            Rating_ATK.text = "B";
        else if (enemy.atk <= 55)
            Rating_ATK.text = "B+";
        else if (enemy.atk <= 80)
            Rating_ATK.text = "A";
        else if (enemy.atk <= 110)
            Rating_ATK.text = "A+";
        else if (enemy.atk <= 160)
            Rating_ATK.text = "S";
        else
            Rating_ATK.text = "S+";

        // def
        if (enemy.bDef <= 0) 
            Rating_DEF.text = "E";
        else if (enemy.bDef <= 5)
            Rating_DEF.text = "D";
        else if (enemy.bDef <= 10)
            Rating_DEF.text = "C";
        else if (enemy.bDef <= 15)
            Rating_DEF.text = "C+";
        else if (enemy.bDef <= 25)
            Rating_DEF.text = "B";
        else if (enemy.bDef <= 45)
            Rating_DEF.text = "B+";
        else if (enemy.bDef <= 65)
            Rating_DEF.text = "A";
        else if (enemy.bDef <= 80)
            Rating_DEF.text = "A+";
        else if (enemy.bDef <= 100)
            Rating_DEF.text = "S";
        else
            Rating_DEF.text = "SS";

        // res
        if (enemy.bRes <= 0) 
            Rating_RES.text = "E";
        else if (enemy.bRes <= 5)
            Rating_RES.text = "D";
        else if (enemy.bRes <= 10)
            Rating_RES.text = "C";
        else if (enemy.bRes <= 15)
            Rating_RES.text = "C+";
        else if (enemy.bRes <= 30)
            Rating_RES.text = "B";
        else if (enemy.bRes <= 40)
            Rating_RES.text = "B+";
        else if (enemy.bRes <= 50)
            Rating_RES.text = "A";
        else if (enemy.bRes <= 70)
            Rating_RES.text = "A+";
        else if (enemy.bRes <= 85)
            Rating_RES.text = "S";
        else
            Rating_RES.text = "SS";

        // arng
        if (enemy.attackPattern == EntityBase.AttackPattern.NONE) Rating_ARNG.text = "E";
        else 
        {
            float arngValue = enemy.attackPattern == EntityBase.AttackPattern.RANGED ? enemy.b_attackRange : enemy.b_attackRange * 2.25f;
            if (arngValue <= 0)
                Rating_ARNG.text = "E";
            else if (arngValue <= 100f)
                Rating_ARNG.text = "D";
            else if (arngValue <= 200f)
                Rating_ARNG.text = "C";
            else if (arngValue <= 250f)
                Rating_ARNG.text = "C+";
            else if (arngValue <= 350f)
                Rating_ARNG.text = "B";
            else if (arngValue <= 450f)
                Rating_ARNG.text = "B+";
            else if (arngValue <= 600f)
                Rating_ARNG.text = "A";
            else if (arngValue <= 750f)
                Rating_ARNG.text = "A+";
            else if (arngValue <= 900f)
                Rating_ARNG.text = "S";
            else
                Rating_ARNG.text = "S+";
        }

        // aspd
        if (enemy.b_attackInterval <= 0)
            Rating_ASPD.text = "E";
        else if (enemy.b_attackInterval < 0.25f) 
            Rating_ASPD.text = "SS";
        else if (enemy.b_attackInterval <= 0.8f)
            Rating_ASPD.text = "S";
        else if (enemy.b_attackInterval <= 1f)
            Rating_ASPD.text = "A+";
        else if (enemy.b_attackInterval <= 1.5f)
            Rating_ASPD.text = "A";
        else if (enemy.b_attackInterval <= 2f)
            Rating_ASPD.text = "B+";
        else if (enemy.b_attackInterval <= 2.5f)
            Rating_ASPD.text = "B";
        else if (enemy.b_attackInterval <= 3.5f)
            Rating_ASPD.text = "C+";
        else if (enemy.b_attackInterval <= 5f)
            Rating_ASPD.text = "C";
        else if (enemy.b_attackInterval <= 7f)
            Rating_ASPD.text = "D";
        else
            Rating_ASPD.text = "E";

        // mspd
        if (enemy.moveSpeed <= 0) 
            Rating_MSPD.text = "E";
        else if (enemy.moveSpeed <= 50f)
            Rating_MSPD.text = "D";
        else if (enemy.moveSpeed <= 80f)
            Rating_MSPD.text = "C";
        else if (enemy.moveSpeed <= 110f)
            Rating_MSPD.text = "C+";
        else if (enemy.moveSpeed <= 150f)
            Rating_MSPD.text = "B";
        else if (enemy.moveSpeed <= 180f)
            Rating_MSPD.text = "B+";
        else if (enemy.moveSpeed <= 220f)
            Rating_MSPD.text = "A";
        else if (enemy.moveSpeed <= 270f)
            Rating_MSPD.text = "A+";
        else if (enemy.moveSpeed <= 330f)
            Rating_MSPD.text = "S";
        else
            Rating_MSPD.text = "S+";

        // drng
        if (enemy.detectionRange <= 0)
            Rating_DRNG.text = "E";
        else if (enemy.detectionRange <= 100f)
            Rating_DRNG.text = "D";
        else if (enemy.detectionRange <= 200f)
            Rating_DRNG.text = "C";
        else if (enemy.detectionRange <= 250f)
            Rating_DRNG.text = "C+";
        else if (enemy.detectionRange <= 350f)
            Rating_DRNG.text = "B";
        else if (enemy.detectionRange <= 450f)
            Rating_DRNG.text = "B+";
        else if (enemy.detectionRange <= 600f)
            Rating_DRNG.text = "A";
        else if (enemy.detectionRange <= 750f)
            Rating_DRNG.text = "A+";
        else if (enemy.detectionRange <= 900f)
            Rating_DRNG.text = "S";
        else
            Rating_DRNG.text = "S+";

        // calv
        float calvValue = enemy.DangerRange_RatioOfAttackRange;
        if (enemy.attackPattern == EntityBase.AttackPattern.MELEE) calvValue = 1 - calvValue;

        if (calvValue <= 0.15f)
            Rating_CALV.text = "Lo";
        else if (calvValue <= 0.8f)
            Rating_CALV.text = "Med";
        else
            Rating_CALV.text = "Hi";

        EnemyDescription.text = 
            $"<color=#b1b1b1><i>{enemy.Description}</i></color>\n\n" +
            $"<color=#E5E5E5>{enemy.Skillset}</color>";
    }

    private IEnumerator LoadEnemyPrefabs()
    {
        IsLoadingEnemyPrefabs = true;

        HashSet<int> uniqueIndices = new(); // prevent duplicate loads
        EnemyCode[] appearingEnemies = AppearingEnemies[selectedIndex].Enemies;

        Vector3 InitialPosition = new(75, -75);

        Vector3 CurrentPosition = InitialPosition;
        float X_Offset = 160f, Y_Offset = -160;
        short RowDisplayCount = 1;
        const short MaxDisplayPerRow = 4;

        foreach (var code in appearingEnemies)
        {
            if (code == EnemyCode.DUMMY) continue;

            GameObject btnEnemyViewGO = Instantiate(EnemyViewPrefab, CurrentPosition, Quaternion.identity, EnemyViewContent.transform);
            btnEnemyViewGO.transform.localPosition = CurrentPosition;

            Vector3 position = btnEnemyViewGO.transform.localPosition;
            Button e = btnEnemyViewGO.GetComponent<Button>();
            e.onClick.AddListener(() => StartCoroutine(GetEnemyInformation(code, position)));
            
            CreatedEnemyViewPrefabs.Add(btnEnemyViewGO);

            RowDisplayCount++;
            CurrentPosition = new(CurrentPosition.x + X_Offset, CurrentPosition.y);

            if (RowDisplayCount > MaxDisplayPerRow)
            {
                CurrentPosition = new(InitialPosition.x, CurrentPosition.y + Y_Offset);
                RowDisplayCount = 1;
            }

            Image enemyImage = e.GetComponent<Image>();
            if (CharacterPrefabsStorage.EnemyPrefabs.ContainsKey((int)code))
            {
                enemyImage.sprite = 
                    IsEnemyEncoutered(code)
                        ? CharacterPrefabsStorage.EnemyPrefabs[(int)code].GetComponent<EnemyBase>().Icon
                        : DefaultEnemyIcon;
                continue;
            }

            if (uniqueIndices.Add((int)code)) // only process unique ones
            {
                var reference = characterPrefabsStorage.EnemyAssetReferences[(int)code];
                var handle = DataHandler.Instance.LoadAddressable<GameObject>(reference);
                yield return handle;
                CharacterPrefabsStorage.EnemyPrefabs[(int)code] = handle.Result;
                enemyImage.sprite = IsEnemyEncoutered(code) 
                    ? handle.Result.GetComponent<EnemyBase>().Icon
                    : DefaultEnemyIcon;
            }
        }

        IsLoadingEnemyPrefabs = false;
        LevelEnemyView.SetActive(true);
    }

    IEnumerator OverlayFadeIn()
    {
        Image image = Overlay.GetComponentInChildren<Image>();
        Overlay.SetActive(true);
        float c = 0, d = 1;
        while (c < d)
        {
            image.color = Color.Lerp(Color.clear, Color.black, c * 1.0f / d);
            c += Time.deltaTime;
            yield return null;
        }

        image.color = Color.black;
        DontDestroyOnLoad(Overlay);
    }

    IEnumerator OverlayFadeOut()
    {
        Image image = Overlay.GetComponentInChildren<Image>();
        Overlay.SetActive(true);
        float c = 0, d = 0.5f;
        while (c < d)
        {
            image.color = Color.Lerp(Color.black, Color.clear, c * 1.0f / d);
            c += Time.deltaTime;
            yield return null;
        }

        image.color = Color.black;
        Overlay.SetActive(false);
    }

    public void NextPage()
    {
        if (CurrentPageIndex < TotalPages - 1)
        {
            CurrentPageIndex++;
            AssignLevels();
        }
    }

    public void PrevPage()
    {
        if (CurrentPageIndex > 0)
        {
            CurrentPageIndex--;
            AssignLevels();
        }
    }

    public void Deselect()
    {
        if (isViewingMap) return;

        enableCM = false;
        levelDifficultyModifier.AdjustMaxDiffOnCMSelect(enableCM);
        StartCoroutine(ScaleLevelSelection(false));
    }

    public void Confirm() => StartCoroutine(ConfirmLevelSelection());

    public void Quit()
    {
        CharacterPrefabsStorage.ClearPrebattleData();
        SceneManager.LoadScene("MainMenu");
    }
}

[Serializable] public class StageEnvironment 
{ 
    public EnvironmentType[] Environments;
}

[Serializable] public class AppearingEnemies
{
    public EnemyBase.EnemyCode[] Enemies;
}

public class EnemyStatKey
{
    public EnemyCode Code;

    // if the stats of the enemy changes in this level,
    // use the level index as part of the key to differentiate it from the default stats;
    // otherwise, set it to -1 to save the trouble of maintaining the keys for levels without stat changes
    public int LevelIndex;
    public bool hasChanged;

    public override bool Equals(object obj)
    {
        if (obj is not EnemyStatKey other) return false;
        if (Code != other.Code) return false;

        return
            (hasChanged == false && other.hasChanged == false) || (LevelIndex == other.LevelIndex);
    }

    public override int GetHashCode()
    {
        if (!hasChanged) return HashCode.Combine(Code, false);
        return HashCode.Combine(Code, LevelIndex, true);
    }
}