using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnemyTooltipsScript : MonoBehaviour
{
    [SerializeField] RectTransform tooltipsTransform;
    [SerializeField] float showUpTime = 0.5f, holdTime = 8f;
    [SerializeField] Vector2 targetposition = new(-320, -125);
    Vector2 startPosition;

    [SerializeField] Image icon;
    [SerializeField] TMP_Text txtName, txtDescription;

    private EnemyBase enemyBase;
    private bool isShowing = false;

    private List<EnemyTooltipsScript> enemyTooltipsScripts;

    private void Start()
    {
        enemyTooltipsScripts = FindObjectsOfType<EnemyTooltipsScript>().Where(tt => tt && tt.isShowing && tt != this).ToList();
        startPosition = tooltipsTransform.anchoredPosition = new(targetposition.x + 700, targetposition.y);

        enemyBase = transform.parent.GetComponent<EnemyBase>();
        if (enemyBase)
        {
            var data = enemyBase.GetTooltipsData();
            icon.sprite = data.Icon;
            txtName.text = data.Name;
            txtDescription.text = data.Description;
            holdTime = data.HoldTime;

            StartCoroutine(ShowUp());
        }
    }

    IEnumerator ShowUp()
    {
        while (enemyTooltipsScripts.Any(tt => tt && tt.isShowing))
        {
            enemyTooltipsScripts.RemoveAll(tt => !tt || !tt.isShowing);
            yield return null;
        }

        isShowing = true;
        float elapsedTime = 0f;
        while (elapsedTime < showUpTime)
        {
            tooltipsTransform.anchoredPosition = Vector2.Lerp(startPosition, targetposition, elapsedTime * 1.0f / showUpTime);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        tooltipsTransform.anchoredPosition = targetposition;
        yield return new WaitForSeconds(holdTime);

        StartCoroutine(Disappear());
    }

    IEnumerator Disappear()
    {
        isShowing = false;
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