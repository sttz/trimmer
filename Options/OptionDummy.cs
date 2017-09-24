#if OPTION_Dummy || UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace sttz.Workbench
{

public class OptionDummy : OptionToggle
{
	public override string Name { get { return "Dummy"; } }

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

	public override void PostprocessScene(Scene scene, bool isBuild, bool includedInBuild, Profile profile)
	{
		base.PostprocessScene(scene, isBuild, includedInBuild, profile);
	}

#endif
}

}
#endif