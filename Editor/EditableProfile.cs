using sttz.Workbench.Extensions;
using UnityEditor;
using UnityEngine;

namespace sttz.Workbench
{

/// <summary>
/// Base class for <c>EditorProfile</c> and <c>BuildProfile</c>.
/// </summary>
public abstract class EditableProfile : ScriptableObject
{
    public abstract ValueStore Store { get; }
    public abstract void SaveIfNeeded();
    public abstract void EditOption(GUIContent label, IOption option, ValueStore.Node node);
}

}