using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using static Unity.Collections.AllocatorManager;
using Button = UnityEngine.UI.Button;
using Image = UnityEngine.UI.Image;

public class SkillTree_Manager : MonoBehaviour
{
    [SerializeField] Button TechViewBtn;
    [SerializeField] GameObject Block, SkillHighlight;

    [SerializeField] GameObject SENSES, TECHS, SPECS;
    [SerializeField] GameObject Borders;

    [SerializeField] GameObject SENSES_BLOCK, TECHS_BLOCK, SPECS_BLOCK, TECHS_PRECEDE_BLOCK, SPECS_PRECEDE_BLOCK, SIDEBAR, OVERLAY;
    [SerializeField] Button SensesUnlockBtn, TechsUnlockBtn, SpecsUnlockBtn;
    [SerializeField] TMP_Text SensesUnlockTxt, TechsUnlockTxt, SpecsUnlockTxt;
    [SerializeField] short FUMO_COST_SENSE = 3, FUMO_COST_TECHS = 3, FUMO_COST_SPECS = 3;
    [SerializeField] TMP_Text FumoCnt, SelectedCnt, FlavourTxt, Title;

    [SerializeField] string[] FlavourTxts;

    private AudioSource[] audioSources;
    LevelSelectionScript LevelSelectionScript;

    SkillTree_Outview Outview;
    [SerializeField] ScrollRect skillViewScroll;
    [SerializeField] Image skillViewScrollbarImg;

    private short PlayerMaxSkills = 0;

    public enum SkillType
    {
        INVENTIO,
        ARS,
        THEORIA,
    }

    public enum SkillName
    {
        NONE,
        WINGED_STEPS_A,
        WINGED_STEPS_B,
        WINGED_STEPS_C,
        GEOLOGIST_OBSERVE,
        GEOLOGIST_STUDY,
        GEOLOGIST_EXPLORE,
        EQUIPMENT_BLADE,
        EQUIPMENT_SCOPE,
        EQUIPMENT_RADIO,
        EQUIPMENT_PROVISIONS,
        DASH_TOUCH,
        DASH_LETHAL,
        DASH_FAITH,
        WINDBLOW_NORTH,
        WINDBLOW_SOUTH,
        FREEZE_BLOOM,
        FREEZE_CHARGE,
        FREEZE_SUPERCONDUCT,
        OBSCURE_VISION,
        GRAVITY,
        JUGGERNAUNT_DURATION,
        JUGGERNAUNT_IGNITE,
        JUGGERNAUNT_PULL,
        JUGGERNAUNT_AFTERSHOCK,
        VICTORY_ATK,
        VICTORY_REFRESH,
        SPIRAL_MORE,
        SPIRAL_TRAVEL,
        SPIRAL_SHADOW,
        BLACKFLASH,
        MINT_PHALANX,
        MINT_WINDRUSH,
        ATTENTION_BOOK,
        ATTENTION_DEVICE,
        SWAP_START_SPECIAL,
        DASH_AFTERIMAGES,
        SWAP_START_ATK,
        SPECIAL_MSPD,
        SPIRAL_PHANTOM,
        HEAVY_HITTER,
        JUGGERNAUNT_SHINDOUKAKU,
        BREAK_THE_ICE,
        HUNGER,
        TERRAIN,
        AMULET,
        BEYOND_NIGHT,
        SPIRAL_FIELD_EXPERT,
        TIME_DILATION,
        BUBBLE_ARTS,
        HAIR_RIBBON,
        SPIRAL_READ,
        WIND_ANTHEM,
        FREEZE_HOLD,
        ACCELERATION,
        ABSOLUTISM,
        STATIS,
        HEAT_DEATH,
        CERTAIN_FATES,
        HIBERNATE,
        KNOTS,
        A_NICE_LOOKING_ROCK,
        ADAPTION,
        SMOKE_TOXIC,
        SMOKE_BLIND,
        SWAP_START_MSPD,
        ULTIMATE_BUFF,
    }

    public static SkillTree_Manager Instance;
    [SerializeField] private GameObject TickOverlay;

    [SerializeField] private Image skillIconImage;
    Sprite defaultSkillIcon;

    [SerializeField] private TMP_Text skillNameText, skillDetailsText;
    string defaultSkillName, defaultSkillDetailsText;

    [SerializeField] private GameObject skillDetailsPanel;

    [HideInInspector] public List<SkillTree_SkillComponent> allSkills;
    private SkillTree_SkillComponent selectingSkill;
    private HashSet<SkillName> exclusions = new();

    Image SkillViewPanelImg;
    [SerializeField] Image[] TechImgs;

    [SerializeField] Button SelectButton, OkButton, ClearBtn;
    [SerializeField] private short MaxSkillCount = 2;

    [SerializeField] Color
          IdleColor = new(0.35f, 0.35f, 0.35f),
          SpecsSkillViewPanelColor = new(0.57f, 0.51f, 0),
          SensesSkillViewPanelColor = new(0, 0.59f, 0.65f),
          TechsSkillViewPanelColor = new(0.57f, 0, 0.8f);

    [SerializeField] private string[] SecretKeys = {
        "ANGOUNOWALTZ",
        "NOODLES",
    };

    AudioSource BGM;

    public void GetPlayerProgress()
    {
        bool techUnlocked = SaveDataManager.IsEligibleForTechUnlock();

        TechViewBtn.interactable = techUnlocked;
        Block.SetActive(!techUnlocked);
    }

    LevelDifficultyModifier levelDifficultyModifier;

    public void CheckUnlockStatus()
    {
        if (!levelDifficultyModifier) levelDifficultyModifier = FindFirstObjectByType<LevelDifficultyModifier>();
        levelDifficultyModifier.GetDifficulties();

        bool IsSensesUnlocked = PlayerPrefs.GetInt("SensesUnlocked", 0) != 0,
             IsTechUnlocked = PlayerPrefs.GetInt("TechsUnlocked", 0) != 0,
             IsSpecsUnlocked = PlayerPrefs.GetInt("SpecsUnlocked", 0) != 0;

        PlayerMaxSkills = GetPlayerMaxSkills(IsSensesUnlocked, IsTechUnlocked, IsSpecsUnlocked);

        SetBlocksActiveState(IsSensesUnlocked, IsTechUnlocked, IsSpecsUnlocked);

        SetUIMaxSkill();
    }

    short GetPlayerMaxSkills(bool IsSensesUnlocked, bool IsTechUnlocked, bool IsSpecsUnlocked)
    {
        short playerMaxSkills = 0;
        if (IsSensesUnlocked) playerMaxSkills += 2;
        if (IsTechUnlocked) playerMaxSkills += 2;

        return playerMaxSkills;
    }

    void SetBlocksActiveState(bool IsSensesUnlocked, bool IsTechUnlocked, bool IsSpecsUnlocked)
    {
        SENSES_BLOCK.SetActive(!IsSensesUnlocked);
        TECHS_PRECEDE_BLOCK.SetActive(!IsSensesUnlocked);

        TECHS_BLOCK.SetActive(!IsTechUnlocked && !TECHS_PRECEDE_BLOCK.activeSelf);
        SPECS_PRECEDE_BLOCK.SetActive(!IsTechUnlocked);

        SPECS_BLOCK.SetActive(!IsSpecsUnlocked && !SPECS_PRECEDE_BLOCK.activeSelf);

        int fumo = PlayerPrefs.GetInt("Fumo", 0);
        SensesUnlockBtn.interactable = fumo >= FUMO_COST_SENSE;
        TechsUnlockBtn.interactable = fumo >= FUMO_COST_TECHS;
        SpecsUnlockBtn.interactable = fumo >= FUMO_COST_SPECS;

        FumoCnt.text = "x " + fumo;
        SensesUnlockTxt.text = $"Spend       x{FUMO_COST_SENSE} to unlock this branch \n(+2 slots).";
        TechsUnlockTxt.text = $"Spend       x{FUMO_COST_TECHS} to unlock this branch \n(+2 slot).";
        SpecsUnlockTxt.text = $"Spend          x{FUMO_COST_SPECS} to unlock this branch.";
    }

    void SetUIMaxSkill()
    {
        int maxSkill = Mathf.Min(PlayerMaxSkills, MaxSkillCount);
        for (int i = 0; i < maxSkill; i++)
        {
            Image techImg = TechImgs[i];
            if (i + 1 > maxSkill)
            {
                techImg.color = new(0.35f, 0.35f, 0.35f);
                techImg.GetComponentsInChildren<Image>(true)[1].gameObject.SetActive(true);
            }
            else
            {
                techImg.color = Color.white;
                techImg.GetComponentsInChildren<Image>(true)[1].gameObject.SetActive(false);
            }
        }
    }

    public void UnlockSense()
    {
        int fumo = PlayerPrefs.GetInt("Fumo", 0);
        if (fumo < FUMO_COST_SENSE) return;

        fumo -= FUMO_COST_SENSE;
        PlayerPrefs.SetInt("Fumo", fumo);
        PlayerPrefs.SetInt("SensesUnlocked", 1);
        PlayerPrefs.Save();

        StartCoroutine(RemoveSeals(SENSES_BLOCK));
    }

    public void UnlockTechs()
    {
        int fumo = PlayerPrefs.GetInt("Fumo", 0);
        if (fumo < FUMO_COST_TECHS) return;

        fumo -= FUMO_COST_TECHS;
        PlayerPrefs.SetInt("Fumo", fumo);
        PlayerPrefs.SetInt("TechsUnlocked", 1);
        PlayerPrefs.Save();

        StartCoroutine(RemoveSeals(TECHS_BLOCK));
    }

    [SerializeField] GameObject MessageBox;
    public void UnlockSpecs()
    {
        int fumo = PlayerPrefs.GetInt("Fumo", 0);
        if (fumo < FUMO_COST_SPECS) return;

        fumo -= FUMO_COST_SPECS;
        PlayerPrefs.SetInt("Fumo", fumo);
        PlayerPrefs.SetInt("SpecsUnlocked", 1);
        PlayerPrefs.Save();

        StartCoroutine(RemoveSeals(SPECS_BLOCK));
    }

    IEnumerator RemoveSeals(GameObject BlockObject)
    {
        yield return new WaitForSeconds(0.5f);

        float duration = 1.5f;
        float elapsedTime = 0f;
        List<Image> seals = BlockObject.GetComponentsInChildren<Image>().Where(i => i.type == Image.Type.Filled).ToList();
        if (seals.Count > 0)
        {
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / duration);
                seals.ForEach(img => img.fillAmount = Mathf.Lerp(1f, 0f, t));
                yield return null;
            }

            seals.ForEach(img => img.fillAmount = 0f);
            yield return new WaitForSeconds(1f);
        }

        Image image = BlockObject.GetComponent<Image>();
        Color color = image.color, end = Color.white;
        end.a = color.a;
        duration = 0.25f;
        elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
            image.color = Color.Lerp(color, end, t);
            yield return null;
        }
        image.color = end;

        yield return new WaitForSeconds(0.2f);

        CanvasGroup cg = BlockObject.GetComponent<CanvasGroup>();
        elapsedTime = 0f;
        duration = 1f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);
            cg.alpha = Mathf.Lerp(1f, 0f, t);
            yield return null;
        }

        cg.alpha = 0f;
        BlockObject.SetActive(false);

        if (BlockObject == SPECS_BLOCK)
        {
            GameObject o = Instantiate(MessageBox);
            PopupMessageBox popupMessageBox = o.GetComponent<PopupMessageBox>();
            popupMessageBox.SetMessage("Exploration mode <color=yellow>Researcher</color> is now available!");
            popupMessageBox.Display(5);
        }

        CheckUnlockStatus();
        Clear();
    }

    public static bool ShowIntro = true;
    public bool IsIntroPlaying;
    [SerializeField] private GameObject QuitBtn;
    IEnumerator Intro()
    {
        if (!ShowIntro) yield break;

        //preps
        OVERLAY.SetActive(true);
        CircularMaskMover circularMaskMover = OVERLAY.GetComponent<CircularMaskMover>();
        circularMaskMover.radius = 0f;

        IsIntroPlaying = true;

        int introCount = PlayerPrefs.GetInt("SkillTreeIntroCount", 0);
        introCount++;

        PlayerPrefs.SetInt("SkillTreeIntroCount", introCount);
        PlayerPrefs.Save();

        QuitBtn.SetActive(false);

        List<GameObject> techBlocks = new()
        {
            SENSES,
            TECHS,
            SPECS,
            SENSES_BLOCK,
            TECHS_BLOCK,
            SPECS_BLOCK,
            TECHS_PRECEDE_BLOCK,
            SPECS_PRECEDE_BLOCK,
            SIDEBAR,
            Title.gameObject,
        };

        List<Image> borderImages = Borders.GetComponentsInChildren<Image>().ToList();
        borderImages.ForEach(img =>
        {
            img.fillAmount = 0;
        });

        float yFloat = 500f;
        HashSet<SkillTree_SkillComponent> compos = allSkills.OrderBy(a => a.skillType).ToHashSet();
        foreach (var block in techBlocks)
        {
            CanvasGroup cg = block.GetComponent<CanvasGroup>();
            Vector3 originalScale = block.transform.localScale;

            cg.alpha = 0;
        }

        string TitleOriginal = FlavourTxt.text;
        if (introCount >= 10)
        {
            TitleOriginal = FlavourTxts[Random.Range(0, FlavourTxts.Length)];
        }

        FlavourTxt.text = "";

        //intro sequence
        yield return new WaitForSeconds(0.6f);

        float overlayDurationFirstHalf = 2f,
              overlayDurationSecondHalf = 0.25f,
              firstTimeCoverRatio = 0.35f,
              pause = 0.5f,
              elapsed = 0f;
        while (elapsed < overlayDurationFirstHalf)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / overlayDurationFirstHalf);
            circularMaskMover.radius = Mathf.Lerp(0f, firstTimeCoverRatio, t);
            yield return null;
        }
        circularMaskMover.radius = firstTimeCoverRatio;

        yield return new WaitForSeconds(pause);
        elapsed = 0f;
        while (elapsed < overlayDurationSecondHalf)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / overlayDurationSecondHalf);
            circularMaskMover.radius = Mathf.Lerp(firstTimeCoverRatio, 1f, t);
            yield return null;
        }

        circularMaskMover.radius = 1f;
        OVERLAY.SetActive(false);

        yield return StartCoroutine(ZoomAndFadeIn(Title.gameObject, 1.5f));

        float waitTime = 0.7f / TitleOriginal.Length;
        foreach (char c in TitleOriginal)
        {
            FlavourTxt.text += c;
            yield return new WaitForSeconds(waitTime);
        }

        yield return new WaitUntil(() => allSkills.All(s => s.Button != null));
        yield return new WaitForSeconds(0.5f);

        Vector3 targetPos = new Vector3(0, yFloat, 0);

        foreach (var skill in compos)
        {
            skill.transform.localPosition += targetPos;
            skill.Button.interactable = false;
        }

        foreach (var block in techBlocks)
        {
            if (block == SIDEBAR || block == Title.gameObject) continue;
            StartCoroutine(ZoomAndFadeIn(block));
            yield return new WaitForSeconds(0.25f);
        }

        yield return new WaitForSeconds(0.3f);

        float delay = 0f;
        foreach (var skill in compos)
        {
            StartCoroutine(Drop(skill, yFloat, delay));
            delay += 0.025f;
        }

        borderImages.ForEach(img =>
        {
            StartCoroutine(FillImage(img));
        });

        yield return new WaitForSeconds(0.75f + delay);

        FindObjectsOfType<UIMututallyExclusive>(true).ToList().ForEach(me =>
        {
            me.DoIntro();
        });

        yield return new WaitForSeconds(2f);

        StartCoroutine(ZoomAndFadeIn(SIDEBAR, 0.4f, 0));
        yield return new WaitForSeconds(0.25f);

        foreach (var skill in compos)
        {
            skill.Button.interactable = true;
        }

        ShowIntro = false;
        IsIntroPlaying = false;
        QuitBtn.SetActive(true);
    }

    IEnumerator FillImage(Image image)
    {
        if (image.fillMethod == Image.FillMethod.Radial360)
        {
            image.fillAmount = 1;
            yield break;
        }

        float duration = 0.8f;
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            image.fillAmount = Mathf.Lerp(0, 1, elapsedTime / duration);
            yield return null;
        }
        image.fillAmount = 1;
    }

    IEnumerator ZoomAndFadeIn(GameObject techBlock, float duration = 1.2f, float initDelay = 1f)
    {
        CanvasGroup cg = techBlock.GetComponent<CanvasGroup>();
        Vector3 originalScale = techBlock.transform.localScale;

        cg.alpha = 0;
        if (techBlock != SIDEBAR) techBlock.transform.localScale = originalScale * 1.25f;

        yield return new WaitForSeconds(initDelay);

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            cg.alpha = Mathf.Lerp(0, 1, elapsedTime / duration);
            techBlock.transform.localScale = Vector3.Lerp(techBlock.transform.localScale, originalScale, elapsedTime / duration);
            yield return null;
        }

        cg.alpha = 1;
        techBlock.transform.localScale = originalScale;
    }

    IEnumerator Drop(SkillTree_SkillComponent skill, float yFloat, float delay)
    {
        Vector3 targetPos = new Vector3(skill.transform.localPosition.x, skill.transform.localPosition.y - yFloat, skill.transform.position.z);
        yield return new WaitForSeconds(0.75f + delay);

        float duration = 0.5f;
        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            skill.transform.localPosition = Vector3.Lerp(skill.transform.localPosition, targetPos, elapsedTime / duration);
            yield return null;
        }
        skill.transform.localPosition = targetPos;
    }

    Dictionary<SkillName, SkillDataSet> skillSaves = new();
    private void OnEnable()
    {
        skillSaves = new(CharacterPrefabsStorage.Skills);
        LevelSelectionScript.BGMSource().Stop();
        BGM.Play();

        var scrollviews = GetComponentsInChildren<ScrollRect>(true);
        foreach (var item in scrollviews)
        {
            if (item.verticalScrollbar) item.verticalScrollbar.value = 0;
            if (item.horizontalScrollbar) item.horizontalScrollbar.value = 0;
        }
        CheckUnlockStatus();

        if (ShowIntro)
            StartCoroutine(Intro());
        else
            Outview.PlayQuickFadeOut();
    }

    private void OnDisable()
    {
        ClearSelectingSkill();
        Outview.SetSkills();

        BGM.Stop();
        LevelSelectionScript.BGMSource().Play();

        var scrollviews = GetComponentsInChildren<ScrollRect>(true);
        foreach (var item in scrollviews)
        {
            if (item.verticalScrollbar) item.verticalScrollbar.value = 0;
            if (item.horizontalScrollbar) item.horizontalScrollbar.value = 0;
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            Outview = FindObjectOfType<SkillTree_Outview>(true);

            allSkills = FindObjectsOfType<SkillTree_SkillComponent>(true).ToList();
            allSkills.ForEach(skill =>
            {
                Instantiate(TickOverlay, skill.transform.position, Quaternion.identity, skill.transform);
                skill.OnTickOverlayCreated();
            });

            SkillViewPanelImg = skillDetailsPanel.GetComponent<Image>();
            defaultSkillIcon = skillIconImage.sprite;
            defaultSkillName = skillNameText.text;
            defaultSkillDetailsText = skillDetailsText.text;
            SelectedCnt.text = $"<color=#a1a1a1>Selected: {CharacterPrefabsStorage.Skills.Count}/{MaxSkillCount}</color>";

            for (int i = 0; i < TechImgs.Length; i++)
            {
                TechImgs[i].gameObject.SetActive(MaxSkillCount >= (i + 1));
            }

            audioSources = GetComponents<AudioSource>();

            audioSources[0].volume = PlayerPrefs.GetFloat("BGM", 1f);
            BGM = audioSources[0];

            for (int i = 1; i < audioSources.Length; ++i)
            {
                audioSources[i].volume = PlayerPrefs.GetFloat("SFX", 1f);
            }

            LevelSelectionScript = FindObjectOfType<LevelSelectionScript>(true);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        OnSceneLoad_GetTechs();
    }

    private void Update()
    {
        if (selectingSkill && SkillHighlight.activeSelf)
            SkillHighlight.transform.position = selectingSkill.transform.position;

        OkButton.interactable = IsSkillsEqual(skillSaves, CharacterPrefabsStorage.Skills) == false && !IsIntroPlaying;
        ClearBtn.interactable = CharacterPrefabsStorage.Skills.Count > 0 && !IsIntroPlaying;
        SelectButton.interactable = selectingSkill && CharacterPrefabsStorage.Skills.Count < MaxSkillCount;

        EnterSecretCode();
    }

    string PlayerInputStr = string.Empty;
    void EnterSecretCode()
    {
        if (PlayerPrefs.GetInt("SecretCodeRedeemed", 0) != 0) return;

        foreach (char c in Input.inputString)
        {
            if (!char.IsLetter(c)) continue;

            PlayerInputStr += char.ToUpper(c);

            if (SecretKeys.Any(s => PlayerInputStr.Contains(s)))
            {
                int fumo = PlayerPrefs.GetInt("Fumo", 0);
                fumo += 10;
                PlayerPrefs.SetInt("Fumo", fumo);
                PlayerPrefs.SetInt("SecretCodeRedeemed", 1);
                PlayerPrefs.Save();

                CheckUnlockStatus();
                Clear();
            }
        }
    }

    public void OnSkillSelected(SkillTree_SkillComponent skill)
    {
        if (IsIntroPlaying) return;

        audioSources[1].Play();
        allSkills.ForEach(s => { if (!exclusions.Contains(s.skillName)) s.OnSkillClear(); });

        if (selectingSkill == skill)
        {
            selectingSkill = null;
            DeselectSkill(skill);
            return;
        }

        selectingSkill = skill;
        ShowSkillDetails(skill);
    }

    private void DeselectSkill(SkillTree_SkillComponent skill)
    {
        if (IsIntroPlaying) return;

        SkillHighlight.SetActive(false);

        skill.ResetMutuallyExclusive();

        skillDetailsText.text = defaultSkillDetailsText;
        skillNameText.text = defaultSkillName;
        skillIconImage.sprite = defaultSkillIcon;

        SkillViewPanelImg.color = IdleColor;
    }

    private void ShowSkillDetails(SkillTree_SkillComponent skill)
    {
        if (!selectingSkill || IsIntroPlaying) return;

        skillViewScroll.verticalNormalizedPosition = 1f;

        SkillHighlight.transform.position = skill.transform.position;
        SkillHighlight.SetActive(true);

        skill.SetMutuallyExclusive();

        SkillViewPanelImg.color = skillViewScrollbarImg.color = skill.skillType switch
        {
            SkillType.INVENTIO => SensesSkillViewPanelColor,
            SkillType.ARS => TechsSkillViewPanelColor,
            SkillType.THEORIA => SpecsSkillViewPanelColor,
            _ => IdleColor,
        };

        skillDetailsText.text =
            (skill.skillDescription + $"\n\n<i><color=#b1b1b1>{skill.favorText}</color></i>").Replace(@"\n", "\n");
        skillNameText.text = skill.skillNameText;
        skillIconImage.sprite = skill.skillIcon;
    }

    public void ConfirmSkillSelect()
    {
        if (selectingSkill == null || IsIntroPlaying) return;

        if (selectingSkill.OnSelectSFX) selectingSkill.OnSelectSFX.Play();
        else audioSources[2].Play();

        exclusions = selectingSkill.OnSkillSelected(exclusions);
        selectingSkill.Button.interactable = false;
        selectingSkill = null;
        SkillHighlight.SetActive(false);

        OnSelect_Update();
    }

    public void Clear()
    {
        CharacterPrefabsStorage.Skills.Clear();

        exclusions.Clear();
        allSkills.ForEach(s => s.OnSkillClear());

        ClearSelectingSkill();

        OnSelect_Update();
    }

    public void ClearSelectingSkill()
    {
        selectingSkill = null;
        SkillHighlight.SetActive(false);

        skillDetailsText.text = defaultSkillDetailsText;
        skillNameText.text = defaultSkillName;
        skillIconImage.sprite = defaultSkillIcon;

        SkillViewPanelImg.color = IdleColor;
    }

    private void OnSceneLoad_GetTechs()
    {
        allSkills.ForEach(s => s.OnSkillClear());

        foreach (var item in CharacterPrefabsStorage.Skills)
        {
            SkillTree_SkillComponent comp = allSkills.Find(s => s.skillName == item.Key);
            if (comp == null) return;
            exclusions = comp.OnSkillSelected(exclusions, false);
            comp.Button.interactable = false;

            OnSelect_Update();
        }
    }

    [SerializeField] GameObject Techs;
    public void OnSelect_Update()
    {
        foreach (var item in TechImgs)
        {
            item.sprite = defaultSkillIcon;
        }

        int maxSkill = Mathf.Min(PlayerMaxSkills, MaxSkillCount);

        int selectedCnt = CharacterPrefabsStorage.Skills.Count;
        short cnt = 0;
        foreach (var item in CharacterPrefabsStorage.Skills)
        {
            TechImgs[cnt].sprite = item.Value.skillIcon;
            cnt++;
        }

        bool isMaxed = CharacterPrefabsStorage.Skills.Count >= maxSkill;
        if (isMaxed)
            allSkills.ForEach(s =>
            {
                if (s.Button) s.Button.interactable = false;
            });

        SelectedCnt.text = isMaxed
            ?
            $"<color=#FFF775>Selected: {selectedCnt}/{maxSkill}</color>"
            :
            $"<color=#A1A1A1>Selected: {selectedCnt}/{maxSkill}</color>";

        OkButton.interactable = !IsSkillsEqual(CharacterPrefabsStorage.Skills, skillSaves);
    }

    bool IsSkillsEqual(Dictionary<SkillName, SkillDataSet> skillsSet, Dictionary<SkillName, SkillDataSet> skillsSave)
    {
        if (skillsSet.Count != skillsSave.Count) return false;
        foreach (var item in skillsSet)
        {
            var key = item.Key;
            if (!skillsSave.ContainsKey(key)) return false;
        }
        return true;
    }

    [SerializeField] GameObject LeaveNotice;
    public void OnQuitButtonClick()
    {
        if (IsSkillsEqual(CharacterPrefabsStorage.Skills, skillSaves))
        {
            ForceQuit();
            return;
        }
        LeaveNotice.SetActive(true);
    }

    public void ForceQuit()
    {
        CharacterPrefabsStorage.Skills = new(skillSaves);
        OnSceneLoad_GetTechs();
        Outview.SetSkills();

        Outview.PlayQuickFadeIn(GetMousePosition(), false);
        OnSelect_Update();
    }

    public void SaveSkills()
    {
        if (audioSources[1]) audioSources[1].Play();
        skillSaves = new(CharacterPrefabsStorage.Skills);
    }

    Vector2 GetMousePosition()
    {
        Vector2 mousePos = Input.mousePosition;
        return new (mousePos.x / Screen.width, mousePos.y / Screen.height);
    }
}