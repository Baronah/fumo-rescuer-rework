using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelDifficultyModifier : MonoBehaviour
{
    [SerializeField] Button DiffUp, DiffDown;
    [SerializeField] TMP_Text ModifierText, ModifierDetail;
    [SerializeField] TMP_Text RecordText;
    [SerializeField] RectTransform Content, Background;
    
    private short MinDiff = 0, MaxDiff = 16;

    private short CurrentDifficultyLevel => CharacterPrefabsStorage.DifficultyLevel;

    bool IsObserver => CurrentDifficultyLevel <= 0;
    bool IsNormal => CurrentDifficultyLevel == 1;

    private void Start()
    {
        if (DiffUp.onClick.GetPersistentEventCount() <= 0) DiffUp.onClick.AddListener(AddDiff);
        if (DiffDown.onClick.GetPersistentEventCount() <= 0) DiffDown.onClick.AddListener(LowerDiff);
        GetDifficulties();
    }

    public void GetDifficulties()
    {
        MaxDiff = (short)(SaveDataManager.AllResearchesUnlocked ? 16 : 1);
        SetDifficultyDescription();
    }

    public void SetRecordText(string levelName)
    {
        var (NM_Difficulty, CM_Difficulty) = SaveDataManager.GetLevelHighestDifficulty(levelName);

        string NM;
        if (NM_Difficulty == null) NM = "N/A";
        else if (NM_Difficulty <= 0) NM = "OB.";
        else if (NM_Difficulty == 1) NM = "EX.";
        else NM = $"RS.{NM_Difficulty - 1}";

        string CM;
        if (CM_Difficulty == null) CM = "N/A";
        else if (CM_Difficulty <= 0) CM = "OB.";
        else if (CM_Difficulty == 1) CM = "EX.";
        else CM = $"RS.{CM_Difficulty - 1}";

        RecordText.text =
            $"<size=48>RECORDS</size>\n\n" +
            $"<color=yellow>Normal:</color> <b>{NM}</b>" +
            $"\n<color=red>C.Mode:</color>  <b>{CM}</b>\n\n";
    }

    public void AdjustMaxDiffOnCMSelect(bool IsCM)
    {
        MinDiff = (short)(IsCM ? 1 : 0);

        CharacterPrefabsStorage.DifficultyLevel = (short) Mathf.Max(MinDiff, CurrentDifficultyLevel);
        SetDifficultyDescription();
    }

    public enum DiffType 
    {
        Observer = 0,
        Normal = 1,
        EnemiesMissionTypesBuffs_1 = 2,
        EnemiesMissionTypesBuffs_2 = 3,
        EnemiesUncombatDRBuff_1 = 4,
        EnemiesUncombatDRBuff_2 = 5,
        ExtraEnemies = 6,
        EnemiesAlertBuff = 7,
        EnemiesStatusResistant = 8,
        PlayerCD_1 = 9, 
        PlayerCD_2 = 10,
        PlayerSwap_1 = 11,
        PlayerSwap_2 = 12,
        PlayerFieldDebuff_1 = 13, 
        PlayerFieldDebuff_2 = 14,
        Player1HP = 15
    };

    public void SetDifficultyDescription()
    {
        DiffUp.interactable = CurrentDifficultyLevel < MaxDiff;
        DiffDown.interactable = CurrentDifficultyLevel > MinDiff;

        ModifierText.text = GetDifficultyName(out VertexGradient textGradientOut);
        ModifierText.colorGradient = textGradientOut;

        if (IsObserver)
        {
            ModifierDetail.text =
                $"- Your units have <color=green>+50% HP</color>, <color=#ff4545>+30% ATK</color> and their special and ultimate cooldowns are reduced by 40%.\n\n" +
                $"<color=yellow>- Completing a level in this difficulty will unlock the next level but will not unlock the Challenge Mode for this level.</color>\n\n" +
                $"- Observer is not available in Challenge Mode.";
        }
        else if (IsNormal)
        {
            ModifierDetail.text = "Standard gameplay, everything is normal here.";
        }
        else
        {
            int ConvertedDiffLevel = CurrentDifficultyLevel - 1;

            ModifierDetail.text =
                $"<i>The battle receives these following changes:</i>\n\n" +
                $"All enemies have their <color=green>HP</color> and <color=#ff4545>ATK</color> increased by " +
                $"<color=green>{Mathf.CeilToInt(EnemyStatsLookup.GetEnemiesHpMultiplier() * 100)}%</color> and " +
                $"<color=#ff4545>{Mathf.CeilToInt(EnemyStatsLookup.GetEnemiesAtkMultiplier() * 100)}%</color>, respectively.";

            if (ConvertedDiffLevel == (int)DiffType.EnemiesMissionTypesBuffs_1)
            { 
                ModifierDetail.text +=
                "\n\nIn <color=yellow>survival and defense</color> mission, all enemies have <color=yellow>+20 DEF</color> and <color=#00ffbf>+10% MSPD</color>."
                +
                "\n\nIn <color=#00ffbf>rescue</color> mission, enemies have <color=#00ffff>+20 RES</color> and <color=#ffd61f>+10 ASPD</color>."
                +
                $"\n\nIn <color=red>annihilation</color> mission, enemies have <color=green>+15% HP</color> and <color=#ff4545>+10% ATK</color>.";
            }
            else if (ConvertedDiffLevel >= (int)DiffType.EnemiesMissionTypesBuffs_2)
            {
                ModifierDetail.text +=
                "\n\nIn <color=yellow>survival and defense</color> mission, all enemies have <color=yellow>+40 DEF</color> and <color=#00ffbf>+25% MSPD</color>."
                +
                "\n\nIn <color=#00ffbf>rescue</color> mission, enemies have <color=#00ffff>+40 RES</color> and <color=#ffd61f>+25 ASPD</color>."
                +
                $"\n\nIn <color=red>annihilation</color> mission, enemies have <color=green>+25% HP</color> and <color=#ff4545>+20% ATK</color>.";
            }

            if (ConvertedDiffLevel == (int)DiffType.EnemiesUncombatDRBuff_1)
                ModifierDetail.text += $"\n\nEnemies who are not in combat mode takes <color=yellow>30% less</color> physical and magical damage.";
            else if (ConvertedDiffLevel >= (int)DiffType.EnemiesUncombatDRBuff_2)
                ModifierDetail.text += $"\n\nEnemies who are not in combat mode takes <color=yellow>60% less</color> physical and magical damage.";

            if (ConvertedDiffLevel >= (int)DiffType.ExtraEnemies)
                ModifierDetail.text += "\n\nMore enemies will appear.";

            if (ConvertedDiffLevel >= (int)DiffType.EnemiesAlertBuff)
                ModifierDetail.text += "\n\nWhen patrolling enemies are attacked, they gain <color=yellow>+30% MSPD</color> when moving toward the source of the attack.";

            if (ConvertedDiffLevel >= (int) DiffType.EnemiesStatusResistant)
                ModifierDetail.text += $"\n\nMelee enemies gains <color=#ff00ff>status resistance</color>, which halves the durations of negative statuses on them.";

            if (ConvertedDiffLevel == (int) DiffType.PlayerCD_1)
                ModifierDetail.text += "\n\nYour units special's CD <color=#ff9717>+50%</color>, ultimate's CD <color=#ff9717>+25%</color>.";
            else if (ConvertedDiffLevel >= (int) DiffType.PlayerCD_2)
                ModifierDetail.text += "\n\nYour units special's CD <color=#ff9717>+100%</color>, ultimate's CD <color=#ff9717>+50%.</color>";

            if (ConvertedDiffLevel == (int) DiffType.PlayerSwap_1)
                ModifierDetail.text += "\n\nSwap cool-down increased by <color=#ff9717>50%</color>.";
            else if (CurrentDifficultyLevel >= (int) DiffType.PlayerSwap_2)
                ModifierDetail.text += "\n\nSwap cool-down increased by <color=#ff9717>100%</color>.";

            if (ConvertedDiffLevel == (int) DiffType.PlayerFieldDebuff_1)
                ModifierDetail.text += "\n\nAfter staying on the field for 10 seconds, your character gradually has <color=#5b7ccf>reduced ATK</color>, up to 80% after 40 seconds (resets on swap).";
            else if (ConvertedDiffLevel >= (int) DiffType.PlayerFieldDebuff_2)
                ModifierDetail.text += "\n\nAfter staying on the field for 5 seconds, your character gradually has reduced <color=#5b7ccf>ATK and MSPD</color>, up to 80% and 50%, respectively, after 40 seconds (resets on swap).";

            if (ConvertedDiffLevel >= (int) DiffType.Player1HP)
                ModifierDetail.text += "\n\nUpon entering the stage, <color=red>your HP becomes 1</color>. Afterward, healing effectiveness -90%, effect gradually expires over 40 seconds.";
        
            if (ConvertedDiffLevel >= (int) DiffType.ExtraEnemies)
                ModifierDetail.text += "\n\n<color=#00FFD5><i>Unveils something interesting when completed in Challenge Mode ;)</i></color>";
        }
    }

    public void SetContentSize()
    {
        Canvas.ForceUpdateCanvases();
        Background.sizeDelta = new Vector2(Background.sizeDelta.x, Mathf.Max(150, Content.sizeDelta.y + 50f));
    }

    public void AddDiff()
    {
        if (CurrentDifficultyLevel >= MaxDiff) return;

        CharacterPrefabsStorage.DifficultyLevel++;
        SetDifficultyDescription();
    }

    public void LowerDiff()
    {
        if (CurrentDifficultyLevel <= MinDiff) return;

        CharacterPrefabsStorage.DifficultyLevel--;
        SetDifficultyDescription();
    }

    string GetDifficultyName(out VertexGradient textGradientOut)
    {
        int Diff = CurrentDifficultyLevel - 1;

        // default at yellow
        textGradientOut = new VertexGradient();
        textGradientOut.topLeft = textGradientOut.bottomLeft = Color.yellow;
        textGradientOut.topRight = textGradientOut.bottomRight = new(0.76f, 0.63f, 0.22f);

        if (IsObserver)
        {
            textGradientOut.topLeft = textGradientOut.bottomLeft = new (0, 1, 0.685f);
            textGradientOut.topRight = textGradientOut.bottomRight = new(0.46f, 0.75f, 0.54f);
            return "Observer";
        }

        if (IsNormal)
        {
            textGradientOut.topLeft = textGradientOut.bottomLeft = new Color(1, 0.97f, 0.53f);
            textGradientOut.topRight = textGradientOut.bottomRight = new(0.8f, 0.8f, 0.05f);
            return "Explorer";
        }

        string DefaultText = $"Researcher\nLV. {Diff}";

        if (Diff >= (int) DiffType.PlayerCD_1)
        {
            textGradientOut.topLeft = textGradientOut.bottomLeft = Color.red;
            textGradientOut.topRight = textGradientOut.bottomRight = new(0.62f, 0.28f, 0.28f);
        }
        else if (Diff >= (int) DiffType.ExtraEnemies)
        {
            textGradientOut.topLeft = textGradientOut.bottomLeft = new(1, 0.565f, 0);
            textGradientOut.topRight = textGradientOut.bottomRight = new(0.76f, 0.63f, 0.22f);
        }

        return DefaultText;
    }
}