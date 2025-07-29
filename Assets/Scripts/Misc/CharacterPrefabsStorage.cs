using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(fileName = "CharacterPrefabsStorage", menuName = "ScriptableObjects/CharacterPrefabsStorage")]
public class CharacterPrefabsStorage : ScriptableObject
{
	public AssetReference[] PlayerAssetReferences;
	public AssetReference[] EnemyAssetReferences;

	public static Dictionary<int, GameObject> PlayerPrefabs = new();
	public static Dictionary<int, GameObject> EnemyPrefabs = new();
}
