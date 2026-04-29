// todo: coroutines

using UmbraSharp.Runtime.VirtualMachine;

namespace UmbraSharp;

public class Coro {
	internal readonly VM vm;

	internal Coro(VM vm) {
		Statistics.trace_alloc_coro();
		this.vm = vm;
	}
}