using Cinemachine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static EntityBase;

public class PlayerManager : MonoBehaviour
{
    private CameraMovement mainCamera;
    [SerializeField] private CinemachineVirtualCamera virtualCamera;

    public enum PlayerType { MELEE, RANGED }
    [SerializeField] private PlayerType playerStartType;
    [SerializeField] private Transform PlayerSpawnpoint;
    [SerializeField] private GameObject SwapEffect;
    [SerializeField] private float SwapCooldown = 20f;
    [SerializeField] private Image Swapsymbol, AttackSprite, AttackCD, SkillSprite, SkillCD, SpecialSprite, SpecialCD, SwapCD, ActivePlayer, SwapToPlayer;
    [SerializeField] private Sprite MeleeIcon, RangedIcon;
    [SerializeField] private TMP_Text txtViewKey, txtAttackKey, txtSpecialKey, txtSkillKey;

    public KeyCode ViewKey = KeyCode.V, SwapKey = KeyCode.Space, AttackKey = KeyCode.Z, SkillKey = KeyCode.A, SpecialKey = KeyCode.X;

    private PlayerBase player;
    [SerializeField] private float swapCooldownTimer = 0f;
    [SerializeField] GameObject SwapReadyEffect;

    [SerializeField] private GameObject SkillView_Overlay, SkillView;
    [SerializeField] private Image PlayerIcon, SkillView_Attack, SkillView_Skill, SkillView_Special;
    [SerializeField] private TMP_Text SkillView_Attributes, SkillView_AttackText, SkillView_SkillName, SkillView_SkillText, SkillView_SpecialName, SkillView_SpecialText;
    private Coroutine skillViewCoroutine;

    public bool IsReadingSkillView => skillViewCoroutine != null && SkillView_Overlay.activeSelf;
    private bool CanSwapPlayer => swapCooldownTimer >= SwapCooldown && player && player.IsAlive();

    public bool IsPlayerAlive = true;

    [SerializeField] private GameObject[] Disables;

    List<Image> CDs => new() { AttackCD, SkillCD, SpecialCD, SwapCD };
    
    Coroutine AttackCooldownCoroutine, SkillCooldownCoroutine, SpecialCooldownCoroutine;

    private bool IsStageStarted = false;

    private void Start()
    {
        SwapPlayer();

        mainCamera = FindObjectOfType<CameraMovement>();

        txtViewKey.text = GetCharFromKeyCode(ViewKey).ToString();
        txtAttackKey.text = GetCharFromKeyCode(AttackKey).ToString();
        txtSkillKey.text = GetCharFromKeyCode(SkillKey).ToString();
        txtSpecialKey.text = GetCharFromKeyCode(SpecialKey).ToString();
    }

    private void Update()
    {
        if (!IsStageStarted) return;

        if (player && mainCamera)
        {
            mainCamera.UpdatePlayerMovement(player.transform);
        }

        swapCooldownTimer += Time.deltaTime;
        SwapReadyEffect.SetActive(CanSwapPlayer);

        if (Input.GetKeyDown(SwapKey) && CanSwapPlayer)
        {
            SwapPlayer();
        }
        else if (Input.GetKeyDown(ViewKey))
        {
            ViewSkill();
        }
    }

    public void SwapPlayer()
    {
        if (IsStageStarted && (!CanSwapPlayer || !player || !player.IsAlive())) return;
        if (IsStageStarted) swapCooldownTimer = 0f;

        ResetAllCooldown();

        bool activePlayerIsMelee = IsStageStarted ? player is PlayerMelee : playerStartType != PlayerType.MELEE;

        Vector3 spawnPosition = IsStageStarted ? player.transform.position : PlayerSpawnpoint.position;

        GameObject Effect = Instantiate(SwapEffect, spawnPosition, Quaternion.identity);
        GameObject newPlayerPrefab = activePlayerIsMelee
            ? CharacterPrefabsStorage.PlayerPrefabs[(int) PlayerType.RANGED] 
            : CharacterPrefabsStorage.PlayerPrefabs[(int)PlayerType.MELEE];
        Instantiate(newPlayerPrefab, spawnPosition, Quaternion.identity);

        StartCoroutine(FadeOut(Effect, IsStageStarted ? 1f : 2f));

        if (activePlayerIsMelee)
        {
            SwapToPlayer.sprite = MeleeIcon;
            ActivePlayer.sprite = RangedIcon;
        }
        else
        {             
            SwapToPlayer.sprite = RangedIcon;
            ActivePlayer.sprite = MeleeIcon;
        }

        StartCoroutine(SwapCooldownE(SwapCooldown, swapCooldownTimer));
    }

    public void SwapCooldownOnStart()
    {
        bool activePlayerIsMelee = player is not PlayerMelee;
        if (activePlayerIsMelee)
        {
            SwapToPlayer.sprite = MeleeIcon;
            ActivePlayer.sprite = RangedIcon;
        }
        else
        {
            SwapToPlayer.sprite = RangedIcon;
            ActivePlayer.sprite = MeleeIcon;
        }

        StartCoroutine(SwapCooldownE(SwapCooldown, swapCooldownTimer));
    }

    public void Register(PlayerBase player)
    {
        if (!this.player)
        {
            this.player = player;
            virtualCamera.Follow = player.transform;
            IsStageStarted = true;

            SwapCooldownOnStart();
            return;
        }

        StartCoroutine(AssignSwappedPlayerAttributes(player));
    }

    IEnumerator AssignSwappedPlayerAttributes(PlayerBase newPlayer)
    {
        AttackSprite.sprite = AttackCD.sprite = newPlayer.AttackSprite;
        SkillSprite.sprite = SkillCD.sprite = newPlayer.SkillSprite;
        SpecialSprite.sprite = SpecialCD.sprite = newPlayer.SpecialSprite;

        short percentageHealth = player.GetHealthPercentage();
        newPlayer.SetHealth(Mathf.Max(1, newPlayer.GetMaxHealth() *  percentageHealth / 100));
        
        EntityManager.SpriteRenderers.Remove(player.GetSpriteRenderer());
        EntityManager.Enemies.ForEach(e =>
        {
            e.ChangeAggro(newPlayer);  
        });

        yield return new WaitForEndOfFrame();
        Destroy(player.gameObject);
        player = newPlayer; 
        virtualCamera.Follow = player.transform;
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
        Quaternion startRotation = Swapsymbol.transform.rotation;
        Quaternion endRotation = startRotation * Quaternion.Euler(0, 0, 180);
        while (elapsed < duration)
        {
            Swapsymbol.transform.rotation = Quaternion.Lerp(startRotation, endRotation, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        Swapsymbol.transform.rotation = endRotation;
    }

    public IEnumerator SwapCooldownE(float duration, float init = 0)
    {
        float waitDuration = duration - init;
        StartCoroutine(Cooldown(SwapCD, duration, init));

        yield return StartCoroutine(RotateSwapSymbol(0.35f));
        Swapsymbol.color = new Color(1, 1, 1, 0.25f);
        yield return new WaitForSeconds(waitDuration - 0.35f);
        Swapsymbol.color = Color.white;
    }

    public IEnumerator Cooldown(Image CD, float duration, float init = 0)
    {
        TMP_Text Count = CD.GetComponentInChildren<TMP_Text>();
        float c = init;
        while (c < duration)
        {
            CD.fillAmount = Mathf.Lerp(1, 0, c * 1.0f / duration);
            Count.text = Math.Round(duration - c, 1) + "s";
            c += Time.deltaTime;
            yield return null;
        }

        Count.text = "";
        CD.fillAmount = 0;
    }

    public void ResetAllCooldown()
    {
        if (AttackCooldownCoroutine != null) StopCoroutine(AttackCooldownCoroutine);
        if (SkillCooldownCoroutine != null) StopCoroutine(SkillCooldownCoroutine);
        if (SpecialCooldownCoroutine != null) StopCoroutine(SpecialCooldownCoroutine);
        CDs.ForEach(cd =>
        {
            cd.fillAmount = 0;
            TMP_Text Count = cd.GetComponentInChildren<TMP_Text>();
            Count.text = "";
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

    char GetCharFromKeyCode(KeyCode key)
    {
        bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (key >= KeyCode.A && key <= KeyCode.Z)
        {
            return shift ? key.ToString()[0] : char.ToLower(key.ToString()[0]);
        }

        if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
        {
            string normal = "0123456789";
            string shifted = ")!@#$%^&*(";
            int index = key - KeyCode.Alpha0;
            return shift ? shifted[index] : normal[index];
        }

        // Add more mappings here as needed (symbols, punctuation, etc.)
        return '\0';
    }

    public void OnPlayerDeath()
    {
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

    public void ViewSkill()
    {
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
            if (moveSpeed <= 60f) return "VERY SLOW";
            if (moveSpeed <= 100f) return "SLOW";
            if (moveSpeed <= 160f) return "NORMAL";
            if (moveSpeed <= 240f) return "FAST";
            return "VERY FAST";
        }
    }
}