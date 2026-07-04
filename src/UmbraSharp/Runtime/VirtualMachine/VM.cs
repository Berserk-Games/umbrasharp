using UmbraSharp.Internal;
using VectSharp;

namespace UmbraSharp.Runtime.VirtualMachine;

internal sealed partial class VM {
	internal const int STACK_SIZE = 1 << 7;
	internal const int MAX_STACK_SIZE = 1 << 14;

	internal const int MAX_CALL_STACK_SIZE = 1 << 12;

	public readonly Dbg dbg;
	public readonly StructStack<StackFrame> call_stack;
	public readonly RegStack regs;
	public Coro? owning_coro;

	private VM(Page render, Coro? owning_coro = null) {
		Statistics.trace_alloc_vm();
		this.dbg = new(this, render);
		this.call_stack = StructStackPool.acquire<StackFrame>();
		this.regs = new(VM.STACK_SIZE, VM.MAX_STACK_SIZE, this.dbg);
		this.owning_coro = owning_coro;
	}
}