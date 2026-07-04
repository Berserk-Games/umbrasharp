using UmbraSharp.Runtime.VirtualMachine;

namespace UmbraSharp;

public class Script {
	public static class Std {
		[Flags]
		public enum Libs {
			Lua_Core = 1 << 0,
			Lua_Coroutine = 1 << 1,
			Lua_Package = 1 << 2,
			Lua_String = 1 << 3,
			Lua_Utf8 = 1 << 4,
			Lua_Table = 1 << 5,
			Lua_Math = 1 << 6,
			Lua_Io = 1 << 7,
			Lua_Os_System = 1 << 8,
			Lua_Os_Time = 1 << 9,
			Lua_Bit32 = 1 << 10,
			Lua_Debug = 1 << 11,

			/// <remarks>
			/// adds <c>gcinfo</c>, <c>getfenv</c>, <c>newproxy</c>, <c>setfenv</c>, <c>typeof</c>
			/// </remarks>
			Luau_Core_Ext = 1 << 12,
			/// <remarks>
			/// adds <c>math.atan2</c>, <c>math.cosh</c>, <c>math.lerp</c>, <c>math.map</c>, <c>math.log10</c>, <c>math.sinh</c>, <c>math.tanh</c>, <c>math.noise</c>, <c>math.clamp</c>, <c>math.sign</c>, <c>math.round</c>
			/// </remarks>
			Luau_Math_Ext = 1 << 13,
			/// <remarks>
			/// adds <c>table.foreach</c>, <c>table.foreachi</c>, <c>table.getn</c>, <c>table.maxn</c>, <c>table.create</c>, <c>table.find</c>, <c>table.clear</c>, <c>table.freeze</c>, <c>table.isfrozen</c>, <c>table.clone</c>
			/// </remarks>
			Luau_Table_Ext = 1 << 14,
			/// <remarks>
			/// adds <c>string.split</c>
			/// </remarks>
			Luau_String_Ext = 1 << 15,
			/// <remarks>
			/// adds <c>debug.info</c>
			/// </remarks>
			Luau_Debug_Ext = 1 << 16,
			Luau_Vector = 1 << 17,
			Luau_Buffer = 1 << 18,

			MoonSharp_Core_Ext = 1 << 19,
			MoonSharp_Math_Ext = 1 << 20,
			MoonSharp_String_Ext = 1 << 21,
			MoonSharp_Dynamic = 1 << 22,
			MoonSharp_Json = 1 << 23,

			UmbraSharp_Umbra = 1 << 24,
		}

		public const Libs LUA_ALL = 0
			| Libs.Lua_Core
			| Libs.Lua_Coroutine
			| Libs.Lua_Package
			| Libs.Lua_String
			| Libs.Lua_Utf8
			| Libs.Lua_Table
			| Libs.Lua_Math
			| Libs.Lua_Io
			| Libs.Lua_Os_System
			| Libs.Lua_Os_Time
			| Libs.Lua_Bit32
			| Libs.Lua_Debug;
		public const Libs LUA_SANDBOX = 0
			| Libs.Lua_Core
			| Libs.Lua_Coroutine
			| Libs.Lua_String
			| Libs.Lua_Utf8
			| Libs.Lua_Table
			| Libs.Lua_Math
			| Libs.Lua_Os_Time
			| Libs.Lua_Bit32;

		public const Libs LUAU_LIBS = 0
			| Libs.Luau_Core_Ext
			| Libs.Luau_Math_Ext
			| Libs.Luau_Table_Ext
			| Libs.Luau_String_Ext
			| Libs.Luau_Debug_Ext
			| Libs.Luau_Vector
			| Libs.Luau_Buffer;

		public const Libs MOONSHARP_LIBS = 0
			| Libs.MoonSharp_Core_Ext
			| Libs.MoonSharp_String_Ext
			| Libs.MoonSharp_Math_Ext
			| Libs.MoonSharp_Dynamic
			| Libs.MoonSharp_Json;

		public const Libs UMBRASHARP_LIBS = 0
			| Libs.UmbraSharp_Umbra;
	}

	public class Config {
		public enum GeneralizedIteration {
			/// just error
			None,
			/// `key, value` iteration
			Luau,
			/// `key` only iteration
			MoonSharp,
		}

		/// <summary>the behavior of the default generalized table iterator</summary>
		public GeneralizedIteration default_generalized_iteration = GeneralizedIteration.None;
		/// <summary>permit Luau syntax + __iter metamethod</summary>
		public bool luau_support = false;
		/// <summary>permit MoonSharp syntax + __iterator metamethod</summary>
		public bool moonsharp_support = false;
		/// <summary>permit `goto` and labels (lua 5.2)</summary>
		public bool goto_support = false;
		/// <summary>permit bitwise operators (lua 5.3)</summary>
		public bool bitwise_ops_support = false;
		/// <summary>permit attributes on locals (lua 5.4)</summary>
		public bool local_attr_support = false;
		/// <summary>permit named vararg tables (lua 5.5)</summary>
		public bool named_vararg_support = false;
		public Std.Libs default_standard_libraries = Std.LUA_SANDBOX;

		public void lua52() {
			this.goto_support = true;
		}

		public void lua53() {
			this.lua52();
			this.bitwise_ops_support = true;
		}

		public void lua54() {
			this.lua53();
			this.local_attr_support = true;
		}

		public void lua55() {
			this.lua54();
			this.named_vararg_support = true;
		}

		/// <summary>
		/// configure Luau compatibility,
		/// attempting to be as close to Luau as possible
		/// </summary>
		public void luau() {
			this.luau_support = true;
			this.default_generalized_iteration = GeneralizedIteration.Luau;
			this.default_standard_libraries |= Std.LUAU_LIBS;
		}

		/// <summary>
		/// configure MoonSharp compatibility,
		/// attempting to be as close to MoonSharp as possible
		/// </summary>
		public void moonsharp() {
			this.lua52();
			this.moonsharp_support = true;
			this.default_generalized_iteration = GeneralizedIteration.MoonSharp;
			this.default_standard_libraries |= Std.MOONSHARP_LIBS;
		}

		/// <summary>configure UmbraSharp libraries</summary>
		public void umbrasharp() {
			this.default_standard_libraries |= Std.UMBRASHARP_LIBS;
		}
	}

	public Config config = new();
}