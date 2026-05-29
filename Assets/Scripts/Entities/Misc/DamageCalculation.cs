using UnityEngine;

namespace DamageCalculation 
{
    public class ModifyRawDamage : IDamageStep
    {
        public void Process(EntityBase attacker, EntityBase target, DamageInstance instance)
        {
            if (target.damageAmplify != 0) instance.Multiply(1.0f + target.damageAmplify / 100f);
            if (target.damageReduction != 0)
            {
                float multiplier = 1.0f - target.damageReduction / 100f;
                instance.Multiply(multiplier, multiplier, 1f);
            }

            if (target is EnemyBase e && !e.HasSpottedPlayer)
            {
                float multiplier = 1.0f - e.DamageReductionOutsideCombat;
                instance.Multiply(multiplier, multiplier, 1f);
            }
        }
    }

    public class AccountSkillTreeEffects : IDamageStep
    {
        public void Process(EntityBase attacker, EntityBase target, DamageInstance instance)
        {
            if (attacker == target) return;

            float distance = 0;
            bool calculatedDistance = false;

            PlayerBase playerBase = attacker as PlayerBase;
            if (playerBase)
            {
                if (playerBase.Skills.Contains(SkillTree_Manager.SkillName.EQUIPMENT_BLADE))
                {
                    float maxiumDamageThreshold = 80f;
                    float targetMissingHealth = target.GetMissinghealthPercentage();

                    float damageMultiply = Mathf.Lerp(1.0f, 1.5f, targetMissingHealth / maxiumDamageThreshold);

                    instance.Multiply(damageMultiply);
                }
                
                if (playerBase.Skills.Contains(SkillTree_Manager.SkillName.EQUIPMENT_SCOPE))
                {
                    distance = Vector3.Distance(attacker.transform.position, target.transform.position);
                    calculatedDistance = true;
                    float maxDistance = Mathf.Min(Mathf.Max(350, attacker.attackRange * 1.2f), 1000);
                    if (distance <= 100) distance = 0;

                    float damageMultiply = Mathf.Lerp(1.0f, 1.5f, distance * 1.0f / maxDistance);
                    instance.Multiply(damageMultiply);
                }
            }

            if (CharacterPrefabsStorage.Skills.ContainsKey(SkillTree_Manager.SkillName.ABSOLUTISM) 
                && target.attackPattern == EntityBase.AttackPattern.MELEE)
            {
                if (!calculatedDistance && attacker)
                {
                    distance = Vector3.Distance(attacker.transform.position, target.transform.position);
                    calculatedDistance = true;
                }

                // If the target is out of melee range, reduce the physical and magical damage by 50%
                if (calculatedDistance && distance > target.attackRange + 50f) instance.Multiply(0.5f, 0.5f, 1f);
            }
        }
    }

    public class CalculateDefense : IDamageStep
    {
        public void Process(EntityBase attacker, EntityBase target, DamageInstance instance)
        {
            if (target.IsPhysicalImmune) instance.PhysicalDamage = 0;
            if (target.IsMagicalImmune) instance.MagicalDamage = 0;

            float MIN_PHYSICALDMG = attacker.MIN_PHYSICAL_DMG,
                  MIN_MAGICALDMG = attacker.MIN_MAGICAL_DMG;

            float physicalDamage = 
                    Mathf.Max(
                        instance.PhysicalDamage * MIN_PHYSICALDMG, 
                        instance.PhysicalDamage * (100 - ((target.def - attacker.defIgn) * (100 - attacker.defPen) / 100)) / 100
                    );
            float magicalDamage = 
                    Mathf.Max(
                        instance.MagicalDamage * MIN_MAGICALDMG, 
                        instance.MagicalDamage * (100 - ((target.res - attacker.resIgn) * (100 - attacker.resPen) / 100)) / 100
                    );

            instance.PhysicalDamage = instance.PhysicalDamage > 0 && physicalDamage <= 0 ? 1 : physicalDamage;
            instance.MagicalDamage = instance.MagicalDamage > 0 && magicalDamage <= 0 ? 1 : magicalDamage;
        }
    }

    public class AccountDodges : IDamageStep
    {
        public void Process(EntityBase attacker, EntityBase target, DamageInstance instance)
        {
            if (target.PhysicalDodgeChance <= 0 && target.MagicalDodgeChance <= 0) return;
            if (target.IsStunned || target.IsFrozen) return;

            if (instance.PhysicalDamage > 0 && target.PhysicalDodgeChance > 0)
            {
                bool Dodged = Random.Range(0, 100) < target.PhysicalDodgeChance;
                if (Dodged) instance.PhysicalDamage = 0;
            }

            if (instance.MagicalDamage > 0 && target.MagicalDodgeChance > 0)
            {
                bool Dodged = Random.Range(0, 100) < target.MagicalDodgeChance;
                if (Dodged) instance.MagicalDamage = 0;
            }

            instance.IsDodged = instance.TotalDamage == 0;
        }
    }
}