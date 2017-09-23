#if OPTION_BuildOnlyDummy || UNITY_EDITOR
using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace sttz.Workbench
{

public class OptionBuildOnlyDummy : OptionToggle
{
	public override string Name { get { return "BuildOnlyDummy"; } }

	public OptionBuildOnlyDummy()
	{
		DefaultValue = "";
		Category = "Debug";
		BuildOnly = true;
	}

	public override void Apply()
	{
		//
	}

#if UNITY_EDITOR

	public override void Remove()
	{
		//
	}

#endif
}

}
#endif