using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UI_DarkZoneEffect : MonoBehaviour
{
    [SerializeField] float offsetY = 100f;
    [HideInInspector] public EntityBase entity;

    [SerializeField] float pulseDuration = 4f, pulsePause = 1f;
    [SerializeField] Image Glowpart;

    Color glowColor;
    bool isInitialized = false;

    Coroutine pulseCoroutine;

    StageManager stageManager;

    public void Initialize(EntityBase targetEntity)
    {
        if (!this) return;

        if (!isInitialized)
        {
            stageManager = FindObjectOfType<StageManager>();
            glowColor = Glowpart.color;
            isInitialized = true;
        }

        entity = targetEntity;

        gameObject.SetActive(true);
        Glowpart.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0);
        pulseCoroutine = StartCoroutine(PulseGlow());
    }

    IEnumerator PulseGlow()
    {
        if (stageManager.EnableLowDetail)
        {
            Glowpart.color = glowColor;
            yield break;
        }

        float elapsedTime = 0f;
        while (true)
        {
            // Fade in
            while (elapsedTime < pulseDuration)
            {
                float alpha = Mathf.Lerp(0, 1, elapsedTime * 1.0f / pulseDuration);
                Glowpart.color = new Color(glowColor.r, glowColor.g, glowColor.b, alpha);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            Glowpart.color = new Color(glowColor.r, glowColor.g, glowColor.b, 1);
            yield return new WaitForSeconds(pulsePause);

            // Fade out
            elapsedTime = 0f;
            while (elapsedTime < pulseDuration)
            {
                float alpha = Mathf.Lerp(1, 0, elapsedTime * 1.0f / pulseDuration);
                Glowpart.color = new Color(glowColor.r, glowColor.g, glowColor.b, alpha);
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            elapsedTime = 0f;

            Glowpart.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0);
            yield return new WaitForSeconds(pulsePause);
        }
    }

    public void DisableEffect()
    {
        if (!this) return;

        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
        }
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!this) return;

        if (entity == null && !isInitialized) return;
        else if (entity == null)
        {
            Destroy(gameObject);
            return;
        }

        if (!entity.IsAlive())
        {
            entity = null;
            DisableEffect();
            return;
        }
        transform.position = entity.transform.position + Vector3.up * offsetY;
    }
}