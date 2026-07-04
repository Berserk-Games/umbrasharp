using System.Diagnostics.CodeAnalysis;

namespace UmbraSharp.Internal;

// todo: specific exception types

// c# will have a never/bottom type one day tm
// trust trust
// it's coming guys..

public class InternalError(string error): Exception {
	public static InternalError unreachable() => new("unreachable code");
	public static InternalError invalid_enum(string more) => new($"unreachable default case in switch over enum (invalid variant: {more})");

	public readonly string error = error;

	public override string Message => $"internal umbrasharp error: {this.error}";
}

[Serializable]
public class SyntaxError(int offset, string error, string? near = null): Exception {
	public static SyntaxError expected(int offset, string what, string near) => new(offset, $"{what} near", near);
	public static SyntaxError unexpected_symbol(int offset, string near) => new(offset, "unexpected symbol", near);
	public static SyntaxError unfinished(int offset, string what, string near = "<eof>") => new(offset, $"unfinished {what}", near);

	public readonly int offset = offset;
	public readonly string error = error;
	public readonly string? near = near;

	public override string Message => this.near is not null ? $"[{offset}] {this.error} near '{this.near}'" : $"[{offset}] {this.error}";
}

/// <summary>runtime error throwing helpers</summary>
public class RuntimeError: Exception {
	/// <summary>helper to throw an exception indicating the <paramref name="got" /> type was not the same as <paramref name="expected" /></summary>
	/// <remarks>
	/// this function takes a type parameter <typeparamref name="T"/>,
	/// but only uses it to allow it to be used as an expression (eg. else clause in a ternary),
	/// this function never returns, so <typeparamref name="T"/> can be any type.
	/// </remarks>
	[DoesNotReturn]
	public static T expected<T>(string expected, string got) => throw new Exception($"{expected} expected, got {got}");
	[DoesNotReturn]
	public static T expected<T>(Val.Ty expected, Val.Ty operand) => RuntimeError.expected<T>(expected.name(), operand.name());
	[DoesNotReturn]
	public static T expected_what<T>(string expected, string got, string what) => throw new Exception($"{what} ({expected} expected, got {got})");

	[DoesNotReturn]
	public static T attempt_to<T>(string action) => throw new Exception($"attempt to {action}");
	[DoesNotReturn]
	public static T attempt_to<T>(string action, string operand) => RuntimeError.attempt_to<T>($"{action} a {operand} value");
	[DoesNotReturn]
	public static T attempt_to<T>(string action, string lhs, string rhs) => RuntimeError.attempt_to<T>($"{action} {lhs} with {rhs}");
	[DoesNotReturn]
	public static T attempt_to_what<T>(string action, string operand, string what) => RuntimeError.attempt_to<T>($"{action} a {operand} value ({what})");
	[DoesNotReturn]
	public static T attempt_to_what<T>(string action, string lhs, string rhs, string what) => RuntimeError.attempt_to<T>($"{action} {lhs} with {rhs} ({what})");

	[DoesNotReturn]
	public static T attempt_to_perform_arithmetic<T>(string action, Val.Ty operand) => RuntimeError.attempt_to<T>($"perform arithmetic ({action}) on {operand.name()}");
	[DoesNotReturn]
	public static T attempt_to_perform_arithmetic<T>(string action, Val.Ty lhs, Val.Ty rhs) => RuntimeError.attempt_to<T>(
		lhs == rhs
		? $"perform arithmetic ({action}) on {lhs.name()}"
		: $"perform arithmetic ({action}) on {lhs.name()} and {rhs.name()}"
	);

	[DoesNotReturn]
	public static T missing_member<T>(Val.Ty indexee, in Val index) => throw new Exception(
		index.is_string
		? $"this {indexee.name()} does not have a key named '{index.assume_string}'"
		: $"cannot index {indexee.name()} with a {index.ty.name()}"
	);

	[DoesNotReturn]
	internal static void table_overflow() => throw new Exception("table overflow");
}

public static class ErrorHelpers {
	public static string dbg_str_or_ty(this Val val) => val.is_string ? $"'{val}'" : val.ty.name();
}

public sealed class None {
	private None() { }
}