using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class OneDirectionalPassage : MonoBehaviour
{
    public enum TargetDirection
    {
        NONE,
        UP,
        DOWN,
        LEFT,
        RIGHT
    }

    public TargetDirection AllowPassingThroughDirection;

    private Collider2D selfCollider;

    private void Start()
    {
        selfCollider = GetComponents<Collider2D>().FirstOrDefault(c => !c.isTrigger);
    }

    private void CheckDirection(Collider2D collider)
    {
        if (!selfCollider || !collider.GetComponent<EntityBase>()) return;

        // Try to get velocity from Rigidbody2D
        var rb = collider.attachedRigidbody;
        if (!rb) return;

        Physics2D.IgnoreCollision(selfCollider, collider, true);
    }

    bool IsCorrectDirection(Vector3 direction, TargetDirection targetDirection)
    {
        return targetDirection switch
        {
            TargetDirection.UP => direction.y > 0.1f,
            TargetDirection.DOWN => direction.y < -0.1f,
            TargetDirection.RIGHT => direction.x > 0.1f,
            TargetDirection.LEFT => direction.x < -0.1f,
            _ => false,
        };
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision || !collision.gameObject || collision.isTrigger) return;
        CheckDirection(collision);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (!collision || !collision.gameObject || collision.isTrigger) return;
        CheckDirection(collision);
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!collision || !collision.gameObject || collision.isTrigger) return;
        Physics2D.IgnoreCollision(selfCollider, collision, false);
    }
}