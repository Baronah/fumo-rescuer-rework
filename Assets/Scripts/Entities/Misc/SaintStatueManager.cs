using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SaintStatueManager : MonoBehaviour
{
    public static SaintStatueManager instance;
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(this.gameObject);
        }
    }

    readonly List<SaintStatue> statues = new();
    public void RegisterStatue(SaintStatue saintStatue)
    {
        if (statues.Contains(saintStatue)) return;
        statues.Add(saintStatue);
    }

    public void UnregistStatue(SaintStatue saintStatue)
    {
        if (!statues.Contains(saintStatue)) return;
        statues.Remove(saintStatue);
    }

    IEnumerable<SaintStatue> applicableStatues;
    public void OnMedicalTileHealingReceive(float amount, float interval, bool usePercentageHealth)
    {
        applicableStatues = statues.Where(s => s && s.IsAlive() && s.environmentalTilesStandingOn.Contains(StageManager.EnvironmentType.MEDICAL_TILE));
        int count = applicableStatues.Count();
        if (count <= 0) return;
        
        float healingAdd = applicableStatues.Sum(s => s.GetBonusHealingAmount(interval));

        EntityManager.Enemies.ForEach(enemy =>
        {
            if (!enemy || !enemy.IsAlive()) return;
            float amountBase = usePercentageHealth ? amount * enemy.mHealth : amount;
            float healingTotal = healingAdd + amountBase * count;
            enemy.Heal(healingTotal, false);
        });
    }
}