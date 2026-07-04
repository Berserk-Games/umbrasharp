using UmbraSharp.Internal;

namespace UmbraSharp.Compiler;

public abstract class Expr(int loc): UserData(null), IPretty {
	public abstract class LValue(int loc): Expr(loc) {
		// todo
	}

	public abstract class Adjustable(int loc, bool adjust_results): Expr(loc) {
		public bool adjust_results = adjust_results;
	}

	public sealed class LiteralNil(int loc): Expr(loc) {
		public override bool is_prefix => false;
		public override bool is_foldable => true;

		public override Val eval(Table globals) => default;

		public override void pretty(ref Pretty p) => p.dst.Append("nil");
	}

	public sealed class LiteralBool(int loc, bool value): Expr(loc) {
		public bool value = value;

		public override bool is_prefix => false;
		public override bool is_foldable => true;

		public override Val eval(Table globals) => this.value;

		public override void pretty(ref Pretty p) => p.dst.Append(this.value ? "true" : "false");
	}

	public sealed class LiteralNumber(int loc, Val.Num value): Expr(loc) {
		public Val.Num value = value;

		public override bool is_prefix => false;
		public override bool is_foldable => true;

		public override Val eval(Table globals) => this.value;

		public override void pretty(ref Pretty p) => p.dst.Append(this.value.ToString());
	}

	public sealed class LiteralString(int loc, Str value): Expr(loc) {
		public Str value = value;

		public override bool is_prefix => false;
		public override bool is_foldable => true;

		public override Val eval(Table globals) => this.value;

		public override void pretty(ref Pretty p) {
			p.dst.Append('"');
			p.dst.Append(
				this.value.span.ToString()
					.Replace("\\", "\\\\")
					.Replace("\a", "\\a")
					.Replace("\b", "\\b")
					.Replace("\f", "\\f")
					.Replace("\n", "\\n")
					.Replace("\r", "\\r")
					.Replace("\t", "\\t")
					.Replace("\v", "\\v")
					.Replace("\0", "\\0")
					.Replace("\"", "\\\"")
			);
			p.dst.Append('"');
		}
	}

	public sealed class InterpString(int loc, Str prefix, InterpString.Segment[] segments): Expr(loc) {
		public readonly record struct Segment(Expr value, Str suffix);

		public Str prefix = prefix;
		public Segment[] segments = segments;

		public override bool is_prefix => false;
		public override bool is_foldable => true;

		public override Val eval(Table globals) {
			var b = StringBuilderPool.acquire();
			b.Append(this.prefix.span);
			foreach (var seg in this.segments) {
				b.Append(seg.value.eval(globals).raw_tostring().span);
				b.Append(seg.suffix.span);
			}
			return StringBuilderPool.release_tostring(b);
		}

		public override void pretty(ref Pretty p) {
			static string clean(Str str) => str.span.ToString()
				.Replace("\\", "\\\\")
				.Replace("\a", "\\a")
				.Replace("\b", "\\b")
				.Replace("\f", "\\f")
				.Replace("\n", "\\n")
				.Replace("\r", "\\r")
				.Replace("\t", "\\t")
				.Replace("\v", "\\v")
				.Replace("\0", "\\0")
				.Replace("`", "\\`");

			p.dst.Append('`');
			p.dst.Append(clean(this.prefix));
			foreach (var seg in this.segments) {
				p.dst.Append('{');
				seg.value.pretty(ref p);
				p.dst.Append('}');
				p.dst.Append(clean(seg.suffix));
			}
			p.dst.Append('`');
		}
	}

	public sealed class Var(int loc, bool adjusted): Adjustable(loc, adjusted) {
		public override bool is_prefix => false;
		public override bool is_foldable => false;

		public override Val eval(Table globals) => RuntimeError.attempt_to<Val>("use '...' in dynamic context");

		public override void pretty(ref Pretty p) {
			if (this.adjust_results) p.dst.Append("(...)");
			else p.dst.Append("...");
		}
	}

	public sealed class Name(int loc, Str name): LValue(loc) {
		public Str name = name;

		public override bool is_prefix => true;
		public override bool is_foldable => false;

		public override Val eval(Table globals) => globals.raw_get(this.name);

		public override void pretty(ref Pretty p) => p.dst.Append(this.name.span);
	}

	public sealed class TableConstructor(int loc, TableConstructor.Entry[] entries, bool prime): Expr(loc) {
		public readonly record struct Entry(Expr? key, Str? key_str, Expr val);

		public TableConstructor.Entry[] entries = entries;
		public bool prime = prime;

		public override bool is_prefix => false;
		public override bool is_foldable {
			get {
				foreach (var entry in this.entries) {
					if (entry.key is not null && !entry.key.is_foldable) return false;
					if (!entry.val.is_foldable) return false;
				}
				return true;
			}
		}

		public override Val eval(Table globals) {
			var tbl = new Table();

			var array = 0;
			foreach (var entry in this.entries) {
				if (entry.key is not null) tbl.raw_set(entry.key.eval(globals), entry.val.eval(globals));
				else if (entry.key_str is Str key_str) tbl.raw_set(key_str, entry.val.eval(globals));
				else tbl.raw_set(++array, entry.val.eval(globals));
			}

			return tbl;
		}

		// todo: overflow into multiple lines
		public override void pretty(ref Pretty p) {
			if (this.prime) p.dst.Append('$');

			if (this.entries.Length == 0) {
				p.dst.Append("{}");
				return;
			}

			p.dst.Append("{ ");
			var first = true;
			foreach (var entry in this.entries) {
				if (first) first = false;
				else p.dst.Append(", ");

				if (entry.key is not null) {
					p.dst.Append('[');
					entry.key.pretty(ref p);
					p.dst.Append("] = ");
					entry.val.pretty(ref p);
				} else if (entry.key_str is Str key_str) {
					p.dst.Append(key_str.span);
					p.dst.Append(" = ");
					entry.val.pretty(ref p);
				} else entry.val.pretty(ref p);
			}
			p.dst.Append(" }");
		}
	}

	public sealed class Binary(int loc, Expr lhs, Binary.Op op, Expr rhs): Expr(loc) {
		public enum Op {
			Add,
			Sub,
			Mul,
			Div,
			DivFloor,
			Mod,
			Pow,
			Concat,
			Eq,
			Ne,
			Lt,
			Le,
			Gt,
			Ge,
			And,
			Or,
		}

		public Expr lhs = lhs;
		public Op op = op;
		public Expr rhs = rhs;

		public override bool is_prefix => false;
		public override bool is_foldable => this.lhs.is_foldable && this.rhs.is_foldable;

		public override Val eval(Table globals) {
			var lhs = this.lhs.eval(globals);

			if (this.op == Op.Or) return lhs.is_truthy ? lhs : this.rhs.eval(globals);
			if (this.op == Op.And) return lhs.is_truthy ? this.rhs.eval(globals) : lhs;

			var rhs = this.rhs.eval(globals);
			return this.op switch {
				Op.Add => lhs.raw_add(rhs),
				Op.Sub => lhs.raw_sub(rhs),
				Op.Mul => lhs.raw_mul(rhs),
				Op.Div => lhs.raw_div(rhs),
				Op.DivFloor => lhs.raw_div_floor(rhs),
				Op.Mod => lhs.raw_mod(rhs),
				Op.Pow => lhs.raw_pow(rhs),
				Op.Concat => throw InternalError.unreachable(), // todo
				Op.Eq => lhs.raw_eq(rhs),
				Op.Ne => !lhs.raw_eq(rhs),
				Op.Lt => lhs.raw_lt(rhs),
				Op.Le => lhs.raw_le(rhs),
				Op.Gt => rhs.raw_lt(lhs),
				Op.Ge => rhs.raw_le(lhs),
				_ => default,
			};
		}

		public override void pretty(ref Pretty p) {
			p.dst.Append('(');
			this.lhs.pretty(ref p);
			p.dst.Append(this.op switch {
				Op.Add => " + ",
				Op.Sub => " - ",
				Op.Mul => " * ",
				Op.Div => " / ",
				Op.DivFloor => " // ",
				Op.Mod => " % ",
				Op.Pow => " ^ ",
				Op.Concat => " .. ",
				Op.Eq => " == ",
				Op.Ne => " ~= ",
				Op.Lt => " < ",
				Op.Le => " <= ",
				Op.Gt => " > ",
				Op.Ge => " >= ",
				Op.And => " and ",
				Op.Or => " or ",
				_ => default,
			});
			this.rhs.pretty(ref p);
			p.dst.Append(')');
		}
	}

	public sealed class Unary(int loc, Unary.Op op, Expr operand): Expr(loc) {
		public enum Op {
			Not,
			Unm,
			Len,
		}

		public Op op = op;
		public Expr operand = operand;

		public override bool is_prefix => false;
		public override bool is_foldable => this.operand.is_foldable;

		public override Val eval(Table globals) => (this.op, this.operand.eval(globals)) switch {
			(Op.Not, var operand) => !operand.is_truthy,
			(Op.Unm, var operand) => operand.raw_unm(),
			(Op.Len, var operand) => operand.raw_len,
			_ => default,
		};

		public override void pretty(ref Pretty p) {
			p.dst.Append(this.op switch {
				Op.Not => "(not ",
				Op.Unm => "(-",
				Op.Len => "(#",
				_ => default,
			});
			this.operand.pretty(ref p);
			p.dst.Append(')');
		}
	}

	public sealed class Index(int loc, Expr indexee, Expr[] index): LValue(loc) {
		public Expr indexee = indexee;
		public Expr[] index = index;

		public override bool is_prefix => true;
		public override bool is_foldable {
			get {
				if (!this.indexee.is_foldable) return false;
				foreach (var idx in this.index) {
					if (!idx.is_foldable) return false;
				}
				return true;
			}
		}

		public override Val eval(Table globals) {
			var indexee = this.indexee.eval(globals);
			var index = this.index[0].eval(globals);
			if (!indexee.is_table) RuntimeError.attempt_to<None>("index non-table");
			if (index.is_nil || (index.is_double && double.IsNaN(index.assume_number.assume_double))) RuntimeError.attempt_to<None>("index with nil or nan key");
			return indexee.raw_get(index);
		}

		public override void pretty(ref Pretty p) {
			if (this.indexee.is_prefix) this.indexee.pretty(ref p);
			else {
				p.dst.Append('(');
				this.indexee.pretty(ref p);
				p.dst.Append(')');
			}
			p.dst.Append('[');
			p.delimited<Expr>(this.index.AsSpan(), ", ");
			p.dst.Append(']');
		}
	}

	public sealed class Field(int loc, Expr indexee, Str index): LValue(loc) {
		public Expr indexee = indexee;
		public Str index = index;

		public override bool is_prefix => true;
		public override bool is_foldable => this.indexee.is_foldable;

		public override Val eval(Table globals) {
			var indexee = this.indexee.eval(globals);
			if (!indexee.is_table) RuntimeError.attempt_to<None>("index non-table");
			return indexee.raw_get(this.index);
		}

		public override void pretty(ref Pretty p) {
			if (this.indexee.is_prefix) this.indexee.pretty(ref p);
			else {
				p.dst.Append('(');
				this.indexee.pretty(ref p);
				p.dst.Append(')');
			}
			p.dst.Append('.');
			p.dst.Append(this.index.span);
		}
	}

	public sealed class Function(int loc, Str[] args, Str? vararg, Stmt.Block body): Expr(loc) {
		public readonly record struct Attr(int loc, Str name, Expr[] args);

		public Str[] args = args;
		public Str? vararg = vararg;
		public Stmt.Block body = body;

		public override bool is_prefix => false;
		// functions arent really useful to constant fold, and checking for impurities is more difficult
		public override bool is_foldable => false;

		public override Val eval(Table globals) => RuntimeError.attempt_to<Val>("define function in dynamic");

		public override void pretty(ref Pretty p) {
			// todo
		}
	}

	public sealed class Call(int loc, Expr callee, Expr[] args, bool adjust_results): Adjustable(loc, adjust_results) {
		public Expr callee = callee;
		public Expr[] args = args;

		public override bool is_prefix => true;
		// not worth trying to figure out if an IIFE captures upvars
		public override bool is_foldable => false;

		public override Val eval(Table globals) => RuntimeError.attempt_to<Val>("call in dynamic");

		public override void pretty(ref Pretty p) {
			if (this.adjust_results) p.dst.Append('(');
			if (this.callee.is_prefix) this.callee.pretty(ref p);
			else {
				p.dst.Append('(');
				this.callee.pretty(ref p);
				p.dst.Append(')');
			}
			p.dst.Append('(');
			p.delimited<Expr>(this.args.AsSpan(), ", ");
			p.dst.Append(')');
			if (this.adjust_results) p.dst.Append(')');
		}
	}

	public sealed class MethodCall(int loc, Expr callee, Str method, Expr[] args, bool adjust_results): Adjustable(loc, adjust_results) {
		public Expr callee = callee;
		public Str method = method;
		public Expr[] args = args;

		public override bool is_prefix => true;
		public override bool is_foldable => false;

		public override Val eval(Table globals) => RuntimeError.attempt_to<Val>("call in dynamic");

		public override void pretty(ref Pretty p) {
			if (this.adjust_results) p.dst.Append('(');
			if (this.callee.is_prefix) this.callee.pretty(ref p);
			else {
				p.dst.Append('(');
				this.callee.pretty(ref p);
				p.dst.Append(')');
			}
			p.dst.Append(':');
			p.dst.Append(this.method.span);
			p.dst.Append('(');
			var first = true;
			foreach (var arg in this.args) {
				if (first) first = false;
				else p.dst.Append(", ");
				arg.pretty(ref p);
			}
			p.dst.Append(')');
			if (this.adjust_results) p.dst.Append(')');
		}
	}

	public sealed class If(int loc, If.Case if_case, If.Case[] elseif_cases, Expr else_case): Expr(loc) {
		public readonly record struct Case(Expr cond, Expr body);

		public Case if_case = if_case;
		public Case[] elseif_cases = elseif_cases;
		public Expr else_case = else_case;

		public override bool is_prefix => false;
		public override bool is_foldable {
			get {
				foreach (var c in this.elseif_cases)
					if (!c.cond.is_foldable || !c.body.is_foldable) return false;
				return this.if_case.cond.is_foldable && this.if_case.body.is_foldable && this.else_case.is_foldable;
			}
		}

		public override Val eval(Table globals) {
			if (this.if_case.cond.eval(globals).is_truthy) return this.if_case.body.eval(globals);

			foreach (var c in this.elseif_cases)
				if (c.cond.eval(globals).is_truthy) return c.body.eval(globals);

			return this.else_case.eval(globals);
		}

		public override void pretty(ref Pretty p) {
			p.dst.Append("(if ");
			this.if_case.cond.pretty(ref p);
			p.dst.Append(" then ");
			this.if_case.body.pretty(ref p);

			foreach (var elseif_case in this.elseif_cases) {
				p.dst.Append(" elseif ");
				elseif_case.cond.pretty(ref p);
				p.dst.Append(" then ");
				elseif_case.body.pretty(ref p);
			}

			p.dst.Append(" else ");
			this.else_case.pretty(ref p);
			p.dst.Append(')');
		}
	}

	public int loc;

	public abstract bool is_prefix { get; }
	public abstract bool is_foldable { get; }

	public abstract Val eval(Table globals);

	public abstract void pretty(ref Pretty p);

	// public abstract void compile();
}
