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
	BuildPlayerOptions PrepareBuild(BuildPlayerOptions options, bool includedInBuild);
	void PostprocessScene(Scene scene, bool isBuild, bool includedInBuild);
	void PreprocessBuild(BuildTarget target, string path, bool includedInBuild);
	void PostprocessBuild(BuildTarget target, string path, bool includedInBuild);

	IEnumerable<string> GetSctiptingDefineSymbols(bool includedInBuild, string parameter, string value);
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