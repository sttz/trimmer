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
public abstract class OptionEnum<TEnum> : Option<TEnum>
{
	#if UNITY_EDITOR
	public override string EditGUI(string input)
	{
		var enumValue = (Enum)(object)Parse(input);
		if (!IsMask) {
			enumValue = EditorGUILayout.EnumPopup(enumValue);
		} else {
			enumValue = EditorGUILayout.EnumMaskField(enumValue);
		}
		return Save((TEnum)(object)enumValue);
	}
	#endif

	private bool? _isMask;
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
