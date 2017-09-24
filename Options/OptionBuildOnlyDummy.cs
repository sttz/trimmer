#if OPTION_BuildOnlyDummy || UNITY_EDITOR
using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace sttz.Workbench
{

[BuildOnly]
public class OptionBuildOnlyDummy : OptionToggle
{
	public override string Name { get { return "BuildOnlyDummy"; } }

	protected override void Configure()
	{
		DefaultValue = "";
		Category = "Debug";
	}

	public override void Apply()
	{
		base.Apply();
	}

#if UNITY_EDITOR

	public override void Remove()
	{
		base.Remove();
	}

#endif
}

}
#endif