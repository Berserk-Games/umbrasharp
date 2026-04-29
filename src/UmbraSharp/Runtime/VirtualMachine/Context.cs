namespace UmbraSharp.Runtime.VirtualMachine;

public sealed class Slot(Val value): UserData(0) {
	public Val value = value;
}

internal sealed partial class VM {
	public struct StackFrame {
		/// the function being called
		public readonly required FnProto fn { get; init; }
		public readonly required object? extra { get; init; }

		// todo: continuations

		// todo: error handlers

		/// number of varargs *below* the base ptr
		public readonly required int varargs { get; init; }
		/// base ptr to start of registers
		public readonly required int bp { get; init; }
		/// the stack indices to put the start of returns in
		public readonly required StackSpan ret_dst { get; init; }
		/// whether the rest of the returns should be pushed onto the varret stack
		public readonly required bool ret_dst_var { get; init; }

		/// whether this frame returns directly to the CLR
		public required bool root;
		/// instruction pointer for lua code
		public required int ip;
	}

	public struct Reg(Val value) {
		private Slot? slot;
		private Val inline = value;

		public Val value {
			readonly get => this.closed ? this.slot!.value : this.inline;
			set {
				if (this.closed) this.slot!.value = value;
				else this.inline = value;
			}
		}

		public readonly bool closed => this.slot != null;

		public readonly Reg copy => new(this.value);

		public Slot close() {
			if (this.closed) return this.slot!;

			this.slot = new(this.inline);
			this.inline = default;
			return this.slot;
		}

		public override readonly string ToString() => this.closed ? $"[{this.slot!.value}]" : $"({this.inline})";
	}

	private int base_top {
		get {
			if (this.call_stack.Count == 0) return 0;
			var frame = this.call_stack.Peek();
			return frame.bp + frame.fn switch {
				NativeFnProto => 0,
				Bytecode.LuaFnProto fn => fn.args + fn.extra_regs,
				_ => 0,
			};
		}
	}

	private static readonly NativeFnProto ROOT_CALL_FN = new(
		static (ref NativeFnProto.CallContext _, object _) => throw new Exception("attempt to call ROOT_CALL_FN.callee"),
		"<clr>"
	);

	public StackSpan root_call(FnProto fn, int args) {
		if (this.call_stack.Count >= StaticConfig.MAX_CALL_STACK_SIZE - 1) throw new StackOverflowException("lua call stack overflow");

		this.dbg.advance("root_call: before");

		var base_top = this.regs.top - args;

		this.call_stack.Push(new StackFrame {
			fn = VM.ROOT_CALL_FN,
			extra = null,
			varargs = 0,
			bp = base_top,
			ret_dst = default,
			ret_dst_var = true,

			root = true,
			ip = -1,
		});

		// todo

		this.call_stack.Pop();

		this.dbg.advance("root_call: after");

		return default; // todo
	}

	public int nested_call(FnProto fn, StackSpan arg_src, StackSpan ret_dst, bool ret_dst_var) {
		// todo: call execution loop, handle yield results (probably split yieldable calls into another function that can take a continuation)
		return 0;
	}

	public int native_call(NativeFnProto fn, object extra, StackSpan arg_src, StackSpan ret_dst, bool ret_dst_var) {
		if (this.call_stack.Count >= StaticConfig.MAX_CALL_STACK_SIZE - 1) throw new StackOverflowException("lua call stack overflow");

		this.dbg.advance("native_call: before");

		var base_top = this.base_top;

		this.call_stack.Push(new StackFrame {
			fn = fn,
			extra = extra,
			varargs = 0,
			bp = base_top,
			ret_dst = ret_dst,
			ret_dst_var = ret_dst_var,

			root = false,
			ip = -1,
		});

		NativeFnProto.CallContext ctx = new(
			this,
			new(arg_src, base_top..this.regs.top),
			ret_dst,
			ret_dst_var
		);
		for (var i = base_top; i < this.regs.top; i++) this.dbg.notify_pop(i);
		this.regs.top = base_top;
		this.dbg.advance("native_call: precall");
		// todo: figure out how native functions should yield (probably a flag in their CallContext so they can just `ctx.yielding(1) ctx.yielding(2) ctx.yield_continue(continuation, continuation_extra)`)
		fn.callee.Invoke(ref ctx, extra);
		this.dbg.advance("native_call: postcall");
		for (var i = ctx.returned; i < ret_dst.len; i++) this.regs.data[ret_dst[i]].value = Val.NIL;

		this.call_stack.Pop();

		this.dbg.advance("native_call: after");

		return ctx.returned;
	}

	public void lua_begin_call(Bytecode.LuaFnProto fn, Slot[] upvars, StackSpan arg_src, StackSpan ret_dst, bool ret_dst_var) {
		if (this.call_stack.Count >= StaticConfig.MAX_CALL_STACK_SIZE - 1) throw new StackOverflowException("lua call stack overflow");

		this.dbg.advance("lua_call: before");

		var base_top = this.base_top;

		var src = new StackSpanVar(arg_src, base_top..this.regs.top);
		Console.WriteLine($"num_varargs: {fn.varargs} ? {src.len} - {fn.args} : 0");
		var num_varargs = fn.varargs ? src.len - fn.args : 0;

		this.call_stack.Push(new StackFrame {
			fn = fn,
			extra = upvars,
			varargs = num_varargs,
			bp = this.regs.top + num_varargs,
			ret_dst = ret_dst,
			ret_dst_var = ret_dst_var,

			root = false,
			ip = fn.addr,
		});

		var varargs = this.regs.alloc(num_varargs);
		var dst = new StackSpanVar(this.regs.alloc(fn.args), varargs);
		this.regs.copy_to(src.trim(dst.len), dst);
		var extra_regs = this.regs.alloc(fn.extra_regs);
		for (var i = extra_regs.start; i < extra_regs.end; i++) this.dbg.notify_set(i);

		this.dbg.advance("lua_call: after");
	}

	public void lua_ret(StackSpan ret_src, bool ret_src_var) {
		this.dbg.advance("lua_ret: before");

		var src = new StackSpanVar(
			ret_src,
			ret_src_var ? this.base_top..this.regs.top : default
		);

		var frame = this.call_stack.Pop();
		var old_top = this.regs.top;
		this.regs.top = this.base_top;
		var dst = new StackSpanVar(
			frame.ret_dst,
			frame.ret_dst_var ? this.regs.alloc(src.len - frame.ret_dst.len) : default
		);
		this.regs.copy_to(src.trim(dst.len), dst);

		this.dbg.advance("lua_ret: copy");

		for (var i = old_top; i < this.regs.top; i++) this.dbg.notify_pop(i);

		this.dbg.advance("lua_ret: after");
	}
}