using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using static LevelDifficultyModifier;

public class SaveDataManager
{
    public static int Fumos => PlayerPrefs.GetInt("Fumo", 0);
    public static int AllTimeFumos => PlayerPrefs.GetInt("AllTimeFumo", 0);
    public static bool IsResearchUnlocked => PlayerPrefs.GetInt("ResearchIntroPlayed", 0) != 0;
    public static bool AllResearchesUnlocked 
        => PlayerPrefs.GetInt("TechsUnlocked", 0) != 0 
        && PlayerPrefs.GetInt("SpecsUnlocked", 0) != 0 
        && PlayerPrefs.GetInt("SensesUnlocked", 0) != 0;
    public static bool UseDVDTittleSettings => PlayerPrefs.GetInt("DVDTitle", 1) != 0;
    public static bool HasMintInTitle => PlayerPrefs.GetInt("MintInTitle", 1) != 0;
    public static bool EnableHitStop => PlayerPrefs.GetInt("EnableHitStop", 1) != 0;
    public static void SetDdTitleSettings(bool v, GameObject Title)
    {
        PlayerPrefs.SetInt("DVDTitle", v ? 1 : 0);
        Title.GetComponent<DVDLogo>().enabled = v;
    }

    public static void SetMintInTitle(bool v, GameObject Mint)
    {
        PlayerPrefs.SetInt("MintInTitle", v ? 1 : 0);
        Mint.SetActive(v);
    }

    public static void ToggleHitStop(bool v)
    {
        PlayerPrefs.SetInt("EnableHitStop", v ? 1 : 0);
    }

    public static bool IsEligibleForTechUnlock()
    {
        return GetAllTimeFumo() >= 4 && PlayerPrefs
            .GetString("CompletedLevels", "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Any(c => c.Contains("_CM_"));
    }

    public static int GetAllTimeFumo()
    {
        int aFumo = 0;
        var regex = new Regex(@"^FM-(\d+)(_CM)?_(-?\d+)$");
        string[] CompletedLevels = PlayerPrefs
            .GetString("CompletedLevels", "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var level in CompletedLevels)
        {
            var match = regex.Match(level);
            if (!match.Success) continue;

            aFumo++;                            // NM entry always counts
            if (match.Groups[2].Success)        // _CM group present
                aFumo++;
        }
        return aFumo;
    }

    public static float GetSFXVolume() => PlayerPrefs.GetFloat("SFX", 1.0f);
    public static float GetBGMVolume() => PlayerPrefs.GetFloat("BGM", 1.0f);

    public static List<string> GetAllCompletedLevels()
    {
        List<string> CompletedLevels = PlayerPrefs
            .GetString("CompletedLevels", string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        for (int i = 0; i < CompletedLevels.Count; i++)
        {
            string entry = CompletedLevels[i];
            if (!Regex.IsMatch(entry, @"_\d+$"))
                CompletedLevels[i] = entry + "_1";
        }

        PlayerPrefs.SetString("CompletedLevels", string.Join(' ', CompletedLevels));

        return CompletedLevels;
    }

    public static void SaveLevelCompletion(string LevelName, out bool IsFirsttime, out CompletionType completionType, out bool IsDifficultyHigher)
    {
        IsFirsttime = IsDifficultyHigher = false;
        completionType = CompletionType.UNCLEARED;

        short Difficulty = CharacterPrefabsStorage.DifficultyLevel;
        bool IsChallengeMode = CharacterPrefabsStorage.EnableChallengeMode;

        string NM_Prefix = LevelName;
        string CM_Prefix = LevelName + "_CM";

        // --- Load & migrate old entries (no difficulty suffix → _1) ---
        List<string> CompletedLevels = GetAllCompletedLevels();

        // --- Find existing entries ---
        string existingNM = CompletedLevels.FirstOrDefault(s =>
            s.StartsWith(NM_Prefix + "_") && !s.Contains("_CM"));

        string existingCM = CompletedLevels.FirstOrDefault(s =>
            s.StartsWith(CM_Prefix + "_"));

        // --- Helper: parse difficulty from entry ---
        int GetEntryDifficulty(string entry)
        {
            if (entry == null) return -1;
            int lastUnderscore = entry.LastIndexOf('_');
            return int.TryParse(entry.Substring(lastUnderscore + 1), out int d) ? d : -1;
        }

        // --- Apply save logic ---
        if (IsChallengeMode)
        {
            IsFirsttime = existingCM == null;
            completionType = Difficulty > (int) DiffType.Normal ? CompletionType.CHALLENGE_MODE_DIFF : CompletionType.CHALLENGE_MODE;

            if (existingCM == null)
            {
                Debug.Log("No existing CM found");

                IsDifficultyHigher = true;
                CompletedLevels.Add(CM_Prefix + "_" + Difficulty);
            }
            else if (Difficulty > GetEntryDifficulty(existingCM))
            {
                Debug.Log("Override. Existing CM diff: " + GetEntryDifficulty(existingCM) + "\nCurrent diff: " + Difficulty);

                IsDifficultyHigher = true;

                CompletedLevels.Remove(existingCM);
                CompletedLevels.Add(CM_Prefix + "_" + Difficulty);
            }
            else
                Debug.Log("Existing CM diff: " + GetEntryDifficulty(existingCM) + "\nCurrent diff: " + Difficulty);
        }
        else
        {
            IsFirsttime = existingNM == null;
            completionType = Difficulty == (int) DiffType.Observer 
                ? CompletionType.OBSERVER_NORMAL 
                : Difficulty > (int) DiffType.Normal ? CompletionType.NORMAL_DIFF 
                : CompletionType.NORMAL;

            if (existingNM == null)
            {
                IsDifficultyHigher = true;
                CompletedLevels.Add(NM_Prefix + "_" + Difficulty);
            }
            else if (Difficulty > GetEntryDifficulty(existingNM))
            {
                IsDifficultyHigher = true;

                CompletedLevels.Remove(existingNM);
                CompletedLevels.Add(NM_Prefix + "_" + Difficulty);
            }
        }

        PlayerPrefs.SetString("CompletedLevels", string.Join(' ', CompletedLevels));
    }

    /// <summary>
    /// Returns the highest difficulty cleared for both NM and CM of a given level.
    /// Returns -1 if the mode has never been cleared.
    /// </summary>
    public static (int? NM_Difficulty, int? CM_Difficulty) GetLevelHighestDifficulty(string LevelName)
    {
        string NM_Prefix = LevelName;
        string CM_Prefix = LevelName + "_CM";

        List<string> CompletedLevels = PlayerPrefs
            .GetString("CompletedLevels", string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        int? ParseDifficulty(string entry)
        {
            if (entry == null) return null;
            int lastUnderscore = entry.LastIndexOf('_');
            return int.TryParse(entry.Substring(lastUnderscore + 1), out int d) ? d : null;
        }

        string nmEntry = CompletedLevels.FirstOrDefault(s =>
            s.StartsWith(NM_Prefix + "_") && !s.Contains("_CM"));

        string cmEntry = CompletedLevels.FirstOrDefault(s =>
            s.StartsWith(CM_Prefix + "_"));

        return (ParseDifficulty(nmEntry), ParseDifficulty(cmEntry));
    }

    public enum CompletionType
    { 
        UNCLEARED,
        OBSERVER_NORMAL,
        NORMAL,
        NORMAL_DIFF,
        CHALLENGE_MODE,
        CHALLENGE_MODE_DIFF,
        CHALLENGE_MODE_DIFF_HIGH,
    };

    public static CompletionType GetLevelCompletionType(string LevelName)
    {
        var (nmDiff, cmDiff) = GetLevelHighestDifficulty(LevelName);

        if (cmDiff >= 7) return CompletionType.CHALLENGE_MODE_DIFF_HIGH;
        if (cmDiff > 1) return CompletionType.CHALLENGE_MODE_DIFF;
        if (cmDiff == 1) return CompletionType.CHALLENGE_MODE;
        if (nmDiff > 1) return CompletionType.NORMAL_DIFF;
        if (nmDiff == 1) return CompletionType.NORMAL;
        if (cmDiff == 0 || nmDiff == 0) return CompletionType.OBSERVER_NORMAL;

        return CompletionType.UNCLEARED;
    }
}