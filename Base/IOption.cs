using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace sttz.Workbench {

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
	/// <summary>
	/// The default value for the option, used if there is no value defined
	/// for the option in the ini file.
	/// </summary>
	string DefaultIniValue { get; }
	/// <summary>
	/// Parse a string and set the option's Value.
	/// </summary>
	void Load(string input);
	/// <summary>
	/// Save the option's current value to a string value.
	/// </summary>
	string Save();
	/// <summary>
	/// Apply the option's current value to the scene.
	/// </summary>
	/// <remarks>
	/// This is called whenever the option's value changed and also after 
	/// a new scene has loaded.
	/// </remarks>
	void Apply();

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
	bool IsDefaultVariant { get; set; }

	/// <summary>
	/// Add a new variant to the main variant option. Only used if <see cref="IsVariant"/>
	/// is true and can only be called on the main variant option (<see cref="IsDefaultVariant"/>).
	/// </summary>
	void AddVariant(IOption variant);
	/// <summary>
	/// Get a variant option by its parameter. Only used if <see cref="IsVariant"/>
	/// is true and can only be called on the main variant option (<see cref="IsDefaultVariant"/>).
	/// </summary>
	IOption GetVariant(string parameter);
	/// <summary>
	/// Enumerate all variants of this option.
	/// </summary>
	IEnumerable<IOption> Variants { get; }

	/// <summary>
	/// Types of the option's child options.
	/// </summary>
	/// <remarks>
	/// Child options are created together with their parent option when the
	/// parent is instantiated and need to implement <see cref="IChildOption"/>.
	/// </remarks>
	Type[] ChildOptionTypes { get; }
	/// <summary>
	/// Wether the option has child options.
	/// </summary>
	bool HasChildren { get; }
	/// <summary>
	/// Get a child option by its name.
	/// </summary>
	IChildOption GetChild(string name);
	/// <summary>
	/// Enumerate all children of this option.
	/// </summary>
	IEnumerable<IChildOption> Children { get; }

	/// <summary>
	/// The default category the option is placed under.
	/// </summary>
	string DefaultCategory { get; }
	/// <summary>
	/// The option's current category.
	/// </summary>
	string Category { get; set; }

	#if UNITY_EDITOR

	/// <summary>
	/// Build-only options are options that configure part of the build 
	/// process. Their value cannot be changed in the editor profile,
	/// in the ini file or using the prompt. Therefore, build-only options
	/// also cannot be configured to be included in builds (they are always
	/// removed).
	/// </summary>
	bool BuildOnly { get; }

	/// <summary>
	/// Remove the option from the build.
	/// </summary>
	/// <remarks>
	/// This is part of the build system and allows the option to edit 
	/// the scenes before they are built. When called, the option instance
	/// is initialized, <see cref="Value"/> and <see cref="Parameter"/>
	/// are set. Parameter options are called at least once for their
	/// default parameter and once more for every additional parameter.
	/// </remarks>
	// TODO: Make more generic?
	void Remove();

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
	void PostprocessBuild(BuildTarget target, string pathToBuiltProject, bool optionRemoved, Profile profile);

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
// TODO: Still needed?
public interface IOption<TValue> : IOption
{
	/// <summary>
	/// The value of the option.
	/// </summary>
	/// <remarks>
	/// Changing the value will not apply the option. <see cref="Apply"/> needs to be called
	/// to apply the value change.
	/// </remarks>
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

/// <summary>
/// Interface identifying a child option.
/// </summary>
public interface IChildOption : IOption { }

}