using System.Diagnostics;
using UmbraSharp.Runtime;

namespace UmbraSharp.Internal;

/// <summary>tracks information for the purposes of developing UmbraSharp</summary>
/// <remarks>nonfunctional in release mode</remarks>
public static class Statistics {
	public struct Capture {
		public long time;
		public Stats stats;
		public string[] log;
	}

	public struct Stats {
		public int instructions;
		public int vm_pool_acquired;
		public int vm_pool_released;
		public int alloc_vm;
		public int alloc_slot;
		public int alloc_upvalues;
		public int alloc_coro;
		public int alloc_table;
		public int alloc_internal;

		public override readonly string ToString() => $"""
			stats:
				{this.instructions} instructions executed
				{this.vm_pool_acquired} VMs acquired from the
				{this.alloc_vm} new VMs allocated
				{this.vm_pool_released} VMs released into the pool
				{this.alloc_slot} upvar slots allocated
				{this.alloc_upvalues} upvar arrays allocated
				{this.alloc_coro} coroutines allocated
				{this.alloc_table} tables allocated
				{this.alloc_internal} internal allocations
		""";
	}

	/// <summary>whether individual allocations, executed instructions should be put into the log field of captures</summary>
	public static bool full_trace = false;

	public static Stats current => new() {
		instructions = Statistics.total.instructions - Statistics.last_reset_stats.instructions,
		vm_pool_acquired = Statistics.total.vm_pool_acquired - Statistics.last_reset_stats.vm_pool_acquired,
		vm_pool_released = Statistics.total.vm_pool_released - Statistics.last_reset_stats.vm_pool_released,
		alloc_vm = Statistics.total.alloc_vm - Statistics.last_reset_stats.alloc_vm,
		alloc_slot = Statistics.total.alloc_slot - Statistics.last_reset_stats.alloc_slot,
		alloc_upvalues = Statistics.total.alloc_upvalues - Statistics.last_reset_stats.alloc_upvalues,
		alloc_coro = Statistics.total.alloc_coro - Statistics.last_reset_stats.alloc_coro,
		alloc_table = Statistics.total.alloc_table - Statistics.last_reset_stats.alloc_table,
		alloc_internal = Statistics.total.alloc_internal - Statistics.last_reset_stats.alloc_internal,
	};
	public static Stats total = default;

	private static Stats last_reset_stats = default;
	private static long last_reset_time = Stopwatch.GetTimestamp();

	private static readonly List<string> log = [];

	/// <summary>reset the statistics
	[Conditional("DEBUG")]
	public static void reset() {
		Statistics.last_reset_stats = Statistics.total;
		Statistics.last_reset_time = Stopwatch.GetTimestamp();
		Statistics.log.Clear();
	}

	public static Capture capture() {
#if !DEBUG
		return default;
#else
		var time_end = Stopwatch.GetTimestamp();
		return new() {
			time = time_end - Statistics.last_reset_time,
			stats = Statistics.current,
			log = [.. Statistics.log],
		};
#endif
	}

	[Conditional("DEBUG")]
	internal static void trace(string msg) {
		if (!Statistics.full_trace) return;
		Statistics.log.Add(msg);
		Console.Error.Write("[trace] ");
		Console.Error.WriteLine(msg);
	}

	[Conditional("DEBUG")]
	internal static void trace_inst(Str fn, int idx, ByteCode.Inst inst) {
		if (Statistics.full_trace) Statistics.trace($"inst [{fn}+{idx:X4}] {inst}");
	}

	[Conditional("DEBUG")]
	internal static void trace_vm_pool_acquired() {
		if (Statistics.full_trace) Statistics.trace("vm pool -> acquired");
		Statistics.total.vm_pool_acquired++;
	}

	[Conditional("DEBUG")]
	internal static void trace_vm_pool_released() {
		if (Statistics.full_trace) Statistics.trace("vm pool <- released");
		Statistics.total.vm_pool_released++;
	}

	[Conditional("DEBUG")]
	internal static void trace_alloc_vm() {
		if (Statistics.full_trace) Statistics.trace("alloc -> vm");
		Statistics.total.alloc_vm++;
	}

	[Conditional("DEBUG")]
	internal static void trace_slot() {
		if (Statistics.full_trace) Statistics.trace("alloc -> register slot");
		Statistics.total.alloc_slot++;
	}

	[Conditional("DEBUG")]
	internal static void trace_alloc_upvalues() {
		if (Statistics.full_trace) Statistics.trace("alloc -> lua functions upvalues");
		Statistics.total.alloc_upvalues++;
	}

	[Conditional("DEBUG")]
	internal static void trace_alloc_coro() {
		if (Statistics.full_trace) Statistics.trace("alloc -> thread");
		Statistics.total.alloc_coro++;
	}

	[Conditional("DEBUG")]
	internal static void trace_alloc_table() {
		if (Statistics.full_trace) Statistics.trace("alloc -> table");
		Statistics.total.alloc_table++;
	}

	[Conditional("DEBUG")]
	internal static void trace_alloc_internal(string kind) {
		if (Statistics.full_trace) Statistics.trace(kind);
		Statistics.total.alloc_internal++;
	}

	[Conditional("DEBUG")]
	internal static void internal_warning(string err) {
		Console.Error.WriteLine($"[UmbraSharp // INTERNAL WARNING] {err}");
		Statistics.trace($"internal warning: {err}");
	}
}