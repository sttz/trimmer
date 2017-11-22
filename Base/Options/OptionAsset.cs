//
// Trimmer Framework for Unity - https://sttz.ch/trimmer
// Copyright © 2017 Adrian Stutz
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
//

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
/// <remarks>
/// OptionAsset uses two different strategies to serialize asset references
/// in the editor and in the player.
/// 
/// In the editor, asset references are saved as GUIDs and loaded using Unity's
/// `AssetDatabase` API.
///
/// To make sure references are included in builds, the references are injected
/// into the first scene during build as part of the <see cref="ProfileContainer" />.
/// The Option then uses the GUID at runtime to look up the reference in the 
/// container.
/// 
/// > [!NOTE]
/// > The reference will only be injected into the build when the Option is
/// > included. If only the Option's associated feature is included, the
/// > reference won't be injected and it's up to the subclass to inject the
/// > reference as needed.
/// </remarks>
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

		// Only include reference when Opotion is included,
		// we're building the first scene and a reference is set
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
