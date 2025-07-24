using UnityEngine;

namespace DamageCalculation 
{
    public class ModifyRawDamage : IDamageStep
    {
        public void Process(EntityBase attacker, EntityBase target, DamageInstance instance)
        {

        }
    }

    public class CalculateDefense : IDamageStep
    {

        public void Process(EntityBase attacker, EntityBase target, DamageInstance instance)
        {
            float MIN_PHYSICALDMG = attacker.MIN_PHYSICAL_DMG,
                  MIN_MAGICALDMG = attacker.MIN_MAGICAL_DMG;

            int physicalDamage = (int) Mathf.Max(instance.PhysicalDamage * MIN_PHYSICALDMG, instance.PhysicalDamage - target.def);
            int magicalDamage = (int) Mathf.Max(instance.MagicalDamage * MIN_MAGICALDMG, instance.MagicalDamage * (100 - target.res) / 100);

            instance.PhysicalDamage = physicalDamage;
            instance.MagicalDamage = magicalDamage;
        }
    }
}