#if OPTION_Dummy || UNITY_EDITOR
using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace sttz.Workbench
{

public class OptionParentDummy : OptionToggle
{
	public override string Name { get { return "ParentDummy"; } }

	public OptionParentDummy()
	{
		DefaultIniValue = "";
		CreateChildren();
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

	public class OptionParentDummyChild1 : OptionString
	{
		public override string Name { get { return "Child1"; } }

		public OptionParentDummyChild1()
		{
			DefaultIniValue = "";
		}
	}

	public class OptionParentDummyChild2 : OptionString
	{
		public override string Name { get { return "Child2"; } }

		public OptionParentDummyChild2()
		{
			DefaultIniValue = "";
			IsVariant = true;
			VariantDefaultParameter = "Child2Variant";
			CreateChildren();
		}

		public class OptionParentDummyChild3 : OptionString
		{
			public override string Name { get { return "Child3"; } }

			public OptionParentDummyChild3()
			{
				DefaultIniValue = "";
			}
		}
	}

}

}
#endif