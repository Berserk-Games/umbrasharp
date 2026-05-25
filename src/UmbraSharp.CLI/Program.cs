using System.Diagnostics;
using UmbraSharp.Internal;
using UmbraSharp.Runtime;
using UmbraSharp.Runtime.VirtualMachine;
using VectSharp.SVG;

namespace UmbraSharp.CLI;

using IB = ByteCode.Inst.Builder;

public class Program {
	public static void Main(string[] _) {
		Statistics.full_trace = true;

		Program.test_exec();
	}

	public static void test_exec() {
		Statistics.full_trace = true;

		var test_native = new NativeFnProto(static (ref NativeFnProto.CallContext data, object? extra) => {
			Console.WriteLine($"number of args: {data.args}");
			var first = data.arg(0);
			data.ret(first);
			data.ret(1979);
		}, "test_native");

		var bytecode = new ByteCode() {
			chunk_name = "main",

			instructions = [
				// IB.call(true, 1, 0..1, 0..0),
				IB.tail_call(1, 0..1),
				IB.ret(0..0),

				IB.call(false, 0, 0..1, 0..2),
				IB.ld_fmt(0, 0),
				IB.ret(0..1),
			],
			constants = [1],
			format_strings = [
				new("the meaning of life is ", [
					new(null, 0, ", and the book was released in "),
					new(null, 1, "."),
				])
			],
			funcs = [default, default],
			closures = [],
		};

		var produce = new ByteCode.LuaFnProto() {
			bytecode = bytecode,
			dumpable = true,
			addr = 0,
			len = 2,
			name = "produce",

			args = 1,
			varargs = false,
			extra_regs = 1,
			upvalues = [],
		};
		bytecode.funcs[0] = new(produce, []);
		bytecode.funcs[1] = new(test_native, 1);

		var consume = new ByteCode.LuaFnProto() {
			bytecode = bytecode,
			dumpable = true,
			addr = 2,
			len = 3,
			name = "consume",

			args = 1,
			varargs = false,
			extra_regs = 1,
			upvalues = [],
		};

		// verify validity
		foreach (var err in bytecode.validate()) {
			Console.WriteLine($"bytecode error: {err}");
		}

		Statistics.reset();

		var vm = VM.acquire();

		var args = vm.regs.alloc(1);
		vm.regs[args][0] = new(42);
		var rets = vm.root_call(new(consume, []), args.len);
		foreach (var ret in vm.regs[rets]) Console.WriteLine($"ret: {ret}");

		vm.dbg.render.SaveAsSVG("dbgdraw.svg");

		vm.release();

		var cap = Statistics.capture();

		Console.WriteLine($"took {cap.time}s");
		Console.WriteLine(cap.stats);
		Console.WriteLine($"{cap.time} log entries");
	}
}
