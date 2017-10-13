#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

using UnityEditor;

namespace sttz.Workbench
{

[ExecuteInEditMode]
[EditorOnly]
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

	public override void PostprocessScene(Scene scene, bool isBuild, bool includedInBuild, RuntimeProfile profile)
	{
		base.PostprocessScene(scene, isBuild, includedInBuild, profile);
	}
}

}
#endif