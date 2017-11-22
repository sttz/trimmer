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

using UnityEngine;

namespace sttz.Trimmer
{

/// <summary>
/// Script used by <see cref="OptionHelper.GetSingleton*"/> and
/// <see cref="OptionHelper.InjectFeature*"/> to make the injected 
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