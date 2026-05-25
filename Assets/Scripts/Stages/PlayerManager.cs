using Cinemachine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using static EntityBase;
using static LevelDifficultyModifier;
using static PlayerToxicSmoke;

[Singleton]
public class PlayerManager : MonoBehaviour
{
    public static PlayerManager _instance { get; private set; }
    private void GetSingleton()
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

    private CameraMovement mainCamera;
    [SerializeField] private CinemachineVirtualCamera virtualCamera;

    public enum PlayerType { MELEE, RANGED }
    [SerializeField] private PlayerType playerStartType;
    public PlayerType PlayerStartType => playerStartType;

    [SerializeField] private Transform PlayerSpawnpoint;
    [SerializeField] private GameObject SwapEffect;
    [SerializeField] private float SwapCooldown = 20f;

    [SerializeField] private Image Swapsymbol, AttackSprite, AttackCD, SkillSprite, SkillCD, SpecialSprite, SpecialCD, SwapCD, ActivePlayer, SwapToPlayer;
    [SerializeField] private Sprite MeleeIcon, RangedIcon;

    [Header("Input guide")]
    [SerializeField] private TMP_Text txtSwapKey; 
    [SerializeField] private TMP_Text txtViewKey, txtSViewKey, txtAttackKey, txtSpecialKey, txtSkillKey, txtSlowKey, txtMapKey;
    [SerializeField] private TMP_Text txtMapOffInstruction, txtSkillViewOffInstruction;

    [Header("Swap system")]
    private PlayerBase player;
    public PlayerBase activePlayer => player;

    [SerializeField] private float swapCooldownTimer = 0f;
    private float swapCurrentRotation = 0;

    private float SwapOverflowTimer = 0f;
    [SerializeField] private short SwapStacks = 0;
    [SerializeField] private short SwapMaxStacks = 2;
    [SerializeField] GameObject SwapReadyEffect;
    [SerializeField] private Image[] SwapStacksImg;
    [SerializeField] private Sprite SwapStackAvailable, SwapStackUnavailable;

    [Header("Skill View")]
    [SerializeField] private GameObject PlayerInfoSect;
    [SerializeField] private GameObject TechInfoSect, SkillView_Overlay, SkillView;
    [SerializeField] private Image PlayerIcon, SkillView_Attack, SkillView_Skill, SkillView_Special;
    [SerializeField] private TMP_Text SkillView_Attributes, SkillView_AttackText, SkillView_SkillName, SkillView_SkillText, SkillView_SpecialName, SkillView_SpecialText;
    private Coroutine skillViewCoroutine;

    [SerializeField] private GameObject[] TechViews;
    [SerializeField] private TMP_Text EmptyTechViewsText;

    public bool IsReadingSkillView => skillViewCoroutine != null && SkillView_Overlay.activeSelf;
    private bool CanSwapPlayer => swapCooldownTimer >= SwapCooldown && player && player.IsAlive();

    public bool IsPlayerAlive = true;

    [SerializeField] private GameObject[] Disables;

    List<Image> CDs => new() { AttackCD, SkillCD, SpecialCD, SwapCD };
    enum CDSlot { ATTACK, SKILL, SPECIAL, SWAP }

    Coroutine AttackCooldownCoroutine, SkillCooldownCoroutine, SpecialCooldownCoroutine;

    private bool IsStageStarted = false;
    private List<AudioSource> sfxs;
    
    private AudioSource swapSfx => sfxs[0];
    private AudioSource hit_01_sfx => sfxs[1];
    private AudioSource hit_02_sfx => sfxs[2];

    public StageManager stageManager => StageManager._instance;

    private bool EnableHitStop = true;
    private bool RadioActive = false;

    #region PlayerBattleData
    public bool FirstBlackFlash = true;
    #endregion

    private void Awake()
    {
        GetSingleton();

        SkillView_Overlay.SetActive(false);
        TechInfoSect.SetActive(false);
        PlayerInfoSect.SetActive(true);

        GetPlayerStartType();
        UpdateKeybindTexts();

        EnableHitStop = SaveDataManager.EnableHitStop;
        RadioActive = CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.EQUIPMENT_RADIO);
    }

    public void UpdateKeybindTexts()
    {
        txtViewKey.text = KeybindButton.GetDisplayNameForKey(InputManager.Instance.ViewInfoKey);
        txtAttackKey.text = KeybindButton.GetDisplayNameForKey(InputManager.Instance.AttackKey);
        txtSkillKey.text = KeybindButton.GetDisplayNameForKey(InputManager.Instance.SkillKey);
        txtSpecialKey.text = KeybindButton.GetDisplayNameForKey(InputManager.Instance.SpecialKey);
        txtSwapKey.text = KeybindButton.GetDisplayNameForKey(InputManager.Instance.PlayerSwapKey);
        txtSViewKey.text = KeybindButton.GetDisplayNameForKey(InputManager.Instance.SwapInfoKey);
        txtSlowKey.text = KeybindButton.GetDisplayNameForKey(InputManager.Instance.SlowKey);
        txtMapKey.text = KeybindButton.GetDisplayNameForKey(InputManager.Instance.ViewMapKey);

        string moveKeys = InputManager.Instance.CurrentScheme == InputManager.MovementScheme.ArrowKeys
            ? "Up/down arrows"
            : "W/S";

        txtMapOffInstruction.text = 
            $"Use your movement keys to move the camera." +
            $"\nUse [Ctrl] + [{moveKeys}] to zoom In/Out." +
            $"\nPress '{KeybindButton.GetDisplayNameForKey(InputManager.Instance.ViewMapKey)}' again to close this.";

        txtSkillViewOffInstruction.text =
            $"Press '{KeybindButton.GetDisplayNameForKey(InputManager.Instance.ViewInfoKey)}' again to close this.";
    }

    private void Start()
    {
        sfxs = GetComponents<AudioSource>().ToList();
        sfxs.Remove(sfxs.ElementAt(0)); // Remove the music audio source

        GetPlayerSkills();
        SwapPlayer();

        mainCamera = FindObjectOfType<CameraMovement>();
    }

    private void GetPlayerStartType()
    {
        playerStartType = CharacterPrefabsStorage.startingPlayer;
        if (playerStartType == PlayerType.MELEE)
        {
            SwapToPlayer.sprite = RangedIcon;
            ActivePlayer.sprite = MeleeIcon;
            Swapsymbol.transform.rotation = Quaternion.Euler(0, 0, 0);
        }
        else
        {
            SwapToPlayer.sprite = MeleeIcon;
            ActivePlayer.sprite = RangedIcon;
            Swapsymbol.transform.rotation = Quaternion.Euler(0, 0, 180);
            swapCurrentRotation = -180;
        }
    }

    private void Update()
    {
        if (!IsStageStarted) return;

        if (player && mainCamera)
        {
            mainCamera.UpdatePlayerMovement(player.transform);
        }

        UpdateSwapUI();
        UpdateUpgradeBar();

        if (Input.GetKeyDown(GlobalStageManager.PlayerSwapKey) && CanSwapPlayer)
        {
            SwapPlayer();
        }
        else if (Input.GetKeyDown(GlobalStageManager.ViewInfoKey))
        {
            ViewSkill();
        }
        else if (Input.GetKeyDown(GlobalStageManager.SwapInfoKey))
        {
            SwapView();
        }
    }

    void UpdateSwapUI()
    {
        for (int i = 0; i < SwapStacksImg.Length; ++i)
        {
            Image image = SwapStacksImg[i];
            bool countForCharge = CanSwapPlayer && SwapStacks >= i;
            float amount;
            if (!countForCharge) amount = 0;
            else if (SwapStacks > i) amount = 1;
            else amount = SwapOverflowTimer / SwapCooldown;

            image.fillAmount = amount;
            image.sprite = countForCharge && image.fillAmount >= 1 ? SwapStackAvailable : SwapStackUnavailable;
        }

        float add = Time.deltaTime;
        swapCooldownTimer += add;
        if (CanSwapPlayer && SwapStacks < SwapMaxStacks)
        {
            if (RadioActive) add += add * 0.2f * (SwapStacks + 1);
            SwapOverflowTimer += add;
            if (SwapOverflowTimer >= SwapCooldown)
            {
                SwapOverflowTimer = 0;
                SwapStacks = (short)Mathf.Min(SwapStacks + 1, SwapMaxStacks);
            }
        }
        else SwapOverflowTimer = 0;

        SwapReadyEffect.SetActive(CanSwapPlayer);
    }

    public void PlayerInvoke_SetSkillUI(SkillTree_Manager.SkillName name, Sprite sprite, Color color)
    {
        if (StatusesUI.ContainsKey(name)) return;

        GameObject upgradeUI = Instantiate(UpgradeUIPrefab, StatusBarUI.transform);
        Image[] upgradeImages = upgradeUI.GetComponentsInChildren<Image>();
        foreach (var img in upgradeImages)
        {
            img.sprite = sprite;
            img.color = color;
        }

        StatusesUI.Add(name, upgradeUI);

        barUpdateCounter = 999;
        UpdateUpgradeBar();
    }

    public void PlayerInvoke_RemoveSkillUI(SkillTree_Manager.SkillName name)
    {
        if (!StatusesUI.ContainsKey(name) || name == SkillTree_Manager.SkillName.KNOTS) return;
        Destroy(StatusesUI[name]);
        StatusesUI.Remove(name);

        barUpdateCounter = 999;
        UpdateUpgradeBar();
    }

    [SerializeField] GameObject StatusBarUI, UpgradeUIPrefab;
    Dictionary<SkillTree_Manager.SkillName, GameObject> StatusesUI = new();
    
    short barUpdateCounter = 0;
    void UpdateUpgradeBar()
    {
        barUpdateCounter++;
        if (barUpdateCounter < 6) return;
        barUpdateCounter = 0;

        List<GameObject> Statuses = StatusesUI.Values.ToList();
        if (Statuses.Count <= 0) return;

        Vector3 position = Vector3.zero;
        int mid = Statuses.Count / 2;
        float offset = 150f;

        for (int i = 0; i < StatusesUI.Count; ++i)
        {
            GameObject upgrade = Statuses[i];
            
            position = new Vector3(offset * (i - mid), 0, 0);
            if (StatusesUI.Count % 2 == 0) position.x += offset / 2;

            upgrade.transform.localPosition = position;
        }
    }

    public bool IsFirstTimeStageEnter = true;
    public bool MintBlessing = false;
    public void GetPlayerSkills()
    {
        short diff = (short)(CharacterPrefabsStorage.DifficultyLevel - 1);
        if (diff >= (int) DiffType.PlayerSwap_1)
        {
            float mul = diff == (int)DiffType.PlayerSwap_1 ? 1.5f : 2f;
            SwapCooldown *= mul;
            swapCooldownTimer *= mul;
            SwapOverflowTimer = 0;
        }

        if (CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.EQUIPMENT_RADIO))
        {
            SwapCooldown *= 0.85f;
        }

        if (CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.AMULET))
        {
            MintBlessing = true;
        }
    }

    public enum SkillType { NONE, SPECIAL, ULTIMATE }
    public SkillType RangedSealSkill = SkillType.NONE, MeleeSealSkill = SkillType.NONE;

    public bool hasVowed => RangedSealSkill != SkillType.NONE || MeleeSealSkill != SkillType.NONE;

    public SkillType GetVowSkill(PlayerBase player)
    {
        var playerType = player.GetPlayerType();
        var sealedSkill = playerType == PlayerType.MELEE ? MeleeSealSkill : RangedSealSkill;
        if (sealedSkill == SkillType.NONE) return SkillType.NONE;

        return sealedSkill == SkillType.SPECIAL ? SkillType.ULTIMATE : SkillType.SPECIAL;
    }

    public void SetSealSkill(PlayerBase player, SkillType skillType)
    {
        var playerType = player.GetPlayerType();

        if (playerType == PlayerType.MELEE) MeleeSealSkill = skillType;
        else RangedSealSkill = skillType;

        ResetAllCooldown();
        AssignPlayerSkillSprites(player);
    }

    [SerializeField] private GameObject SwapSmokeUpgradePrefab;
    Coroutine SwapCoroutine;
    public void SwapPlayer()
    {
        if (IsStageStarted && (!CanSwapPlayer || !player || !player.IsAlive())) return;
        if (IsStageStarted)
        {
            if (SwapStacks > 0) SwapStacks--;
            else
            {
                SwapOverflowTimer = swapCooldownTimer = 0f;
            }
        }

        PlayerType swapToPlayertype;
        if (IsStageStarted)
        {
            swapToPlayertype = 
                player is PlayerMelee
                ?
                PlayerType.RANGED
                :
                PlayerType.MELEE;
        }
        else
        {
            swapToPlayertype = playerStartType;
        }

        Vector3 spawnPosition = IsStageStarted ? player.transform.position : PlayerSpawnpoint.position;

        GameObject newPlayerPrefab = CharacterPrefabsStorage.PlayerPrefabs[(int) swapToPlayertype];
        
        GameObject inPlayer = Instantiate(newPlayerPrefab, spawnPosition, Quaternion.identity);
        swapSfx.Play();

        if (IsStageStarted && player)
        {
            player.OnFieldSwapOut(inPlayer.GetComponent<PlayerBase>());
        }

        if (swapToPlayertype == PlayerType.RANGED)
        {
            SwapToPlayer.sprite = MeleeIcon;
            ActivePlayer.sprite = RangedIcon;
        }
        else
        {             
            SwapToPlayer.sprite = RangedIcon;
            ActivePlayer.sprite = MeleeIcon;
        }

        StartCoroutine(SwapCooldownE(SwapCooldown, swapCooldownTimer, IsStageStarted));

        SpawnEntranceSmoke(spawnPosition);
    }

    void SpawnEntranceSmoke(Vector3 spawnPosition)
    {
        GameObject Effect = Instantiate(SwapEffect, spawnPosition, Quaternion.identity);
        StartCoroutine(FadeOut(Effect, IsStageStarted ? 1f : 2f));

        BUG_SPRAY_TYPE type = BUG_SPRAY_TYPE.NONE;
        if (CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.SMOKE_TOXIC))
            type = BUG_SPRAY_TYPE.POISON;
        else if (CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.SMOKE_BLIND))
            type = BUG_SPRAY_TYPE.SMOKE;

        if (type == BUG_SPRAY_TYPE.NONE) return;

        GameObject toxicSmoke = Instantiate(SwapSmokeUpgradePrefab, spawnPosition, Quaternion.identity);
        toxicSmoke.GetComponent<PlayerToxicSmoke>().SetType(type);
    }

    public void SwapCooldownOnStart()
    {
        AssignPlayerSkillSprites(player);
        StartCoroutine(SwapCooldownE(SwapCooldown, swapCooldownTimer, false));
    }

    void AssignPlayerSkillSprites(PlayerBase player)
    {
        AttackSprite.sprite = AttackCD.sprite = player.AttackSprite;
        SkillSprite.sprite = SkillCD.sprite = player.SkillSprite;
        SpecialSprite.sprite = SpecialCD.sprite = player.SpecialSprite;

        ResetAllCooldown();
    }

    public void Register(PlayerBase player)
    {
        if (!this.player)
        {
            StartCoroutine(AssignNewplayerAttributes(player));
            return;
        }

        StartCoroutine(AssignSwappedPlayerAttributes(player));
    }

    IEnumerator AssignNewplayerAttributes(PlayerBase player)
    {
        this.player = player;
        virtualCamera.Follow = player.transform;
        IsStageStarted = true;
        player.SettleSwappedInPlayer = true;
        SwapCooldownOnStart();

        yield return new WaitUntil(() => player && player.IsComponentsInitialized);

        player.OnFieldEnter();
    }

    IEnumerator AssignSwappedPlayerAttributes(PlayerBase newPlayer)
    {
        yield return new WaitUntil(() => newPlayer && newPlayer.IsComponentsInitialized);

        AssignPlayerSkillSprites(newPlayer);

        short percentageHealth = player.GetHealthPercentage();
        newPlayer.SetHealth(Mathf.Max(1, newPlayer.GetMaxHealth() *  percentageHealth / 100));
        newPlayer.OnFieldEnter();

        EntityManager.SpriteRenderers.Remove(player.GetSpriteRenderer());
        EntityManager.Enemies.ForEach(e =>
        {
            e.ChangeAggro(newPlayer);  
        });

        if (player.GetSpriteRenderer().flipX) 
        {
            newPlayer.GetSpriteRenderer().flipX = true;
            newPlayer.FlipAttackPosition();
        }

        var outPlayer = player.gameObject;
        outPlayer.SetActive(false);
        Destroy(outPlayer, 1f);

        player = newPlayer; 
        virtualCamera.Follow = player.transform;

        ResetAllCooldown();
    }

    public IEnumerator AttackCooldown(float duration, float init = 0)
    {
        if (AttackCooldownCoroutine != null) StopCoroutine(AttackCooldownCoroutine);
        AttackCooldownCoroutine = StartCoroutine(Cooldown(AttackCD, duration, init));
        yield return AttackCooldownCoroutine;
    }
    public IEnumerator SkillCooldown(float duration, float init = 0)
    {
        if (SkillCooldownCoroutine != null) StopCoroutine(SkillCooldownCoroutine);
        SkillCooldownCoroutine = StartCoroutine(Cooldown(SkillCD, duration, init));
        yield return SkillCooldownCoroutine;
    }

    public IEnumerator SpecialCooldown(float duration, float init = 0)
    {
        if (SpecialCooldownCoroutine != null) StopCoroutine(SpecialCooldownCoroutine);
        SpecialCooldownCoroutine = StartCoroutine(Cooldown(SpecialCD, duration, init));
        yield return SpecialCooldownCoroutine;
    }

    IEnumerator RotateSwapSymbol(float duration)
    {
        float elapsed = 0f;
        Quaternion startRotation = Quaternion.Euler(0, 0, swapCurrentRotation);
        Quaternion endRotation = startRotation * Quaternion.Euler(0, 0, 180);
        swapCurrentRotation += 180;

        while (elapsed < duration)
        {
            Swapsymbol.transform.rotation = Quaternion.Lerp(startRotation, endRotation, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        Swapsymbol.transform.rotation = endRotation;
    }

    public IEnumerator SwapCooldownE(float duration, float init = 0, bool rotateSymbol = true)
    {
        float waitDuration = duration - init;
        SwapCoroutine = StartCoroutine(Cooldown(SwapCD, duration, init, true));

        float minusTime = 0f;
        if (rotateSymbol)
        {
            minusTime = 0.35f;
            yield return StartCoroutine(RotateSwapSymbol(0.35f));
        }
        Swapsymbol.color = new Color(1, 1, 1, 0.25f);
        yield return new WaitForSeconds(waitDuration - minusTime);
        Swapsymbol.color = Color.white;

        SwapCoroutine = null;
    }

    public IEnumerator Cooldown(Image CD, float duration, float init = 0, bool isCooldownForSwap = false)
    {
        TMP_Text Count = CD.GetComponentInChildren<TMP_Text>();
        float cooldownTimer = init;
        while (cooldownTimer < duration)
        {
            CD.fillAmount = Mathf.Lerp(1, 0, cooldownTimer * 1.0f / duration);
            Count.text = Math.Round(duration - cooldownTimer, 1) + "s";
            cooldownTimer += Time.deltaTime;
            yield return null;
        }

        Count.text = "";
        CD.fillAmount = 0;
        if (isCooldownForSwap) SwapCoroutine = null; 
    }

    bool IsSkillSealed(SkillType skillType)
    {
        if (!player) return false;

        PlayerType playerType = player.GetPlayerType();
        return (playerType == PlayerType.MELEE && 
                ((skillType == SkillType.SPECIAL && MeleeSealSkill == SkillType.SPECIAL) || 
                 (skillType == SkillType.ULTIMATE && MeleeSealSkill == SkillType.ULTIMATE)))
            ||
               (playerType == PlayerType.RANGED &&
                ((skillType == SkillType.SPECIAL && RangedSealSkill == SkillType.SPECIAL) ||
                 (skillType == SkillType.ULTIMATE && RangedSealSkill == SkillType.ULTIMATE)));
    }

    public void ResetAllCooldown()
    {
        if (AttackCooldownCoroutine != null) StopCoroutine(AttackCooldownCoroutine);
        if (SkillCooldownCoroutine != null) StopCoroutine(SkillCooldownCoroutine);
        if (SpecialCooldownCoroutine != null) StopCoroutine(SpecialCooldownCoroutine);

        short cnt = 0;
        CDs.ForEach(cd =>
        {
            float fillAmount = 0;
            string text = "";

            if (cnt == (short)CDSlot.SKILL && IsSkillSealed(SkillType.ULTIMATE)
             || (cnt == (short)CDSlot.SPECIAL && IsSkillSealed(SkillType.SPECIAL)))
            {
                fillAmount = 1;
                text = $"<size=48><b><color=red>X</color></b></size>";
            }

            cd.fillAmount = fillAmount;
            TMP_Text Count = cd.GetComponentInChildren<TMP_Text>();
            Count.text = text;
            cnt++;
        });
    }

    IEnumerator FadeOut(GameObject o, float duration)
    {
        SpriteRenderer renderer = o.GetComponentInChildren<SpriteRenderer>();
        Color startColor = renderer.color;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
            renderer.color = new Color(startColor.r, startColor.g, startColor.b, alpha);

            elapsed += Time.deltaTime;
            yield return null;
        }

        renderer.color = new Color(startColor.r, startColor.g, startColor.b, 0f);
        Destroy(o);
    }

    public DamageInstance OnPlayerDamageTarget(PlayerBase player, DamageInstance dmg, EntityBase target)
    {
        if (!player || !player.IsAlive()) return dmg;
        return dmg;
    }

    public void OnPlayerAttacked(float strength)
    {
        if (strength >= 0.8f) hit_02_sfx.Play();
        else hit_01_sfx.Play();

        if (EnableHitStop) mainCamera.CallShakeCoroutine(strength);
    }

    public void OnPlayerDeath()
    {
        if (CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.HEAT_DEATH))
        {
            var enemies = EntityManager.Enemies;
            foreach (var enemy in enemies)
            {
                if (!enemy || !enemy.IsAlive()) continue;
                enemy.TakeDamage(new(0, 0, 200 + enemy.mHealth * 0.1f), null, null, true);
            }

            stageManager.CheckWinWithHeathDeath();
        }

        IsPlayerAlive = false;
        StopAllCoroutines();

        foreach (var item in Disables)
        {
            item.SetActive(true);
        } 

        CDs.ForEach(cd =>
        {
            cd.fillAmount = 1;
            TMP_Text Count = cd.GetComponentInChildren<TMP_Text>();
            Count.text = "";
        });

        Swapsymbol.color = new Color(1, 1, 1, 0.25f);
    }

    public void MintBlessingRevival()
    {
        if (!MintBlessing) return;

        ResetAllCooldown();
        if (swapCooldownTimer < SwapCooldown)
        {
            if (SwapCoroutine != null)
            {
                StopCoroutine(SwapCoroutine);
                SwapCoroutine = null;
            }

            swapCooldownTimer = SwapCooldown;
            Swapsymbol.color = Color.white;
            SwapCD.fillAmount = 0;
            SwapCD.GetComponentInChildren<TMP_Text>().text = "";
        }
        else if (SwapStacks < SwapMaxStacks) SwapStacks++;

        MintBlessing = false;
    }

    public void ViewSkill()
    {
        if (!IsStageStarted || !activePlayer) return;

        stageManager.UpdateTimeScale();

        if (skillViewCoroutine != null) StopCoroutine(skillViewCoroutine);

        if (SkillView_Overlay.activeSelf)
        {
            skillViewCoroutine = StartCoroutine(HideSkillView());
        }
        else
        {
            SetSkillViewAttributes();
            SkillView_Overlay.SetActive(true);
            skillViewCoroutine = StartCoroutine(ShowSkillView());
        }
    }

    public void OnAttackButtonPress() { if (activePlayer) activePlayer.Action_Attack(); }
    public void OnSkillButtonPress() { if (activePlayer) activePlayer.Action_Skill(); }
    public void OnSpecialButtonPress() { if (activePlayer) activePlayer.Action_Special(); }

    IEnumerator ShowSkillView()
    {
        float InitY = SkillView.transform.localPosition.y, TargetY = 170;
        float c = 0, d = 0.35f, cJump = 0.02f;
        while (c < d)
        {
            SkillView.transform.localPosition = new Vector3(SkillView.transform.localPosition.x, Mathf.Lerp(InitY, TargetY, c * 1.0f / d), SkillView.transform.localPosition.z);
            c += cJump;
            yield return new WaitForSecondsRealtime(cJump);
        }

        SkillView.transform.localPosition = new Vector3(SkillView.transform.localPosition.x, TargetY, SkillView.transform.localPosition.z);
    }

    IEnumerator HideSkillView()
    {
        float InitY = SkillView.transform.localPosition.y, TargetY = -500;
        float c = 0, d = 0.35f, cJump = 0.02f;
        while (c < d)
        {
            SkillView.transform.localPosition = new Vector3(SkillView.transform.localPosition.x, Mathf.Lerp(InitY, TargetY, c * 1.0f / d), SkillView.transform.localPosition.z);
            c += cJump;
            yield return new WaitForSecondsRealtime(cJump);
        }

        SkillView.transform.localPosition = new Vector3(SkillView.transform.localPosition.x, TargetY, SkillView.transform.localPosition.z);
        SkillView_Overlay.SetActive(false);
    }

    private void SetSkillViewAttributes()
    {
        PlayerTooltipsInfo info = player.GetPlayerTooltipsInfo();

        PlayerIcon.sprite = info.Icon;

        SkillView_Attributes.text =
            (info.attackPattern == AttackPattern.MELEE ? $"<color=yellow>{info.attackPattern}</color>" : $"<color=blue>{info.attackPattern}</color>") 
            + ", " 
            + (info.damageType == DamageType.MAGICAL ? $"<color=#800080>{info.damageType}</color>" : $"<color=#9C2007>{info.damageType}</color>") + "\n\n" +
            $"<color=green>HP: {info.health} / {info.mHealth} ({info.health * 100 / info.mHealth}%)</color>\n\n" +
            $"<color=#9C2007>ATK: {info.atk} ({info.bAtk} + {info.atk - info.bAtk})</color>\n\n" +
            $"<color=#800000>ASPD: {info.ASPD}</color>\n\n" +
            $"<color=yellow>DEF: {info.def} ({info.bDef} + {info.def - info.bDef})</color>\n\n" +
            $"<color=#00ffff>RES: {info.res} ({info.bRes} + {info.res - info.bRes})</color>\n\n" +
            $"<color=black>MSPD: {info.MSPD}</color>";

        SkillView_Attack.sprite = info.AttackSprite;
        SkillView_Skill.sprite = info.SkillSprite;
        SkillView_Special.sprite = info.SpecialSprite;
        SkillView_AttackText.text = info.AttackText;
        SkillView_SkillName.text = info.SkillName;
        SkillView_SkillText.text = info.SkillText;
        SkillView_SpecialName.text = info.SpecialName;
        SkillView_SpecialText.text = info.SpecialText;

        // techs
        int techCount = CharacterPrefabsStorage.Skills.Count;
        bool hasTech = techCount > 0;

        EmptyTechViewsText.text = hasTech ? "FINDINGS:" : "Look like you didn't bring any finding :(";
        
        for (int i = 0; i < TechViews.Length; i++)
        {
            GameObject techView = TechViews[i];
            if (!hasTech)
            {
                techView.SetActive(false);
                continue;
            }

            bool hasTechThisLoop = (i + 1) <= techCount;
            if (!hasTechThisLoop) continue;
            
            SkillDataSet skillDataSet = CharacterPrefabsStorage.Skills.ElementAt(i).Value;
            Image skillIcon = techView.GetComponentInChildren<Image>();
            TMP_Text[] txts = techView.GetComponentsInChildren<TMP_Text>();

            skillIcon.sprite = skillDataSet.skillIcon;
            txts[0].text = skillDataSet.nameInString;
            txts[1].text = skillDataSet.skillDescription;
        }
    }

    [SerializeField] private AudioSource FreezeSfx;
    [SerializeField] private GameObject FreezeRing, FreezeRingMark;
    public void ChainFreeze(Dictionary<EntityBase, float> InitialHitDictionary, float FreezeRange, float FreezeDurationMin, float FreezeDurationMax, float MinDistanceForFreezeDuration)
        => StartCoroutine(UnleashFreezeMarks(InitialHitDictionary, FreezeRange, FreezeDurationMin, FreezeDurationMax, MinDistanceForFreezeDuration));

    private IEnumerator UnleashFreezeMarks(Dictionary<EntityBase, float> InitialHitDictionary, float FreezeRange, float FreezeDurationMin, float FreezeDurationMax, float MinDistanceForFreezeDuration)
    {
        if (!CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.FREEZE_BLOOM)) yield break;

        Dictionary<EntityBase, float> HitDictionary = new Dictionary<EntityBase, float>(InitialHitDictionary);
        Dictionary<EntityBase, GameObject> Marks = new();
        while (HitDictionary.Count > 0)
        {
            foreach (var enemy in HitDictionary.Keys)
            {
                if (Marks.ContainsKey(enemy)) continue;
                GameObject mark = Instantiate(FreezeRingMark, enemy.transform.position + new Vector3(0, 80), Quaternion.identity, enemy.transform);
                Marks.Add(enemy, mark);
            }

            yield return new WaitForSeconds(2f);
            if (FreezeSfx) FreezeSfx.Play();
            yield return new WaitForSeconds(0.1f);

            Dictionary<EntityBase, float> HitThisRound = new();
            foreach (var pair in HitDictionary)
            {
                EntityBase SourceEntity = pair.Key;
                if (!SourceEntity) continue;

                if (Marks.ContainsKey(SourceEntity) && Marks[SourceEntity])
                {
                    Destroy(Marks[SourceEntity]);
                    Marks.Remove(SourceEntity);
                }

                GameObject o = Instantiate(FreezeRing, SourceEntity.transform.position, Quaternion.identity);
                o.GetComponent<PlayerRangedFreezeObj>().TargetScale *= FreezeRange / 450f;

                List<EntityBase> nearbyHits =
                    EntityBase.Base_SearchForEntitiesAroundCertainPoint(typeof(EnemyBase), SourceEntity.transform.position, FreezeRange, true).ToList();

                foreach (EntityBase nearby in nearbyHits)
                {
                    EnemyBase enemy = nearby as EnemyBase;
                    float distance = Vector3.Distance(SourceEntity.transform.position, enemy.transform.position);
                    float freezeDuration = distance >= FreezeRange * 0.8f
                        ?
                        FreezeDurationMin
                        :
                        Mathf.Lerp(FreezeDurationMin, pair.Value, MinDistanceForFreezeDuration * 1.0f / distance);
                    enemy.ApplyFreeze(enemy, freezeDuration);

                    if (nearby != SourceEntity)
                    {
                        if (CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.WINDBLOW_NORTH))
                        {
                            float pushDuration = distance >= FreezeRange * 0.8f
                                ?
                                0.1f
                                :
                                Mathf.Lerp(0.12f, 0.23f, MinDistanceForFreezeDuration * 1.0f / distance);

                            enemy.PushEntityFrom(enemy, SourceEntity.transform, 1.8f, pushDuration);
                        }
                        else if (CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.WINDBLOW_SOUTH))
                            enemy.PullEntityTowards(enemy, SourceEntity.transform, 2.1f, 0.2f);
                    }

                    if (!HitThisRound.ContainsKey(enemy) && !InitialHitDictionary.ContainsKey(enemy))
                    {
                        HitThisRound.Add(nearby, freezeDuration);
                        InitialHitDictionary.Add(nearby, freezeDuration);
                    }
                }
            }

            HitDictionary = new(HitThisRound.Where(h => h.Key));
        }
    }

    public void SwapView()
    {
        if (!SkillView_Overlay.activeSelf) return;
        bool activeInfoView = !PlayerInfoSect.activeSelf;
        PlayerInfoSect.SetActive(activeInfoView);
        TechInfoSect.SetActive(!activeInfoView);
    }

    public void ClearStageBGM(float duration)
    {
        StartCoroutine(ClearBGMCouroutine(duration));
    }

    IEnumerator ClearBGMCouroutine(float duration)
    {
        stageManager.StageBGM.Pause();
        yield return new WaitForSeconds(duration);
        stageManager.StageBGM.Play();
    }
}

public class PlayerTooltipsInfo
{
    public Sprite Icon { get; set; }
    public Sprite AttackSprite { get; set; }
    public Sprite SkillSprite { get; set; }
    public Sprite SpecialSprite { get; set; }
    public string AttackText { get; set; }
    public string SkillName { get; set; }
    public string SkillText { get; set; }
    public string SpecialName { get; set; }
    public string SpecialText { get; set; }
    public int mHealth { get; set; }
    public int health { get; set; }
    public short bDef { get; set; }
    public short def { get; set; }
    public short bAtk { get; set; }
    public short atk { get; set; }
    public short bRes { get; set; }
    public short res { get; set; }
    public float moveSpeed { get; set; }
    public AttackPattern attackPattern { get; set; }
    public DamageType damageType { get; set; }
    public float attackSpeed { get; set; }
    public float attackRange { get; set; }
    public float attackInterval { get; set; }

    public string ASPD
    {
        get
        {
            if (attackSpeed <= 0.15f) return "VERY FAST";
            if (attackSpeed <= 0.3f) return "FAST";
            if (attackSpeed <= 0.6f) return "NORMAL";
            if (attackSpeed <= 1.1f) return "SLOW";
            return "VERY SLOW";
        }
    }

    public string MSPD
    {
        get
        {
            if (moveSpeed <= 80f) return "VERY SLOW";
            if (moveSpeed <= 130f) return "SLOW";
            if (moveSpeed <= 190f) return "NORMAL";
            if (moveSpeed <= 270f) return "FAST";
            return "VERY FAST";
        }
    }
}