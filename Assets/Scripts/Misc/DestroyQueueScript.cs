using System.Collections;
using UnityEngine;

public class DestroyQueueScript : MonoBehaviour
{
    [SerializeField] private float delayBeforeDestroy = 0.5f;

    private void Start()
    {
        Destroy(this.gameObject, delayBeforeDestroy);
    }
}   