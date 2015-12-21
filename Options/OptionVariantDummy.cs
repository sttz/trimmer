#if OPTION_Dummy || UNITY_EDITOR
using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace sttz.Workbench
{

public class OptionVariantDummy : OptionString
{
	public override string Name { get { return "VariantDummy"; } }

	public OptionVariantDummy()
	{
		DefaultIniValue = "";
		IsVariant = true;
		VariantDefaultParameter = "Dummy";
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