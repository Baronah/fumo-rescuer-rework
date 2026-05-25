using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

public class Candleknight : EnemyBase
{
    [SerializeField] private GameObject Candle;
    private List<Candle> CandlesPlaced = new();
    public short MaxCandles = 3;
    
    public bool CanPlaceMoreCandles => CandlesPlaced.Count < MaxCandles;

    private int RemainingCandles => MaxCandles - CandlesPlaced.Count();

    [SerializeField] private float candleDropCooldown = 20f, candleDropExecuteTime = 3f;
    [SerializeField] private float cooldownTimer = 5f;

    [SerializeField] GameObject CandlesIndicator;
    [SerializeField] GameObject CandleAmmoPrefab;

    List<Image> CandleImages;

    private bool CanPlaceCandle()
    {
        return environmentalTilesStandingOn.Contains(StageManager.EnvironmentType.DARK_ZONE)
            && IsAlive()
            && CanPlaceMoreCandles
            && cooldownTimer >= candleDropCooldown
            && CanAttack
            && !IsAttackLocked;
    }

    public override void FixedUpdate()
    {
        base.FixedUpdate();
        ShowCandles();
    }

    short ShowCandlesCnt = 0;
    void ShowCandles()
    {
        ShowCandlesCnt++;
        if (ShowCandlesCnt < 5) return;
        ShowCandlesCnt = 0;

        CandlesIndicator.SetActive(IsAlive() && RemainingCandles > 0);
        if (CandlesIndicator.activeSelf)
        {
            for (int i = 0; i < CandleImages.Count; i++)
            {
                CandleImages[i].gameObject.SetActive(RemainingCandles >= (i + 1));
            }
        }
    }

    public override void EnemyFixedBehaviors()
    {
        base.EnemyFixedBehaviors();

        cooldownTimer += Time.deltaTime;
        if (CanPlaceCandle())
        {
            StartPlacingCandle();
        }
    }

    public override void InitializeComponents()
    {
        base.InitializeComponents();
        CandleImages = CandlesIndicator.GetComponentsInChildren<Image>().OrderBy(i => i.transform.position.x).ToList();
    }

    public override void Move()
    {
        if (PlacingCandle) return;
        base.Move();
    }

    public override IEnumerator Attack()
    {
        if (PlacingCandle) yield break;
        yield return StartCoroutine(base.Attack());
    }

    void StartPlacingCandle()
    {
        if (!CanPlaceCandle()) return;
        StartCoroutine(IPlaceCandle());
    }

    private bool PlacingCandle => animator.GetBool("skill");
    IEnumerator IPlaceCandle()
    {
        if (!CanPlaceCandle()) yield break;

        StopMovement();
        CancelAttack();
        animator.SetBool("skill", true);
        cooldownTimer = 0f;
        float count = 0f, duration = candleDropExecuteTime - 0.5f;
        while (count < duration)
        {
            count += Time.deltaTime;
            if (IsFrozen || IsStunned)
            {
                animator.SetBool("skill", false);
                yield break;
            }
            yield return null;
        }

        animator.SetTrigger("skill_end");
        count = 0;
        duration = 0.35f;
        while (count < duration)
        {
            count += Time.deltaTime;
            if (IsFrozen || IsStunned)
            {
                animator.SetBool("skill", false);
                yield break;
            }
            yield return null;
        }

        PlaceCandle();

        yield return new WaitForSeconds(0.15f);
        animator.SetBool("skill", false);
    }

    void PlaceCandle()
    {
        GameObject o = Instantiate(Candle, transform.position, Quaternion.identity);
        StageManager.OnEnemySpawn(o.GetComponent<EnemyBase>());
        CandlesPlaced.Add(o.GetComponent<Candle>());
    }

    public override void OnDeath()
    {
        if (!canRevive && RemainingCandles > 0) PlaceCandle();
        base.OnDeath();
    }

    public override void WriteStats()
    {
        Description = "A knight of the Nova Knightclub. " +
            "With the Candle Knight as its figurehead, " +
            "this Knightclub with a beautiful name recruits elegant knights proficient in using Arts from far and wide.";
        Skillset =
            $"• Holds {MaxCandles} candles. While in shrouded area, stops moving to channel for several seconds " +
            $"and places a candle on the spot when completed.\n" +
            "• Candle lightens the area around itself, removing the effect of shrouded zones.\n" +
            "• Candle can be frozen to temporarily disable its lighting effect throughout the duration.\n" +
            "• Drops all remaining candles upon death.";
        TooltipsDescription =
            $"Holds {MaxCandles} candles. Places a candle on shrouded zone to <color=yellow>lighten its surrounding area</color>. Drops candle upon death.";

        base.WriteStats();
    }
}