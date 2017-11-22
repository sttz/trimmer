//
// Trimmer Framework for Unity - https://sttz.ch/trimmer
// Copyright © 2017 Adrian Stutz
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
//

using System;
using UnityEngine;

namespace sttz.Trimmer
{

/// <summary>
/// Container to hold the project's <see cref="Version"/> to
/// let Unity serialize it during the build.
/// </summary>
public class VersionContainer : MonoBehaviour
{
    public Version version;

    void OnEnable()
    {
        Version.ProjectVersion = version;
        Debug.Log(Application.productName + " " + Version.ProjectVersion);
    }
}

}