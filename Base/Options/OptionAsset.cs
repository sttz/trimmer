// Workaround for docfx documentation building
#if !UNITY_5 && !UNITY_2017 && !UNITY_2018
#define UNITY_EDITOR
#endif

#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;

namespace sttz.Workbench.BaseOptions
{

/// <summary>
/// Option base class with a Unity asset as value.
/// </summary>
/// <remarks>
/// This option type is only available in the editor.
/// </remarks>
[Capabilities(OptionCapabilities.CanPlayInEditor)]
public abstract class OptionAsset<TUnity> : Option<TUnity> where TUnity : UnityEngine.Object
{
	public override string EditGUI(GUIContent label, string input)
	{
		return Save((TUnity)EditorGUILayout.ObjectField(label, Parse(input), typeof(TUnity), false));
	}

	override public TUnity Parse(string input)
	{
		if (string.IsNullOrEmpty(input))
			return DefaultValue;

		var path = AssetDatabase.GUIDToAssetPath(input);
		if (string.IsNullOrEmpty(path))
			return DefaultValue;

		var asset = (TUnity)AssetDatabase.LoadAssetAtPath(path, typeof(TUnity));
		return asset;
	}

	public override void Load(string input)
	{
		Value = Parse(input);
	}

	override public string Save(TUnity input)
	{
		var path = AssetDatabase.GetAssetPath(input);
		if (string.IsNullOrEmpty(path))
			return string.Empty;

		var guid = AssetDatabase.AssetPathToGUID(path);
		return guid;
	}

	public override string Save()
	{
		return Save(Value);
	}
}

}
#endif