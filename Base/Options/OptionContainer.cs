#if !NO_WORKBENCH || UNITY_EDITOR

using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace sttz.Workbench.BaseOptions
{

/// <summary>
/// Option that only acts as a container.
/// </summary>
/// <remarks>
/// The container Option doesn't show a GUI, ignores any input value 
/// and always saves itself as an empty string.
/// </remarks>
public abstract class OptionContainer : Option
{
	#if UNITY_EDITOR
	public override string EditGUI(string input)
	{
        GUILayout.FlexibleSpace();
		return input;
	}
	#endif

	override public void Load(string input)
	{
		// NOP
	}

	override public string Save()
	{
		return string.Empty;
	}
}

}

#endif
