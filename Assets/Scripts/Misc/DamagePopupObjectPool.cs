using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamagePopupObjectPool : MonoBehaviour
{
    [SerializeField] private GameObject damagePopupPrefab;
    [SerializeField] private int initialPoolSize = 10;
    public static DamagePopupObjectPool Instance { get; private set; }

    private void Start()
    {
        if (Instance == null)
        {
            Instance = this;
            for (int i = 0; i < initialPoolSize; i++)
            {
                GameObject obj = Instantiate(damagePopupPrefab);
                obj.SetActive(false);
                damagePopupPool.Enqueue(obj);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    Queue<GameObject> damagePopupPool = new();

    private DamagePopup GetDamagePopup()
    {
        if (damagePopupPool.Count > 0)
        {
            DamagePopup popup = damagePopupPool.Dequeue().GetComponent<DamagePopup>();
            popup.gameObject.SetActive(true);
            return popup;
        }
        else
        {
            GameObject obj = Instantiate(damagePopupPrefab);
            return obj.GetComponent<DamagePopup>();
        }
    }

    public void ShowDamagePopup(string msg, Vector3 position)
    {
        DamagePopup popup = GetDamagePopup();
        popup.transform.position = position;
        popup.text.text = msg;
        popup.gameObject.SetActive(true);
    }

    public void ReturnDamagePopup(DamagePopup popup)
    {
        popup.gameObject.SetActive(false);
        damagePopupPool.Enqueue(popup.gameObject);
    }
}
