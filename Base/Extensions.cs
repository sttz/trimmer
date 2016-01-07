using System;
using System.Collections.Generic;

namespace sttz.Workbench.Extensions
{

/// <summary>
/// Extension methods used internally in Workbench.
/// (In a separate namespace so that importing <c>sttz.Workbench</c> won't import them).
/// </summary>
public static class Extensions
{
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
	/// Equalses the ignoring case.
	/// </summary>
	public static bool EqualsIgnoringCase(this string first, string second)
	{
		return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
	}
}

}

