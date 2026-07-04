using System.Collections.Concurrent;
using UmbraSharp.Internal;
using VectSharp;

namespace UmbraSharp.Runtime.VirtualMachine;

internal sealed partial class VM {
	private static readonly ConcurrentBag<VM> POOL = [];

	public static VM acquire(Coro? owning_coro = null) {
		if (VM.POOL.TryTake(out var vm)) {
			if (vm.active) {
				Statistics.internal_warning("attempt to acquire already-active VM! something already acquired it. trying again");
				return VM.acquire(owning_coro);
			}
			Statistics.trace_vm_pool_acquired();
		} else vm = new(new Page(4096, 4096 * 8), owning_coro);
		vm.active = true;
		return vm;
	}

	private bool active = false;

	public void release() {
		if (this.active) {
			VM.POOL.Add(this);
			this.active = false;
			Statistics.trace_vm_pool_released();
		} else Statistics.internal_warning("attempt to release inactive VM! something already released it. not releasing into the pool");
	}

#if DEBUG
	~VM() {
		if (this.active) Statistics.internal_warning("VM dropped without releasing into the pool!");
	}
#endif
}