//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

#if !NO_TRIMMER || UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using sttz.Trimmer.Extensions;
using System.Diagnostics;

#if UNITY_EDITOR
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Build.Reporting;
#endif

namespace sttz.Trimmer {

// TODO: Document editor-only methods/props
// TODO: Document main-option-only methods/props

#if UNITY_EDITOR

/// <summary>
/// Flags indicating wether an Option and its feature should be included in a build.
/// </summary>
/// <remarks>
/// In the editor, the inclusion of an Option is set in Build Profiles in
/// the right-hand column for each main Option. In code, the inclusion
/// is stored in the Build Profile's <see cref="ValueStore"/>, specifically
/// in <see cref="ValueStore.RootNode.Inclusion"/>.
/// 
/// The actual symbols being defined also depends on the Option's
/// <see cref="OptionCapabilities"/>. Only if the Option has the right
/// capabilities and the inclusion has been set, will the symbols be defined.
/// 
/// > [!NOTE]
/// > The inclusion only applies to the main Option. All child and variant
/// > Options will inherit the inclusion from their main parent.
/// </remarks>
[Flags]
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
    /// Flag indicating the option should apply its build changes.
    /// </summary>
    Build = 1<<2,

    /// <summary>
    /// Mask including both feature and option.
    /// </summary>
    FeatureAndOption = Feature | Option,
}

/// <summary>
/// Interface for options to interact with its profile.
/// </summary>
public interface IEditorProfile
{
    /// <summary>
    /// The build targets of this profile.
    /// </summary>
    IEnumerable<BuildTarget> BuildTargets { get; }
    /// <summary>
    /// The profile used to manage options in the editor.
    /// </summary>
    RuntimeProfile EditProfile { get; }
}

#endif

/// <summary>
/// Enum indicating the capabilities of the Option.
/// </summary>
/// <remarks>
/// The enum contains specific flags that represent different capabilities and also
/// a set of default masks that represent common combinations of flags.
/// 
/// The capabilities control where an Option is visible:
/// * If neither <see cref="HasAssociatedFeature"/>, <see cref="CanIncludeOption"/>
///   or <see cref="ConfiguresBuild"/> is set, the Option will not be shown in
///   Build Profiles.
/// * If neither <see cref="CanPlayInEditor"/> or <see cref="ExecuteInEditMode"/> is set,
///   the Option will not be shown in the Editor Profile.
/// 
/// > [!NOTE]
/// > Capabilities are only valid on the main Option, all child and variant Options will
/// > inherit the capabilities from the main Option.
/// </remarks>
[Flags]
public enum OptionCapabilities
{
    None,

    // ------ Flags ------

    /// <summary>
    /// Flag indicating the option has an associated feature that can be included/excluded from the
    /// build using Build Profiles.
    /// </summary>
    HasAssociatedFeature = 1<<0,
    
    /// <summary>
    /// Flag indicating the Option can be included in builds. If not set, the Option will always
    /// be removed from builds.
    /// </summary>
    CanIncludeOption = 1<<1,

    /// <summary>
    /// Flag indicating the Option integrates into the build process, configuring the build
    /// options or pre-/post-processes scenes and the build.
    /// </summary>
    ConfiguresBuild = 1<<2,

    /// <summary>
    /// Flag indicating the Option can be used when playing in the editor. If not set, the Option
    /// will not be loaded when playing the project in the editor.
    /// </summary>
    CanPlayInEditor = 1<<3,

    /// <summary>
    /// Flag indicating the Option should be loaded in edit mode. If set, the Option
    /// will be loaded when not playing in the editor.
    /// </summary>
    ExecuteInEditMode = 1<<4,

    // ------ Presets ------

    /// <summary>
    /// Default preset mask. The Option can be included in the build, its 
    /// build configuration callbacks are called and it is loaded when playing
    /// in the editor.
    /// </summary>
    PresetDefault = CanIncludeOption | ConfiguresBuild | CanPlayInEditor,

    /// <summary>
    /// Preset mask. Like <see cref="PresetDefault"/> but also has an associated 
    /// feature that can be included/excluded from the build.
    /// </summary>
    PresetWithFeature = PresetDefault | HasAssociatedFeature,

    /// <summary>
    /// Preset mask. A simple Option that can be included in the build
    /// and gets loaded in the editor but doesn't process the build and has 
    /// no associated feature.
    /// </summary>
    PresetOptionOnly = CanIncludeOption | CanPlayInEditor,
}

/// <summary>
/// Attribute used to indicate the <see cref="OptionCapabilities" /> of an 
/// <see cref="Option"/> subclass.
/// </summary>
/// <remarks>
/// If no attribute is applied, an Option has the 
/// <see cref="OptionCapabilities.PresetDefault"/> capabilities.
/// 
/// > [!NOTE]
/// > The Capabilities attribute is only valid on the main Option, 
/// > all child and variant Options will inherit the capabilities from the main Option.
/// </remarks>
[AttributeUsage(AttributeTargets.Class)]
[Conditional("UNITY_EDITOR")]
public class CapabilitiesAttribute : Attribute
{
    public OptionCapabilities Capabilities { get; protected set; }

    public CapabilitiesAttribute(OptionCapabilities caps)
    {
        Capabilities = caps;
    }
}

/// <summary>
/// Enum defining the variance of an Option.
/// </summary>
/// <seealso cref="Option.Variance"/>  
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
    /// the parameter is set automatically.
    /// </summary>
    Array
}

/// <summary>
/// Base class for Trimmer Options.
/// </summary>
/// <remarks>
/// Options are the basic building blocks to integrate your project 
/// into Trimmer. Trimmer detects all Option classes
/// in your project, so there's no additional configuration necessary
/// besides adding the Option source files to your project.
/// 
/// Each Option has a value, which you can edit in the editor and which
/// can also be changed in the player using the <see cref="RuntimeProfile"/>.
/// The runtime profile is only a script API, use the bundled Options to
/// change Option values in the player using configuration files 
/// (<see cref="Options.OptionIniFile"/>) or a simple GUI 
/// (<see cref="Options.OptionPrompt"/>).
/// 
/// Options can model more complicated data than simple values in two ways:
/// * <b>Variant Options</b> allow to have multiple instances of the same
///   Option type that differ by their <see cref="VariantParameter"/>,
///   e.g. to have a volume Option, which can control multiple channels.
/// * <b>Child Options</b> allow Options to group multiple different values
///   together.
/// 
/// To make an Option variant, set its <see cref="Variance"/>. To add child
/// Options, define Option classes nested inside other Option classes.
/// 
/// Most of the time, you want to extend one of the typed base classes
/// that fit the type of Option you want to create:
/// * <see cref="BaseOptions.OptionAsset{TUnity}" />
/// * <see cref="BaseOptions.OptionContainer" />
/// * <see cref="BaseOptions.OptionEnum{TEnum}" />
/// * <see cref="BaseOptions.OptionFloat" />
/// * <see cref="BaseOptions.OptionInt" />
/// * <see cref="BaseOptions.OptionString" />
/// * <see cref="BaseOptions.OptionToggle" />
/// </remarks>
public abstract class Option
{
    // -------- Implement / Override in Sub-Classes --------

    /// <summary>
    /// Configure the Option instance during instantiation.
    /// </summary>
    /// <remarks>
    /// Override this method instead of the constructor to configure your
    /// Option instance. Most Option properties should only bet set once
    /// in this method and then not changed after the Option is created.
    /// </remarks>
    protected virtual void Configure()
    {
        // NOP
    }

    /// <summary>
    /// Prefix for the Trimmer scripting defines symbols.
    /// </summary>
    /// <remarks>
    /// Conditional compilation defines with this prefix are considered
    /// managed by Trimmer. All symbols with this prefix will be removed
    /// before a build starts and Options need to re-define their symbols
    /// in <see cref="GetScriptingDefineSymbols*"/>.
    /// </remarks>
    public const string DEFINE_PREFIX = "TR_";

    /// <summary>
    /// Prefix applied after the <see cref="DEFINE_PREFIX"/> for the 
    /// Option scripting define symbols.
    /// </summary>
    public const string OPTION_PREFIX = "Option";

    #if UNITY_EDITOR

    /// <summary>
    /// Used to track if Option values have changed when editing.
    /// </summary>
    public static bool changed = false;

    /// <summary>
    /// The capabilities of the Option.
    /// </summary>
    /// <remarks>
    /// Used to cache the attribute value. To change the capabilities,
    /// use the <see cref="CapabilitiesAttribute"/>.
    /// 
    /// > [!NOTE]
    /// > This property is only available in the editor.
    /// 
    /// > [!NOTE]
    /// > This property only applies to main Options. Child and variant Options
    /// > will inherit the capabilities from their main parent.
    /// </remarks>
    public OptionCapabilities Capabilities { get; private set; }

    /// <summary>
    /// The editor profile this option belongs to.
    /// </summary>
    /// <remarks>
    /// > [!NOTE]
    /// > This property is only available in the editor
    /// > when not in play mode.
    /// </remarks>
    public IEditorProfile EditorProfile { get; private set; }

    /// <summary>
    /// Set the editor profile for the option and all of its variants and children.
    /// </summary>
    /// <param name="profile"></param>
    public void SetEditorProfile(IEditorProfile profile)
    {
        EditorProfile = profile;

        if (variants != null) {
            foreach (var variant in variants) {
                variant.SetEditorProfile(profile);
            }
        }

        if (children != null) {
            foreach (var child in children) {
                child.SetEditorProfile(profile);
            }
        }
    }

    /// <summary>
    /// The `BuildTarget`s this Option supports. (null = all)
    /// </summary>
    /// <remarks>
    /// > [!NOTE]
    /// > This property is only available in the editor.
    /// 
    /// > [!NOTE]
    /// > This property only applies to main Options. Child and variant Options
    /// > will inherit the supported targets from their main parent.
    /// </remarks>
    public IEnumerable<BuildTarget> SupportedTargets { get; protected set; }

    /// <summary>
    /// Determines if the Option is available on the given build target.
    /// </summary>
    /// <remarks>
    /// It's possible to hide an Option in Build Profiles if they don't
    /// apply to the profile's build targets (i.e. an iOS-only Option on
    /// an Android Build Profile). Unavailable Options can be shown using
    /// an Option in Trimmer's preferences but they will always
    /// be removed from builds.
    /// 
    /// > [!NOTE]
    /// > This method is only available in the editor.
    /// 
    /// > [!NOTE]
    /// > This method only applies to main Options. Child and variant Options
    /// > will inherit the availability from their main parent.
    /// </remarks>
    public virtual bool IsAvailable(BuildTarget target)
    {
        if (SupportedTargets == null)
            return true;
        
        return SupportedTargets.Contains(target);
    }

    /// <summary>
    /// Determines if the Option is available on one of the given build targets.
    /// </summary>
    public bool IsAvailable(IEnumerable<BuildTarget> targets)
    {
        foreach (var target in targets) {
            if (IsAvailable(target))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Determines if the inclusion of the associated feature should
    /// be overridden in case only the feature is included but
    /// isn't properly configured or enabled.
    /// </summary>
    /// <remarks>
    /// This method is only called if the Option has an associated 
    /// feature and only if the feature is included but the Option is not.
    /// 
    /// The method allows the Option to check if the feature is
    /// properly configured and only include it if it is. Since
    /// only the feature is included and the Option is not, it's
    /// potentially not possible to properly configure the feature
    /// in the build and therefore it makes no sense to include it.
    /// 
    /// Returning `false` will change the inclusion from 
    /// <see cref="OptionInclusion.Feature"/> to <see cref="OptionInclusion.Remove"/>.
    /// 
    /// > [!NOTE]
    /// > This method is only available in the editor.
    /// 
    /// > [!NOTE]
    /// > This method only applies to main Options. Child and variant Options
    /// > will inherit the inclusion from their main parent.
    /// </remarks>
    public virtual bool ShouldIncludeOnlyFeature()
    {
        return true;
    }

    /// <summary>
    /// Callback invoked for every scene during build.
    /// </summary>
    /// <remarks>
    /// This callback gives Options a chance to modify scenes during the
    /// build process. This can be used to e.g. inject a script into the 
    /// scene or remove some game objects.
    /// 
    /// Unlike Unity's `OnProcessScene`, this method is not called when
    /// playing in the editor. Use <see cref="Apply"/> and Unity's 
    /// `SceneManager` API instead.
    /// 
    /// > [!NOTE]
    /// > This method is only available in the editor.
    /// </remarks>
    /// <param name="scene">The scene that is being processed.</param>
    /// <param name="inclusion">Wether the option is included in the build.</param>
    public virtual void PostprocessScene(Scene scene, OptionInclusion inclusion)
    {
        if (variants != null) {
            foreach (var variant in variants) {
                variant.PostprocessScene(scene, inclusion);
            }
        }

        if (children != null) {
            foreach (var child in children) {
                child.PostprocessScene(scene, inclusion);
            }
        }
    }

    /// <summary>
    /// The priority of the Option's processing callbacks.
    /// </summary>
    /// <remarks>
    /// This determines the order in which all Option's processing callbacks
    /// are called (<see cref="PostprocessScene"/>, <see cref="PrepareBuild"/>,
    /// <see cref="PreprocessBuild"/> and <see cref="PostprocessBuild"/>).
    /// 
    /// Lower values will be called first.
    /// 
    /// > [!WARNING]
    /// > This only orders the Options between themselves. This does not affect
    /// > the order in regard to other consumers of these Unity events.
    /// 
    /// > [!NOTE]
    /// > This property is only available in the editor.
    /// 
    /// > [!NOTE]
    /// > This property only applies to main Options. Child and variant Options
    /// > will inherit the order from their main parent.
    /// </remarks>
    public int PostprocessOrder { get; protected set; }

    /// <summary>
    /// Callback invoked before a profile build is started.
    /// </summary>
    /// <remarks>
    /// When a build is started on a <see cref="T:sttz.Trimmer.Editor.BuildProfile"/>, all options
    /// will receive this callback before the build is started.
    /// 
    /// This callback allows Option to influence the build settings, including
    /// build options, output path and included scenes.
    /// 
    /// By default, the build will include the scenes set in Unity's build 
    /// player window and the options will be set to `BuildOptions.None`.
    /// If no Option sets the location path name, the user will be prompted 
    /// to choose it.
    /// 
    /// > [!WARNING]
    /// > This method will not be called for regular Unity builds,
    /// > started from the build player window or using the build menu item.
    /// 
    /// > [!NOTE]
    /// > This method is only available in the editor.
    /// </remarks>
    /// <param name="options">The current options</param>
    /// <param name="inclusion">Wether the Option is included in the  build.</param>
    /// <returns>The modified options.</returns>
    public virtual BuildPlayerOptions PrepareBuild(BuildPlayerOptions options, OptionInclusion inclusion)
    {
        if (variants != null) {
            foreach (var variant in variants) {
                options = variant.PrepareBuild(options, inclusion);
            }
        }

        if (children != null) {
            foreach (var child in children) {
                options = child.PrepareBuild(options, inclusion);
            }
        }

        return options;
    }

    /// <summary>
    /// Callback invoked right before a build.
    /// </summary>
    /// <remarks>
    /// This callback is invoked before the build, for both profile builds
    /// as well as regular Unity builds.
    /// 
    /// > [!NOTE]
    /// > This method is only available in the editor.
    /// </remarks>
    /// <param name="report">Unity's build report</param>
    /// <param name="inclusion">Wether this option is included in the build</param>
    public virtual void PreprocessBuild(BuildReport report, OptionInclusion inclusion)
    {
        if (variants != null) {
            foreach (var variant in variants) {
                variant.PreprocessBuild(report, inclusion);
            }
        }

        if (children != null) {
            foreach (var child in children) {
                child.PreprocessBuild(report, inclusion);
            }
        }
    }

    /// <summary>
    /// Callback invoked after the build completed.
    /// </summary>
    /// <remarks>
    /// This callback is invoked after the build has been completed, for 
    /// both profile builds and regular Unity builds.
    /// 
    /// > [!NOTE]
    /// > This method is only available in the editor.
    /// </remarks>
    /// <param name="report">Unity's build report</param>
    /// <param name="inclusion">Wether this option is included in the build</param>
    public virtual void PostprocessBuild(BuildReport report, OptionInclusion inclusion)
    {
        if (variants != null) {
            foreach (var variant in variants) {
                variant.PostprocessBuild(report, inclusion);
            }
        }

        if (children != null) {
            foreach (var child in children) {
                child.PostprocessBuild(report, inclusion);
            }
        }
    }

    /// <summary>
    /// Callback invoked if the build fails with an error.
    /// </summary>
    /// <remarks>
    /// This callback is invoked after the build fails with an error, for
    /// profile builds only.
    ///
    /// Any state that was temporarily modified in <see cref="PrepareBuild"/> or
    /// <see cref="PreprocessBuild"/> should be cleaned up in either this callback or in
    /// <see cref="PostprocessBuild"/>.
    ///
    /// This callback catches and logs all exceptions so that an error in one
    /// implementation will not prevent the others from running.
    /// 
    /// > [!NOTE]
    /// > This method is only available in the editor.
    /// </remarks>
    /// <param name="report">
    /// The build report that Unity provided,
    /// or <see langword="null"/> if the build failed before
    /// the player itself could be compiled.
    /// </param>
    public virtual void OnBuildError([CanBeNull] BuildReport report)
    {
        if (variants != null) {
            foreach (var variant in variants) {
                try {
                    variant.OnBuildError(report);
                }
                catch (Exception e) {
                    UnityEngine.Debug.LogException(e);
                }
            }
        }
        
        if (children != null) {
            foreach (var child in children) {
                try {
                    child.OnBuildError(report);
                }
                catch (Exception e) {
                    UnityEngine.Debug.LogException(e);
                }
            }
        }
    }

    /// <summary>
    /// The scripting define symbols set by this Option.
    /// </summary>
    /// <remarks>
    /// By default, this method will define 
    /// <pre><see cref="DEFINE_PREFIX"/> + <see cref="OPTION_PREFIX"/> + <see cref="Name"/></pre>
    /// if the Option is included and 
    /// <pre><see cref="DEFINE_PREFIX"/> + <see cref="Name"/></pre>
    /// if the associated feature is included.
    /// 
    /// Only the main Option will set these symbols but child and variant Options
    /// can set additional symbols.
    /// 
    /// > [!NOTE]
    /// > This method is only available in the editor.
    /// </remarks>
    public virtual void GetScriptingDefineSymbols(OptionInclusion inclusion, HashSet<string> symbols)
    {
        // Only the root option has a toggle in the build profile
        if (Parent == null) {
            if (inclusion.HasFlag(OptionInclusion.Feature) 
                    && Capabilities.HasFlag(OptionCapabilities.HasAssociatedFeature)) {
                symbols.Add(DEFINE_PREFIX + Name);
            }

            if (inclusion.HasFlag(OptionInclusion.Option) 
                    && Capabilities.HasFlag(OptionCapabilities.CanIncludeOption)) {
                symbols.Add(DEFINE_PREFIX + OPTION_PREFIX + Name);
            }
        }

        if (variants != null) {
            foreach (var variant in variants) {
                variant.GetScriptingDefineSymbols(inclusion, symbols);
            }
        }

        if (children != null) {
            foreach (var child in children) {
                child.GetScriptingDefineSymbols(inclusion, symbols);
            }
        }
    }

    /// <summary>
    /// Do the editor GUI to edit this option.
    /// </summary>
    /// <remarks>
    /// The bundled subclasses in <see cref="sttz.Trimmer.BaseOptions"/>
    /// already provide implementations for this method. Override it
    /// to implement your custom GUI for the editor.
    /// 
    /// > [!NOTE]
    /// > This method is only available in the editor.
    /// </remarks>
    public abstract bool EditGUI();

    #endif

    /// <summary>
    /// The name of the Option.
    /// </summary>
    /// <remarks>
    /// The name is used in the editor, to identify the option in config
    /// files and to set the Option's scripting define symbols.
    /// 
    /// By default, the name is the Option's class name, minus the 
    /// <see cref="OPTION_PREFIX"/>. This ensures that the scripting 
    /// define symbol set for the Option matches its class name.
    /// 
    /// In case you set the name to something that doesn't start with the 
    /// Option prefix, the prefix will be prepended to the Option's scripting
    /// define symbol.
    /// 
    /// e.g.
    /// | Class Name        | &#x2192; Option Name       | &#x2192; Scripting Define Symbol |
    /// | ---               | ---                        | ---                              |
    /// | OptionExample     | &#x2192; Example           | &#x2192; OptionExample           |
    /// | NonDefaultExample | &#x2192; NonDefaultExample | &#x2192; OptionNonDefaultExample |
    /// </remarks>
    public string Name { get; protected set; }

    /// <summary>
    /// The Option containing this option (if any).
    /// </summary>
    /// <remarks>
    /// In case of variant Options, the parent is set to the main variant
    /// that contains all other variants.
    /// 
    /// In case of child Options and the main variant, the parent is set 
    /// to the Option containing the child / main variant.
    /// 
    /// The parent is `null` for main Options.
    /// </remarks>
    public Option Parent {
        get {
            return _parent;
        }
        set {
            if (_parent == value) return;

            _parent = value;
            
            InvalidatePathRecursive();
        }
    }
    private Option _parent;

    /// <summary>
    /// The path to this Option.
    /// </summary>
    /// <summary>
    /// The path consists of option names separated by «/» and variants 
    /// separated by «:» and their parameter.
    /// 
    /// You can use <see cref="RuntimeProfile.GetOption"/> to find an
    /// Option by its path.
    /// </summary>
    public string Path {
        get {
            if (_path == null) {
                _path = GetPathRecursive(this);
            }
            return _path;
        }
    }
    string _path;

    /// <summary>
    /// Internal helper method to construct an Option's path recursively.
    /// </summary>
    protected string GetPathRecursive(Option current)
    {
        if (current.Variance != OptionVariance.Single && !current.IsDefaultVariant) {
            if (current.Parent != null) {
                return GetPathRecursive(current.Parent) + ":" + current.VariantParameter;
            } else {
                throw new Exception("A non-default variant needs to have a parent.");
            }
        } else {
            if (current.Parent != null) {
                return GetPathRecursive(current.Parent) + "/" + current.Name;
            } else {
                return current.Name;
            }
        }
    }

    /// <summary>
    /// Internal helper method to invalidate all child/variant Option's
    /// paths recursively.
    /// </summary>
    public void InvalidatePathRecursive()
    {
        _path = null;

        foreach (var child in Children) {
            child.InvalidatePathRecursive();
        }
        foreach (var variant in Variants) {
            variant.InvalidatePathRecursive();
        }
    }

    /// <summary>
    /// Parse and load an input string.
    /// </summary>
    /// <remarks>
    /// This method is called with the value defined in the profile
    /// or entered by the user. This method should parse the input
    /// and then save it to <see cref="Option{TValue}.Value"/>.
    /// If the input is empty or contains an invalid value, the 
    /// <see cref="Option{TValue}.DefaultValue"/> should be used instead.
    /// 
    /// Load should only parse the value but not yet apply it
    /// to the project. Use <see cref="Apply"/> to act on the Option's
    /// value.
    /// </remarks>
    public abstract void Load(string input);

    /// <summary>
    /// Serialize the Option's value to a string.
    /// </summary>
    /// <remarks>
    /// The value returned by this method will later be supplied to
    /// <see cref="Load"/> and should survive the round-trip without
    /// loss.
    /// </remarks>
    public abstract string Save();

    /// <summary>
    /// Control the order Options' <see cref="Apply"/> methods get called.
    /// </summary>
    /// <remarks>
    /// Lower values get called first.
    /// </remarks>
    public int ApplyOrder { get; protected set; }

    /// <summary>
    /// Apply the Option to the current environment.
    /// </summary>
    /// <remarks>
    /// This method is called when the Option should act on its value.
    /// 
    /// This is when the game is started in the editor or in a player,
    /// or when the Option's value is changed while the game is playing.
    /// 
    /// Main Options as well as its children and variants are applied 
    /// together. E.g. when the main Option's or one of its children's
    /// value is changed, all of their Apply methods will be called.
    /// 
    /// This method does not get called when scenes change. Use Unity's
    /// `SceneManager` callbacks to get notified when scenes get loaded
    /// and unloaded or the active scene changes.
    /// </remarks>
    public virtual void Apply()
    {
        if (variants != null) {
            foreach (var variant in variants) {
                variant.Apply();
            }
        }

        if (children != null) {
            foreach (var child in children) {
                child.Apply();
            }
        }
    }

    /// <summary>
    /// Look for the root Option and then call its <see cref="Apply"/> method.
    /// </summary>
    public void ApplyFromRoot()
    {
        Option root = this;
        while (root.Parent != null) {
            root = root.Parent;
        }

        root.Apply();
    }

    // -------- Init --------

    /// <summary>
    /// Main constructor.
    /// </summary>
    /// <remarks>
    /// All Option classes need to have a constructor with no arguments.
    /// 
    /// > [!CAUTION]
    /// > Don't override this constructor to initialize your Option
    /// > subclass. Override <see cref="Configure"/> instead.
    /// </remarks>
    public Option()
    {
        Name = GetType().Name;
        if (Name.StartsWith(OPTION_PREFIX)) {
            Name = Name.Substring(OPTION_PREFIX.Length);
        }

        Parent = null;

        #if UNITY_EDITOR
        Capabilities = OptionCapabilities.PresetDefault;
        var attr = (CapabilitiesAttribute)GetType()
            .GetCustomAttributes(typeof(CapabilitiesAttribute), true)
            .FirstOrDefault();
        if (attr != null) {
            Capabilities = attr.Capabilities;
        }
        #endif
        
        Configure();
        
        if (Variance != OptionVariance.Single) {
            IsDefaultVariant = true;
            if (string.IsNullOrEmpty(VariantDefaultParameter)) {
                if (Variance == OptionVariance.Array) {
                    VariantDefaultParameter = "0";
                } else {
                    VariantDefaultParameter = "Default";
                }
            }
            VariantParameter = VariantDefaultParameter;
        }

        CreateChildren();
    }

    // -------- Variants --------

    /// <summary>
    /// The variance of the Option.
    /// </summary>
    /// <remarks>
    /// By default, an Option is invariant and there's only a single instance / value of it.
    /// 
    /// An Option can also be variant, in which case multiple instances can exist and
    /// the instances are distinguished by their <see cref="VariantParameter"/>.
    /// 
    /// There are two types of variance, where the only difference is if their parameters
    /// are set by the user (<see cref="OptionVariance.Dictionary"/>) or if an index
    /// is assigned automatically as parameter (<see cref="OptionVariance.Array"/>).
    /// 
    /// Variant Options have a default variant (<see cref="IsDefaultVariant"/>), that acts 
    /// as the container for all other variants and its parameter is always set to 
    /// the <see cref="VariantDefaultParameter"/>.
    /// </remarks>
    public OptionVariance Variance { get; protected set; }

    /// <summary>
    /// The parameter of a variant Option.
    /// </summary>
    /// <remarks>
    /// The parameter is only used when <see cref="Variance"/> is not 
    /// <see cref="OptionVariance.Single"/>.
    /// 
    /// When <see cref="Variance"/> is <see cref="OptionVariance.Array"/>, the
    /// parameter is assigned automatically and will be overwritten if it's changed.
    /// </remarks>
    public string VariantParameter {
        get {
            return _variantParameter;
        }
        set {
            Assert.IsTrue(Variance != OptionVariance.Single, "Cannot set VariantParameter, option is not variant.");

            if (_variantParameter == value)
                return;

            if (Parent != null && Parent.GetVariant(value, false) != null) {
                throw new Exception("A variant with parameter '" + value + "' already exists.");
            }

            _variantParameter = value;
        }
    }
    string _variantParameter;

    /// <summary>
    /// The parameter of the default variant.
    /// </summary>
    /// <remarks>
    /// Variants are created on-demand when new parameters appear. To ensure
    /// the Option can always receive callbacks, a single Option is always
    /// guaranteed to exist and that Option uses the variant default parameter.
    /// </remarks>
    public string VariantDefaultParameter { get; protected set; }

    /// <summary>
    /// Wether this option instance is the default variant.
    /// </summary>
    /// <remarks>
    /// Variant options can have an arbitrary number of instances, each with
    /// a different variant parameter to distinguish them. Variant options are
    /// created on-demand when a new parameter appears. However, the one
    /// instance using the <see cref="VariantDefaultParameter"/> is guaranteed 
    /// to always exist and acts as container for the other variants.
    /// 
    /// <see cref="AddVariant"/>, <see cref="GetVariant"/> and <see cref="RemoveVariant"/>
    /// can only be called on the default variants.
    /// </remarks>
    public bool IsDefaultVariant { get; set; }

    private List<Option> variants;

    /// <summary>
    /// The variants contained in the default variant.
    /// </summary>
    /// <remarks>
    /// Only the <see cref="IsDefaultVariant" /> Option can contain variants.
    /// All other Options return an empty enumerable.
    /// 
    /// The enumerable does not contain the default variant itself.
    /// </remarks>
    public IEnumerable<Option> Variants {
        get {
            if (variants == null) {
                return Enumerable.Empty<Option>();
            } else {
                return variants;
            }
        }
    }

    /// <summary>
    /// Add a new variant option.
    /// </summary>
    /// <remarks>
    /// > [!WARNING]
    /// > This method can only be called on default variants, where
    /// > <see cref="IsDefaultVariant" /> is true, and will throw an 
    /// > exception when called on other Options.
    /// 
    /// > [!TIP]
    /// > For array variants, the parameter can be assigned arbitrarily and
    /// > will be overwritten on insertion. However it can be used to control
    /// > where the variant will be inserted, as parameters will be first
    /// > sorted before the indices are re-assigned (e.g. inserting "5.5"
    /// > will insert the variant between "5" and "6").
    /// </remarks>
    public Option AddVariant(string parameter)
    {
        Assert.IsTrue(Variance != OptionVariance.Single, "Invalid call to AddVariant, option is not variant.");
        Assert.IsTrue(IsDefaultVariant, "Invalid call to AddVariant, option is not the default variant.");

        Assert.IsNotNull(parameter);
        Assert.IsFalse(string.Equals(parameter, VariantDefaultParameter, StringComparison.OrdinalIgnoreCase), "Cannot add variant with default parameter.");
        Assert.IsTrue(variants == null || variants.Find(v => v.VariantParameter.EqualsIgnoringCase(parameter)) == null, "Variant with parameter already exists.");

        var instance = (Option)Activator.CreateInstance(GetType());
        instance.Parent = this;
        instance.VariantParameter = parameter;
        instance.IsDefaultVariant = false;

        if (variants == null)
            variants = new List<Option>();
        variants.Add(instance);

        if (Variance == OptionVariance.Array) {
            RenumberArrayVariants();
        }

        return instance;
    }

    /// <summary>
    /// Get the variant Option with the given parameter.
    /// </summary>
    /// <remarks>
    /// > [!WARNING]
    /// > This method can only be called on default variants, where
    /// > <see cref="IsDefaultVariant" /> is true, and will throw an 
    /// > exception when called on other Options.
    /// 
    /// `GetVariant` can also be used to get the default variant itself,
    /// i.e. when <paramref name="parameter"/> equals <see cref="VariantDefaultParameter" />,
    /// the method will return `this`.
    /// </remarks>
    /// <param name="create">Wether a new variant should be created if one doesn't currently exist</param>
    public Option GetVariant(string parameter, bool create = true)
    {
        Assert.IsTrue(Variance != OptionVariance.Single, "Invalid call to GetVariant, option is not variant.");
        Assert.IsTrue(IsDefaultVariant, "Invalid call to GetVariant, option is not the default variant.");

        if (string.Equals(parameter, VariantDefaultParameter, StringComparison.OrdinalIgnoreCase))
            return this;

        if (!create && variants == null)
            return null;

        Option variant = null;
        if (variants != null) {
            variant = variants.Find(v => v.VariantParameter.EqualsIgnoringCase(parameter));
        }
        
        if (create && variant == null) {
            variant = AddVariant(parameter);
        }

        return variant;
    }

    /// <summary>
    /// Remove a variant Option.
    /// </summary>
    /// <remarks>
    /// > [!WARNING]
    /// > This method can only be called on default variants, where
    /// > <see cref="IsDefaultVariant" /> is true, and will throw an 
    /// > exception when called on other Options.
    /// </remarks>
    public void RemoveVariant(Option option)
    {
        Assert.IsTrue(Variance != OptionVariance.Single, "Invalid call to RemoveVariant, option is not variant.");
        Assert.IsTrue(IsDefaultVariant, "Invalid call to RemoveVariant, option is not the default variant.");

        Assert.IsTrue(variants != null && variants.Contains(option), "Invalid call to RemoveVariant, option is not a variant of this instance.");

        variants.Remove(option);
        option.Parent = null;

        if (Variance == OptionVariance.Array) {
            RenumberArrayVariants();
        }
    }

    /// <summary>
    /// Remove all variant Options.
    /// </summary>
    /// <remarks>
    /// > [!WARNING]
    /// > This method can only be called on default variants, where
    /// > <see cref="IsDefaultVariant" /> is true, and will throw an 
    /// > exception when called on other Options.
    /// </remarks>
    public void ClearVariants()
    {
        Assert.IsTrue(Variance != OptionVariance.Single, "Invalid call to ClearVariants, option is not variant.");
        Assert.IsTrue(IsDefaultVariant, "Invalid call to ClearVariants, option is not the default variant.");

        if (variants == null) return;

        foreach (var variant in variants) {
            variant.Parent = null;
        }
        variants.Clear();
    }

    /// <summary>
    /// Internal helper method that ensures parameters in array variants 
    /// are all numbers and sequential.
    /// </summary>
    protected void RenumberArrayVariants()
    {
        Assert.IsTrue(Variance == OptionVariance.Array, "Invalid call to RenumberArrayVariants, option is not an array variant.");
        Assert.IsTrue(IsDefaultVariant, "Invalid call to RenumberArrayVariants, option is not the default variant.");

        // Default variant is always 0
        VariantParameter = "0";

        // First order parameters using natural sort, then assign sequential indices
        var comparer = NumericStringComparer.Instance;
        variants.Sort((a, b) => comparer.Compare(a.VariantParameter, b.VariantParameter));
        for (int i = 0; i < variants.Count; i++) {
            variants[i].VariantParameter = (i + 1).ToString();
        }
    }

    // -------- Children --------

    private List<Option> children;

    /// <summary>
    /// Wether this Option has children.
    /// </summary>
    public bool HasChildren {
        get {
            return children != null && children.Count > 0;
        }
    }

    /// <summary>
    /// The children of this Option.
    /// </summary>
    /// <remarks>
    /// Child Options are nested classes of the current Option class.
    /// They are detected automatically and instantiated when their
    /// parent Option is instantiated.
    /// </remarks>
    public IEnumerable<Option> Children {
        get {
            if (children == null) {
                return Enumerable.Empty<Option>();
            } else {
                return children;
            }
        }
    }

    /// <summary>
    /// Internal helper method to create the child Option instances.
    /// </summary>
    protected void CreateChildren()
    {
        var type = GetType();

        var nested = type.GetNestedTypes(BindingFlags.Public);
        foreach (var nestedType in nested) {
            if (nestedType.IsAbstract || !typeof(Option).IsAssignableFrom(nestedType))
                continue;

            if (children == null)
                children = new List<Option>();

            var child = (Option)Activator.CreateInstance(nestedType);
            child.Parent = this;
            children.Add(child);
        }

        if (children != null) {
            children.Sort((a, b) => a.ApplyOrder.CompareTo(b.ApplyOrder));
        }
    }

    /// <summary>
    /// Get a child Option by its name.
    /// </summary>
    public Option GetChild(string name)
    {
        if (children == null)
            return null;

        foreach (var child in children) {
            if (child.Name.EqualsIgnoringCase(name)) {
                return child;
            }
        }

        return null;
    }

    /// <summary>
    /// Get a child Option instance by its type.
    /// </summary>
    public TOption GetChild<TOption>() where TOption : Option
    {
        if (children != null) {
            foreach (var child in children) {
                if (child is TOption)
                    return (TOption)child;
            }
        }

        return default(TOption);
    }

    // -------- Category --------

    /// <summary>
    /// Category of the Option.
    /// </summary>
    /// <remarks>
    /// The category is used to group options in the editor.
    /// 
    /// > [!NOTE]
    /// > This property only applies to main Options. Child and variant Options
    /// > will inherit the category from their main parent.
    /// </remarks>
    public string Category {
        get {
            return _category;
        }
        protected set {
            _category = value;
        }
    }
    string _category = "General";
}

/// <summary>
/// Option subclass that defines the type of the Option value.
/// </summary>
/// <remarks>
/// This subclass is mostly an informal standard, adopted by all
/// subclasses in <see cref="sttz.Trimmer.BaseOptions"/> but not
/// actually used in the rest of Trimmer's code.
/// </remarks>
public abstract class Option<TValue> : Option
{
    /// <summary>
    /// The typed value of the Option.
    /// </summary>
    public TValue Value { get; set; }

    /// <summary>
    /// The default value, used when input is empty or invalid.
    /// </summary>
    public TValue DefaultValue { get; protected set; }

    /// <summary>
    /// Parse a string value to the Option Value's type.
    /// </summary>
    /// <remarks>
    /// If the input is empty or parsing fails, <see cref="DefaultValue"/>
    /// should be returned.
    /// </remarks>
    public abstract TValue Parse(string input);

    /// <summary>
    /// Serialize a typed value to a string.
    /// </summary>
    /// <remarks>
    /// The string returned by Save can later be fed back to <see cref="Parse" />
    /// and should survive the round-trip without loss.
    /// </remarks>
    public abstract string Save(TValue input);
}

}

#endif
