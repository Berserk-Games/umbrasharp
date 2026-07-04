namespace UmbraSharp.Compiler;

public static class Compiler {
	// public static Expr parse_expr(string src) { }

	// public static Stmt parse_stmt(string src) { }

	// public static Block parse_chunk(string src) { }

	// public static Fn compile(Expr src) { }

	public static (int line, int col) line_col(int offset, Str source) {
		var haystack = source.span[0..(offset - 1)];
		var start = haystack.LastIndexOf('\n');
		return (haystack.Count('\n'), offset - (start != -1 ? start : 0) - 1);
	}
}