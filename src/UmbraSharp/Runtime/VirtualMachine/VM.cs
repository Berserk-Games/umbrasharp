using UmbraSharp.Internal;
using VectSharp;

namespace UmbraSharp.Runtime.VirtualMachine;

internal sealed partial class VM {
	internal const int STACK_SIZE = 1 << 7;
	internal const int MAX_STACK_SIZE = 1 << 14;

	internal const int CALL_STACK_SIZE = 1 << 5;
	internal const int MAX_CALL_STACK_SIZE = 1 << 12;

	public readonly Dbg dbg;
	public readonly CallStack call_stack;
	public readonly RegStack regs;
	public Coro? owning_coro;

	private VM(Page render, Coro? owning_coro = null) {
		Statistics.trace_alloc_vm();
		this.dbg = new(this, render);
		this.call_stack = new(CALL_STACK_SIZE, MAX_CALL_STACK_SIZE);
		this.regs = new(STACK_SIZE, MAX_STACK_SIZE, this.dbg);
		this.owning_coro = owning_coro;
	}
}