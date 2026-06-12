using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class DamagePopup : MonoBehaviour
{
    public float duration;
    public TMP_Text text;
    public Vector3 OffSet;

    Vector3 InitScale;

    Vector3 InitPosition = Vector3.zero;
    Vector3 FinalPosition;

    // Start is called before the first frame update
    void Awake()
    {
        InitScale = transform.localScale;
    }

    void OnEnable()
    {
        FinalPosition = InitPosition + OffSet;
        StartCoroutine(MoveUpAndShrink(duration));
    }

    // Update is called once per frame
    IEnumerator MoveUpAndShrink(float duration)
    {
        transform.localScale = InitScale;
        text.transform.localPosition = InitPosition;

        float countUp = 0;

        while (countUp < duration)
        {
            float LerpValue = countUp * 1.0f / duration;

			text.transform.localPosition =
				new Vector3(
					Mathf.Lerp(InitPosition.x, FinalPosition.x, LerpValue),
					Mathf.Lerp(InitPosition.y, FinalPosition.y, LerpValue),
					0
				);

            transform.localScale = Vector3.Lerp(InitScale, Vector3.zero, LerpValue);

            yield return null;
            countUp += Time.deltaTime;
        }

        DamagePopupObjectPool.Instance.ReturnDamagePopup(this);
    }
}
