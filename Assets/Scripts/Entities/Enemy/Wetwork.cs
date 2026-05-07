using System.Collections;
using UnityEngine;

public class Wetwork : EnemyBase
{
    [SerializeField] private float DefPenUp_perSecond = 1f;
    [SerializeField] private float DefPenUp_cap = 50f;
    [SerializeField] private float AtkPercentageUp_perSecond = 0.05f;
    [SerializeField] private float AtkPercentageUp_cap = 2.0f;
    [SerializeField] private GameObject PS;

    private float AtkPercentageUp_count = 0, DefPenUp_count = 0;
    public void ChargeMaxAtkStack()
    {
        AtkPercentageUp_count = AtkPercentageUp_cap;
        DefPenUp_count = DefPenUp_cap;
    }

    private short AtkAdd = 0, DefPenAdd = 0;   

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
            DefPenUp_count = Mathf.Min(DefPenUp_count + DefPenUp_perSecond, DefPenUp_cap);
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

        DefPenAdd = (short) DefPenUp_count;
        defPen += DefPenAdd;

        if (sfxs[0]) sfxs[0].Play();
        yield return StartCoroutine(base.OnAttackComplete());
        
        atk -= AtkAdd;
        AtkPercentageUp_count = 0f;
    
        defPen -= DefPenAdd;
        DefPenUp_count = 0f;
    }

    public override void OnDeath()
    {
        base.OnDeath();
        PS.SetActive(false);
    }

    public override void WriteStats()
    {
        Description = "An assassin who makes up for their average skills with clever plots. They are adept at hiding in the darkness, sharpening their blades and continually increasing their ATK.";
        Skillset = "• The first attack deals greatly increased damage. This effect is slowly recharged while not attacking.";
        TooltipsDescription = "An assassin who prefers close-ranged combat. The first attack made deals greatly increased damage.";

        base.WriteStats();
    }
}