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
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace sttz.Trimmer.BaseOptions
{

/// <summary>
/// Option base class with an enumeration as value.
/// </summary>
/// <remarks>
/// If the given enum type has the `Flags` attribute applied, the Option
/// automatically switches to Unity's flags/mask field. You can override 
/// this by setting <see cref="IsMask"/> in <see cref="Option.Configure"/>.
/// 
/// Note that prior to Unity 2017.3 (and its EnumFlagsField method), Unity's
/// handling of flags enum is severily limited and the enum's values must
/// be increasing powers of two without gaps.
/// </remarks>
public abstract class OptionEnum<TEnum> : Option<TEnum>
{
	#if UNITY_EDITOR
	public override string EditGUI(string input)
	{
		var enumValue = (Enum)(object)Parse(input);
		if (!IsMask) {
			enumValue = EditorGUILayout.EnumPopup(enumValue);
		} else {
			#if UNITY_2017_3_OR_NEWER
			enumValue = EditorGUILayout.EnumFlagsField(enumValue);
			#else
			enumValue = EditorGUILayout.EnumMaskField(enumValue);
			#endif
		}
		return Save((TEnum)(object)enumValue);
	}
	#endif

	/// <summary>
	/// Set wether the underlying enum is a flags enum and the value should be treated as a mask.
	/// </summary>
	/// <remarks>
	/// Set this property in the Option's <see cref="Option.Configure"/> method. If not set,
	/// the value defaults to `true` when the enumeration has the `Flags` attribute applied.
	/// </remarks>
	public bool IsMask {
		get {
			if (_isMask == null) {
				_isMask = typeof(TEnum).GetCustomAttributes(typeof(FlagsAttribute), false).Any();
			}
			return _isMask ?? false;
		}
		set {
			_isMask = value;
		}
	}
	private bool? _isMask;

	override public TEnum Parse(string input)
	{
		if (string.IsNullOrEmpty(input))
			return DefaultValue;

		try {
			return (TEnum)Enum.Parse(typeof(TEnum), input, true);
		} catch {
			Debug.LogError("Failed to parse enum value of " + typeof(TEnum).Name + ": " + input);
			return DefaultValue;
		}
	}

	override public void Load(string input)
	{
		Value = Parse(input);
	}

	override public string Save(TEnum input)
	{
		return ((Enum)(object)input).ToString("F");
	}

	override public string Save()
	{
		return Save(Value);
	}
}

}

#endif
