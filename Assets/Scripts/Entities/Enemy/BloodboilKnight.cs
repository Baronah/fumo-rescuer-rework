using UnityEngine;

public class BloodboilKnight : EnemyBase
{
    [SerializeField] GameObject SkillEffect;
    public float atkAddPerEnemyKilled = 0.125f;
    public float aspdAddPerEnemyKilled = 10f;
    public float mspdAddPerEnemyKilled = 0.125f;
    public short maxStackCount = 10;

    private short stackCount = 0;

    public override void InitializeComponents()
    {
        base.InitializeComponents();
    }

    public override void FixedUpdate()
    {
        base.FixedUpdate();

        if (!ViewOnlyMode) SkillEffect.SetActive(stackCount >= (maxStackCount / 2) && IsAlive());
    }

    public void OnEnemyDeath()
    {
        if (stackCount >= maxStackCount) return;
        
        stackCount++;
        atk += (short)(bAtk * atkAddPerEnemyKilled);
        ASPD += aspdAddPerEnemyKilled;
        moveSpeed += (short)(b_moveSpeed * mspdAddPerEnemyKilled);
    }

    public override void WriteStats()
    {
        Description = "A reowned knight from the Bloodboil Knightclub. The Bloodboil Knights have long been known for their ferocity and aggression, from trainees to knight nobles. Whenever one of their brothers falls, they gain courage and combat prowess.";
        Skillset = 
            $"• Every time an enemy is defeated, ATK, ASPD and MSPD are increased. This effect stacks up to {maxStackCount} times.";
        TooltipsDescription = 
            $"Gains increased ATK, ASPD and MSPD every time an enemy is defeated. Stacks up to {maxStackCount} times.";

        base.WriteStats();
    }
}
