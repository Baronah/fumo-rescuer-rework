using System.Collections;
using UnityEngine;

public class Archer : EnemyBase
{
    [SerializeField] private float AtkPercentageUp_perSecond = 0.05f;
    [SerializeField] private float AtkPercentageUp_cap = 2.0f;
    [SerializeField] private GameObject PS;

    private float AtkPercentageUp_count = 0;
    public void ChargeMaxAtkStack() => AtkPercentageUp_count = AtkPercentageUp_cap;

    private short AtkAdd = 0;

    public override void InitializeComponents()
    {
        base.InitializeComponents();
        ChargeMaxAtkStack();
        if (!ViewOnlyMode) StartCoroutine(IncreasesAtk());
    }

    IEnumerator IncreasesAtk()
    {
        while (true)
        {
            if (!IsAlive()) yield break;

            yield return new WaitForSeconds(1f);
            PS.SetActive(IsAlive() && AtkPercentageUp_count >= AtkPercentageUp_cap / 2);
            if (IsAttackLocked) continue;

            AtkPercentageUp_count = Mathf.Min(AtkPercentageUp_count + AtkPercentageUp_perSecond, AtkPercentageUp_cap);
        }
    }

    public override IEnumerator Attack()
    {
        yield return StartCoroutine(base.Attack());
    }

    public override IEnumerator OnAttackComplete()
    {
        AtkAdd = (short)(bAtk * AtkPercentageUp_count);
        atk += AtkAdd;
        if (sfxs[0]) sfxs[0].Play();
        yield return StartCoroutine(base.OnAttackComplete());
        atk -= AtkAdd;
        AtkPercentageUp_count = 0f;
    }

    public override void OnDeath()
    {
        base.OnDeath();
        PS.SetActive(false);
    }

    public override void WriteStats()
    {
        Description = "A crossbowman that is adept at hiding in the darkness and assassinating their targets with special bolts.";
        Skillset = "• The first attack deals greatly increased damage. This effect is slowly recharged while not attacking.";
        TooltipsDescription = "Seasoned archer that can perform long-range shots. The first attack made deals greatly increased damage.";

        base.WriteStats();
    }
}