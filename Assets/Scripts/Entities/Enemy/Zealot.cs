using System.Collections;
using System.Linq;
using UnityEngine;

public class Zealot : EnemyBase
{
    [SerializeField] private SpriteRenderer barrierEffect;
    [SerializeField] private float barrierMaxHealth = 100f;
    [SerializeField] private float speedMultuplierOnBarrierBreak = 1.5f;
    [SerializeField] private float AspdBonusOnBarrierBreak = 50;
    [SerializeField] private short weightPenaltyOnBarrierBreak = 2;

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

    public override IEnumerator OnAttackComplete()
    {
        if (sfxs[0]) sfxs[0].Play();
        return base.OnAttackComplete();
    }

    public override void TakeDamage(DamageInstance damage, EntityBase source, ProjectileScript projectileInfo = null, bool IgnoreInvulnerability = false, bool CalculateDamage = false)
    {
        if (!this || !this.IsAlive() || (this.isInvulnerable && !IgnoreInvulnerability)) return;

        if (source != this)
        {
            OnAttackReceive(source);
            ShowDamageDealt(damage);
        }

        if (barrierHealth > 0)
        {
            if (sfxs[1]) sfxs[1].Play();
            barrierHealth -= damage.TotalDamage;
            barrierEffect.color = new Color(barrierinitialColor.r, barrierinitialColor.g, barrierinitialColor.b, Mathf.Lerp(1, 0.5f, (barrierMaxHealth - barrierHealth) * 1.0f / barrierMaxHealth));
            barrierEffect.gameObject.SetActive(barrierHealth > 0);

            if (barrierHealth <= 0)
            {
                moveSpeed *= speedMultuplierOnBarrierBreak;
                ASPD += AspdBonusOnBarrierBreak;
                weight -= weightPenaltyOnBarrierBreak;
            }
        }
        else
            AdjustHealthOnDamageReceive(damage);

        if (barrierHealth <= 0 && damage.TotalDamage > 0) StartCoroutine(PulseSprite());
        ProcessAdaption(damage, source);
    }

    public override void WriteStats()
    {
        Description = "A combatant who has once in the verge of death. The dance on the edge of life and death has given them a unique tactical system. Through Arts Resonance, they are able to create a barrier that absorbs damage.";
        Skillset = 
            "• Has a barrier that absorbs damage.\n" +
            "• Upon losing the barrier, gains increased MSPD and ASPD, but reduced weight.\n" +
            "• When alerted by a Sentinel, the barrier will be loss instantly.";
        TooltipsDescription = "<color=green>Has a barrier that absorbs damage</color>, and gains <color=yellow>increased MSPD and ASPD</color> when the barrier is destroyed. " +
            "<color=yellow>If alerted early</color>, <color=red>forfeits</color> self barrier.";

        base.WriteStats();
    }
}