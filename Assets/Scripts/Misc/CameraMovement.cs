using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    public float moveTime;
    public Vector3 offset;
    public int size;

    public void UpdatePlayerMovement(Transform targetTransform)
    {
        if (targetTransform == null) return;

        Vector3 finalPosition = targetTransform.position + offset;
        transform.position = Vector3.Lerp(transform.position, finalPosition, moveTime * Time.deltaTime);
    }
}
