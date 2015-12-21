using System;
using UnityEngine;

/// <summary>
/// Store a version number, e.g. the project's current version.
/// </summary>
public class VersionInfo : ScriptableObject
{
	/// <summary>
	/// Major version number.
	/// </summary>
	public int major = 1;
	/// <summary>
	/// Minor version number.
	/// </summary>
	public int minor = 0;
	/// <summary>
	/// Maintenance version number.
	/// </summary>
	public int maintenance = 0;

	/// <summary>
	/// Build number.
	/// </summary>
	public int build = 0;

	/// <summary>
	/// Automatically increment build after every build.
	/// </summary>
	public bool autoIncrementBuild;

	/// <summary>
	/// Return the version as major.minor string (e.g. 1.2)
	/// </summary>
	public string MajorMinor {
		get {
			return string.Format("{0}.{1}", major, minor);
		}
	}

	/// <summary>
	/// Return the version as major.minor.maintenace string (e.g. 1.2.3)
	/// </summary>
	public string MajorMinorMaintenance {
		get {
			return string.Format("{0}.{1}.{2}", major, minor, maintenance);
		}
	}

	/// <summary>
	/// Return the version as major.minor.maintenace.build string (e.g. 1.2.3.4)
	/// </summary>
	public string MajorMinorMaintenanceBuild {
		get {
			return string.Format("{0}.{1}.{2}.{3}", major, minor, maintenance, build);
		}
	}

	/// <summary>
	/// Returns the version as a string in the form "major.minor.maintenance (build)"
	/// (e.g. "1.2.3 (4)")
	/// </summary>
	public override string ToString()
	{
		return string.Format("{0}.{1}.{2} ({3})", major, minor, maintenance, build);
	}
}
