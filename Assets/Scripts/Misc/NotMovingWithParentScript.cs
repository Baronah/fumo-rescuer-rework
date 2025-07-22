using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NotMovingWithParentScript : MonoBehaviour
{
    private Vector3 initialPosition;
    private Quaternion initialRotation;

    void Start()
    {
        // Store the initial world position and rotation
        initialPosition = transform.position;
        initialRotation = transform.rotation;
    }

    void LateUpdate()
    {
        // Maintain the world position and rotation
        transform.position = initialPosition;
        transform.rotation = initialRotation;
    }
}
