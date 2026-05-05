using UmbraSharp.Runtime;
using UmbraSharp.Runtime.VirtualMachine;

namespace UmbraSharp;

public abstract class FnProto {
	public abstract bool hide_from_trace { get; }
	public abstract string debug_name { get; }
}

public sealed class NativeFnProto(NativeFnProto.Callee callee, string name, bool hide_from_trace = false): FnProto {
	public struct CallContext {
		internal readonly VM vm;
		internal readonly StackSpanVar arg_src;
		/// the stack indices to put the start of returns in
		internal readonly StackSpan ret_dst;
		/// whether the rest of the returns should be pushed onto the varret stack
		internal readonly bool ret_var;
		internal int returned;
		internal VM.Yield yield;

		public readonly int args => this.arg_src.len;

		internal CallContext(VM vm, StackSpanVar arg_src, StackSpan ret_dst, bool ret_var) {
			this.vm = vm;
			this.arg_src = arg_src;
			this.ret_dst = ret_dst;
			this.ret_var = ret_var;
		}

		/// get an argument
		/// <note>arguments are invalid once a return has been pushed</note>
		public readonly Val arg(int i) {
			if (this.returned != 0) throw new InvalidOperationException("cannot read arguments from NativeFn.CallContext after function has written return values");
			return i < this.arg_src.len ? this.vm.regs.data[this.arg_src[i]].value : default;
		}

		public void ret(Val val) {
			if (this.returned < this.ret_dst.len) {
				this.vm.regs.data[this.ret_dst[this.returned]].value = val;
				this.vm.dbg?.notify_set(this.ret_dst[this.returned]);
			} else if (this.ret_var) {
				this.vm.regs.push(val);
			}
			this.returned++;
		}

		public void tail_call(Fn fn) {
			if (this.returned != 0) throw new InvalidOperationException("cannot read arguments from NativeFn.CallContext after function has written return values");
			// todo
		}

		// todo: yield
	}

	public delegate void Callee(ref CallContext ctx, object? extra);

	public readonly Callee callee = callee;
	public override string debug_name { get; } = name;
	public override bool hide_from_trace { get; } = hide_from_trace;
}

public readonly struct Fn {
	public readonly FnProto proto;
	public readonly object? extra;

	internal Fn(FnProto proto, object? extra) {
		this.proto = proto;
		this.extra = extra;
	}

	public Fn(NativeFnProto proto, object? extra) : this((FnProto)proto, extra) { }

	public Fn(ByteCode.LuaFnProto proto, Slot[] upvalues) : this((FnProto)proto, upvalues) { }
}
