using VectSharp;

namespace UmbraSharp.Runtime.VirtualMachine;

internal sealed partial class VM {
	private static readonly Stack<VM> POOL = new(16);

	public static VM acquire(Coro? owning_coro = null) {
		if (VM.POOL.TryPop(out var vm)) {
			if (vm.active) {
				Statistics.internal_warning("attempt to acquire already-active VM! something already acquired it. trying again");
				return VM.acquire(owning_coro);
			} else vm.active = true;
			Statistics.trace_vm_pool_acquired();
		} else vm = new(new Page(4096, 4096), owning_coro);
		return vm;
	}

	private bool active = false;

	public void release() {
		if (this.active) {
			VM.POOL.Push(this);
			this.active = false;
			Statistics.trace_vm_pool_released();
		} else Statistics.internal_warning("[US]: attempt to release inactive VM! something already released it. not releasing into the pool");
	}

#if DEBUG
	~VM() {
		if (this.active) Statistics.internal_warning("[US]: VM dropped without releasing into the pool!");
	}
#endif
}