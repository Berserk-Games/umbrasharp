namespace UmbraSharp.Runtime.VirtualMachine;

internal sealed partial class VM {
	internal readonly struct Yield {
		public readonly required StackSpan values { get; init; }
		public readonly required NativeFnProto.Callee continuation { get; init; }
		public readonly required object? extra { get; init; }
	}

	public Yield? execution_loop() {
		// todo: iterate over top of callstack instructions (if the top is a native fn, return)
		throw new NotImplementedException("todo");
	}
}