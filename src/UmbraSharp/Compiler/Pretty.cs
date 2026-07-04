using System.Text;

namespace UmbraSharp.Compiler;

public struct Pretty(StringBuilder dst) {
	public static string print(IPretty target) {
		var p = new Pretty(new());
		target.pretty(ref p);
		return p.dst.ToString();
	}

	public readonly StringBuilder dst = dst;
	public int indent;

	public readonly void newline() {
		this.dst.Append('\n');
		this.dst.Append('\t', this.indent);
	}

	public void delimited_pretty<T>(ReadOnlySpan<T> values, string delimiter) where T : IPretty {
		var first = true;
		foreach (var val in values) {
			if (first) first = false;
			else this.dst.Append(delimiter);
			val.pretty(ref this);
		}
	}

	public readonly void delimited<T>(ReadOnlySpan<T> values, string delimiter) {
		var first = true;
		foreach (var val in values) {
			if (first) first = false;
			else this.dst.Append(delimiter);
			this.dst.Append(val?.ToString());
		}
	}
}

public interface IPretty {
	void pretty(ref Pretty p);
}
