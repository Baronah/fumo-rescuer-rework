using UnityEngine;

public class Gloompincer : EnemyBase
{
    [SerializeField] public float shroudedMspdBuff = 0.5f;
    [SerializeField] public float shroudedAspdBuff = 10f;

    public void OnShroudedZoneEnter()
    {
        damageType = DamageType.MAGICAL;
        ApplyEffect(Effect.AffectedStat.MSPD, "GLOOMPINCER_SHROUDED_MSPPD_BUFF", shroudedMspdBuff * 100, 9999f, true);
        ApplyEffect(Effect.AffectedStat.ASPD, "GLOOMPINCER_SHROUDED_ASPD_BUFF", shroudedAspdBuff, 9999f, false);
        StageManager.OnEnemySpecialAbilityActivate(this);
    }

    public void OnShroudedZoneExit()
    {
        damageType = DamageType.PHYSICAL;
        RemoveEffect("GLOOMPINCER_SHROUDED_MSPPD_BUFF");
        RemoveEffect("GLOOMPINCER_SHROUDED_ASPD_BUFF");
        StageManager.OnEnemySpecialAbilityStop(this);
    }

    public override void WriteStats()
    {
        Description = "A Gloompincer that escaped from the arenas and adapted to life in the city's sewers. It has grown used to the darkness and become much more savage.";
        Skillset =
            "• Immune to the effect of shrouded zones.\n" +
            "• While inside shrouded areas, MSPD greatly increases, and damage type changes to magical.";
        TooltipsDescription = "While inside shrouded zones, " +
            "<color=yellow>MSPD greatly increases</color> and attacks deal magical damage.";

        base.WriteStats();
    }
}