using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace UmbraSharp;

/// <summary>the lua value type, which causes no allocation when constructed</summary>
public readonly struct Val {
	/// <summary>the type of a value</summary>
	public enum Ty: byte {
		/// <summary>a value representing nothing</summary>
		Nil = 0,
		/// <summary>true/false</summary>
		Boolean = 1,
		/// <summary>a long or double value</summary>
		Number = 2,
		/// <summary>a string of text</summary>
		String = 3,
		/// <summary>a value representing a CLR class instance</summary>
		Userdata = 4,
		/// <summary>a CLR or lua function</summary>
		Function = 5,
		/// <summary>a coroutine, which can be resumed</summary>
		Thread = 6,
		/// <summary>a table, containing array and hash data</summary>
		Table = 7,
	}

	/// <summary>the value of <see cref="Ty.Nil" /></summary>
	public static readonly Val NIL = default; // bits 0 would make this.ty == Ty.Nil, which is what we want
	/// <summary>the value of a true <see cref="Ty.Boolean" /></summary>
	public static readonly Val TRUE = new(Ty.Boolean, 1, null, null);
	/// <summary>the value of a false <see cref="Ty.Boolean" /></summary>
	public static readonly Val FALSE = new(Ty.Boolean, 0, null, null);

	// two values that will never be used anywhere else, so we use them to mark whether an number is an integer or float
	/// <summary>internal long marker</summary>
	private static readonly object NUM_INT = new();
	/// <summary>internal double marker</summary>
	private static readonly object NUM_FLT = new();

	/// <summary>type name of some type <paramref name="ty" /></summary>
	public static string type_name_of(Ty ty) => ty switch {
		Ty.Nil => "nil",
		Ty.Boolean => "boolean",
		Ty.Number => "number",
		Ty.String => "string",
		Ty.Userdata => "userdata",
		Ty.Function => "function",
		Ty.Thread => "thread",
		Ty.Table => "table",
		_ => "?",
	};

	// todo: specific exception types?
	/// <summary>helper to throw an exception indicating the <paramref name="got" /> type was not the same as <paramref name="expected" /></summary>
	[DoesNotReturn]
	public static void expected(Ty expected, Ty got) => throw new Exception($"{Val.type_name_of(expected)} expected, got {Val.type_name_of(got)}");
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
	public static T expected<T>(Ty expected, Ty got) => throw new Exception($"{Val.type_name_of(expected)} expected, got {Val.type_name_of(got)}");

	public readonly Ty ty;

	// boolean => `0` (false) or any other number (true, usually 1)
	// number => bits for the `long` or `double`
	private readonly long repr_stack;
	// number => `NUM_INT` or `NUM_FLT` determining the type of the value
	// string => `string` value
	// userdata => `object` underlying object
	// function => `FnProto` prototype
	// thread => `Coro` instance
	// table => `Table` instance
	private readonly object? repr_heap;
	// function (lua) => `Slot[]?` array of upvars
	// function (clr) => `object?` extra info
	private readonly object? repr_extra;

	#region type extraction

	public bool is_boolean => this.ty == Ty.Boolean;
	public bool as_boolean => this.is_boolean ? this.assume_boolean : Val.expected<bool>(Ty.Boolean, this.ty);
	public bool assume_boolean => this.repr_stack != 0;

	#region numbers

	public bool is_number => this.ty == Ty.Number;

	public bool is_long => object.ReferenceEquals(this.repr_heap, Val.NUM_INT);
	public long as_long_bits => this.is_number ? this.assume_long_bits : Val.expected<long>(Ty.Number, this.ty);
	public long assume_long_bits => this.repr_stack;

	public long as_long_trunc => this.is_long ? this.as_long_bits : (long)this.as_double_bits;
	public long assume_long_trunc => this.is_long ? this.assume_long_bits : (long)this.assume_double_bits;
	private long double_to_long_assert() {
		var f = this.assume_double_bits;
		var i = (long)f;
		return i == f ? i : throw new Exception("number has no integer representation");
	}
	public long as_long_assert => this.is_long ? this.as_long_bits : this.double_to_long_assert();
	public long assume_long_assert => this.is_long ? this.assume_long_bits : this.double_to_long_assert();

	public bool is_double => object.ReferenceEquals(this.repr_heap, Val.NUM_FLT);
	public double as_double_bits => this.is_number ? this.assume_double_bits : Val.expected<long>(Ty.Number, this.ty);
	public double assume_double_bits => BitConverter.Int64BitsToDouble(this.repr_stack);

	public double as_double => this.is_double ? this.as_double_bits : this.as_long_bits;
	public double assume_double => this.is_double ? this.assume_double_bits : this.assume_long_bits;

	#endregion numbers

	public bool is_string => this.ty == Ty.String;
	public string as_string => this.is_string ? this.assume_string : Val.expected<string>(Ty.String, this.ty);
	public string assume_string => (string)this.repr_heap!;

	public bool is_userdata => this.ty == Ty.Userdata;
	public UserData as_userdata => this.is_userdata ? this.assume_userdata : Val.expected<UserData>(Ty.Userdata, this.ty);
	public UserData assume_userdata => (UserData)this.repr_heap!;

	public bool is_function => this.ty == Ty.Function;
	public Fn as_function => this.is_function ? this.assume_function : Val.expected<Fn>(Ty.Function, this.ty);
	public Fn assume_function => new((FnProto)this.repr_heap!, this.repr_extra);

	public bool is_thread => this.ty == Ty.Thread;
	public Coro as_thread => this.is_thread ? this.assume_thread : Val.expected<Coro>(Ty.Thread, this.ty);
	public Coro assume_thread => (Coro)this.repr_heap!;

	public bool is_table => this.ty == Ty.Table;
	public Table as_table => this.is_table ? this.assume_table : Val.expected<Table>(Ty.Table, this.ty);
	public Table assume_table => (Table)this.repr_heap!;

	#endregion type extraction

	#region constructors

	private Val(Ty ty, long repr_stack, object? repr_heap, object? repr_extra) {
		this.ty = ty;
		this.repr_stack = repr_stack;
		this.repr_heap = repr_heap;
		this.repr_extra = repr_extra;
	}
	public Val() : this(Ty.Nil, 0, null, null) { }
	public Val(bool value) : this(Ty.Boolean, value ? 1 : 0, null, null) { }
	public Val(long value) : this(Ty.Number, value, null, null) { }
	public Val(double value) : this(Ty.Number, BitConverter.DoubleToInt64Bits(value), null, null) { }
	public Val(string value) : this(Ty.String, 0, value, null) { }
	public Val(UserData value) : this(Ty.Userdata, 0, value, null) { }
	public Val(Fn value) : this(Ty.Function, 0, value.proto, value.extra) { }
	public Val(Coro value) : this(Ty.Thread, 0, value, null) { }
	public Val(Table value) : this(Ty.Table, 0, value, null) { }

	#endregion constructors

	public string type_name() => Val.type_name_of(this.ty);

	#region raw operators

	public bool raw_equals(Val rhs) => (this.ty, rhs.ty) switch {
		(Ty.Nil, Ty.Nil) => true,
		(Ty.Boolean, Ty.Boolean) => this.repr_stack == rhs.repr_stack,
		(Ty.String, Ty.String) => this.repr_heap == rhs.repr_heap,
		(Ty.Userdata, Ty.Userdata) => this.repr_heap == rhs.repr_heap && this.repr_extra == rhs.repr_extra,
		(Ty.Function, Ty.Function) => this.repr_heap == rhs.repr_heap && this.repr_extra == rhs.repr_extra,
		(Ty.Thread, Ty.Thread) => this.repr_heap == rhs.repr_heap,
		(Ty.Table, Ty.Table) => this.repr_heap == rhs.repr_heap,
		_ => false,
	};

	public string raw_tostring() => this.ty switch {
		Ty.Nil => "nil",
		Ty.Boolean => this.as_boolean ? "true" : "false",
		// todo: match lua (inf, nan, proper digits, etc)
		Ty.Number => this.is_long ? this.as_long_trunc.ToString() : this.as_double.ToString(),
		Ty.String => this.as_string!,
		// this is a hack, hashcodes aren't guaranteed to be unique
		// PUC-Rio lua just uses the actual pointer, we could *technically* do that by pinning the object for a moment,
		// but the GC is free to relocate objects (defragmenting, etc), so that's not stable.
		// the other option would be to do it like MoonSharp does: increment an integer for each alloc, and use that.
		// however this means storing an extra component of data:
		// - userdata/threads/tables could store it in themselves
		// - functions would have to do both:
		//   - store one in the function prototype (for when no upvalues/extra)
		//   - wrap upvalues/extra in another class (extra alloc *solely to store an integer*)
		// .....or we can pin the object eternally (incredibly awful idea) (who up leaking they memory)
		Ty.Userdata => $"userdata: {RuntimeHelpers.GetHashCode(this.repr_heap)}",
		Ty.Function => $"function: {HashCode.Combine(RuntimeHelpers.GetHashCode(this.repr_heap), RuntimeHelpers.GetHashCode(this.repr_extra))}",
		Ty.Thread => $"thread: {RuntimeHelpers.GetHashCode(this.repr_heap)}",
		Ty.Table => $"table: {RuntimeHelpers.GetHashCode(this.repr_heap)}",
		_ => "?",
	};

	// todo: more

	#endregion raw operators

	#region lua operators

	// todo: implement once root calls, processing loop, tables, userdata [meta,]method resolution are real

	#endregion

	public override string ToString() => this.raw_tostring();

	public override int GetHashCode() => this.ty switch {
		Ty.Nil => 0,
		// normally would be 0 or 1, but 0 is nil so might as well just -1
		Ty.Boolean => this.as_boolean ? 1 : -1,
		Ty.Number => this.as_long_bits.GetHashCode(),
		Ty.String => this.as_string.GetHashCode(),
		Ty.Userdata => RuntimeHelpers.GetHashCode(this.repr_heap),
		Ty.Function => HashCode.Combine(RuntimeHelpers.GetHashCode(this.repr_heap), RuntimeHelpers.GetHashCode(this.repr_extra)),
		Ty.Thread => RuntimeHelpers.GetHashCode(this.repr_heap),
		Ty.Table => RuntimeHelpers.GetHashCode(this.repr_heap),
		_ => 0,
	};

	public static implicit operator Val(bool val) => new(val);
	public static implicit operator Val(double val) => new(val);
	public static implicit operator Val(string val) => new(val);
	public static implicit operator Val(Fn val) => new(val);
	// no implicit userdata conversion
	public static implicit operator Val(Coro val) => new(val);
	public static implicit operator Val(Table val) => new(val);
}
