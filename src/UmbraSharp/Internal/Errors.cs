using System.Diagnostics.CodeAnalysis;

namespace UmbraSharp.Internal;

// todo: specific exception types?

/// <summary>error throwing helpers</summary>
public static class Errors {
	// c# will have a never type one day tm
	// trust trust
	// it's coming guys..
	/// <summary>helper to throw an exception indicating the <paramref name="got" /> type was not the same as <paramref name="expected" /></summary>
	/// <remarks>
	/// this function takes a type parameter <typeparamref name="T"/>,
	/// but only uses it to allow it to be used as an expression (eg. else clause in a ternary),
	/// this function never returns, so <typeparamref name="T"/> can be any type.
	/// </remarks>
	[DoesNotReturn]
	public static T expected<T>(string expected, string got) => throw new Exception($"{expected} expected, got {got}");
	[DoesNotReturn]
	public static T expected<T>(Val.Ty expected, Val.Ty got) => Errors.expected<T>(Val.type_name_of(expected), Val.type_name_of(got));

	[DoesNotReturn]
	internal static void table_overflow() => throw new Exception("table overflow");
}