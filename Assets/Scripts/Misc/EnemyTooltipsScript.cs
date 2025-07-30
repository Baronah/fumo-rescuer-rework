using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnemyTooltipsScript : MonoBehaviour
{
    [SerializeField] Image Progress;
    [SerializeField] RectTransform tooltipsTransform;
    [SerializeField] float showUpTime = 0.5f, holdTime = 8f;
    [SerializeField] Vector2 targetposition = new(-320, -125);
    Vector2 startPosition;

    [SerializeField] Image icon;
    [SerializeField] TMP_Text txtName, txtDescription;

    private EnemyBase enemyBase;
    public static bool isAnyTooltipsShowing = false;

    public void Initialize(EnemyBase owner)
    {
        if (owner == null) return;
        enemyBase = owner;

        startPosition = tooltipsTransform.anchoredPosition = new(targetposition.x + 700, targetposition.y);
        StartCoroutine(OnStart());
    }

    IEnumerator OnStart()
    {
        while (isAnyTooltipsShowing) yield return null;

        if (!enemyBase)
        {
            Destroy(gameObject);
            yield break;
        }

        var data = enemyBase.GetTooltipsData();
        icon.sprite = data.Icon;
        txtName.text = data.Name;
        txtDescription.text = data.Description;
        holdTime = data.HoldTime;

        StartCoroutine(ShowUp());
    }

    IEnumerator ShowUp()
    {
        isAnyTooltipsShowing = true;
        float elapsedTime = 0f;
        while (elapsedTime < showUpTime)
        {
            tooltipsTransform.anchoredPosition = Vector2.Lerp(startPosition, targetposition, elapsedTime * 1.0f / showUpTime);
            elapsedTime += Time.deltaTime;

            yield return null;
        }

        tooltipsTransform.anchoredPosition = targetposition;
        
        elapsedTime = 0f;
        while (elapsedTime < holdTime)
        {
            Progress.fillAmount = 1.0f - elapsedTime * 1.0f / holdTime;
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        Progress.fillAmount = 0f;
        StartCoroutine(Disappear());
    }

    IEnumerator Disappear()
    {
        isAnyTooltipsShowing = false;
        float elapsedTime = 0f;
        while (elapsedTime < showUpTime)
        {
            tooltipsTransform.anchoredPosition = Vector2.Lerp(targetposition, startPosition, elapsedTime * 1.0f / showUpTime);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        Destroy(gameObject);
    }
}

public class TooltipsData
{
    public Sprite Icon { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public float HoldTime { get; set; } = 8f;   
}