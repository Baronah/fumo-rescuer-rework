using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DamagePopup : MonoBehaviour
{
    public float duration;
    public TMP_Text text;
    public Vector3 OffSet;

    Vector3 InitScale;

    Vector3 InitPosition;
    Vector3 FinalPosition;

    // Start is called before the first frame update
    void Start()
    {
        InitScale = transform.localScale;

        InitPosition = transform.localPosition;
        FinalPosition = transform.localPosition + OffSet;

        StartCoroutine(MoveUpAndShrink(duration));
    }

    // Update is called once per frame
    IEnumerator MoveUpAndShrink(float duration)
    {
        float countUp = 0;

        while (countUp < duration)
        {
            float LerpValue = countUp * 1.0f / duration;

			text.transform.position =
				new Vector3(
					Mathf.Lerp(InitPosition.x, FinalPosition.x, LerpValue),
					Mathf.Lerp(InitPosition.y, FinalPosition.y, LerpValue),
					0
				);

            transform.localScale = Vector3.Lerp(InitScale, Vector3.zero, LerpValue);

            yield return null;
            countUp += Time.deltaTime;
        }

        Destroy(gameObject);
    }
}
