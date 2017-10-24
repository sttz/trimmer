// Workaround for docfx documentation building
#if !UNITY_5 && !UNITY_2017 && !UNITY_2018
#define UNITY_EDITOR
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace sttz.Workbench {

#if UNITY_EDITOR

/// <summary>
/// Attribute indicating the given option has no runtime part
/// and is only applicable to the build process.
/// </summary>
/// <remarks>
/// Build-only options never appear in the editor profile and
/// are always removed at build-time. Build-only options' Apply
/// method is never called.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
[Conditional("UNITY_EDITOR")]
public class BuildOnlyAttribute : Attribute {}

/// <summary>
/// Attribute indicating the given option can only be used in
/// the editor and will always be removed in builds.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
[Conditional("UNITY_EDITOR")]
public class EditorOnlyAttribute : Attribute {}

/// <summary>
/// Enum indicating how and option behaves during the build process.
/// </summary>
/// <remarks>
/// Conceptually, Workbench deals with Options that are its building blocks and
/// configure some aspect of a project dynamically. That aspect of the project
/// is referred to as a «feature».
/// 
/// When building, there are a few scenarios, how an option and its feature are
/// compiled:
/// * Both are included: The build includes both the Workbench Option as well
///   as its associated feature. The Option allows to configure the build at
///   runtime.
/// * Only the feature is included: The Option only configures the feature in
///   the editor. At build-time the Option statically configures the feature
///   in the build and is itself not included. The feature cannot be configured
///   at runtime using Workbench.
/// * Both are removed: Neither the Option nor its feature are included in the
///   build and if set up correctly, the build won't contain a trace that the
///   Option or feature ever existed.
/// 
/// As an example, assume there's a platform integration that requires an API key.
/// The feature is the integration script	 and maybe some conditionally-compiled
/// snippets of code in other scripts. The Option controls the conditional compilation,
/// injects the integration script when enabled and configures the API key.
/// 
/// When building for another platform, the integration should be completely 
/// removed, leaving no unrelated code in the build. When doing a release build,
/// the API key should be baked into the build, the Option removed and no way
/// left to change the API key at runtime. In a development build, the Option
/// might be included, to be able to override the API key to one used for testing.
/// 
/// In this scenario, different build profiles would configure the build differently:
/// Profiles for other platforms would completely remove the build and feature,
/// the release build profile would only remove the Option and the development
/// profile would include both Option and feature.
/// 
/// Note that it's not possible to include the Option but not the Feature. Having
/// an <c>OptionInclusion</c> value with only the <c>Option</c> flag set is invalid.
/// </remarks>
public enum OptionInclusion
{
	/// <summary>
	/// Remove the feature and the option form the build.
	/// </summary>
	Remove = 0,

	/// <summary>
	/// Flag indicating the feature should be included.
	/// </summary>
	Feature = 1<<0,

	/// <summary>
	/// Flag indicating the option should be included.
	/// </summary>
	Option = 1<<1,

	/// <summary>
	/// Mask including both feature and option.
	/// </summary>
	FeatureAndOption = Feature | Option
}

/// <summary>
/// Helper class with extensions to check <see cref="OptionInclusion"/> flags.
/// </summary>
public static class OptionInclusionExtensions
{
	/// <summary>
	/// Check wether the <see cref="OptionInclusion"/> mask includes the <see cref="OptionInclusion.Option"/> flag.
	/// </summary>
	public static bool IncludesOption(this OptionInclusion inclusion)
	{
		return (inclusion & OptionInclusion.Option) == OptionInclusion.Option;
	}

	/// <summary>
	/// Check wether the <see cref="OptionInclusion"/> mask includes the <see cref="OptionInclusion.Feature"/> flag.
	/// </summary>
	public static bool IncludesFeature(this OptionInclusion inclusion)
	{
		return (inclusion & OptionInclusion.Feature) == OptionInclusion.Feature;
	}
}

#endif

/// <summary>
/// Define how an Option can be variant.
/// </summary>
public enum OptionVariance
{
	/// <summary>
	/// The Option is not variant. There exists only a single instance with a single value.
	/// </summary>
	Single,
	
	/// <summary>
	/// The Option is a dictionary. It has variants that differ by their parameter
	/// and the parameter is set explicitly.
	/// </summary>
	Dictionary,

	/// <summary>
	/// The Option is an array. It has variants that are ordered by an index and
	/// the parameter is automatically set.
	/// </summary>
	Array
}

/// <summary>
/// Interface for Workbench options.
/// </summary>
public interface IOption
{
	string Name { get; }
	IOption Parent { get; set; }
	string Path { get; }
	void InvalidatePathRecursive();
	string DefaultValue { get; }
	void Load(string input);
	string Save();
	int ApplyOrder { get; }
	void Apply();
	void ApplyFromRoot();
	OptionVariance Variance { get; }
	string VariantParameter { get; set; }
	string VariantDefaultParameter { get; }
	bool IsDefaultVariant { get; }
	IOption AddVariant(string parameter);
	IOption GetVariant(string parameter, bool create = true);
	void RemoveVariant(IOption option);
	IEnumerable<IOption> Variants { get; }
	bool HasChildren { get; }
	IOption GetChild(string name);
	IEnumerable<IOption> Children { get; }
	string Category { get; }

	#if UNITY_EDITOR

	bool IsAvailable(IEnumerable<BuildTarget> targets);
	bool BuildOnly { get; }
	bool EditorOnly { get; }

	int PostprocessOrder { get; }
	BuildPlayerOptions PrepareBuild(BuildPlayerOptions options, OptionInclusion inclusion);
	void PostprocessScene(Scene scene, bool isBuild, OptionInclusion inclusion);
	void PreprocessBuild(BuildTarget target, string path, OptionInclusion inclusion);
	void PostprocessBuild(BuildTarget target, string path, OptionInclusion inclusion);

	IEnumerable<string> GetSctiptingDefineSymbols(OptionInclusion inclusion, string parameter, string value);
	string EditGUI(GUIContent label, string input);

	#endif
}

/// <summary>
/// Interface for <see cref="IOption" /> subclasses that defines
/// the type of the Option value.
/// </summary>
/// <remarks>
/// This interface is not used anywhere and acts more as a convention
/// for how IOption subclasses should be implemented.
/// </remarks>
public interface IOption<TValue> : IOption
{
	/// <summary>
	/// The typed value of the Option.
	/// </summary>
	TValue Value { get; set; }
	/// <summary>
	/// Parse a string value to the Option Value's type.
	/// </summary>
	/// <remarks>
	/// If the input is empty or parsing fails, <see cref="IOption.DefaultValue"/>
	/// should be used.
	/// 
	/// The method can be called on not fully initialized and sahred Option 
	/// instances and should be careful when relying on external state.
	/// </remarks>
	TValue Parse(string input);
	/// <summary>
	/// Serialize a typed value to a string.
	/// </summary>
	/// <remarks>
	/// The string returned by Save can later be fed back to <see cref="Parse" />
	/// and should survive the round-trip without loss.
	/// 
	/// The method can be called on not fully initialized and sahred Option 
	/// instances and should be careful when relying on external state.
	/// </remarks>
	string Save(TValue input);
}

}