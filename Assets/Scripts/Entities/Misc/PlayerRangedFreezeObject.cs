using System.Collections;
using UnityEngine;

public class PlayerRangedFreezeObj : MonoBehaviour
{
    [SerializeField] Vector3 TargetScale = new Vector3(300f, 300f);

    private void Start()
    {
        StartCoroutine(Grow());
    }

    IEnumerator Grow()
    {
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();

        float c = 0, d = 0.2f;
        while (c < d)
        {
            c += Time.deltaTime;
            transform.localScale = Vector3.Lerp(Vector3.zero, TargetScale, c / d);
            yield return null;
        }

        transform.localScale = TargetScale;

        c = 0;
        d = 0.15f;
        Color init = spriteRenderer.color;
        while (c < d)
        {
            c += Time.deltaTime;
            spriteRenderer.color = Color.Lerp(init, Color.clear, c / d);
            yield return null;
        }

        // Destroy the object after the animation is complete
        Destroy(gameObject);
        yield return null;
    }
}