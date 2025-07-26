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

public class StageManager : MonoBehaviour
{
    private CameraMovement mainCamera;
    [SerializeField] private CinemachineVirtualCamera virtualCamera;

    [SerializeField] private GameObject PlayerMeleePrefab, PlayerRangedPrefab, SwapEffect;
    [SerializeField] private float SwapCooldown = 20f;
    [SerializeField] private Image Swapsymbol, AttackSprite, AttackCD, SkillSprite, SkillCD, SpecialSprite, SpecialCD, SwapCD, ActivePlayer, SwapToPlayer;
    [SerializeField] private Sprite MeleeIcon, RangedIcon;
    [SerializeField] private TMP_Text txtAttackKey, txtSpecialKey, txtSkillKey;

    public KeyCode SwapKey = KeyCode.Space, AttackKey = KeyCode.Z, SkillKey = KeyCode.A, SpecialKey = KeyCode.X;

    private PlayerBase player;
    [SerializeField] private float swapCooldownTimer = 0f;
    [SerializeField] GameObject SwapReadyEffect;
    private bool CanSwapPlayer => swapCooldownTimer >= SwapCooldown && player && player.IsAlive();

    [SerializeField] private GameObject[] Disables;

    List<Image> CDs => new() { AttackCD, SkillCD, SpecialCD, SwapCD };
    
    Coroutine AttackCooldownCoroutine, SkillCooldownCoroutine, SpecialCooldownCoroutine;

    private void Start()
    {
        mainCamera = FindObjectOfType<CameraMovement>();

        txtAttackKey.text = GetCharFromKeyCode(AttackKey).ToString();
        txtSkillKey.text = GetCharFromKeyCode(SkillKey).ToString();
        txtSpecialKey.text = GetCharFromKeyCode(SpecialKey).ToString();
    }

    private void Update()
    {
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
    }

    public void SwapPlayer()
    {
        if (!CanSwapPlayer || !player || !player.IsAlive()) return;
        swapCooldownTimer = 0f;

        ResetAllCooldown();

        bool activePlayerIsMelee = player is PlayerMelee;

        GameObject Effect = Instantiate(SwapEffect, player.transform.position, Quaternion.identity);
        GameObject newPlayerPrefab = activePlayerIsMelee ? PlayerRangedPrefab : PlayerMeleePrefab;
        Instantiate(newPlayerPrefab, player.transform.position, Quaternion.identity);

        StartCoroutine(FadeOut(Effect, 1f));

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

        StartCoroutine(SwapCooldownE(SwapCooldown));
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
        
        EntityManager.Entities.Remove(player);
        EntityManager.Entities.ForEach(e =>
        {
            if (e is EnemyBase en)
            {
                en.ChangeAggro(newPlayer);  
            }
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
        StartCoroutine(RotateSwapSymbol(0.35f));
        Swapsymbol.color = new Color(1, 1, 1, 0.25f);
        yield return StartCoroutine(Cooldown(SwapCD, duration, init));
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
}
