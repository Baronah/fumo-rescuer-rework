using System.Linq;
using UnityEngine;

public class Zealot : EnemyBase
{
    [SerializeField] private SpriteRenderer barrierEffect;
    [SerializeField] private float barrierMaxHealth = 100f;
    [SerializeField] private float speedMultuplierOnBarrierBreak = 1.5f;
    private float barrierHealth;

    private Color barrierinitialColor;

    public override void InitializeComponents()
    {
        attackPattern = AttackPattern.MELEE;
        damageType = DamageType.PHYSICAL;

        base.InitializeComponents();
        barrierinitialColor = barrierEffect.color;
        barrierHealth = barrierMaxHealth;
    }

    public override void OnFirsttimePlayerSpot(bool viaAlert = false)
    {
        base.OnFirsttimePlayerSpot(viaAlert);
        
        if (viaAlert) TakeDamage(new DamageInstance(0, 0, Mathf.CeilToInt(barrierHealth)), this);
    }

    public override void TakeDamage(DamageInstance damage, EntityBase source)
    {
        if (source != this)
        {
            OnAttackReceive(source);
            ShowDamageDealt(damage);
        }

        if (barrierHealth > 0)
        {
            barrierHealth -= damage.TotalDamage;
            barrierEffect.color = new Color(barrierinitialColor.r, barrierinitialColor.g, barrierinitialColor.b, Mathf.Lerp(1, 0.5f, (barrierMaxHealth - barrierHealth) * 1.0f / barrierMaxHealth));
            barrierEffect.gameObject.SetActive(barrierHealth > 0);

            if (barrierHealth <= 0)
            {
                moveSpeed *= speedMultuplierOnBarrierBreak;
            }
        }
        else
            AdjustHealthOnDamageReceive(damage);

        if (barrierHealth <= 0 && damage.TotalDamage > 0) StartCoroutine(PulseSprite());
    }

    public override void WriteStats()
    {
        Description = "";
        Skillset = ".";
        TooltipsDescription = "Melee unit, attacks deal physical damage. <color=green>Has a barrier that absorbs damage</color>, and gains <color=yellow>greatly increased movespeed</color> when the barrier is destroyed. " +
            "<color=yellow>If alerted early</color>, <color=red>forfeits</color> self barrier.";

        base.WriteStats();
    }
}