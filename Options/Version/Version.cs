//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

using System;

namespace sttz.Trimmer
{

/// <summary>
/// Struct holding project version information.
/// </summary>
/// <remarks>
/// This class is following basic semantic versioning principles.
/// </remarks>
[Serializable]
public struct Version : IComparable, IComparable<Version>, IEquatable<Version>
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
    /// The <see cref="Options.OptionVersion"/> injects the container into the build,
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
    /// Create a new version instance.
    /// </summary>
    public Version(int major, int minor, int patch, int build = 0, string commit = null, string branch = null)
    {
        this.major = major;
        this.minor = minor;
        this.patch = patch;
        this.build = build;
        this.commit = commit;
        this.branch = branch;
    }

    /// <summary>
    /// Parse a version string in the format "x.x.x".
    /// </summary>
    public static Version Parse(string input)
    {
        string error;
        return Parse(input, out error);
    }

    /// <summary>
    /// Parse a version string in the format "x.x.x".
    /// </summary>
    public static Version Parse(string input, out string error)
    {
        var version = new Version();

        var parts = input.Split('.');
        if (parts.Length > 4) {
            error = "Version: Too many components in input '" + input + "'";
            return default(Version);
        }

        if (parts.Length > 0 && (!int.TryParse(parts[0], out version.major) || version.major < 0)) {
            error = "Version: The major version part is not a positive number: " + parts[0];
            return default(Version);
        }
        if (parts.Length > 1 && (!int.TryParse(parts[1], out version.minor) || version.minor < 0)) {
            error = "Version: The minor version part is not a positive number: " + parts[1];
            return default(Version);
        }
        if (parts.Length > 2 && (!int.TryParse(parts[2], out version.patch) || version.patch < 0)) {
            error = "Version: The patch version part is not a positive number: " + parts[2];
            return default(Version);
        }

        error = null;
        return version;
    }

    /// <summary>
    /// Check if this struct represents a valid version.
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
    /// Return the version as major.minor.patch.build string (e.g. 1.2.3+4)
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

    // ------ IComparable ------

    public int CompareTo(object obj)
    {
        if (obj is Version) {
            return CompareTo((Version)obj);
        } else {
            throw new ArgumentException("Argument is not a Version instance.", nameof(obj));
        }
    }

    public int CompareTo(Version other)
    {
        var result = major.CompareTo(other.major);
        if (result != 0) return result;

        result = minor.CompareTo(other.minor);
        if (result != 0) return result;

        result = patch.CompareTo(other.patch);
        if (result != 0) return result;

        return build.CompareTo(other.build);
    }

    // ------ IEquatable ------

    override public bool Equals(object obj)
    {
        if (obj is Version) {
            return Equals((Version)obj);
        } else {
            return false;
        }
    }

    override public int GetHashCode()
    {
        int code = 0;
        code |= (major & 0x0000000F) << 28;
        code |= (minor & 0x000000FF) << 20;
        code |= (patch & 0x000000FF) << 12;
        code |= (build & 0x00000FFF);
        return code;
    }

    public bool Equals(Version other)
    {
        return
            major == other.major
            && minor == other.minor
            && patch == other.patch
            && build == other.build;
    }

    // ------ Operators ------

    public static bool operator ==(Version lhs, Version rhs)
    {
        return lhs.Equals(rhs);
    }

    public static bool operator !=(Version lhs, Version rhs)
    {
        return !lhs.Equals(rhs);
    }

    public static bool operator <(Version lhs, Version rhs)
    {
        return lhs.CompareTo(rhs) < 0;
    }

    public static bool operator >(Version lhs, Version rhs)
    {
        return lhs.CompareTo(rhs) > 0;
    }

    public static bool operator <=(Version lhs, Version rhs)
    {
        return lhs.CompareTo(rhs) <= 0;
    }

    public static bool operator >=(Version lhs, Version rhs)
    {
        return lhs.CompareTo(rhs) >= 0;
    }
}

}
