using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FumoScript : MonoBehaviour
{
    [SerializeField] private GameObject clearEffect, squishTargetObject, rangeIndicator;
    [SerializeField] private AudioClip WinBGM;
    public AudioClip f_WinBGM => WinBGM;

    [SerializeField] private short squishCount = 1;
    [SerializeField] private float squishDelay = 0.1f;
    [SerializeField] private float squishInterval = 5f;
    [SerializeField] private float squishDuration = 0.2f;
    [SerializeField] private float squishAmount = 0.2f;

    [SerializeField] private float SkillRange = 800f;
    RectTransform rectTransform;

    RawImage glowImg;
    Vector3 originalScale;
    AudioSource audioSource;
    GameObject sprite;

    public enum FumoObjectiveType
    {
        PICK_UP,
        PROTECT,
    }

    public FumoObjectiveType ObjectiveType = FumoObjectiveType.PICK_UP;

    public GameObject Fumo => sprite;

    private bool isPickedUp = false, isSquishing = false;
    void Start()
    {
        rectTransform = rangeIndicator.GetComponent<RectTransform>();

        glowImg = GetComponentInChildren<RawImage>();
        sprite = transform.Find("Object/Sprite").gameObject;
        Canvas[] cvs = GetComponentsInChildren<Canvas>();
        foreach (var cv in cvs) cv.sortingLayerID = SortingLayer.NameToID("Ground");

        audioSource = GetComponent<AudioSource>();

        originalScale = sprite.transform.localScale;

        StartCoroutine(SkillEffect());
        StartCoroutine(SquishCoroutine());
    }

    [SerializeField] GameObject PushEffect;
    float baseRange = 1000f;

    readonly bool WindRush = CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.MINT_WINDRUSH);
    readonly bool Phalanx = CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.MINT_PHALANX);

    Image rangeImg;
    Color initColor, glowColor;
    public IEnumerator SkillEffect()
    {
        bool canUseSkill = Phalanx || WindRush;
        
        rangeIndicator.SetActive(canUseSkill);

        rangeImg = rangeIndicator.GetComponent<Image>();
        
        initColor = rangeImg.color;
        glowColor = new Color(initColor.r, initColor.g, initColor.b, initColor.a * 2.35f);

        if (!canUseSkill) yield break;

        rectTransform.sizeDelta = new(
                SkillRange * 2f,
                SkillRange * 2f
            );

        float waitInterval = Phalanx ? 0.5f : 5.2f, jump = 0.25f;
        float counter = 0f;

        while (true)
        {
            counter += jump;

            var player = EntityBase.Base_SearchForNearestEntityAroundCertainPoint(typeof(PlayerBase), transform.position, SkillRange, true);
            rangeImg.color = player ? glowColor : initColor;
            
            if (!player || counter < waitInterval)
            {
                yield return new WaitForSeconds(jump);
                continue;
            }

            counter = 0;
            if (Phalanx)
            {
                string Key = "FUMO_SKILL_PHALANX";
                player.ApplyEffect(Effect.AffectedStat.DEF, Key, 215, waitInterval + 0.05f, true);
                player.ApplyEffect(Effect.AffectedStat.RES, Key, 25, waitInterval + 0.05f, false);
            }
            else if (WindRush)
            {
                GameObject o = Instantiate(PushEffect, transform.position, Quaternion.identity);
                o.transform.localScale *= SkillRange / baseRange;
                Destroy(o, 0.5f);

                var enemies = EntityBase.Base_SearchForEntitiesAroundCertainPoint(typeof(EnemyBase), transform.position, SkillRange, true);
                foreach (var enemy in enemies)
                {
                    player.DealDamage(enemy, new DamageInstance(0, 30, 0));
                    enemy.PushEntityFrom(enemy, transform.position, 3.7f, 0.25f, true);
                }
            }

            yield return new WaitForSeconds(jump);
        }
    }

    IEnumerator SquishCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(squishInterval);
            StartCoroutine(Squish());
        }
    }

    IEnumerator Squish()
    {
        if (isSquishing) yield break;

        isSquishing = true;

        Transform targetTransform = sprite.transform;
        Vector3 squishedScale = new Vector3(originalScale.x * (1 + squishAmount), originalScale.y * (1 - squishAmount), originalScale.z);
        float duration = squishDuration;
        float elapsedTime;

        for (int i = 0; i < squishCount; i++) 
        { 
            elapsedTime = 0f;
            if (audioSource != null) audioSource.Play();

            // Squish down
            while (elapsedTime < duration)
            {
                targetTransform.localScale = Vector3.Lerp(originalScale, squishedScale, (elapsedTime / duration));
                elapsedTime += Time.unscaledDeltaTime;
                yield return null;
            }
            targetTransform.localScale = squishedScale;
            elapsedTime = 0f;
            // Return to original scale
            while (elapsedTime < duration)
            {
                targetTransform.localScale = Vector3.Lerp(squishedScale, originalScale, (elapsedTime / duration));
                elapsedTime += Time.unscaledDeltaTime;
                yield return null;
            }
            targetTransform.localScale = originalScale;
            yield return new WaitForSecondsRealtime(squishDelay);
        }

        isSquishing = false;
    }

    public void SquishFun() => StartCoroutine(Squish());

    public Vector3 OnFumoPickUp()
    {
        if (isPickedUp) return Vector3.zero;

        isSquishing = false;
        rangeIndicator.SetActive(false);
        Vector3 spritePosition = Camera.main.WorldToScreenPoint(sprite.transform.position);

        isPickedUp = true;
        sprite.GetComponent<Image>().color = Color.white;
        Instantiate(clearEffect, transform.position, Quaternion.identity);
        StopAllCoroutines();

        Canvas cv = GetComponent<Canvas>();
        cv.sortingLayerID = SortingLayer.NameToID("UI");
        cv.renderMode = RenderMode.ScreenSpaceOverlay;

        Canvas spriteCV = sprite.GetComponent<Canvas>(), glowCV = glowImg.GetComponent<Canvas>();
        spriteCV.overrideSorting = true;
        glowCV.transform.position = spriteCV.transform.position = spritePosition;

        transform.localScale = originalScale;
        transform.Find("Object/Shadow").gameObject.SetActive(false);
        squishCount = 1;

        audioSource.spatialBlend = 0f;

        return spritePosition;
    }

    public void FumoZoomInComplete()
    {
        originalScale = sprite.transform.localScale;
        GetComponentInChildren<Button>().interactable = true;
    }
}
