//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

using UnityEngine;

namespace sttz.Trimmer
{

/// <summary>
/// Script used by <see cref="OptionHelper.GetSingleton*"/> and
/// <see cref="OptionHelper.InjectFeature"/> to make the injected 
/// singleton scripts persist over scene loads.
/// </summary>
public class InjectionContainer : MonoBehaviour
{
    void OnEnable()
    {
        DontDestroyOnLoad(gameObject);
    }
}

}
