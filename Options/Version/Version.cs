using System;
using UnityEngine;

namespace sttz.Workbench.Options
{

/// <summary>
/// Struct holding project version information.
/// </summary>
/// <remarks>
/// This class is following basic semantic versioning principles.
/// </remarks>
[Serializable]
public struct Version
{
    // ------ Main Version ------

    /// <summary>
    /// The major version of the project.
    /// </summary>
    public int major;
    /// <summary>
    /// The minor version of the project.
    /// </summary>
    public int minor;
    /// <summary>
    /// The patch version of the project.
    /// </summary>
    public int patch;

    // ------ Build Information ------

    /// <summary>
    /// The build number.
    /// </summary>
    public int build;

    /// <summary>
    /// An identifier for the versioned commit from which this version was created.
    /// </summary>
    public string commit;
    /// <summary>
    /// An identifier for the versioned branch from which this version was created.
    /// </summary>
    public string branch;

    // ------ API ------

    /// <summary>
    /// The main project version.
    /// </summary>
    /// <remarks>
    /// This property will be populated from the <see cref="VersionContainer"/>.
    /// The <see cref="OptionVersion"/> injects the container into the build,
    /// so the version is always without additional setup.
    /// </remarks>
    public static Version ProjectVersion {
        get {
            // In case someone calls this before the container's OnEnable is invoked
            if (!loadedVersion) {
                loadedVersion = true;
                var container = UnityEngine.Object.FindObjectOfType<VersionContainer>();
                if (container != null) {
                    _projectVersion = container.version;
                }
            }
            return _projectVersion;
        }
        set {
            _projectVersion = value;
            loadedVersion = true;
        }
    }
    static bool loadedVersion;
    static Version _projectVersion;

    /// <summary>
    /// Check if this struct reprsents a valid version.
    /// </summary>
    /// <remarks>
    /// i.e. <see cref="major"/>, <see cref="minor"/> and <see cref="patch"/> must
    /// not be negative and one of them must be greater than 0.
    /// </remarks>
    public bool IsDefined {
        get {
            return major >= 0 && minor >= 0 && patch >= 0
                && (major > 0 || minor > 0 || patch > 0);
        }
    }

    /// <summary>
	/// Return the version as major.minor string (e.g. 1.2)
	/// </summary>
	public string MajorMinor {
		get {
			return string.Format("{0}.{1}", major, minor);
		}
	}

	/// <summary>
	/// Return the version as major.minor.patch string (e.g. 1.2.3)
	/// </summary>
	public string MajorMinorPatch {
		get {
			return string.Format("{0}.{1}.{2}", major, minor, patch);
		}
	}

	/// <summary>
	/// Return the version as major.minor.patch.build string (e.g. 1.2.3.4)
	/// </summary>
	public string MajorMinorPatchBuild {
		get {
			return string.Format("{0}.{1}.{2}+{3}", major, minor, patch, build);
		}
	}

	/// <summary>
	/// Returns the version as a string in the form "major.minor.patch (build)"
	/// (e.g. "1.2.3 (4)")
	/// </summary>
	public override string ToString()
	{
		return string.Format("{0}.{1}.{2} ({3})", major, minor, patch, build);
	}
}

}