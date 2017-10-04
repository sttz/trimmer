#if OPTION_ParentDummy || UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace sttz.Workbench
{

public class OptionParentDummy : OptionToggle
{
	public override string Name { get { return "ParentDummy"; } }

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

	public class OptionParentDummyChild1 : OptionString
	{
		public override string Name { get { return "Child1"; } }

		protected override void Configure()
		{
			DefaultValue = "";
		}
	}

	public class OptionParentDummyChild2 : OptionString
	{
		public override string Name { get { return "Child2"; } }

		protected override void Configure()
		{
			DefaultValue = "";
			IsVariant = true;
			VariantDefaultParameter = "Child2Variant";
		}

		public class OptionParentDummyChild3 : OptionString
		{
			public override string Name { get { return "Child3"; } }

			protected override void Configure()
			{
				DefaultValue = "";
			}
		}
	}

}

}
#endif