using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UmbraSharp.Internal;

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
		/// <summary>a value representing a CLR UserData instance</summary>
		UserData = 4,
		/// <summary>a value representing a CLR reference + tag</summary>
		LightUserData = 5,
		/// <summary>a CLR or Lua function</summary>
		Function = 6,
		/// <summary>a coroutine, which can be resumed</summary>
		Thread = 7,
		/// <summary>a table, containing array and hash data</summary>
		Table = 8,
		/// <summary>a Luau vector, containing 3 floats</summary>
		Vector = 9,
		/// <summary>a Luau buffer, containing binary data</summary>
		Buffer = 10,
		/// <summary>a Luau class object, containing static members and info</summary>
		Class = 11,
		/// <summary>a Luau class instance, containing members and a reference to the class object</summary>
		Object = 12,
	}

	[StructLayout(LayoutKind.Explicit)]
	private readonly struct ReprStack {
		[FieldOffset(0)] public readonly bool as_bool;
		[FieldOffset(0)] public readonly long as_long;
		[FieldOffset(0)] public readonly double as_double;
		[FieldOffset(0)] public readonly Vector3 as_vec;

		public ReprStack(bool b) : this() => this.as_bool = b;
		public ReprStack(long i) : this() => this.as_long = i;
		public ReprStack(double d) : this() => this.as_double = d;
		public ReprStack(Vector3 v) : this() => this.as_vec = v;
	}

	/// <summary>the value of <see cref="Ty.Nil" /></summary>
	public static readonly Val NIL = default; // bits 0 would make this.ty == Ty.Nil, which is what we want
	/// <summary>the value of a true <see cref="Ty.Boolean" /></summary>
	public static readonly Val TRUE = new(true);
	/// <summary>the value of a false <see cref="Ty.Boolean" /></summary>
	public static readonly Val FALSE = new(false);

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
		Ty.UserData => "userdata",
		Ty.LightUserData => "userdata",
		Ty.Function => "function",
		Ty.Thread => "thread",
		Ty.Table => "table",
		Ty.Vector => "vector",
		Ty.Buffer => "buffer",
		Ty.Class => "class",
		Ty.Object => "object",
		_ => "?",
	};

	public readonly Ty ty;

	// boolean => `0` (false) or any other number (true, usually 1)
	// number => bits for the `long` or `double`
	private readonly ReprStack repr_stack;
	// number => `NUM_INT` or `NUM_FLT` determining the type of the value
	// string => `string` value
	// userdata => `UserData` underlying object
	// lightuserdata = `object?` underlying object
	// function => `FnProto` prototype
	// thread => `Coro` instance
	// table => `Table` instance
	// buffer => `byte[]` data
	// class => `ClassObject` info
	// object => `ClassObject` info
	private readonly object? repr_heap;
	// lightuserdata => `string` tag
	// function (lua) => `Slot[]` array of upvars
	// function (clr) => `object?` extra info
	// object => `Val[]` array of members
	private readonly object? repr_extra;

	#region type extraction

	public bool is_nil => this.ty == Ty.Nil;
	public void as_nil() {
		if (!this.is_nil) Errors.expected<bool>(Ty.Nil, this.ty);
	}

	public bool is_boolean => this.ty == Ty.Boolean;
	public bool as_boolean => this.is_boolean ? this.assume_boolean : Errors.expected<bool>(Ty.Boolean, this.ty);
	public bool assume_boolean => this.repr_stack.as_bool;

	#region numbers

	public bool is_number => this.ty == Ty.Number;

	public bool is_long => object.ReferenceEquals(this.repr_heap, Val.NUM_INT);
	public long as_long_bits => this.is_number ? this.assume_long_bits : Errors.expected<long>(Ty.Number, this.ty);
	public long assume_long_bits => this.repr_stack.as_long;

	public long as_long_trunc => this.is_long ? this.as_long_bits : (long)this.as_double_bits;
	public long assume_long_trunc => this.is_long ? this.assume_long_bits : (long)this.assume_double_bits;
	private long? try_double_to_long() {
		var f = this.assume_double_bits;
		var i = (long)f;
		return i == f ? i : null;
	}
	public long? as_long_try => this.is_long ? this.as_long_bits : this.try_double_to_long();
	public long? assume_long_try => this.is_long ? this.assume_long_bits : this.try_double_to_long();
	private long assert_double_to_long() => this.try_double_to_long() ?? throw new Exception("number has no integer representation");
	public long as_long_assert => this.is_long ? this.as_long_bits : this.assert_double_to_long();
	public long assume_long_assert => this.is_long ? this.assume_long_bits : this.assert_double_to_long();

	public bool is_double => object.ReferenceEquals(this.repr_heap, Val.NUM_FLT);
	public double as_double_bits => this.is_number ? this.assume_double_bits : Errors.expected<long>(Ty.Number, this.ty);
	public double assume_double_bits => this.repr_stack.as_double;

	public double as_double => this.is_double ? this.as_double_bits : this.as_long_bits;
	public double assume_double => this.is_double ? this.assume_double_bits : this.assume_long_bits;

	#endregion numbers

	public bool is_string => this.ty == Ty.String;
	public string as_string => this.is_string ? this.assume_string : Errors.expected<string>(Ty.String, this.ty);
	public string assume_string => (string)this.repr_heap!;

	public bool is_userdata => this.ty == Ty.UserData;
	public UserData as_userdata_raw => this.is_userdata ? this.assume_userdata_raw : Errors.expected<UserData>(Ty.UserData, this.ty);
	public UserData assume_userdata_raw => (UserData)this.repr_heap!;

	public bool is_lightuserdata => this.ty == Ty.LightUserData;
	public object? as_lightuserdata_raw => this.is_lightuserdata ? this.assume_lightuserdata_raw : Errors.expected<object>(Ty.LightUserData, this.ty);
	public object? assume_lightuserdata_raw => this.repr_heap;
	public string as_lightuserdata_tag => this.is_lightuserdata ? this.assume_lightuserdata_tag : Errors.expected<string>(Ty.LightUserData, this.ty);
	public string assume_lightuserdata_tag => (string)this.repr_heap!;
	public T as_lightuserdata<T>(string tag, string? friendly = null) {
		if (!this.is_lightuserdata || this.assume_lightuserdata_tag != tag) Errors.expected<T>(friendly ?? tag, Val.type_name_of(this.ty));
		return (T)this.assume_lightuserdata_raw!;
	}

	public bool is_function => this.ty == Ty.Function;
	public Fn as_function => this.is_function ? this.assume_function : Errors.expected<Fn>(Ty.Function, this.ty);
	public Fn assume_function => new((FnProto)this.repr_heap!, this.repr_extra);

	public bool is_thread => this.ty == Ty.Thread;
	public Coro as_thread => this.is_thread ? this.assume_thread : Errors.expected<Coro>(Ty.Thread, this.ty);
	public Coro assume_thread => (Coro)this.repr_heap!;

	public bool is_table => this.ty == Ty.Table;
	public Table as_table => this.is_table ? this.assume_table : Errors.expected<Table>(Ty.Table, this.ty);
	public Table assume_table => (Table)this.repr_heap!;

	public bool is_vector => this.ty == Ty.Vector;
	public Vector3 as_vector => this.is_vector ? this.assume_vector : Errors.expected<Vector3>(Ty.Vector, this.ty);
	public Vector3 assume_vector => this.repr_stack.as_vec;

	public bool is_buffer => this.ty == Ty.Buffer;
	public byte[] as_buffer => this.is_buffer ? this.assume_buffer : Errors.expected<byte[]>(Ty.Buffer, this.ty);
	public byte[] assume_buffer => (byte[])this.repr_heap!;

	// todo: luau class/object

	public bool is_truthy => this.ty != Ty.Nil && (this.ty != Ty.Boolean || this.assume_boolean);

	#endregion type extraction

	#region constructors

	private Val(Ty ty, ReprStack repr_stack, object? repr_heap = null, object? repr_extra = null) {
		this.ty = ty;
		this.repr_stack = repr_stack;
		this.repr_heap = repr_heap;
		this.repr_extra = repr_extra;
	}
	public Val() : this(Ty.Nil, default) { }
	public Val(bool value) : this(Ty.Boolean, new(value)) { }
	public Val(long value) : this(Ty.Number, new(value), Val.NUM_INT) { }
	public Val(double value) : this(Ty.Number, new(value), Val.NUM_FLT) { }
	public Val(string value) : this(Ty.String, default, value, null) { }
	public Val(UserData value) : this(Ty.UserData, default, value, null) { }
	public Val(object value, string tag) : this(Ty.LightUserData, default, value, tag) { }
	public Val(Fn value) : this(Ty.Function, default, value.proto, value.extra) { }
	public Val(Coro value) : this(Ty.Thread, default, value, null) { }
	public Val(Table value) : this(Ty.Table, default, value, null) { }

	#endregion constructors

	public string type_name => Val.type_name_of(this.ty);

	#region raw operators

	public bool raw_eq(Val rhs) => (this.ty, rhs.ty) switch {
		(Ty.Nil, Ty.Nil) => true,
		(Ty.Boolean, Ty.Boolean) => this.assume_boolean == rhs.assume_boolean,
		(Ty.Number, Ty.Number) => (this.is_long, rhs.is_long) switch {
			(true, true) => this.assume_long_bits == rhs.assume_long_bits,
			(false, false) => this.assume_double_bits == rhs.assume_double_bits,
			(true, false) => this.assume_double == rhs.assume_double_bits,
			(false, true) => this.assume_double_bits == rhs.assume_double,
		},
		(Ty.String, Ty.String) => this.repr_heap == rhs.repr_heap,
		(Ty.UserData, Ty.UserData) => this.repr_heap == rhs.repr_heap && this.repr_extra == rhs.repr_extra,
		(Ty.LightUserData, Ty.LightUserData) => this.repr_heap == rhs.repr_heap,
		(Ty.Function, Ty.Function) => this.repr_extra == rhs.repr_extra, // only compare extra since upvalue slot arrays are unique
		(Ty.Thread, Ty.Thread) => this.repr_heap == rhs.repr_heap,
		(Ty.Table, Ty.Table) => this.repr_heap == rhs.repr_heap,
		(Ty.Vector, Ty.Vector) => this.assume_vector == rhs.assume_vector,
		(Ty.Buffer, Ty.Buffer) => this.repr_heap == rhs.repr_heap,
		(Ty.Class, Ty.Class) => this.repr_heap == rhs.repr_heap,
		(Ty.Object, Ty.Object) => this.repr_extra == rhs.repr_extra, // only compare extra since member arrays are unique
		_ => false,
	};

	public string raw_tostring() => this.ty switch {
		Ty.Nil => "nil",
		Ty.Boolean => this.repr_stack.as_bool ? "true" : "false",
		// todo: match lua (inf, nan, proper digits, etc)
		Ty.Number => this.is_long ? this.assume_long_bits.ToString() : this.assume_double_bits.ToString(),
		Ty.String => this.assume_string,
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
		Ty.UserData => $"userdata: {RuntimeHelpers.GetHashCode(this.repr_heap)}",
		Ty.Function => $"function: {HashCode.Combine(RuntimeHelpers.GetHashCode(this.repr_heap), RuntimeHelpers.GetHashCode(this.repr_extra))}",
		Ty.Thread => $"thread: {RuntimeHelpers.GetHashCode(this.repr_heap)}",
		Ty.Table => $"table: {RuntimeHelpers.GetHashCode(this.repr_heap)}",
		// todo: match luau
		Ty.Vector => $"{this.assume_vector.X}, {this.assume_vector.Y}, {this.assume_vector.Z}",
		Ty.Buffer => $"buffer: {RuntimeHelpers.GetHashCode(this.repr_heap)}",
		// todo: double check that these two are correct
		Ty.Class => $"class: {RuntimeHelpers.GetHashCode(this.repr_heap)}",
		Ty.Object => $"object: {RuntimeHelpers.GetHashCode(this.repr_extra)}",
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
		Ty.Boolean => this.assume_boolean ? 1 : -1,
		Ty.Number => this.assume_long_bits.GetHashCode(),
		Ty.String => this.assume_string.GetHashCode(),
		Ty.Function => HashCode.Combine(RuntimeHelpers.GetHashCode(this.repr_heap), RuntimeHelpers.GetHashCode(this.repr_extra)),
		Ty.Vector => this.assume_vector.GetHashCode(),
		Ty.UserData | Ty.Thread | Ty.Table | Ty.Buffer | Ty.Class => RuntimeHelpers.GetHashCode(this.repr_heap),
		Ty.Object => RuntimeHelpers.GetHashCode(this.repr_extra),
		_ => 0,
	};

	public static implicit operator Val(bool val) => new(val);
	public static implicit operator Val(long val) => new(val);
	public static implicit operator Val(double val) => new(val);
	public static implicit operator Val(string val) => new(val);
	public static implicit operator Val(Fn val) => new(val);
	// no implicit userdata conversion
	public static implicit operator Val(Coro val) => new(val);
	public static implicit operator Val(Table val) => new(val);
}
