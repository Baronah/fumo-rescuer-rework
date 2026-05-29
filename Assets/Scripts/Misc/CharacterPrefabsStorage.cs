using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using static PlayerManager;

[CreateAssetMenu(fileName = "CharacterPrefabsStorage", menuName = "ScriptableObjects/CharacterPrefabsStorage")]
public class CharacterPrefabsStorage : ScriptableObject
{
	public static string LevelSelectionKey = "Level_Selection";
    public AssetReference LevelSelectionScene;

	public AssetReference[] PlayerAssetReferences;
	public AssetReference[] EnemyAssetReferences;
	public AssetReference[] SceneAssetReferences;
	public string[] LevelTitles;

	public static PlayerType startingPlayer = PlayerType.MELEE;
	public static bool EnableChallengeMode = false;
	public static Dictionary<SkillTree_Manager.SkillName, SkillDataSet> Skills = new();

	public static short DifficultyLevel = 1;

    public static string GetSkillName(SkillTree_Manager.SkillName skill)
	{
		return Skills.ContainsKey(skill) ? Skills[skill].nameInString : string.Empty;
    }

    public static void ClearBattleData()
	{
		if (SaveDataManager.IsResearchUnlocked)
		{
			var skillNames = Skills.Select(s => s.Key).ToArray();
			PlayerPrefs.SetString("InventionsUsed", string.Join(" ", skillNames));
			PlayerPrefs.Save();
		}

		ClearPrebattleData();
	}

	public static void ClearPrebattleData()
	{
		DifficultyLevel = 1;
        startingPlayer = PlayerType.MELEE;
        Skills.Clear();
    }

    public static Dictionary<int, GameObject> PlayerPrefabs = new();
	public static Dictionary<int, GameObject> EnemyPrefabs = new();
}
