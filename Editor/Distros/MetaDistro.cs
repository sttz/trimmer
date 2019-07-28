//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace sttz.Trimmer.Editor
{

/// <summary>
/// Run any number of other distros.
/// </summary>
[CreateAssetMenu(fileName = "Meta Distro.asset", menuName = "Trimmer/Meta", order = 100)]
public class MetaDistro : DistroBase
{
    public DistroBase[] distros;

    public override bool CanRunWithoutBuildTargets { get { return true; } }

    protected override IEnumerator DistributeCoroutine(IEnumerable<BuildPath> buildPaths, bool forceBuild)
    {
        foreach (var distro in distros) {
            yield return distro.DistributeCoroutine(forceBuild);
            if (!GetSubroutineResult<bool>()) {
                yield return false; yield break;
            }
        }
        yield return true;
    }
}

}
