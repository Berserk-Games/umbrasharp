using UmbraSharp.Compiler;
using UmbraSharp.Internal;
using UmbraSharp.Runtime;
using UmbraSharp.Runtime.VirtualMachine;
using VectSharp.SVG;

namespace UmbraSharp.CLI;

using IB = ByteCode.Inst.Builder;

public class Program {
	public static void Main(string[] _) {
		Statistics.full_trace = true;
		var script = new Script();
		script.config.lua55();
		script.config.luau();
		script.config.moonsharp();
		script.config.umbrasharp();

		var src = File.ReadAllText("/home/stella/code/umbrasharp/test/parse/lua/basic.lua");
		Console.WriteLine(src);

		try {
			var l = new Lexer(script.config, new(src));
			Token tok;
			var i = 0;
			do {
				i++;
				tok = l.next();
				Console.WriteLine(tok);
			} while (tok.kind != Token.Kind.EOF);
			Console.WriteLine(i);

			var p = new Compiler.Parser(new(script.config, new(src)));
			Console.WriteLine(Pretty.print(p.expr()));
		}
		catch (SyntaxError err) {
			var (line, col) = Compiler.Compiler.line_col(err.offset, src);
			Console.WriteLine($"ln{line + 1}, col{col + 1}");
			throw;
		}

		// Program.test_exec();
	}

	public static void test_exec() {
		var iterator = new NativeFnProto((ref NativeFnProto.CallContext data, object? extra) => {
			var idx = data.arg(2);
			if (idx.as_number.as_long_trunc < 4) {
				data.ret(idx.assume_number.as_long_trunc + 1);
				data.ret("wawa");
			} else {
				data.ret(default);
				data.ret("bwaaa");
			}
		}, "iterator");

		var iter_meta = new NativeFnProto((ref NativeFnProto.CallContext data, object? extra) => {
			data.ret(new Fn(iterator));
			data.ret(default);
			data.ret(0);
		}, "iterator_creator");

		var mt = new Table();
		mt.raw_set("__call", new Fn(iterator));

		var t = new Table();
		t.raw_set("awawa", "wa");
		t.raw_set(2, 9);
		t.raw_set(1, 3);
		t.raw_set(4, 2);
		// t.metatable = mt;

		var script = new Script();

		var bytecode = new ByteCode() {
			script = script,
			chunk_name = "main",

			instructions = new ByteCode.Inst[] {
				IB.copy(new(0), new(-1)),
				IB.iter_prep(0..5),
				IB.iter_step(0..5),
				IB.jmp_nil(3, 2),

				IB.debug(0),

				IB.jmp(-3),
				IB.ret(0..0),
			},
			constants = new Val[] { t },
			format_strings = new ByteCode.FormatString[] {
				new("iter: k = ", [
					new(null, 2, ", v = "),
					new(null, 3, ""),
				]),
				new("iter done", []),
			},
			funcs = default,
			closures = default,
		};

		var test = new ByteCode.LuaFnProto() {
			bytecode = bytecode,
			dumpable = true,
			addr = 0,
			len = 2,
			name = "test",

			args = 0,
			varargs = false,
			extra_regs = 5,
			upvalues = default,
		};

		// verify validity
		foreach (var err in bytecode.validate()) {
			Console.WriteLine($"bytecode error: {err}");
		}

		Statistics.reset();

		var vm = VM.acquire();

		var args = vm.regs.alloc(0);
		// vm.regs[args][0] = new(42);
		var rets = vm.root_call(new(test, []), args.len);
		foreach (var ret in vm.regs[rets]) Console.WriteLine($"ret: {ret}");

		vm.dbg.render.SaveAsSVG("dbgdraw.svg");

		vm.release();

		var cap = Statistics.capture();

		Console.WriteLine($"took {cap.time}s");
		Console.WriteLine(cap.stats);
		Console.WriteLine($"{cap.time} log entries");
	}
}
