using System.Diagnostics;

namespace UmbraSharp;

public readonly struct Val(Val.Ty ty, double repr_stack, object? repr_heap, UserDataMeta? userdata_metatable) {
	public enum Ty: byte {
		Nil = 0,
		Boolean = 1,
		Number = 2,
		String = 3,
		Userdata = 4,
		Function = 5,
		Thread = 6,
		Table = 7,
	}

	public static readonly Val NIL = default; // bits 0 would make this.ty == Ty.Nil, which is what we want
	public static readonly Val TRUE = new(Ty.Boolean, 1, null, null);
	public static readonly Val FALSE = new(Ty.Boolean, 0, null, null);

	public readonly Ty ty = ty;

	private readonly double repr_stack = repr_stack;
	// string => string
	// userdata => underlying object
	// 
	private readonly object? repr_heap = repr_heap;
	private readonly UserDataMeta? userdata_metatable = userdata_metatable;

	// todo: determine whether as_* is a good idea (should they throw exception if not the type)

	public bool is_boolean => this.ty == Ty.Boolean;
	public bool as_boolean => this.repr_stack != 0;

	public bool is_number => this.ty == Ty.Number;
	public double as_number => this.repr_stack;

	public bool is_string => this.ty == Ty.String;
	public string? as_string => this.repr_heap as string;

	public bool is_userdata => this.ty == Ty.Userdata;
	public object? as_userdata => this.repr_heap;
	public UserDataMeta? as_userdata_metatable => this.userdata_metatable;

	public bool is_function => this.ty == Ty.Function;
	public Fn? as_function => this.repr_heap as Fn;

	public bool is_thread => this.ty == Ty.Thread;
	public object? as_thread => this.repr_heap; // todo

	public bool is_table => this.ty == Ty.Table;
	public object? as_table => this.repr_heap; // todo

	public Val(bool value) : this(Ty.Boolean, value ? 1 : 0, null, null) { }
	public Val(double value) : this(Ty.Number, value, null, null) { }
	public Val(string value) : this(Ty.String, 0, value, null) { }
	public Val(object value, UserDataMeta? mt) : this(Ty.Userdata, 0, value, mt) { }
	public Val(Fn value) : this(Ty.Function, 0, value, null) { }
	// todo
	// public Val(bool value): this(Ty.Thread, value ? 1 : 0, null, null) {}
	// public Val(bool value): this(Ty.Table, value ? 1 : 0, null, null) {}

	// todo: operators n stuff

	public override string ToString() => this.ty switch {
		Ty.Nil => "nil",
		Ty.Boolean => this.as_boolean ? "true" : "false",
		Ty.Number => this.as_number.ToString(),
		Ty.String => this.as_string!,
		Ty.Userdata => $"userdata: {0}", // todo
		Ty.Function => $"function: {0}", // todo: function "addr"
		Ty.Thread => $"thread: {0}", // todo: thread "addr"
		Ty.Table => $"table: {0}", // todo: table "addr"
		_ => throw new UnreachableException($"unknown value type {this.ty}"),
	};

	public static implicit operator Val(bool val) => new(val);
	public static implicit operator Val(double val) => new(val);
	public static implicit operator Val(string val) => new(val);
	public static implicit operator Val(Fn val) => new(val);
	// no userdata
	// todo
	// public static implicit operator Val(Coro val) => new(val);
	// public static implicit operator Val(Table val) => new(val);
}
