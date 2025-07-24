using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageInstance
{ 
    public int PhysicalDamage { get; set; }
    
    public int MagicalDamage { get; set; }
    
    public int TrueDamage { get; set; }

    public int TotalDamage => PhysicalDamage + MagicalDamage + TrueDamage;

    public DamageInstance()
    {
        PhysicalDamage = 0;
        MagicalDamage = 0;
        TrueDamage = 0;
    }

    public DamageInstance(int physical, int magical, int trueDamage)
    {
        PhysicalDamage = physical;
        MagicalDamage = magical;
        TrueDamage = trueDamage;
    }
}

public interface IDamageStep
{
    void Process(EntityBase attacker, EntityBase target, DamageInstance instance);
}

public class DamagePipeline
{
    public EntityBase attacker, target;
    public DamageInstance instance;
    private readonly List<IDamageStep> steps = new();

    public void Add(IDamageStep step) => steps.Add(step);
    
    public DamageInstance Calculate()
    {
        foreach (var step in steps)
        {
            step.Process(attacker, target, instance);
        }

        return instance;
    }
}