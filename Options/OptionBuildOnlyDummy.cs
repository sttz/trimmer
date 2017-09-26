#if OPTION_BuildOnlyDummy || UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

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

	public override void PostprocessScene(Scene scene, bool isBuild, bool includedInBuild, RuntimeProfile profile)
	{
		base.PostprocessScene(scene, isBuild, includedInBuild, profile);
	}

#endif
}

}
#endif