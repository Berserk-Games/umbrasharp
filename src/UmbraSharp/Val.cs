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
	public readonly struct Num {
		[FieldOffset(0)] public readonly long assume_long;
		[FieldOffset(0)] public readonly double assume_double;
		[FieldOffset(sizeof(long))] public readonly bool is_double;

		public Num(long l) : this() {
			this.assume_long = l;
			this.is_double = false;
		}

		public Num(double d) : this() {
			this.assume_double = d;
			this.is_double = true;
		}

		public long as_long_trunc => this.is_double ? (long)this.assume_double : this.assume_long;
		private long? try_double_to_long() {
			var d = this.assume_double;
			var l = (long)d;
			return l == d ? l : null;
		}
		public long? as_long_try => this.is_double ? this.try_double_to_long() : this.assume_long;
		public long as_long_assert => this.as_long_try ?? throw new Exception("number has no integer representation");

		public double as_double => this.is_double ? this.assume_double : this.assume_long;

		public static implicit operator Num(long l) => new(l);
		public static implicit operator Num(double l) => new(l);

		// todo: match lua
		public override string ToString() => this.is_double ? this.assume_double.ToString() : this.assume_long.ToString();

		public override int GetHashCode() => this.is_double ? this.assume_double.GetHashCode() : this.assume_long.GetHashCode();
	}

	[StructLayout(LayoutKind.Explicit)]
	private readonly struct ReprStack {
		[FieldOffset(0)] public readonly bool assume_bool;
		[FieldOffset(0)] public readonly Num assume_num;
		[FieldOffset(0)] public readonly Vector3 assume_vec;
		[FieldOffset(0)] public readonly (int offset, int len) assume_str_header;

		public ReprStack(bool b) : this() => this.assume_bool = b;
		public ReprStack(Num n) : this() => this.assume_num = n;
		public ReprStack(Vector3 v) : this() => this.assume_vec = v;
		public ReprStack(int offset, int len) : this() => this.assume_str_header = (offset, len);
	}

	/// <summary>the value of <see cref="Ty.Nil" /></summary>
	public static readonly Val NIL = default; // bits 0 would make this.ty == Ty.Nil, which is what we want
	/// <summary>the value of a true <see cref="Ty.Boolean" /></summary>
	public static readonly Val TRUE = new(true);
	/// <summary>the value of a false <see cref="Ty.Boolean" /></summary>
	public static readonly Val FALSE = new(false);

	public readonly Ty ty;

	// boolean => `bool` value
	// number => `Num` value
	// lightuserdata => `long` stack part
	// vector => `Vector3` value
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
	// object => `Val[]` array of members
	private readonly object? repr_heap;
	// lightuserdata => `Table?` metatable
	// function (lua) => `Slot[]` array of upvars
	// function (clr) => `object?` extra info
	// object => `ClassObject` class
	private readonly object? repr_extra;

	#region type extraction

	public bool is_nil => this.ty == Ty.Nil;
	public void as_nil() {
		if (!this.is_nil) RuntimeError.expected<bool>(Ty.Nil, this.ty);
	}

	public bool is_boolean => this.ty == Ty.Boolean;
	public bool as_boolean => this.is_boolean ? this.assume_boolean : RuntimeError.expected<bool>(Ty.Boolean, this.ty);
	public bool assume_boolean => this.repr_stack.assume_bool;

	#region numbers

	public bool is_number => this.ty == Ty.Number;
	public bool is_double => this.is_number && this.assume_number.is_double;
	public bool is_long => this.is_number && !this.assume_number.is_double;
	public Num as_number => this.is_number ? this.assume_number : RuntimeError.expected<Num>(Ty.Number, this.ty);
	public Num assume_number => this.repr_stack.assume_num;

	#endregion numbers

	public bool is_string => this.ty == Ty.String;
	public Str as_string => this.is_string ? this.assume_string : RuntimeError.expected<Str>(Ty.String, this.ty);
	public Str assume_string => new((string)this.repr_extra!, this.repr_stack.assume_str_header.offset, this.repr_stack.assume_str_header.len);

	public bool is_userdata => this.ty == Ty.UserData;
	public UserData as_userdata_raw => this.is_userdata ? this.assume_userdata_raw : RuntimeError.expected<UserData>(Ty.UserData, this.ty);
	public UserData assume_userdata_raw => (UserData)this.repr_heap!;

	public bool is_lightuserdata => this.ty == Ty.LightUserData;
	public long as_lightuserdata_stack => this.is_lightuserdata ? this.assume_lightuserdata_stack : RuntimeError.expected<long>(Ty.LightUserData, this.ty);
	public long assume_lightuserdata_stack => this.repr_stack.assume_num.assume_long;
	public object? as_lightuserdata_heap => this.is_lightuserdata ? this.assume_lightuserdata_heap : RuntimeError.expected<object>(Ty.LightUserData, this.ty);
	public object? assume_lightuserdata_heap => this.repr_heap;
	public Table? as_lightuserdata_metatable => this.is_lightuserdata ? this.assume_lightuserdata_metatable : RuntimeError.expected<Table>(Ty.LightUserData, this.ty);
	public Table? assume_lightuserdata_metatable => (Table?)this.repr_extra;
	public T as_lightuserdata<T>(Table metatable, Str? friendly = null) {
		if (!this.is_lightuserdata || this.assume_lightuserdata_metatable != metatable) RuntimeError.expected<T>((friendly ?? metatable.meta(MetaMethod.__type).as_string).ToString(), this.ty.name());
		return (T)this.assume_lightuserdata_heap!;
	}

	public bool is_function => this.ty == Ty.Function;
	public Fn as_function => this.is_function ? this.assume_function : RuntimeError.expected<Fn>(Ty.Function, this.ty);
	public Fn assume_function => new((FnProto)this.repr_heap!, this.repr_extra);

	public bool is_thread => this.ty == Ty.Thread;
	public Coro as_thread => this.is_thread ? this.assume_thread : RuntimeError.expected<Coro>(Ty.Thread, this.ty);
	public Coro assume_thread => (Coro)this.repr_heap!;

	public bool is_table => this.ty == Ty.Table;
	public Table as_table => this.is_table ? this.assume_table : RuntimeError.expected<Table>(Ty.Table, this.ty);
	public Table assume_table => (Table)this.repr_heap!;

	public bool is_vector => this.ty == Ty.Vector;
	public Vector3 as_vector => this.is_vector ? this.assume_vector : RuntimeError.expected<Vector3>(Ty.Vector, this.ty);
	public Vector3 assume_vector => this.repr_stack.assume_vec;

	public bool is_buffer => this.ty == Ty.Buffer;
	public byte[] as_buffer => this.is_buffer ? this.assume_buffer : RuntimeError.expected<byte[]>(Ty.Buffer, this.ty);
	public byte[] assume_buffer => (byte[])this.repr_heap!;

	public bool is_class => this.ty == Ty.Class;
	public ClassDefinition as_class => this.is_class ? this.assume_class : RuntimeError.expected<ClassDefinition>(Ty.Class, this.ty);
	public ClassDefinition assume_class => (ClassDefinition)this.repr_heap!;

	public bool is_object => this.ty == Ty.Object;
	public ClassInstance as_object => this.is_object ? this.assume_object : RuntimeError.expected<ClassInstance>(Ty.Object, this.ty);
	public ClassInstance assume_object => new((Val[])this.repr_heap!, (ClassDefinition)this.repr_extra!);

	public bool is_truthy => this.ty != Ty.Nil && (this.ty != Ty.Boolean || this.assume_boolean);

	#endregion type extraction

	#region constructors

	// todo: nonnull assertions
	private Val(Ty ty, ReprStack repr_stack, object? repr_heap = null, object? repr_extra = null) {
		this.ty = ty;
		this.repr_stack = repr_stack;
		this.repr_heap = repr_heap;
		this.repr_extra = repr_extra;
	}
	public Val() : this(Ty.Nil, default) { }
	public Val(bool value) : this(Ty.Boolean, new(value)) { }
	public Val(Num value) : this(Ty.Number, new(value)) { }
	public Val(long value) : this((Num)value) { }
	public Val(double value) : this((Num)value) { }
	public Val(Str value) : this(Ty.String, new(value.offset, value.len), value.buf, null) { }
	public Val(string value) : this((Str)value) { }
	public Val(UserData value) : this(Ty.UserData, default, value, null) { }
	public static Val light_userdata(long stack, object? heap, Table? metatable) => new(Ty.LightUserData, new(stack), heap, metatable);
	public Val(Fn value) : this(Ty.Function, default, value.proto, value.extra) { }
	public Val(Coro value) : this(Ty.Thread, default, value, null) { }
	public Val(Table value) : this(Ty.Table, default, value, null) { }
	public Val(Vector3 value) : this(Ty.Vector, new(value), null, null) { }
	public Val((float x, float y, float z) value) : this(new Vector3(value.x, value.y, value.z)) { }
	public Val(byte[] value) : this(Ty.Buffer, default, value, null) { }
	public Val(ClassDefinition value) : this(Ty.Class, default, value, null) { }
	public Val(ClassInstance value) : this(Ty.Object, default, value.cls, value.members) { }

	#endregion constructors

	public Val meta(MetaMethod meta) => this.ty switch {
		Val.Ty.UserData => this.assume_userdata_raw.metatable is Table mt ? mt.meta(meta) : default,
		Val.Ty.LightUserData => this.assume_lightuserdata_metatable is Table mt ? mt.meta(meta) : default,
		Val.Ty.Table => this.assume_table.metatable is Table mt ? mt.meta(meta) : default,
		Val.Ty.Class => this.assume_class.class_metatable.meta(meta),
		Val.Ty.Object => this.assume_object.cls.instance_metatable.meta(meta),
		// todo: maybe implement global metatables for other types too (later)
		_ => default,
	};

	#region raw operators

	public Val raw_add(Val rhs) => (this.ty, rhs.ty) switch {
		(Ty.Number, Ty.Number) => (this.is_long && rhs.is_long)
			? this.assume_number.assume_long + rhs.assume_number.assume_long
			: this.assume_number.as_double + rhs.assume_number.as_double,
		(Ty.Vector, Ty.Vector) => this.assume_vector + rhs.assume_vector,
		_ => RuntimeError.attempt_to_perform_arithmetic<Val>("add", this.ty, rhs.ty),
	};

	public Val raw_sub(Val rhs) => (this.ty, rhs.ty) switch {
		(Ty.Number, Ty.Number) => (this.is_long && rhs.is_long)
			? this.assume_number.assume_long - rhs.assume_number.assume_long
			: this.assume_number.as_double - rhs.assume_number.as_double,
		(Ty.Vector, Ty.Vector) => this.assume_vector - rhs.assume_vector,
		_ => RuntimeError.attempt_to_perform_arithmetic<Val>("sub", this.ty, rhs.ty),
	};

	private Vector3 assume_promote_vector => this.is_long ? new(this.assume_number.assume_long) : new((float)this.assume_number.assume_double);
	public Val raw_mul(Val rhs) => (this.ty, rhs.ty) switch {
		(Ty.Number, Ty.Number) => (this.is_long && rhs.is_long)
			? this.assume_number.assume_long * rhs.assume_number.assume_long
			: this.assume_number.as_double * rhs.assume_number.as_double,
		(Ty.Number, Ty.Vector) => this.assume_promote_vector * rhs.assume_vector,
		(Ty.Vector, Ty.Number) => this.assume_vector * rhs.assume_promote_vector,
		(Ty.Vector, Ty.Vector) => this.assume_vector * rhs.assume_vector,
		_ => RuntimeError.attempt_to_perform_arithmetic<Val>("mul", this.ty, rhs.ty),
	};

	public Val raw_div(Val rhs) => (this.ty, rhs.ty) switch {
		(Ty.Number, Ty.Number) => this.assume_number.as_double / rhs.assume_number.as_double,
		(Ty.Number, Ty.Vector) => this.assume_promote_vector / rhs.assume_vector,
		(Ty.Vector, Ty.Number) => this.assume_vector / rhs.assume_promote_vector,
		(Ty.Vector, Ty.Vector) => this.assume_vector / rhs.assume_vector,
		_ => RuntimeError.attempt_to_perform_arithmetic<Val>("div", this.ty, rhs.ty),
	};

	private static Vector3 floor_div_vec(Vector3 lhs, Vector3 rhs) => new(
		MathF.Floor(lhs.X / rhs.X),
		MathF.Floor(lhs.Y / rhs.Y),
		MathF.Floor(lhs.Z / rhs.Z)
	);
	public Val raw_div_floor(Val rhs) => (this.ty, rhs.ty) switch {
		(Ty.Number, Ty.Number) => (long)Math.Floor(this.assume_number.as_double / rhs.assume_number.as_double),
		(Ty.Number, Ty.Vector) => Val.floor_div_vec(this.assume_promote_vector, rhs.assume_vector),
		(Ty.Vector, Ty.Number) => Val.floor_div_vec(this.assume_vector, rhs.assume_promote_vector),
		(Ty.Vector, Ty.Vector) => Val.floor_div_vec(this.assume_vector, rhs.assume_vector),
		_ => RuntimeError.attempt_to_perform_arithmetic<Val>("idiv", this.ty, rhs.ty),
	};

	private static long rem(long lhs, long rhs) => lhs - (lhs / rhs * rhs);
	private static double rem(double lhs, double rhs) => lhs - (Math.Floor(lhs / rhs) * rhs);
	public Val raw_mod(Val rhs) => (this.ty, rhs.ty) switch {
		(Ty.Number, Ty.Number) => (this.is_long && rhs.is_long)
			? Val.rem(this.assume_number.assume_long, rhs.assume_number.assume_long)
			: Val.rem(this.assume_number.as_double, rhs.assume_number.as_double),
		_ => RuntimeError.attempt_to_perform_arithmetic<Val>("mod", this.ty, rhs.ty),
	};

	public Val raw_pow(Val rhs) => (this.ty, rhs.ty) switch {
		(Ty.Number, Ty.Number) => (this.is_long && rhs.is_long)
			? Math.Pow(this.assume_number.assume_long, rhs.assume_number.assume_long)
			: Math.Pow(this.assume_number.as_double, rhs.assume_number.as_double),
		_ => RuntimeError.attempt_to_perform_arithmetic<Val>("pow", this.ty, rhs.ty),
	};

	public Val raw_unm() => this.ty switch {
		Ty.Number => this.is_long
			? -this.assume_number.assume_long
			: -this.assume_number.assume_double,
		Ty.Vector => -this.assume_vector,
		_ => RuntimeError.attempt_to_perform_arithmetic<Val>("unm", this.ty),
	};

	public Val raw_bit_and(Val rhs) => (this.ty, rhs.ty) switch {
		(Ty.Number, Ty.Number) => this.assume_number.as_long_assert & rhs.assume_number.as_long_assert,
		_ => RuntimeError.attempt_to_perform_arithmetic<Val>("band", this.ty, rhs.ty),
	};

	public Val raw_bit_or(Val rhs) => (this.ty, rhs.ty) switch {
		(Ty.Number, Ty.Number) => this.assume_number.as_long_assert | rhs.assume_number.as_long_assert,
		_ => RuntimeError.attempt_to_perform_arithmetic<Val>("bor", this.ty, rhs.ty),
	};

	public Val raw_bit_xor(Val rhs) => (this.ty, rhs.ty) switch {
		(Ty.Number, Ty.Number) => this.assume_number.as_long_assert ^ rhs.assume_number.as_long_assert,
		_ => RuntimeError.attempt_to_perform_arithmetic<Val>("bxor", this.ty, rhs.ty),
	};

	private static long shiftl(long val, long shift) {
		if (shift >= 0) return shift < sizeof(long) ? val << (int)shift : 0;
		else return shift < sizeof(long) ? val >> -(int)shift : 0;
	}

	public Val raw_bit_shl(Val rhs) => (this.ty, rhs.ty) switch {
		(Ty.Number, Ty.Number) => Val.shiftl(this.assume_number.as_long_assert, rhs.assume_number.as_long_assert),
		_ => RuntimeError.attempt_to_perform_arithmetic<Val>("shl", this.ty, rhs.ty),
	};

	public Val raw_bit_shr(Val rhs) => (this.ty, rhs.ty) switch {
		(Ty.Number, Ty.Number) => Val.shiftl(this.assume_number.as_long_assert, -rhs.assume_number.as_long_assert),
		_ => RuntimeError.attempt_to_perform_arithmetic<Val>("shr", this.ty, rhs.ty),
	};

	public Val raw_bit_not => this.ty switch {
		Ty.Number => ~this.assume_number.as_long_assert,
		_ => RuntimeError.attempt_to_perform_arithmetic<Val>("bnot", this.ty),
	};

	public int raw_len => this.ty switch {
		// todo: ehehe lua strings are actually byte[] !! hopefully there's a not-awful way to do lengths and encoding properly
		Ty.String => this.assume_string.len,
		Ty.Table => this.assume_table.raw_len,
		// why dont buffers have the length operator defined..?
		_ => RuntimeError.attempt_to_perform_arithmetic<int>("len", this.ty),
	};

	public bool raw_eq(Val rhs) => (this.ty, rhs.ty) switch {
		(Ty.Nil, Ty.Nil) => true,
		(Ty.Boolean, Ty.Boolean) => this.assume_boolean == rhs.assume_boolean,
		(Ty.Number, Ty.Number) => (this.is_long && rhs.is_long)
			? this.assume_number.assume_long == rhs.assume_number.assume_long
			: this.assume_number.as_double == rhs.assume_number.as_double,
		(Ty.String, Ty.String) => this.assume_string == rhs.assume_string,
		(Ty.UserData, Ty.UserData)
		or (Ty.Thread, Ty.Thread)
		or (Ty.Table, Ty.Table)
		or (Ty.Buffer, Ty.Buffer)
		or (Ty.Class, Ty.Class)
		// only compare heap, since member arrays are unique
		or (Ty.Object, Ty.Object) => this.repr_heap == rhs.repr_heap,
		(Ty.LightUserData, Ty.LightUserData) => this.repr_stack.assume_num.assume_long == rhs.repr_stack.assume_num.assume_long && this.repr_heap == rhs.repr_heap && this.repr_extra == rhs.repr_extra,
		(Ty.Function, Ty.Function) => this.repr_extra == rhs.repr_extra, // only compare extra, since upvalue slot arrays are unique
		(Ty.Vector, Ty.Vector) => this.assume_vector == rhs.assume_vector,
		_ => false,
	};

	public int raw_cmp(Val rhs) => (this.ty, rhs.ty) switch {
		(Ty.Number, Ty.Number) => (this.is_long && rhs.is_long)
			? this.assume_number.assume_long.CompareTo(rhs.assume_number.assume_long)
			: this.assume_number.as_double.CompareTo(rhs.assume_number.as_double),
		(Ty.String, Ty.String) => this.assume_string.span.CompareTo(rhs.assume_string.span, StringComparison.Ordinal),
		_ => RuntimeError.attempt_to<int>("compare", this.ty.name(), rhs.ty.name()), // todo: luau error
	};

	public bool raw_lt(Val rhs) => this.raw_cmp(rhs) < 0;

	public bool raw_le(Val rhs) => this.raw_cmp(rhs) <= 0;

	public Val raw_get(Val key) {
		switch (this.ty) {
			case Ty.Table: {
					return this.assume_table.raw_get(key);
				}
			case Ty.Vector when key.is_string: {
					return key.assume_string.span switch {
						"x" or "X" => key.assume_vector.X,
						"y" or "Y" => key.assume_vector.Y,
						"z" or "Z" => key.assume_vector.Z,
						_ => RuntimeError.attempt_to<int>("index", Ty.Vector.name(), key.dbg_str_or_ty()),
					};
				}
			case Ty.Class when key.is_string: {
					var cls = this.assume_class;
					return cls.static_members[cls.static_member_offset(key.assume_string) ?? RuntimeError.missing_member<int>(Ty.Class, key)];
				}
			case Ty.Object when key.is_string: {
					var obj = this.assume_object;
					return obj.members[obj.cls.instance_member_offset(key.assume_string) ?? RuntimeError.missing_member<int>(Ty.Object, key)];
				}
			default:
				RuntimeError.attempt_to<int>("index", this.ty.name(), key.dbg_str_or_ty());
				return default;
		}
	}

	public void raw_set(Val key, Val val) {
		switch (this.ty) {
			case Ty.Table: {
					this.assume_table.raw_set(key, val);
					break;
				}
			case Ty.Class when key.is_string: {
					var cls = this.assume_class;
					cls.static_members[cls.static_member_offset(key.assume_string) ?? RuntimeError.missing_member<int>(Ty.Class, key)] = val;
					break;
				}
			case Ty.Object when key.is_string: {
					var obj = this.assume_object;
					obj.members[obj.cls.instance_member_offset(key.assume_string) ?? RuntimeError.missing_member<int>(Ty.Object, key)] = val;
					break;
				}
			default: {
					RuntimeError.attempt_to<int>("index", this.ty.name(), key.dbg_str_or_ty());
					break;
				}
		}
	}

	public Str raw_tostring() => this.ty switch {
		Ty.Nil => "nil",
		Ty.Boolean => this.repr_stack.assume_bool ? "true" : "false",
		// todo: match lua (inf, nan, proper digits, etc)
		Ty.Number => this.assume_number.ToString(),
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
		Ty.UserData => $"userdata: 0x{RuntimeHelpers.GetHashCode(this.repr_heap):x8}",
		Ty.LightUserData => $"userdata: 0x{HashCode.Combine(this.repr_stack.assume_num.GetHashCode(), RuntimeHelpers.GetHashCode(this.repr_heap)):x8}",
		Ty.Function => $"function: 0x{HashCode.Combine(RuntimeHelpers.GetHashCode(this.repr_heap), RuntimeHelpers.GetHashCode(this.repr_extra)):x8}",
		Ty.Thread => $"thread: 0x{RuntimeHelpers.GetHashCode(this.repr_heap):x8}",
		Ty.Table => $"table: 0x{RuntimeHelpers.GetHashCode(this.repr_heap):x8}",
		// todo: match luau
		Ty.Vector => $"{this.assume_vector.X}, {this.assume_vector.Y}, {this.assume_vector.Z}",
		Ty.Buffer => $"buffer: 0x{RuntimeHelpers.GetHashCode(this.repr_heap):x8}",
		Ty.Class => $"class: 0x{RuntimeHelpers.GetHashCode(this.repr_heap):x8}",
		Ty.Object => $"object: 0x{RuntimeHelpers.GetHashCode(this.repr_heap):x8}",
		_ => "?",
	};

	// todo: more

	#endregion raw operators

	#region lua operators

	// todo: implement once root calls, processing loop, tables, userdata [meta,]method resolution are real

	#endregion

	public override string ToString() => this.raw_tostring().ToString();

	public override int GetHashCode() => this.ty switch {
		Ty.Nil => 0,
		// normally would be 0 or 1, but 0 is nil so might as well just -1
		Ty.Boolean => this.assume_boolean ? 1 : -1,
		Ty.Number => this.assume_number.GetHashCode(),
		Ty.String => this.assume_string.GetHashCode(),
		Ty.LightUserData => HashCode.Combine(this.repr_stack.assume_num.GetHashCode(), RuntimeHelpers.GetHashCode(this.repr_heap)),
		Ty.Function => HashCode.Combine(RuntimeHelpers.GetHashCode(this.repr_heap), RuntimeHelpers.GetHashCode(this.repr_extra)),
		Ty.Vector => this.assume_vector.GetHashCode(),
		Ty.UserData
		or Ty.Thread
		or Ty.Table
		or Ty.Buffer
		or Ty.Class
		or Ty.Object => RuntimeHelpers.GetHashCode(this.repr_heap),
		_ => 0,
	};

	public static implicit operator Val(bool val) => new(val);
	public static implicit operator Val(Num val) => new(val);
	public static implicit operator Val(long val) => new(val);
	public static implicit operator Val(double val) => new(val);
	public static implicit operator Val(Str val) => new(val);
	public static implicit operator Val(string val) => new(val);
	public static implicit operator Val(Fn val) => new(val);
	public static explicit operator Val(UserData val) => new(val);
	public static implicit operator Val(Coro val) => new(val);
	public static implicit operator Val(Table val) => new(val);
	public static implicit operator Val(Vector3 val) => new(val);
	public static implicit operator Val((float x, float y, float z) val) => new(val);
	public static implicit operator Val(byte[] val) => new(val);
	public static implicit operator Val(ClassDefinition val) => new(val);
	public static implicit operator Val(ClassInstance val) => new(val);
}

public static class TyExtensions {
	/// <summary>type name of some type <paramref name="ty" /></summary>
	public static string name(this Val.Ty ty) => ty switch {
		Val.Ty.Nil => "nil",
		Val.Ty.Boolean => "boolean",
		Val.Ty.Number => "number",
		Val.Ty.String => "string",
		Val.Ty.UserData => "userdata",
		Val.Ty.LightUserData => "userdata",
		Val.Ty.Function => "function",
		Val.Ty.Thread => "thread",
		Val.Ty.Table => "table",
		Val.Ty.Vector => "vector",
		Val.Ty.Buffer => "buffer",
		Val.Ty.Class => "class",
		Val.Ty.Object => "object",
		_ => "?",
	};
}
