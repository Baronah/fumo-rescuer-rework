using UnityEngine;

public class GlobalStageManager
{
    private static float SFX, BGM;
    public static float GetSFX() { return SFX; }
    public static float GetBGM() { return BGM; }

    public static KeyCode AttackKey;
    public static KeyCode SpecialKey;
    public static KeyCode SkillKey;
    public static KeyCode PlayerSwapKey;
    public static KeyCode SlowKey;
    public static KeyCode ViewInfoKey;
    public static KeyCode SwapInfoKey;
    public static KeyCode ViewMapKey;

    public static void OnStageStart()
    {
        Cursor.visible = false;

        SFX = SaveDataManager.GetSFXVolume();
        BGM = SaveDataManager.GetBGMVolume();

        AttackKey = InputManager.Instance.AttackKey;
        SpecialKey = InputManager.Instance.SpecialKey;
        SkillKey = InputManager.Instance.SkillKey;
        PlayerSwapKey = InputManager.Instance.PlayerSwapKey;
        SlowKey = InputManager.Instance.SlowKey;
        ViewInfoKey = InputManager.Instance.ViewInfoKey;
        SwapInfoKey = InputManager.Instance.SwapInfoKey;
        ViewMapKey = InputManager.Instance.ViewMapKey;
    }

    public static void ResumeNormalVariables()
    {
        Cursor.visible = true;
        Time.timeScale = 1f;
    }
}