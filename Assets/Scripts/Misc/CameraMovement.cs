using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    public float moveTime;
    public Vector3 offset;
    
    private float size;
    private Camera camera;

    private void Start()
    {
        camera = GetComponent<Camera>();
        size = camera.orthographicSize;
    }

    public void UpdatePlayerMovement(Transform targetTransform)
    {
        if (targetTransform == null) return;

        Vector3 finalPosition = targetTransform.position + offset;
        transform.position = Vector3.Lerp(transform.position, finalPosition, moveTime * Time.deltaTime);
        camera.orthographicSize = Mathf.Lerp(camera.orthographicSize, size, moveTime * Time.deltaTime);
    }

    public IEnumerator MoveShowcases(float showcaseSize, Transform[] points, float[] waittimes)
    {
        yield return new WaitForSeconds(waittimes[0]);

        bool scaleToSize = true;

        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] == null) continue;
            Vector3 targetPosition = points[i].position + offset;
            float elapsedTime = 0f;
            while (elapsedTime < moveTime)
            {
                if (scaleToSize) camera.orthographicSize = Mathf.Lerp(camera.orthographicSize, showcaseSize, elapsedTime / moveTime);
                transform.position = Vector3.Lerp(transform.position, targetPosition, elapsedTime / moveTime);
                elapsedTime += Time.deltaTime;
                yield return null;
            }

            if (scaleToSize)
            {
                camera.orthographicSize = showcaseSize;
                scaleToSize = false;
            }
            transform.position = targetPosition;
            yield return new WaitForSeconds(waittimes[i]);
        }

        yield return null;
    }
}
