using System.Diagnostics;

namespace UmbraSharp.Runtime.VirtualMachine;

internal readonly record struct StackSpan(int start, int end) {
	public readonly int len => this.end - this.start;

	public readonly StackSpan trim(int len) => this.start..(this.start + Math.Min(len, this.len));

	public override string ToString() => $"{this.start}..{this.end}";

	public readonly int this[int i] {
		get {
			Debug.Assert(i >= 0 && i < this.len, $"index {i} exceeded bounds {this}");
			return this.start + i;
		}
	}

	public readonly StackSpan this[Range i] => this[i.Start.Value]..this[i.End.Value];

	public static implicit operator StackSpan(Range range) => new(range.Start.Value, range.End.Value);
}

internal readonly record struct StackSpanVar(StackSpan passed, StackSpan variable) {
	public readonly int len => this.passed.len + this.variable.len;

	public readonly StackSpanVar trim(int len) =>
		len < this.passed.len
			? new(this.passed.trim(len), default)
			: new(this.passed, this.variable.trim(len - this.passed.len));

	public override string ToString() => $"({this.passed} var {this.variable})";

	public readonly int this[int i] => i < this.passed.len ? this.passed[i] : this.variable[i - this.passed.len];
}

// todo: figure out if we want this (it may be helpful to allow spans to be referenced so that native functions could be implemented better, but could cause issues if the user uses anything that modifies the stack while they still hold a ValSpan)
public readonly struct ValSpan {
	internal readonly RegStack stack;
	internal readonly StackSpan span;

	internal ValSpan(RegStack stack, StackSpan span) {
		this.stack = stack;
		this.span = span;
	}

	public readonly int len => this.span.len;

	public readonly ValSpan trim(int len) => new(this.stack, this.span.trim(len));

	public override string ToString() => $"{this.span.start}..{this.span.end}";

	public readonly Val this[int i] => this.stack.data[this.span[i]].value;

	public readonly ValSpan this[Range i] => new(this.stack, this.span[i.Start.Value]..this.span[i.End.Value]);
}

internal sealed class RegStack {
	private readonly Dbg dbg;

	public readonly int max_capacity;
	public VM.Reg[] data;
	public int top = 0;

	public RegStack(int start_capacity, int max_capacity, Dbg dbg) {
		Statistics.trace_alloc_internal($"alloc -> RegStack");
		this.dbg = dbg;
		this.max_capacity = max_capacity;
		Statistics.trace_alloc_internal($"alloc -> RegStack.data[{start_capacity}]");
		this.data = new VM.Reg[start_capacity];
	}

	public StackSpan reserve(int n) {
		if (n < 0) n = 0;
		var end = this.top + n;
		if (end >= this.data.Length) {
			if (end >= this.max_capacity) throw new Exception("lua stack overflow");
			Statistics.trace_alloc_internal($"alloc <-> RegStack.data[{this.data.Length * 2}] (resizing)");
			Array.Resize(ref this.data, this.data.Length * 2);
		}
		return this.top..end;
	}

	public StackSpan alloc(int n) {
		if (n < 0) n = 0;
		var span = this.reserve(n);
		this.top = span.end;
		return span;
	}

	public StackSpan copy(StackSpan src) {
		var dst = this.alloc(src.len);
		for (var i = 0; i < src.len; i++) {
			this.data[dst[i]] = this.data[src[i]].copy;
			this.dbg.notify_copy(src[i], dst[i]);
		}
		return dst;
	}

	public void copy_to(StackSpanVar src, StackSpanVar dst) {
		Console.WriteLine($"{src} -> {dst}");

		for (var i = 0; i < dst.len; i++) {
			if (i < src.len) {
				this.data[dst[i]] = this.data[src[i]].copy;
				this.dbg.notify_copy(src[i], dst[i]);
			} else {

				this.data[dst[i]] = default;
				this.dbg.notify_set(dst[i]);
			}
		}
	}

	public int push(Val val) {
		var range = this.alloc(1);
		this.data[range.start] = new(val);
		dbg.notify_set(range.start);
		return range.start;
	}

	public StackSpan popn(int n) {
		var top = this.top;
		this.top -= n;
		for (var i = this.top; i < top; i++) this.dbg.notify_pop(i);
		return (top - n)..top;
	}
}