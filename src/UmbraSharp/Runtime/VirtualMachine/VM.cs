using VectSharp;

namespace UmbraSharp.Runtime.VirtualMachine;

internal sealed partial class VM {
	public readonly Dbg dbg;
	public readonly Stack<StackFrame> call_stack;
	public readonly RegStack regs;
	public Coro? owning_coro;

	private VM(Page render, Coro? owning_coro = null) {
		Statistics.trace_alloc_vm();
		this.dbg = new(this, render);
		Statistics.trace_alloc_internal($"alloc -> VM.call_stack[{StaticConfig.CALL_STACK_SIZE}] + backing buf");
		this.call_stack = new(StaticConfig.CALL_STACK_SIZE);
		this.regs = new(StaticConfig.STACK_SIZE, StaticConfig.MAX_STACK_SIZE, this.dbg);
		this.owning_coro = owning_coro;
	}
}