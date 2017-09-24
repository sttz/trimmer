#if OPTION_EditModeDummy || UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace sttz.Workbench
{

[ExecuteInEditMode]
public class OptionEditModeDummy : OptionToggle
{
	public override string Name { get { return "EditModeDummy"; } }

	protected override void Configure()
	{
		DefaultValue = "";
		Category = "Debug";
	}

	public override void Apply()
	{
		base.Apply();
        Debug.Log("OptionEditModeDummy.Apply = " + Value);
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