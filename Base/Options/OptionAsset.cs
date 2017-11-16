#if !NO_TRIMMER || UNITY_EDITOR

using System;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace sttz.Trimmer.BaseOptions
{

/// <summary>
/// Option base class with a Unity asset as value.
/// </summary>
public abstract class OptionAsset<TUnity> : Option<TUnity> where TUnity : UnityEngine.Object
{
	#if UNITY_EDITOR
	public override string EditGUI(string input)
	{
		return Save((TUnity)EditorGUILayout.ObjectField(Parse(input), typeof(TUnity), false));
	}

	override public void PostprocessScene(Scene scene, OptionInclusion inclusion)
	{
		base.PostprocessScene(scene, inclusion);

		if ((inclusion & OptionInclusion.Option) != 0
				&& scene.buildIndex == 0 
				&& ProfileContainer.Instance != null
				&& Value != null && !string.IsNullOrEmpty(GuidValue)) {
			ProfileContainer.Instance.AddReference(GuidValue, Value);
		}
	}
	#endif

	public string GuidValue { get; protected set; }

	override public TUnity Parse(string input)
	{
		if (string.IsNullOrEmpty(input))
			return DefaultValue;

		#if UNITY_EDITOR

		var path = AssetDatabase.GUIDToAssetPath(input);
		if (string.IsNullOrEmpty(path))
			return DefaultValue;

		var asset = (TUnity)AssetDatabase.LoadAssetAtPath(path, typeof(TUnity));
		return asset;

		#else
		
		return ProfileContainer.Instance.GetReference<TUnity>(input);
		
		#endif
	}

	public override void Load(string input)
	{
		GuidValue = input;
		Value = Parse(input);
	}

	override public string Save(TUnity input)
	{
		#if UNITY_EDITOR

		var path = AssetDatabase.GetAssetPath(input);
		if (string.IsNullOrEmpty(path))
			return string.Empty;

		var guid = AssetDatabase.AssetPathToGUID(path);
		return guid;

		#else

		return GuidValue;

		#endif
	}

	public override string Save()
	{
		return Save(Value);
	}
}

}

#endif
