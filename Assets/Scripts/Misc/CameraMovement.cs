using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    private Camera Camera;
    public Transform player;

    public float moveTime;
    public Vector3 offset;
    public int size;

    // Start is called before the first frame update
    void Start()
    {
        Camera = GetComponent<Camera>();
    }

    // Update is called once per frame
    void Update()
    {
        if (player == null) return;

        Vector3 finalPosition = player.position + offset;
        transform.position = Vector3.Lerp(transform.position, finalPosition, moveTime * Time.deltaTime);
    }
}
