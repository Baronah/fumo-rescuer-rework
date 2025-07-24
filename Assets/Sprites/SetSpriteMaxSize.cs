using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
public class SetSpriteMaxSize : EditorWindow
{
	private string folderPath = "Assets/Sprites"; 
	private int maxSize = 512;

	[MenuItem("Tools/Set Sprite Max Size")]
	public static void ShowWindow()
	{
		GetWindow<SetSpriteMaxSize>("Set Sprite Max Size");
	}

	private void OnGUI()
	{
		GUILayout.Label("Set Max Size for Sprites", EditorStyles.boldLabel);

		folderPath = EditorGUILayout.TextField("Folder Path", folderPath);
		maxSize = EditorGUILayout.IntField("Max Size", maxSize);

		if (GUILayout.Button("Apply"))
		{
			ApplyMaxSizeToSprites();
		}
	}

	private void ApplyMaxSizeToSprites()
	{
		string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
		foreach (string guid in guids)
		{
			string assetPath = AssetDatabase.GUIDToAssetPath(guid);
			TextureImporter textureImporter = (TextureImporter)AssetImporter.GetAtPath(assetPath);

			if (textureImporter != null)
			{
				textureImporter.maxTextureSize = maxSize;
				textureImporter.SaveAndReimport();
			}
		}

		Debug.Log("Max size set to " + maxSize + " for all sprites in " + folderPath);
	}
}
#endif