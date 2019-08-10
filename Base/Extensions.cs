//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

#if !NO_TRIMMER || UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace sttz.Trimmer.Extensions
{

/// <summary>
/// Extension methods used internally in Trimmer.
/// (In a separate namespace so that importing <c>sttz.Trimmer</c> won't import them).
/// </summary>
public static class Extensions
{
    #if NET_2_0 || NET_2_0_SUBSET
    /// <summary>
    /// Prepend a value to a sequence of values.
    /// </summary>
    public static IEnumerable<TSource> Prepend<TSource>(this IEnumerable<TSource> values, TSource value)
    {
        yield return value;
        foreach (TSource item in values) {
            yield return item;
        }
    }

    /// <summary>
    /// Append a value to a sequence of values.
    /// </summary>
    public static IEnumerable<TSource> Append<TSource>(this IEnumerable<TSource> values, TSource value)
    {
        foreach (TSource item in values) {
            yield return item;
        }
        yield return value;
    }
    #endif

    /// <summary>
    /// Add all elements from an enumerable to the collection.
    /// </summary>
    public static void AddRange<TSource>(this ICollection<TSource> collection, IEnumerable<TSource> elements)
    {
        foreach (var element in elements) {
            collection.Add(element);
        }
    }

    /// <summary>
    /// Struct used in <see cref="IterateWith"/> to iterate two
    /// IEnumerables together.
    /// </summary>
    public struct Pair<TFirst, TSecond>
    {
        public TFirst First { get; private set; }
        public TSecond Second { get; private set; }

        public Pair(TFirst first, TSecond second)
        {
            First = first;
            Second = second;
        }
    }

    /// <summary>
    /// Iterate two enumerations together, ending whenever one of the
    /// enumerations reaches its end.
    /// </summary>
    public static IEnumerable<Pair<TFirst, TSecond>> IterateWith<TFirst, TSecond>(this IEnumerable<TFirst> first, IEnumerable<TSecond> second)
    {
        var firstEnumerator = first.GetEnumerator();
        var secondEnumerator = second.GetEnumerator();

        while (firstEnumerator.MoveNext() && secondEnumerator.MoveNext()) {
            yield return new Pair<TFirst, TSecond>(firstEnumerator.Current, secondEnumerator.Current);
        }
    }

    /// <summary>
    /// Join together a enumerable of strings.
    /// </summary>
    public static string Join(this IEnumerable<string> collection, string separator = ", ")
    {
        if (collection == null || !collection.Any())
            return string.Empty;
        
        return collection.Aggregate((c, n) => c + separator + n);
    }

    /// <summary>
    /// Checks if the string equals the current one, ignoring case (using ordinal comparison).
    /// </summary>
    public static bool EqualsIgnoringCase(this string first, string second)
    {
        return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Replace all occurrences of a string by a new value, ignoring the case
    /// of the original string and the search value.
    /// </summary>
    /// <remarks>
    /// Based on this Stackoverflow answer by JeroenV: https://stackoverflow.com/a/25426773/202741
    /// </remarks>
    public static string ReplaceCaseInsensitive(this string str, string oldValue, string newValue)
    {
        int prevPos = 0;
        string retval = str;
        // find the first occurrence of oldValue
        int pos = retval.IndexOf(oldValue, StringComparison.InvariantCultureIgnoreCase);

        while (pos > -1) {
            // remove oldValue from the string
            retval = retval.Remove(pos, oldValue.Length);

            // insert newValue in its place
            retval = retval.Insert(pos, newValue);

            // check if oldValue is found further down
            prevPos = pos + newValue.Length;
            pos = retval.IndexOf(oldValue, prevPos, StringComparison.InvariantCultureIgnoreCase);
        }

        return retval;
    }

    #if UNITY_EDITOR
    
    /// <summary>
    /// Get the OptionCapabilities of an Option type defined by the 
    /// CapabilitiesAttribute or OptionCapabilities.Default, if no attribute exists.
    /// </summary>
    public static OptionCapabilities GetOptionCapabilities(this Type optionType)
    {
        if (!typeof(Option).IsAssignableFrom(optionType)) {
            Debug.LogError("Invalid call to GetCapabilities: Type does not implement Option");
            return OptionCapabilities.None;
        }

        var attr = (CapabilitiesAttribute)optionType
            .GetCustomAttributes(typeof(CapabilitiesAttribute), true)
            .FirstOrDefault();
        return (attr != null ? attr.Capabilities : OptionCapabilities.PresetDefault);
    }

    #endif
}

/// <summary>
/// Compare strings preserving natural number sorting, i.e. "item_2" comes before "item_10".
/// </summary>
/// <remarks>
/// Based on this Stackoverflow answer by Drew Noakes: https://stackoverflow.com/a/41168219/202741
/// </remarks>
public sealed class NumericStringComparer : IComparer<string>
{
    public static NumericStringComparer Instance {
        get { return _instance; }
    }
    static NumericStringComparer _instance = new NumericStringComparer();

    public int Compare(string x, string y)
    {
        // sort nulls to the start
        if (x == null)
            return y == null ? 0 : -1;
        if (y == null)
            return 1;

        var ix = 0;
        var iy = 0;

        while (true)
        {
            // sort shorter strings to the start
            if (ix >= x.Length)
                return iy >= y.Length ? 0 : -1;
            if (iy >= y.Length)
                return 1;

            var cx = x[ix];
            var cy = y[iy];

            int result;
            if (char.IsDigit(cx) && char.IsDigit(cy))
                result = CompareInteger(x, y, ref ix, ref iy);
            else
                result = cx.CompareTo(y[iy]);

            if (result != 0)
                return result;

            ix++;
            iy++;
        }
    }

    private static int CompareInteger(string x, string y, ref int ix, ref int iy)
    {
        var lx = GetNumLength(x, ix);
        var ly = GetNumLength(y, iy);

        // shorter number first (note, doesn't handle leading zeroes)
        if (lx != ly)
            return lx.CompareTo(ly);

        for (var i = 0; i < lx; i++)
        {
            var result = x[ix++].CompareTo(y[iy++]);
            if (result != 0)
                return result;
        }

        return 0;
    }

    private static int GetNumLength(string s, int i)
    {
        var length = 0;
        while (i < s.Length && char.IsDigit(s[i++]))
            length++;
        return length;
    }
}

}

#endif
