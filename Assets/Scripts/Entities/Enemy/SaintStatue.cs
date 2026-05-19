using System.Collections;
using UnityEngine;

public class SaintStatue : EnemyBase
{
    [SerializeField] GameObject HealEffect;
    [SerializeField] private float DefBuffFlat = 25f, ResBuffFlat = 15f, SelfHpHealingBonus = 0.005f;
    [SerializeField] private float HealOnDeathPercentage = 0.35f, DefFlatBuffOnDeathPercentage = 15f;

    public override void Move()
    {
        
    }

    public override void OnAttackReceive(EntityBase source)
    {

    }

    public override void HandleSpriteFlipping()
    {
        
    }

    public override void OnFirsttimePlayerSpot(bool viaAlert = false)
    {

    }

    public override IEnumerator Attack()
    {
        yield break;
    }

    string DefBuffID => "STATUE_DEF_BUFF_" + gameObject.GetInstanceID();
    string ResBuffID => "STATUE_RES_BUFF_" + gameObject.GetInstanceID();

    public override void FixedUpdate()
    {
        HealEffect.SetActive(environmentalTilesStandingOn.Contains(StageManager.EnvironmentType.MEDICAL_TILE) && IsAlive());
        base.FixedUpdate();
    }

    short cnt = 30;
    public override void EnemyFixedBehaviors()
    {
        cnt++;
        if (cnt < 30) return;

        cnt = 0;
        if (IsAlive())
        {
            EntityManager.Enemies.ForEach(enemy =>
            {
                if (!enemy || !enemy.IsAlive()) return;
                enemy.ApplyEffect(Effect.AffectedStat.DEF, DefBuffID, DefBuffFlat, 9999f, false);
                enemy.ApplyEffect(Effect.AffectedStat.RES, ResBuffID, ResBuffFlat, 9999f, false);
            });
        }
    }

    public void OnMedicalTileHealingReceive(float amount)
    {
        EntityManager.Enemies.ForEach(enemy =>
        {
            if (!enemy || !enemy.IsAlive() || enemy == this) return;
            Heal(amount + mHealth * SelfHpHealingBonus, enemy, false, false);
        });
    }

    public override void OnDeath()
    {
        base.OnDeath();
        EntityManager.Players.ForEach(player =>
        {
            if (!player || !player.IsAlive()) return;
            Heal(player.mHealth * 0.35f, player);
            player.ApplyEffect(Effect.AffectedStat.DEF, "STATUE_DESTROY_DEF_BUFF", 15f, 9999f, false);
        });

        EntityManager.Enemies.ForEach(enemy =>
        {
            if (!enemy || !enemy.IsAlive()) return;
            enemy.RemoveEffect(DefBuffID);
            enemy.RemoveEffect(ResBuffID);
        });
    }

    public override void WriteStats()
    {
        Description = "Church statue that has been abandoned for a very long time. " +
            "Though can no longer distinguish good from evil, it never forgets its duty, " +
            "continuing to bestow blessings upon those who seek for them.";
        Skillset =
            "• Does not move and attack, immunes to crowd-controls, and does not count toward battle progress.\n" +
            "• Increases the DEF and RES of all enemies while present.\n" +
            "• While standing on a <color=green>Medical Tile</color>, transfers its healing effect to all presenting enemies on the map.\n" +
            "• Upon defeat, heals your units once and grants them a permanent DEF buff.";
        TooltipsDescription = "<color=yellow>Increases the DEF and RES of enemies on the map</color>. " +
            "While standing on a <color=green>Medical Tile</color>, continuously heals all presenting enemies.";

        base.WriteStats();
    }
}