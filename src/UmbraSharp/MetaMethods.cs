namespace UmbraSharp;

public enum MetaMethod: byte {
	__index,
	__newindex,

	__call,

	__add,
	__sub,
	__mul,
	__div,
	__idiv,
	__mod,
	__pow,
	__unm,

	__band,
	__bor,
	__bxor,
	__shl,
	__shr,
	__bnot,

	__concat,
	__len,

	__eq,

	__iter,
	__iterator,

	__close,

	__type,

	VARIANTS,
}

public static class MetaMethodExtensions {
	public static string simple_name(this MetaMethod meta) => meta switch {
		MetaMethod.__index => "index",
		MetaMethod.__newindex => "newindex",

		MetaMethod.__call => "call",

		MetaMethod.__add => "add",
		MetaMethod.__sub => "sub",
		MetaMethod.__mul => "mul",
		MetaMethod.__div => "div",
		MetaMethod.__idiv => "idiv",
		MetaMethod.__mod => "mod",
		MetaMethod.__pow => "pow",
		MetaMethod.__unm => "unm",

		MetaMethod.__band => "band",
		MetaMethod.__bor => "bor",
		MetaMethod.__bxor => "bxor",
		MetaMethod.__shl => "shl",
		MetaMethod.__shr => "shr",
		MetaMethod.__bnot => "bnot",

		MetaMethod.__concat => "concat",
		MetaMethod.__len => "len",

		MetaMethod.__eq => "eq",

		MetaMethod.__iter => "iter",
		MetaMethod.__iterator => "iterator",

		MetaMethod.__close => "close",

		MetaMethod.__type => "type",

		_ => throw new InvalidOperationException("invalid metamethod"),
	};
}