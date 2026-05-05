using UmbraSharp.Runtime.VirtualMachine;

namespace UmbraSharp;

public class Script {
	public static class Config {
		public enum GeneralizedIteration {
			/// just error
			None,
			/// `key, value` iteration
			Luau,
			/// `key` only iteration
			MoonSharp,
		}

		[Flags]
		public enum Std {
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

		public const Std STD_LUA_ALL = 0
			| Std.Lua_Core
			| Std.Lua_Coroutine
			| Std.Lua_Package
			| Std.Lua_String
			| Std.Lua_Utf8
			| Std.Lua_Table
			| Std.Lua_Math
			| Std.Lua_Io
			| Std.Lua_Os_System
			| Std.Lua_Debug;
		public const Std STD_LUA_SANDBOX = 0
			| Std.Lua_Core
			| Std.Lua_Coroutine
			| Std.Lua_String
			| Std.Lua_Utf8
			| Std.Lua_Table
			| Std.Lua_Math
			| Std.Lua_Os_Time;

		public const Std STD_LUAU_LIBS = 0
			| Std.Luau_Core_Ext
			| Std.Luau_Math_Ext
			| Std.Luau_Table_Ext
			| Std.Luau_String_Ext
			| Std.Luau_Debug_Ext
			| Std.Luau_Vector
			| Std.Luau_Buffer;

		public const Std STD_MOONSHARP_LIBS = 0
			| Std.MoonSharp_Core_Ext
			| Std.MoonSharp_String_Ext
			| Std.MoonSharp_Math_Ext
			| Std.MoonSharp_Dynamic
			| Std.MoonSharp_Json;

		public const Std STD_UMBRASHARP_LIBS = 0
			| Std.UmbraSharp_Umbra;

		/// <summary>whether generalized iterators should </summary>
		public static GeneralizedIteration generalized_iteration = GeneralizedIteration.None;
		/// <summary>permit Luau syntax + __iter metamethod</summary>
		public static bool luau_support = false;
		/// <summary>permit moonsharp syntax + __iterator metamethod</summary>
		public static bool moonsharp_support = false;
		public static Std default_standard_libraries = STD_LUA_SANDBOX;

		/// <summary>
		/// configure Luau compatibility,
		/// attempting to be as close to Luau as possible
		/// </summary>
		public static void luau() {
			Config.luau_support = true;
			Config.generalized_iteration = GeneralizedIteration.Luau;
			Config.default_standard_libraries |= STD_LUAU_LIBS;
		}

		/// <summary>
		/// configure MoonSharp compatibility,
		/// attempting to be as close to MoonSharp as possible
		/// </summary>
		public static void moonsharp() {
			Config.moonsharp_support = true;
			Config.generalized_iteration = GeneralizedIteration.MoonSharp;
			Config.default_standard_libraries |= STD_MOONSHARP_LIBS;
		}

		/// <summary>configure UmbraSharp libraries</summary>
		public static void umbrasharp() {
			Config.default_standard_libraries |= STD_UMBRASHARP_LIBS;
		}
	}
}