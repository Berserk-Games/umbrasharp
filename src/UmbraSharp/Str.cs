using System.Diagnostics.CodeAnalysis;

namespace UmbraSharp;

// we *could* make these slices over bytes and use utf-8, but given the amount of interop with c#...
// maybe it's best not to, to avoid constantly converting between utf8 and

/// <summary>trivially sliceable string type</summary>
public readonly struct Str {
	internal readonly string? buf;
	internal readonly int offset;
	public readonly int len;

	public ReadOnlyMemory<char> memory => this.buf is not null ? this.buf.AsMemory(offset, len) : default;
	public ReadOnlySpan<char> span => this.buf is not null ? this.buf.AsSpan(offset, len) : default;

	internal Str(string? buf, int offset, int len) {
		if (buf is not null && len > 0) {
			this.buf = buf;
			this.offset = offset;
			this.len = len;
		}
	}
	public Str(string str) : this(str, 0, str.Length) { }

	public Str? try_slice(int offset) {
		if ((uint)offset > (uint)this.len) return null;
		return new(this.buf, this.offset + offset, this.len - offset);
	}

	public Str? try_slice(int offset, int len) {
		if ((ulong)((long)(uint)offset + (long)(uint)len) > (ulong)(uint)this.len) return null;
		return new(this.buf, this.offset + offset, len);
	}

	public Str sub(int start, int end) {
		if (start >= this.len) return default;
		return new(this.buf, this.offset + start, Math.Min(end + 1 - start, this.len));
	}

	public char this[int i] => this.span[i];
	public Str this[Range range] {
		get {
			var (offset, len) = range.GetOffsetAndLength(this.len);
			return this.try_slice(offset, len) ?? throw new IndexOutOfRangeException();
		}
	}

	public override string ToString() => (this.buf is not null && this.offset == 0 && this.len == buf.Length) ? this.buf : this.span.ToString();

	public override int GetHashCode() => string.GetHashCode(this.span);

	public override bool Equals([NotNullWhen(true)] object? obj) => obj switch {
		Str str => this == str,
		string full => this == full.AsSpan(),
		ReadOnlyMemory<char> chars => this == chars.Span,
		char[] chars => this == chars,
		_ => false,
	};

	public static bool operator ==(Str lhs, Str rhs) => (lhs.buf == rhs.buf && lhs.offset == rhs.offset && lhs.len == rhs.len) || lhs.span.Equals(rhs.span, StringComparison.Ordinal);
	public static bool operator !=(Str lhs, Str rhs) => !(lhs == rhs);
	public static bool operator ==(Str lhs, ReadOnlySpan<char> rhs) => lhs.span.Equals(rhs, StringComparison.Ordinal);
	public static bool operator !=(Str lhs, ReadOnlySpan<char> rhs) => !(lhs == rhs);
	public static bool operator ==(ReadOnlySpan<char> lhs, Str rhs) => lhs.Equals(rhs.span, StringComparison.Ordinal);
	public static bool operator !=(ReadOnlySpan<char> lhs, Str rhs) => !(lhs == rhs);

	public static implicit operator Str(string val) => new(val);
	public static implicit operator ReadOnlyMemory<char>(Str str) => str.memory;
	public static implicit operator ReadOnlySpan<char>(Str str) => str.span;
}