using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class UI_SuperConduct : MonoBehaviour
{
    [SerializeField] Image GlowPart;
    EntityBase Target;
    string Key;

    bool Initialized = false;
    public void Inititialize(EntityBase target, string key)
        => StartCoroutine(WaitOneFrameThenInitialize(target, key));

    IEnumerator WaitOneFrameThenInitialize(EntityBase target, string key)
    {
        yield return null;
        Target = target;
        Key = key;

        Initialized = true;
    }

    bool DebuffInEffect => Target.IsAlive() && Target.DefDebuffs.ContainsKey(Key) && Target.DefDebuffs[Key].IsInEffect;
    private void Update()
    {
        if (!Initialized) return;
        if (!DebuffInEffect) Destroy(gameObject);
    }
}