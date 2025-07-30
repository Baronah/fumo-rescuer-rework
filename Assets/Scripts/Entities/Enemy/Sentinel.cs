using System.Collections;
using UnityEngine;

public class Sentinel : EnemyBase
{
    [SerializeField] private float SpeedBuffOnAlert = 1.35f;
    [SerializeField] private float AtkBuffOnAlert = 1.2f;
    [SerializeField] private GameObject DetectCircle;

    private RectTransform DetectCircleRectTransform;

    public override void Start()
    {
        base.Start();
        DetectCircleRectTransform = DetectCircle.GetComponent<RectTransform>();
    }

    public override void InitializeComponents()
    {
        attackPattern = AttackPattern.NONE;

        base.InitializeComponents();
    }

    public override void FixedUpdate()
    {
        base.FixedUpdate();

        if (!SpottedPlayer)
        {
            DetectCircleRectTransform.sizeDelta = new Vector2(
                DetectionRange * 2.05f,
                DetectionRange * 2.05f
            );
        }
        DetectCircle.SetActive(IsAlive());
    }

    public override void OnFirsttimePlayerSpot(bool viaAlert = false)
    {
        if (sfxs[0]) sfxs[0].Play();

        base.OnFirsttimePlayerSpot();
        animator.SetTrigger("skill");
        StartCoroutine(ExpandDetectCircle());

        if (!viaAlert)
        {
            EntityManager.Enemies.ForEach(enemy =>
            {
                if (enemy != this && enemy.IsAlive())
                {
                    enemy.moveSpeed *= SpeedBuffOnAlert;
                    enemy.atk = (short)(enemy.atk * AtkBuffOnAlert);
                }
            });
        }
    }

    IEnumerator ExpandDetectCircle()
    {
        Vector3 currentScale = DetectCircle.transform.localScale, finalScale = currentScale * 10f;
        float expandTime = 0.5f, count = 0;
        while (expandTime > count)
        {
            count += Time.deltaTime;
            DetectCircle.transform.localScale = Vector3.Lerp(currentScale, finalScale, count / expandTime);
            yield return null;
        }

        DetectCircle.transform.localScale = finalScale * 5f; 
    }

    public override void WriteStats()
    {
        Description = "";
        Skillset = "";
        TooltipsDescription = "Does not attack. Upon spotting the player, <color=red>alerts</color> all other enemies who haven't spotted them, increasing their ATK and movespeed.";

        base.WriteStats();
    }
}