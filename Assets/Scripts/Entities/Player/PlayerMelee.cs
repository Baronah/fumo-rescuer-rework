using System.Collections;
using UnityEngine;

public class PlayerMelee : PlayerBase
{
    [SerializeField] private GameObject IllusionPrefab;
    [SerializeField] private float DashSpeed = 3500f;
    [SerializeField] private float DashDuration = 0.5f;
    [SerializeField] private float DashCooldown = 6f;

    [SerializeField] private GameObject SkillEffect;
    [SerializeField] private float SkillCooldown = 30f;
    [SerializeField] private float SkillDuration = 7f;
    [SerializeField] private float BurstHeal_HpPercentage = 0.35f;
    [SerializeField] private float HealPerSecond_HpPercentage = 0.05f;
    [SerializeField] private float DefBoost = 0.5f;
    [SerializeField] private float ResBoost = 10;
    [SerializeField] private float AtkBoost = 0.25f;
    [SerializeField] private float SpeedBoost = 0.35f;

    private bool IsSkillActive = false, CanUseSkill = true, CanUseDash = true;
    private short atkAdd, defAdd, resAdd, speedAdd;

    public override void FixedUpdate()
    {
        base.FixedUpdate();
        SkillEffect.SetActive(IsSkillActive && IsAlive());
    }

    protected override void GetControlInputs()
    {
        if (!IsAlive()) return;

        if (Input.GetKeyDown(stageManager.AttackKey))
        {
            AttackCoroutine = StartCoroutine(Attack());
        }
        else if (Input.GetKeyDown(stageManager.SkillKey) && CanUseSkill)
        {
            StartCoroutine(ActivateSkill());
        }
        else if (Input.GetKeyDown(stageManager.SpecialKey) && CanUseDash)
        {
            StartCoroutine(Dash());
        }
        else
        {
            Move();
        }
    }

    IEnumerator DashLockout()
    {
        CanUseDash = false;
        StartCoroutine(stageManager.SpecialCooldown(DashCooldown));
        yield return new WaitForSeconds(DashCooldown);
        CanUseDash = true;
    }

    IEnumerator SkillLockout()
    {
        CanUseSkill = false;
        StartCoroutine(stageManager.SkillCooldown(SkillCooldown));
        yield return new WaitForSeconds(SkillCooldown);
        CanUseSkill = true;
    }

    IEnumerator Dash()
    {
        if (!CanUseDash) yield break;

        StartCoroutine(DashLockout());
        StartCoroutine(StartMovementLockout(DashDuration));
        StartCoroutine(StartAttackLockout(DashDuration));

        isInvulnerable = true;
        float moveHorizontal = Input.GetAxis("Horizontal");
        float moveVertical = Input.GetAxis("Vertical");

        if (moveHorizontal == 0 && moveVertical == 0)
        {
            moveHorizontal = spriteRenderer.flipX ? -1 : 1;
        }

        float dashTime = 0f;
        while (dashTime < DashDuration)
        {
            var movementInputs = new Vector2(moveHorizontal, moveVertical).normalized;

            rb2d.velocity = CalculateMovement(movementInputs, DashSpeed + moveSpeed * 5f);

            animator.SetFloat("move", Mathf.Abs(moveHorizontal) + Mathf.Abs(moveVertical));

            GameObject Illusion = Instantiate(IllusionPrefab, transform.position, Quaternion.identity);
            SpriteRenderer IllusionSpriteRenderer = Illusion.GetComponentInChildren<SpriteRenderer>();
            IllusionSpriteRenderer.sprite = spriteRenderer.sprite;
            IllusionSpriteRenderer.flipX = spriteRenderer.flipX;
            IllusionSpriteRenderer.color = new Color(1, 1, 1, 0.5f);
            Destroy(Illusion, 0.2f);

            dashTime += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        rb2d.velocity = Vector2.zero;

        yield return new WaitForSeconds(DashDuration);
        isInvulnerable = false;
    }

    IEnumerator ActivateSkill()
    {
        if (!IsAlive() || IsSkillActive || !CanUseSkill) yield break;
        StartCoroutine(SkillLockout());

        IsSkillActive = true;
        Heal(mHealth * BurstHeal_HpPercentage);
        atkAdd = (short) (bAtk * AtkBoost);
        atk += atkAdd;
        defAdd = (short) (bDef * DefBoost);
        def += defAdd;
        resAdd = (short) (ResBoost);
        res += resAdd;
        speedAdd = (short) (b_moveSpeed * SpeedBoost);
        moveSpeed += speedAdd;

        float c = 0, t = 0, d = SkillDuration;
        while (c < d)
        {
            c += Time.deltaTime;
            t += Time.deltaTime;

            if (t >= 1.0f)
            {
                Heal(mHealth * HealPerSecond_HpPercentage);
                t = 0;
            }
            yield return null;
        }

        Heal(mHealth * HealPerSecond_HpPercentage);
        atk -= atkAdd;
        def -= defAdd;
        res -= resAdd;
        moveSpeed -= speedAdd;
        IsSkillActive = false;
    }

    public override PlayerTooltipsInfo GetPlayerTooltipsInfo()
    {
        var info = base.GetPlayerTooltipsInfo();

        info.AttackText = $"Performs an attack that deals {atk} {damageType.ToString().ToLower()} damage to all enemies within range.";

        info.SkillName = "Juggernaunt";
        info.SkillText = 
            $"Immediately heals self for {BurstHeal_HpPercentage * 100}% max HP. In the next {SkillDuration} seconds: " +
            $"ATK +{AtkBoost * 100}%, DEF +{DefBoost * 100}%, RES +{ResBoost}, MSPD +{SpeedBoost * 100}% and " +
            $"regenerate {HealPerSecond_HpPercentage * 100}% max HP every second. {SkillCooldown}s cooldown.";

        info.SpecialName = "Evasion";
        info.SpecialText = 
            $"Dash a short distance toward the movement direction and briefly becomes invulnerable during the process. {DashCooldown}s cooldown.";
        
        return info;
    }
}