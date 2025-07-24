using UnityEngine;

public class Sentinel : EnemyBase
{
    [SerializeField] private GameObject DetectCircle;

    public override void OnFirsttimePlayerSpot(bool viaAlert = false)
    {
        base.OnFirsttimePlayerSpot();
        animator.SetTrigger("skill");
        DetectCircle.transform.localScale *= 100f;
    }

    public override void WriteStats()
    {
        Description = "";
        Skillset = "";
        TooltipsDescription = "Does not attack, instead <color=red>alerts</color> all other enemies upon spotting the player.";

        base.WriteStats();
    }
}