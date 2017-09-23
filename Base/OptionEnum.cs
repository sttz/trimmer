using System;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace sttz.Workbench
{

/// <summary>
/// Option base class with an enumeration as value.
/// </summary>
public abstract class OptionEnum<TEnum> : Option, IOption<TEnum>
{
	#if UNITY_EDITOR
	public override string EditGUI(GUIContent label, string input)
	{
		var enumValue = (Enum)(object)Parse(input);
		if (!IsMask) {
			enumValue = EditorGUILayout.EnumPopup(label, enumValue);
		} else {
			enumValue = EditorGUILayout.EnumMaskField(label, enumValue);
		}
		return Save((TEnum)(object)enumValue);
	}
	#endif

	public TEnum Value { get; set; }

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

	public TEnum Parse(string input)
	{
		if (input.Length == 0)
			input = DefaultValue ?? string.Empty;

		if (string.IsNullOrEmpty(input)) {
			return default(TEnum);
		}

		try {
			return (TEnum)Enum.Parse(typeof(TEnum), input, true);
		} catch {
			Debug.LogError("Failed to parse enum value of " + typeof(TEnum).Name + ": " + input);
			return default(TEnum);
		}
	}

	public override void Load(string input)
	{
		Value = Parse(input);
	}

	public string Save(TEnum input)
	{
		return ((Enum)(object)input).ToString("F");
	}

	public override string Save()
	{
		return Save(Value);
	}
}

}

