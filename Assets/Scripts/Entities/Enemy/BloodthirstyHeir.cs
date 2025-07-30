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

    public override void TakeDamage(DamageInstance damage, EntityBase source)
    {
        base.TakeDamage(damage, source);
        if (IsAlive() && !mspdIncreased)
        {
            mspdIncreased = true;
            moveSpeed *= speedMultiplierOnPlayerSpot;
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
            moveSpeed *= speedMultiplierOnPlayerSpot;
        }
        base.OnFirsttimePlayerSpot(viaAlert);
    }

    public override void WriteStats()
    {
        Description = "A bloodthirsty heir is a relentless pursuer, driven by a desire for vengeance.";
        Skillset = "They are known for their speed and ferocity in combat.";
        TooltipsDescription = "Melee unit, attacks deal magical damage and <color=green>heal self for a portion of damage dealt</color>. <color=yellow>Movespeed greatly increased</color> upon spotting the player or when injured for the first time.";
        
        base.WriteStats();
    }
}