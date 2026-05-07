using System.Collections;
using UnityEngine;

public class Sentinel : EnemyBase
{
    [SerializeField] public float SpeedBuffOnAlert = 0.35f;
    [SerializeField] public float AtkBuffOnAlert = 0.2f;
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
                detectionRange * 2.05f,
                detectionRange * 2.05f
            );
        }
        DetectCircle.SetActive(IsAlive());
    }

    bool alarmed = false;
    public override void OnFirsttimePlayerSpot(bool viaAlert = false)
    {
        base.OnFirsttimePlayerSpot();

        if (alarmed) return;

        alarmed = true;

        animator.SetTrigger("skill");
        StartCoroutine(ExpandDetectCircle());

        if (viaAlert) return;
        if (sfxs[0]) sfxs[0].Play();

        EntityManager.Enemies.ForEach(enemy =>
        {
            if (enemy != this && enemy.IsAlive())
            {
                enemy.ApplyEffect(Effect.AffectedStat.MSPD, "SENTINEL_ALARM_MSPD_" + gameObject.GetInstanceID(), SpeedBuffOnAlert * 100f, 9999f, true);
                enemy.ApplyEffect(Effect.AffectedStat.ATK, "SENTINEL_ALARM_ATK_" + gameObject.GetInstanceID(), AtkBuffOnAlert * 100f, 9999f, true);
            }
        });
    }

    public override IEnumerator Attack()
    {
        yield break;
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

        ApplyEffect(Effect.AffectedStat.ARNG, "ALARM_DETECTION", 15000f, 9999f, false);
        CanDetectThroughWalls = true;
    }

    public override void WriteStats()
    {
        Description = "They are responsible for scouting, patrolling, and issuing early warnings to the entire squad. Once spotting intruder, the Herald will immediately issue a warning that spread to the entire army.";
        Skillset = 
            "• Does not attack, but has a large detection range.\n" +
            "• Upon spotting the player, raises an alarm that alerts all presenting enemies and increases their ATK and MSPD.\n" +
            "• Detection range becomes global after the alarm is raised.";
        TooltipsDescription = "Does not attack. Upon spotting the player, <color=red>alerts</color> all other enemies who haven't spotted them, increasing their ATK and movespeed.";

        base.WriteStats();
    }
}