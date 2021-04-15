//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

#if !NO_TRIMMER || UNITY_EDITOR

using System;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace sttz.Trimmer.BaseOptions
{

/// <summary>
/// Option base class with a Unity asset as value.
/// </summary>
/// <remarks>
/// OptionAsset uses two different strategies to serialize asset references
/// in the editor and in the player.
/// 
/// In the editor, asset references are saved as GUIDs and loaded using Unity's
/// `AssetDatabase` API.
///
/// To make sure references are included in builds, the references are injected
/// into the first scene during build as part of the <see cref="ProfileContainer" />.
/// The Option then uses the GUID at runtime to look up the reference in the 
/// container.
/// 
/// > [!NOTE]
/// > The reference will only be injected into the build when the Option is
/// > included. If only the Option's associated feature is included, the
/// > reference won't be injected and it's up to the subclass to inject the
/// > reference as needed.
/// </remarks>
public abstract class OptionAsset<TUnity> : Option<TUnity> where TUnity : UnityEngine.Object
{
    #if UNITY_EDITOR
    public override bool EditGUI()
    {
        EditorGUI.BeginChangeCheck();
        Value = (TUnity)EditorGUILayout.ObjectField(Value, typeof(TUnity), false);
        return EditorGUI.EndChangeCheck();
    }

    override public void PostprocessScene(Scene scene, OptionInclusion inclusion)
    {
        base.PostprocessScene(scene, inclusion);

        // Only include reference when Option is included,
        // we're building the first scene and a reference is set
        if ((inclusion & OptionInclusion.Option) != 0
                && ProfileContainer.Instance != null
                && Value != null && !string.IsNullOrEmpty(GuidValue)) {
            ProfileContainer.Instance.AddReference(GuidValue, Value);
        }
    }
    #endif

    public string GuidValue { get; protected set; }

    override public TUnity Parse(string input)
    {
        if (string.IsNullOrEmpty(input))
            return DefaultValue;

        #if UNITY_EDITOR

        var path = AssetDatabase.GUIDToAssetPath(input);
        if (string.IsNullOrEmpty(path))
            return DefaultValue;

        var asset = (TUnity)AssetDatabase.LoadAssetAtPath(path, typeof(TUnity));
        return asset;

        #else
        
        return ProfileContainer.Instance.GetReference<TUnity>(input);
        
        #endif
    }

    public override void Load(string input)
    {
        GuidValue = input;
        Value = Parse(input);
    }

    override public string Save(TUnity input)
    {
        #if UNITY_EDITOR

        var path = AssetDatabase.GetAssetPath(input);
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        var guid = AssetDatabase.AssetPathToGUID(path);
        return guid;

        #else

        return GuidValue;

        #endif
    }

    public override string Save()
    {
        return Save(Value);
    }
}

}

#endif
