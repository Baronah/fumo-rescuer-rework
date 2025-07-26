
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EntityManager : MonoBehaviour
{
    public static List<EntityBase> Entities = new();
    public void OnEntitySpawn(EntityBase e) => Entities.Add(e);
    public void OnEntityDeath(EntityBase e) => Entities.Remove(e);

    private short frameCounter = 0;

    private void FixedUpdate()
    {
        frameCounter++;
        if (frameCounter >= 10)
        {
            SortLayerIndex();
            frameCounter = 0;
        }
    }

    private void SortLayerIndex()
    {
        Entities.ToList().OrderBy(e => e.transform.position.y)
            .ToList().ForEach(e => e.GetSpriteRenderer().sortingOrder = Mathf.RoundToInt(e.transform.position.y * -1));
    }
}