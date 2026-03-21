using VectSharp;

namespace UmbraSharp.Runtime.VirtualMachine;

internal sealed partial class VM {
	internal readonly Dbg dbg;
	internal readonly Config config;
	internal readonly Stack<StackFrame> call_stack;
	internal readonly RegStack regs;

	public VM(Config config, Graphics render) {
		this.dbg = new(this, render);
		this.config = config;
		this.call_stack = new(config.call_stack_size);
		this.regs = new(config.stack_size, config.max_stack_size, this.dbg);
	}
}