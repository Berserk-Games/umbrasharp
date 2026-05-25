using UmbraSharp.Internal;

namespace UmbraSharp.Runtime.VirtualMachine;

internal sealed class CallStack {
	public ref struct DownwardsEnumerator {
		private readonly Span<VM.StackFrame> span;
		private int index;

		public readonly ref VM.StackFrame Current => ref this.span[this.index];

		internal DownwardsEnumerator(Span<VM.StackFrame> span) {
			this.span = span;
			this.index = this.span.Length;
		}

		public bool MoveNext() {
			var num = this.index - 1;
			if (num >= 0) {
				this.index = num;
				return true;
			}
			return false;
		}

		public readonly DownwardsEnumerator GetEnumerator() => this;
	}

	public readonly int max_capacity;
	public VM.StackFrame[] data;
	public int top = 0;

	public CallStack(int start_capacity, int max_capacity) {
		Statistics.trace_alloc_internal($"alloc -> CallStack");
		this.max_capacity = max_capacity;
		Statistics.trace_alloc_internal($"alloc -> CallStack.data[{start_capacity}]");
		this.data = new VM.StackFrame[start_capacity];
	}

	public ref VM.StackFrame peek => ref this.data[this.top - 1];
	public Span<VM.StackFrame> span => this.data.AsSpan()[0..this.top];

	public void push(VM.StackFrame frame) {
		var end = this.top + 1;
		if (end >= this.data.Length) {
			if (end > this.max_capacity) throw new Exception("lua call stack overflow");
			Statistics.trace_alloc_internal($"alloc <-> CallStack.data[{this.data.Length * 2}] (resizing)");
			Array.Resize(ref this.data, this.data.Length * 2);
		}
		this.data[this.top++] = frame;
	}

	public VM.StackFrame pop() {
		if (this.top <= 0) throw new Exception("lua call stack underflow");
		return this.data[--this.top];
	}

	public DownwardsEnumerator enumerate_downwards() => new(this.span);
}