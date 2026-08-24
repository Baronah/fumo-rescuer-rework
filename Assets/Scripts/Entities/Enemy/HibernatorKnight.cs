using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HibernatorKnight : EnemyBase
{
    [SerializeField] GameObject SleepEffect;
    [SerializeField] Image SleepFill;

    public float SleepCountTime = 3f;
    private float DarkZoneTimeCounter = 0f;

    bool IsSleeping = false;

    public override void InitializeComponents()
    {
        base.InitializeComponents();
    }

    public override void FixedUpdate()
    {
        base.FixedUpdate(); 
        if (!ViewOnlyMode) CountSleep();
    }

    public override void EnemyFixedBehaviors()
    {
        if (IsSleeping) return;
        base.EnemyFixedBehaviors();
    }

    void CountSleep()
    {
        if (environmentalTilesStandingOn.Contains(StageManager.EnvironmentType.DARK_ZONE))
        {
            DarkZoneTimeCounter += Time.fixedDeltaTime;
            if (!IsSleeping && DarkZoneTimeCounter >= SleepCountTime)
            {
                IsSleeping = true;
                Sleep();
            }
        }
        else
        {
            DarkZoneTimeCounter = 0f;
        }

        SleepEffect.SetActive(IsAlive() && DarkZoneTimeCounter > 0);
        if (SleepEffect.activeSelf) SleepFill.fillAmount = DarkZoneTimeCounter / SleepCountTime;
    }

    void Sleep()
    {
        ApplyEffect(Effect.AffectedStat.DEF, "HIBERNATOR_NATURAL_DEF_DEBUFF", -100f, 9999f, false);
        ApplyEffect(Effect.AffectedStat.RES, "HIBERNATOR_NATURAL_RES_DEBUFF", -100f, 9999f, false);

        CancelAttack();
        StopMovement();
        animator.SetBool("sleep", true);
    }

    public override void Move()
    {
        if (IsSleeping) return;
        base.Move();
    }

    public override IEnumerator Attack()
    {
        if (IsSleeping) yield break;
        StartCoroutine(base.Attack());
    }

    public override void OnAttackReceive(EntityBase source)
    {
        if (IsSleeping) return;
        base.OnAttackReceive(source);
    }

    [SerializeField] public float Wake_AtkBuff = 0.5f, Wake_MspdBuff = 0.3f, Wake_Buff_Duration = 3f;
    public void OnShroudedZoneExit()
    {
        RemoveEffect("HIBERNATOR_NATURAL_DEF_DEBUFF");
        RemoveEffect("HIBERNATOR_NATURAL_RES_DEBUFF");
        
        if (IsSleeping)
        {
            ApplyEffect(Effect.AffectedStat.ATK, "HIBERNATOR_WAKE_ATK_BUFF", Wake_AtkBuff * 100f, Wake_Buff_Duration, true);
            ApplyEffect(Effect.AffectedStat.MSPD, "HIBERNATOR_WAKE_MSPD_BUFF", Wake_MspdBuff * 100f, Wake_Buff_Duration, true);
            StageManager.OnEnemySpecialAbilityActivate(this);
        }

        animator.SetBool("sleep", false);
        IsSleeping = false;
    }

    public override void WriteStats()
    {
        Description = "A commonly seen burly knight whose Knightclub affiliation is unknown, considered rogue. " +
            "They're often found passed out drunk in dark alleyways, " +
            "and should be avoided as they demonstrate ferociously violent tendencies when woken up.";
        Skillset = 
            $"• After staying inside shrouded zones for more than {SleepCountTime} seconds: falls asleep, becomes unable to act and loses all DEF and RES.\n" +
            $"• Upon waking up, has increased ATK and MSPD for a period of time.";
        TooltipsDescription = 
            $"<color=yellow>Falls asleep</color> after staying inside shrouded area for more than {SleepCountTime} seconds, loses all DEF and RES and becomes unable to act.";

        base.WriteStats();
    }
}