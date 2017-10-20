// Workaround for docfx documentation building
#if !UNITY_5 && !UNITY_2017 && !UNITY_2018
#define UNITY_EDITOR
#endif

using System;
using System.Collections.Generic;
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
public class BuildOnlyAttribute : Attribute {}

/// <summary>
/// Attribute indicating the given option can only be used in
/// the editor and will always be removed in builds.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class EditorOnlyAttribute : Attribute {}

#endif

/// <summary>
/// Interface for Workbench options.
/// </summary>
/// <remarks>
/// The simplest is to extend the <see cref="Option"/> subclass for 
/// the required primitive type or to extend the <see cref="Option"/>
/// class directly if a base class for the required type doesn't exist.
/// </remarks>
public interface IOption
{
	/// <summary>
	/// Name of the option.
	/// </summary>
	/// <remarks>
	/// The name of the option is used in the ini files as well as for
	/// scripting define symbols. Note that the name therefore needs to
	/// be a valid C# identifier.
	/// </remarks>
	string Name { get; }
	IOption Parent { get; set; }
	string Path { get; }
	void InvalidatePathRecursive();
	/// <summary>
	/// The default value for the option, used if there is no value defined
	/// for the option in the ini file.
	/// </summary>
	string DefaultValue { get; }
	/// <summary>
	/// Parse a string and set the option's Value.
	/// </summary>
	void Load(string input);
	/// <summary>
	/// Save the option's current value to a string value.
	/// </summary>
	string Save();
	/// <summary>
	/// Control the order options get applied (lower values get applied first).
	/// </summary>
	int ApplyOrder { get; }
	/// <summary>
	/// Apply the option's current value to the scene.
	/// </summary>
	/// <remarks>
	/// This is called whenever the option's value changed and also after 
	/// a new scene has loaded.
	/// </remarks>
	void Apply();
	void ApplyFromRoot();

	/// <summary>
	/// Wether the option can have variants, differentiated by the variant
	/// parameter.
	/// </summary>
	/// <remarks>
	/// Additional Option instances are automatically created for each variant
	/// parameter. At least one instance with the default variant parameter is
	/// guaranteed to exist.
	/// </remarks>
	bool IsVariant { get; }
	bool IsArrayVariant { get; }
	/// <summary>
	/// The variant parameter of a variant option. Not used if <see cref="IsVariant"/>
	/// is false.
	/// </summary>
	string VariantParameter { get; set; }

	/// <summary>
	/// The default variant parameter. Not used if <see cref="IsVariant"/> is false.
	/// An instance with the default variant parameter is guaranteed to exist, while
	/// additional option instances are created for each unique additional 
	/// variant parameter.
	/// </summary>
	string VariantDefaultParameter { get; }
	/// <summary>
	/// Wether this is the main variant option with the default variant parameter.
	/// Not used if <see cref="IsVariant"/> is false.
	/// </summary>
	/// <remarks>
	/// The main variant option contains all other variants. <see cref="GetVariant"/>
	/// can only be used on the main variant option.
	/// </remarks>
	bool IsDefaultVariant { get; }

	/// <summary>
	/// Add a new variant to the main variant option. Only used if <see cref="IsVariant"/>
	/// is true and can only be called on the main variant option (<see cref="IsDefaultVariant"/>).
	/// </summary>
	IOption AddVariant(string parameter);
	/// <summary>
	/// Get a variant option by its parameter. Only used if <see cref="IsVariant"/>
	/// is true and can only be called on the main variant option (<see cref="IsDefaultVariant"/>).
	/// </summary>
	IOption GetVariant(string parameter, bool create = true);
	void RemoveVariant(IOption option);
	/// <summary>
	/// Enumerate all variants of this option.
	/// </summary>
	IEnumerable<IOption> Variants { get; }

	/// <summary>
	/// Types of the option's child options.
	/// </summary>
	Type[] ChildOptionTypes { get; }
	/// <summary>
	/// Wether the option has child options.
	/// </summary>
	bool HasChildren { get; }
	/// <summary>
	/// Get a child option by its name.
	/// </summary>
	IOption GetChild(string name);
	/// <summary>
	/// Enumerate all children of this option.
	/// </summary>
	IEnumerable<IOption> Children { get; }

	/// <summary>
	/// The option's category.
	/// </summary>
	string Category { get; }

	#if UNITY_EDITOR

	bool IsAvailable(IEnumerable<BuildTarget> targets);

	/// <summary>
	/// Build-only options are options that configure part of the build 
	/// process. Their value cannot be changed in the editor profile,
	/// in the ini file or using the prompt. Therefore, build-only options
	/// also cannot be configured to be included in builds (they are always
	/// removed).
	/// </summary>
	bool BuildOnly { get; }
	bool EditorOnly { get; }

	BuildPlayerOptions PrepareBuild(BuildPlayerOptions options, bool includedInBuild, RuntimeProfile profile);

	/// <summary>
	/// Remove the option from the build.
	/// </summary>
	void PostprocessScene(Scene scene, bool isBuild, bool includedInBuild, RuntimeProfile profile);

	void PreprocessBuild(BuildTarget target, string path, bool includedInBuild, RuntimeProfile profile);

	/// <summary>
	/// Order the individual option's PostprocessBuild is getting called.
	/// </summary>
	/// <remarks>
	/// This does not affect the order in regards to non-workbench <c>PostProcessBuild</c>
	/// callbacks, only the order between individual options.
	/// </remarks>
	int PostprocessOrder { get; }
	/// <summary>
	/// Called after the build is complete, gives the option a chance
	/// to edit or supplement the build.
	/// </summary>
	void PostprocessBuild(BuildTarget target, string path, bool includedInBuild, RuntimeProfile profile);

	/// <summary>
	/// The scripting define symbols that should be set when the option is included.
	/// All define symbols need to start with <see cref="Option.DEFINE_PREFIX"/>
	/// to be removed properly.
	/// </summary>
	IEnumerable<string> GetSctiptingDefineSymbols(bool includedInBuild, string parameter, string value);

	/// <summary>
	/// GUI used in the editor to edit the option's value.
	/// </summary>
	string EditGUI(GUIContent label, string input);

	#endif
}

/// <summary>
/// Value interface for options. This generic interface defines the value the option can 
/// take as well as methods used in the editor to edit ini files.
/// </summary>
public interface IOption<TValue> : IOption
{
	/// <summary>
	/// The value of the option.
	/// </summary>
	TValue Value { get; set; }
	/// <summary>
	/// Parse a value from an ini file.
	/// </summary>
	TValue Parse(string input);
	/// <summary>
	/// Serialize a value for an ini file.
	/// </summary>
	string Save(TValue input);
}

}