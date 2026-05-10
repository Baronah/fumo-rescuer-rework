using UnityEngine;

public class BloodthirstyHeir : EnemyBase
{
    [SerializeField] private float speedMultiplierOnPlayerSpot = 3f;
    private bool mspdIncreased = false;

    public override void InitializeComponents()
    {
        attackPattern = AttackPattern.MELEE;
        damageType = DamageType.MAGICAL;

        base.InitializeComponents();
    }

    public override void TakeDamage(DamageInstance damage, EntityBase source, ProjectileScript projectileInfo = null, bool IgnoreInvulnerability = false, bool CalculateDamage = false)
    {
        base.TakeDamage(damage, source, projectileInfo, IgnoreInvulnerability);
        if (IsAlive() && !mspdIncreased)
        {
            mspdIncreased = true;
            ApplyEffect(Effect.AffectedStat.MSPD, "BLOODTHIRSTY_HEIR_SPOT_MSPD", speedMultiplierOnPlayerSpot * 100f, 9999f, true);
        }
    }

    public override void OnSuccessfulAttack(EntityBase target, DamageInstance damage)
    {
        base.OnSuccessfulAttack(target, damage);
        if (sfxs[0]) sfxs[0].Play();
    }

    public override void OnFirsttimePlayerSpot(bool viaAlert = false)
    {
        if (IsAlive() && !mspdIncreased)
        {
            mspdIncreased = true;
            ApplyEffect(Effect.AffectedStat.MSPD, "BLOODTHIRSTY_HEIR_SPOT_MSPD", speedMultiplierOnPlayerSpot * 100f, 9999f, true);
        }
        base.OnFirsttimePlayerSpot(viaAlert);
    }

    public override void WriteStats()
    {
        Description = "Creature from the unknown, adapting blood as their only source of food. " +
            "They always thirst for more blood, strengthening themselves by stealing the blood of others.";
        Skillset =
            "• MSPD greatly increased upon spotting the player or when injured for the first time.\n" +
            "• Attacks restore HP proportional to damage dealt.";
        TooltipsDescription = "Attacks deal magical damage and <color=green>heal self for a portion of damage dealt</color>. <color=yellow>Movespeed greatly increased</color> upon spotting the player or when injured for the first time.";
        
        base.WriteStats();
    }
}