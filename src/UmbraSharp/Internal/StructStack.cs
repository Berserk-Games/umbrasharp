using System.Runtime.CompilerServices;

namespace UmbraSharp.Internal;

public sealed class StructStack<T> {
	public ref struct DownwardsEnumerator {
		private readonly Span<T> span;
		private int index;

		public readonly ref T Current => ref this.span[this.index];

		internal DownwardsEnumerator(Span<T> span) {
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

	public T[] data;
	public int top = 0;

	public StructStack(int capacity) {
		Statistics.trace_alloc_internal($"alloc -> StructStack<{typeof(T).Name}>");
		Statistics.trace_alloc_internal($"alloc -> StructStack<{typeof(T).Name}>.data[{capacity}]");
		this.data = new T[capacity];
	}

	public ref T peek => ref this.data[this.top - 1];
	public Span<T> span => this.data.AsSpan()[0..this.top];

	public void push(T frame) {
		var end = this.top + 1;
		if (end >= this.data.Length) {
			Statistics.trace_alloc_internal($"alloc <-> StructStack<{typeof(T).Name}>.data[{this.data.Length * 2}] (resizing)");
			Array.Resize(ref this.data, this.data.Length * 2);
		}
		this.data[this.top++] = frame;
	}

	public T pop() => this.data[--this.top];

	public void clear() {
		this.top = 0;
		if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) this.data.AsSpan().Clear();
	}

	public DownwardsEnumerator enumerate_downwards() => new(this.span);
}