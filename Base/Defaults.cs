using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace sttz.Workbench {

// TODO: Save categories

/// <summary>
/// Structured representation of ini files.
/// </summary>
/// <remarks>
/// <para>The Defaults class allows to read and save ini files. Formatting
/// and commants are preserved, new values added at the bottom. Comments are
/// supported only as full-line comments and can start with ';', '#' or '//'.
/// Sections are supported but don't serve a purpose besides structuring the
/// file and the options in the editor GUI.</para>
/// 
/// <para>One particular extension to regular ini files are properies with 
/// paramters. Parameters are enclosed in round brackets and follow the 
/// name before the equal sign. Parameters allow to have a single option 
/// that can take different parameter/value pairs, e.g. a Volume option that
/// can set the volume for different channels, where the parameter defines 
/// the channel name.</para>
/// 
/// <para>Defaults instances can also be layered using the <see cref="Parent"/>
/// property, in which case the parent's value is returned if a name doesn't
/// exist in the current Defaults instance. The parent is only ever used for
/// lookup, editing a value always changes only the current Defaults instance.</para>
/// </remarks>
public class Defaults
{
	/// <summary>
	/// Parsed line of the ini file.
	/// </summary>
	/// <remarks>
	/// For lines of the ini that don't contain an option value,
	/// e.g. comment or invalid lines, only line is set, all other
	/// fields are null.
	/// </remarks>
	public struct IniLine
	{
		/// <summary>
		/// Original line in the ini file.
		/// </summary>
		public string line;
		/// <summary>
		/// Category the option belongs to.
		/// </summary>
		public string category;
		/// <summary>
		/// The option name.
		/// </summary>
		public string name;
		/// <summary>
		/// The option parameter.
		/// </summary>
		public string parameter;
		/// <summary>
		/// The option value.
		/// </summary>
		public string value;
	}

	/// <summary>
	/// Create an empty defaults instance.
	/// </summary>
	public Defaults() { }

	/// <summary>
	/// Create an defaults instance and load it with
	/// the contents of the ini file data.
	/// </summary>
	public Defaults(string input)
	{
		Load(input);
	}

	/// <summary>
	/// Get or set the value for the option with the given name.
	/// </summary>
	/// <remarks>
	/// Returns an empty string for options that are not defined
	/// in the defaults. Setting an option to an empty string
	/// removes it from the defaults.
	/// </remarks>
	public string this[string name] {
		get {
			// Value exists
			if (values.ContainsKey(name) && values[name].value != null)
				return values[name].value;

			// Defer to parent
			else if (Parent != null && Parent[name] != string.Empty)
				return Parent[name];

			// No value = empty string
			else
				return string.Empty;
		}
		set {
			// Add new value
			if (!values.ContainsKey(name)) {
				if (!string.IsNullOrEmpty(value)) {
					values[name] = new IniValue(value);
					IsDirty = true;
				}
			
			// Remove value
			} else if (string.IsNullOrEmpty(value)) {
				var line = values[name];
				line.value = null;
				values[name] = line;
				if (values[name].IsEmpty) {
					values.Remove(name);
				}
				IsDirty = true;

			// Change value
			} else if (values[name].value != value) {
				var line = values[name];
				line.value = value;
				values[name] = line;
				IsDirty = true;
			}
		}
	}

	/// <summary>
	/// Get the default value for the option with the given name
	/// and parameter.
	/// </summary>
	/// <remarks>
	/// Returns an empty string for option/parameter combinations
	/// that are not defined in the defaults. Setting an option 
	/// to an empty string removes it from the defaults.
	/// </remarks>
	public string this[string name, string parameter] {
		get {
			// Value exists
			if (values.ContainsKey(name) && values[name].HasParameter(parameter))
				return values[name].paramValues[parameter];

			// Defer to parent
			else if (Parent != null && Parent[name, parameter] != string.Empty)
				return Parent[name, parameter];

			// No value = empty string
			else
				return string.Empty;
		}
		set {
			// Add new value
			if (!values.ContainsKey(name)) {
				if (!string.IsNullOrEmpty(value)) {
					values[name] = new IniValue(parameter, value);
					IsDirty = true;
				}

			// Add parameter
			} else if (!values[name].HasParameter(parameter)) {
				if (!string.IsNullOrEmpty(value)) {
					values[name].SetParamValue(parameter, value);
					IsDirty = true;
				}

			// Remove value
			} else if (string.IsNullOrEmpty(value)) {
				values[name].paramValues.Remove(parameter);
				if (values[name].IsEmpty) {
					values.Remove(name);
				}
				IsDirty = true;

			// Change value
			} else if (values[name].paramValues[parameter] != value) {
				values[name].paramValues[parameter] = value;
				IsDirty = true;
			}
		}
	}

	/// <summary>
	/// Fallback parent defaults.
	/// </summary>
	/// <remarks>
	/// If a value doesn't exist in the current Defaults
	/// instance, the parent is queried for a value.
	/// </remarks>
	public Defaults Parent { get; set; }

	/// <summary>
	/// Tracks if the default values have been modified.
	/// </summary>
	/// <remarks>
	/// <c>Load</c> clears the dirty flag but Save does 
	/// not. The flag needs to be cleared manually
	/// after the defautls have been saved.
	/// </remarks>
	public bool IsDirty { get; set; }

	/// <summary>
	/// Enumerate all defined parameters of the option with the given name.
	/// </summary>
	public IEnumerable<string> OptionParameters(string optionName)
	{
		if (!values.ContainsKey(optionName) || values[optionName].paramValues == null)
			yield break;

		foreach (var param in values[optionName].paramValues.Keys) {
			yield return param;
		}
	}

	/// <summary>
	/// Enumerate all option/parameter pairs defined.
	/// </summary>
	public IEnumerable<KeyValuePair<string, string>> ParameterOptions()
	{
		foreach (var pair in values) {
			if (pair.Value.paramValues == null)
				continue;

			foreach (var paramPair in pair.Value.paramValues) {
				yield return new KeyValuePair<string, string>(pair.Key, paramPair.Key);
			}
		}
	}

	/// <summary>
	/// Get the category an option is under in the ini file or null if no category is defined.
	/// </summary>
	public string GetCategory(string optionName)
	{
		if (categories.ContainsKey(optionName)) {
			return categories[optionName];
		} else {
			return null;
		}
	}

	/// <summary>
	/// Loads a new defaults ini file string, the contents of the string 
	/// replace the existing defaults.
	/// </summary>
	/// <remarks>
	/// Load clears the <c>IsDirty</c> flag.
	/// </remarks>
	public void Load(string input)
	{
		data = input;
		values.Clear();

		ScanData((parsed) => {
			if (parsed.name != null) {
				if (parsed.parameter == null) {
					this[parsed.name] = parsed.value;
				} else {
					this[parsed.name, parsed.parameter] = parsed.value;
				}

				if (parsed.category != null) {
					categories[parsed.name] = parsed.category;
				}
			}
		});

		IsDirty = false;
	}

	/// <summary>
	/// Serialize the defaults to a ini file string.
	/// </summary>
	/// <remarks>
	/// The <c>IsDirty</c> flag needs to be cleared manually after the data has been written.
	/// </remarks>
	public string Save()
	{
		var builder = new StringBuilder();
		var written = new HashSet<string>();

		ScanData((parsed) => {
			if (parsed.name == null) {
				builder.AppendLine(parsed.line);
				return;
			}

			if (values.ContainsKey(parsed.name)) {
				if (parsed.parameter == null) {
					if (values[parsed.name].value != null) {
						builder.Append(parsed.name);
						builder.Append(" = ");
						builder.Append(values[parsed.name].value);
						builder.Append("\n");
						written.Add(parsed.name);
					}
				} else {
					if (values[parsed.name].HasParameter(parsed.parameter)) {
						var key = parsed.name + "(" + parsed.parameter + ")";
						builder.Append(key);
						builder.Append(" = ");
						builder.Append(values[parsed.name].paramValues[parsed.parameter]);
						builder.Append("\n");
						written.Add(key);
					}
				}
			}
		});

		foreach (var pair in values) {
			var name = pair.Key;
			var line = pair.Value;

			if (!written.Contains(name) && line.value != null) {
				builder.Append(name);
				builder.Append(" = ");
				builder.Append(line.value);
				builder.Append("\n");
			}

			if (line.paramValues == null)
				continue;

			foreach (var paramPair in line.paramValues) {
				var key = name + "(" + paramPair.Key + ")";
				if (!written.Contains(key)) {
					builder.Append(key);
					builder.Append(" = ");
					builder.Append(paramPair.Value);
					builder.Append("\n");
				}
			}
		}

		// Last newline character is too much
		builder.Remove(builder.Length - 1, 1);

		return builder.ToString();
	}

	/// <summary>
	/// Scan an ini file line and return the parsed representation.
	/// </summary>
	public static IniLine ScanLine(string line)
	{
		var parsed = new IniLine();
		parsed.line = line;

		var trimmed = line.Trim();

		if (trimmed.StartsWith("#") || trimmed.StartsWith("//") || trimmed.StartsWith(";")) {
			return parsed;
		}

		if (trimmed.StartsWith("[")) {
			var end = trimmed.IndexOf("]");
			if (end < 0)
				return parsed;

			parsed.category = trimmed.Substring(1, end - 1).Trim();
			return parsed;
		}

		var equalIndex = trimmed.IndexOf("=");
		if (equalIndex < 0) {
			return parsed;
		}

		var name = trimmed.Substring(0, equalIndex).Trim();
		string parameter = null;
		var value = trimmed.Substring(equalIndex + 1, trimmed.Length - equalIndex - 1).Trim();
		if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(value)) {
			return parsed;
		}

		var match = IniNameRegex.Match(name);
		if (match.Groups[2].Success) {
			name = match.Groups[1].Value;
			parameter = match.Groups[2].Value;
		}

		parsed.name = name;
		parsed.parameter = parameter;
		parsed.value = value;
		return parsed;
	}

	// -------- Internals --------

	/// <summary>
	/// Internal structure used to handle properties with and without parameters,
	/// without having to allocate anything if an option doesn't have parameters.
	/// An option can have a parameter-less value as well as values with parameters.
	/// </summary>
	private struct IniValue
	{
		public string value;
		public Dictionary<string, string> paramValues;

		public IniValue(string value)
		{
			this.value = value;
			this.paramValues = null;
		}

		public IniValue(string parameter, string value)
		{
			this.value = null;
			this.paramValues = null;
			SetParamValue(parameter, value);
		}

		public bool HasParameter(string parameter)
		{
			return (paramValues != null && paramValues.ContainsKey(parameter));
		}

		public void SetParamValue(string parameter, string value)
		{
			if (paramValues == null)
				paramValues = new Dictionary<string, string>();

			paramValues[parameter] = value;
		}

		public bool IsEmpty {
			get {
				return (value == null && (paramValues == null || paramValues.Count == 0));
			}
		}
	}

	/// <summary>
	/// Regex to parse the ini property name with optional parameter.
	/// </summary>
	private static Regex IniNameRegex = new Regex(@"^(.*?)(?:\((.*)\))?$");

	/// <summary>
	/// Original ini file data.
	/// </summary>
	private string data;
	/// <summary>
	/// Ini properties.
	/// </summary>
	private Dictionary<string, IniValue> values = new Dictionary<string, IniValue>();
	/// <summary>
	/// Mapping of property names to categories.
	/// </summary>
	private Dictionary<string, string> categories = new Dictionary<string, string>();

	/// <summary>
	/// Read the ini file and let a callback process it line by line,
	/// keeping track of the current category.
	/// </summary>
	private void ScanData(Action<IniLine> scanner)
	{
		if (data == null)
			return;

		var lines = data.Split('\n');
		string category = null;
		foreach (var line in lines) {
			var parsed = ScanLine(line);

			if (parsed.category != null) {
				category = parsed.category;
			} else {
				parsed.category = category;
			}

			scanner(parsed);
		}
	}
}

}