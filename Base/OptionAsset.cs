#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;

namespace sttz.Workbench
{

/// <summary>
/// Option base class with a Unity asset as value.
/// </summary>
/// <remarks>
/// This option type is only available in the editor, all subclasses
/// need to keep <see cref="BuildOnly"/> set to <c>true</c>.
/// </remarks>
public abstract class OptionAsset<TUnity> : Option, IOption<TUnity> where TUnity : UnityEngine.Object
{
	public override string EditGUI(GUIContent label, string input)
	{
		return Save((TUnity)EditorGUILayout.ObjectField(label, Parse(input), typeof(TUnity), false));
	}

	public OptionAsset() : base()
	{
		BuildOnly = true;
	}

	public TUnity Value { get; set; }

	public TUnity Parse(string input)
	{
		if (input.Length == 0)
			input = DefaultIniValue ?? string.Empty;

		var path = AssetDatabase.GUIDToAssetPath(input);
		if (string.IsNullOrEmpty(path))
			return null;

		var asset = (TUnity)AssetDatabase.LoadAssetAtPath(path, typeof(TUnity));
		return asset;
	}

	public override void Load(string input)
	{
		Value = Parse(input);
	}

	public string Save(TUnity input)
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