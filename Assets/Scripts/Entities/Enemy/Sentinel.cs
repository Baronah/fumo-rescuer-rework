using System.Collections;
using UnityEngine;

public class Sentinel : EnemyBase
{
    [SerializeField] private float SpeedBuffOnAlert = 1.35f;
    [SerializeField] private float AtkBuffOnAlert = 1.2f;
    [SerializeField] private GameObject DetectCircle;

    public override void FixedUpdate()
    {
        base.FixedUpdate();
        DetectCircle.gameObject.SetActive(IsAlive());
    }

    public override void OnFirsttimePlayerSpot(bool viaAlert = false)
    {
        if (sfxs[0]) sfxs[0].Play();

        base.OnFirsttimePlayerSpot();
        animator.SetTrigger("skill");
        StartCoroutine(ExpandDetectCircle());
        EntityManager.Entities.ForEach(e =>
        {
            if (e is EnemyBase enemy && enemy != this && enemy.IsAlive())
            {
                enemy.moveSpeed *= SpeedBuffOnAlert;
                enemy.atk = (short)(enemy.atk * AtkBuffOnAlert);
            }
        });
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
        TooltipsDescription = "Does not attack. Upon spotted the player, <color=red>alerts</color> all other enemies who haven't spotted them, increasing their ATK and movespeed.";

        base.WriteStats();
    }
}