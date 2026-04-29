using UmbraSharp.Runtime.VirtualMachine;
using VectSharp;
using VectSharp.SVG;

namespace UmbraSharp.CLI;

public class Program {
	public static void Main(string[] args) {
		var vm = VM.acquire();

		vm.dbg.debug_stack();

		// vm.regs.push(new("a"));
		// vm.regs.push(new("b"));
		// vm.regs.push(new("c"));
		// vm.regs.push(new("d"));
		// vm.regs.push(new("e"));

		// vm.dbg.advance();

		// vm.call_stack.Push(new() {
		// 	fn = new LuaFn() {
		// 		proto = new() {
		// 			bytecode = null!,
		// 			addr = 0,
		// 			args = 0,
		// 			varargs = false,
		// 			extra_regs = 3,
		// 			upvalues = [],
		// 			name = "wawa",
		// 			prototypes = [],
		// 		},
		// 		upvalues = [],
		// 	},
		// 	varargs = 0,
		// 	bp = 0,
		// 	ret_dst = default,
		// 	ret_dst_var = false,

		// 	ip = 0,
		// });

		vm.dbg.debug_stack();

		vm.native_call(new NativeFnProto((ref NativeFnProto.CallContext ctx, object extra) => {
			ctx.ret(new(1));
			ctx.ret(new(2));
			ctx.ret(new(3));
			ctx.ret(new(4));
			ctx.ret(new(5));

			vm.dbg.advance("push some args for lua");

			vm.lua_begin_call(new() {
				bytecode = null!,
				dumpable = true,
				addr = 0,
				len = 0,
				args = 2,
				varargs = true,
				extra_regs = 3,
				upvalues = [],
				name = "wawa",
				prototypes = [],
			}, [], 0..0, 0..0, true);

			vm.regs.push(new("vr1"));
			vm.regs.push(new("vr2"));
			vm.regs.push(new("vr3"));

			vm.dbg.advance("push varret test stuff");

			var bp = vm.call_stack.Peek().bp;
			Console.WriteLine($"{bp}");
			var ret_src = (bp + 1)..(bp + 4);
			vm.lua_ret(
				ret_src,
				true
			);


		}, "thing"), null!, 0..0, 0..0, true);

		vm.dbg.render.SaveAsSVG("dbgdraw.svg");
	}
}
