namespace UmbraSharp.Compiler;

public abstract class Stmt: IPretty {
	public readonly record struct Block(NonTerm[] body, Term? term): IPretty {
		public readonly bool should_expand {
			get {
				if (this.body.Length > (this.term is not null ? 0 : 1)) return true;

				foreach (var stmt in this.body) {
					if (stmt.is_long) return true;
				}

				if (this.term is not null && this.term.is_long) return true;

				return false;
			}
		}

		public void pretty(ref Pretty p) {
			var first = true;
			foreach (var stmt in this.body) {
				if (first) first = false;
				else p.newline();
				stmt.pretty(ref p);
			}
			if (this.term is not null) {
				if (!first) p.newline();
				this.term.pretty(ref p);
			}
		}

		public bool pretty_auto_expand(ref Pretty p) {
			if (this.should_expand) {
				p.indent++;
				p.newline();
				this.pretty(ref p);
				p.indent--;
				return true;
			} else {
				p.dst.Append(' ');
				this.pretty(ref p);
				return false;
			}
		}
	}

	#region non-terminating statements

	public abstract class NonTerm: Stmt { }

	#region control structures

	public sealed class Do(Block body): NonTerm {
		public Block body = body;

		public override bool is_long => true;

		public override void pretty(ref Pretty p) {
			p.dst.Append("do");
			if (this.body.pretty_auto_expand(ref p)) p.newline();
			else p.dst.Append(' ');
			p.dst.Append("end");
		}
	}

	public sealed class If(If.Case if_case, If.Case[] elseif_cases, Block? else_case): NonTerm {
		public readonly record struct Case(Expr cond, Block body);

		public Case if_case = if_case;
		public Case[] elseif_cases = elseif_cases;
		public Block? else_case = else_case;

		public override bool is_long => true;

		public override void pretty(ref Pretty p) {
			p.dst.Append("if ");
			this.if_case.cond.pretty(ref p);
			p.dst.Append(" then");
			if (this.if_case.body.pretty_auto_expand(ref p) || this.elseif_cases.Length > 0 || this.else_case is not null) p.newline();
			else p.dst.Append(' ');

			for (var i = 0; i < this.elseif_cases.Length; i++) {
				var elseif_case = this.elseif_cases[i];
				p.dst.Append("elseif ");
				elseif_case.cond.pretty(ref p);
				p.dst.Append(" then ");
				if (elseif_case.body.pretty_auto_expand(ref p) || this.elseif_cases.Length > i + 1 || this.else_case is not null) p.newline();
				else p.dst.Append(' ');
			}

			if (this.else_case is Block block) {
				p.dst.Append("else ");
				if (block.pretty_auto_expand(ref p)) p.newline();
				else p.dst.Append(' ');
			}

			p.dst.Append("end");
		}
	}

	public sealed class ForNumeric(Str variable, Expr initial, Expr limit, Expr? step, Block body): NonTerm {
		public Str variable = variable;
		public Expr initial = initial;
		public Expr limit = limit;
		public Expr? step = step;
		public Block body = body;

		public override bool is_long => true;

		public override void pretty(ref Pretty p) {
			p.dst.Append("for ");
			p.dst.Append(this.variable.span);
			p.dst.Append(" = ");
			this.initial.pretty(ref p);
			p.dst.Append(", ");
			this.limit.pretty(ref p);
			if (this.step is not null) {
				p.dst.Append(", ");
				this.step.pretty(ref p);
			}
			p.dst.Append(" do");
			if (this.body.pretty_auto_expand(ref p)) p.newline();
			else p.dst.Append(' ');
			p.dst.Append("end");
		}
	}

	public sealed class ForGeneric(Str[] variables, Expr iterator, Block body): NonTerm {
		public Str[] variables = variables;
		public Expr iterator = iterator;
		public Block body = body;

		public override bool is_long => true;

		public override void pretty(ref Pretty p) {
			p.dst.Append("for ");
			p.delimited<Str>(this.variables.AsSpan(), ", ");
			p.dst.Append(" in ");
			this.iterator.pretty(ref p);
			p.dst.Append(" do");
			if (this.body.pretty_auto_expand(ref p)) p.newline();
			else p.dst.Append(' ');
			p.dst.Append("end");
		}
	}

	public sealed class While(Expr cond, Block body): NonTerm {
		public Expr cond = cond;
		public Block body = body;

		public override bool is_long => true;

		public override void pretty(ref Pretty p) {
			p.dst.Append("while ");
			this.cond.pretty(ref p);
			p.dst.Append(" do");
			if (this.body.pretty_auto_expand(ref p)) p.newline();
			else p.dst.Append(' ');
			p.dst.Append("end");
		}
	}

	public sealed class Until(Block body, Expr cond): NonTerm {
		public Block body = body;
		public Expr cond = cond;

		public override bool is_long => true;

		public override void pretty(ref Pretty p) {
			p.dst.Append("repeat");
			if (this.body.pretty_auto_expand(ref p)) p.newline();
			else p.dst.Append(' ');
			p.dst.Append("until ");
			this.cond.pretty(ref p);
		}
	}

	#endregion

	public sealed class Assignment(Expr.LValue[] lhs, Expr[] rhs): NonTerm {
		public Expr.LValue[] lhs = lhs;
		public Expr[] rhs = rhs;

		public override bool is_long => false;

		public override void pretty(ref Pretty p) {
			p.delimited<Expr.LValue>(this.lhs.AsSpan(), ", ");
			p.dst.Append(" = ");
			p.delimited<Expr>(this.rhs.AsSpan(), ", ");
		}
	}

	public sealed class CompoundAssignment(Expr.LValue lhs, CompoundAssignment.Op op, Expr rhs): NonTerm {
		public enum Op {
			Add,
			Sub,
			Mul,
			Div,
			DivFloor,
			Mod,
			Pow,
			Concat,
		}

		public Expr.LValue lhs = lhs;
		public Op op = op;
		public Expr rhs = rhs;

		public override bool is_long => true;

		public override void pretty(ref Pretty p) {
			this.lhs.pretty(ref p);
			p.dst.Append(this.op switch {
				Op.Add => " += ",
				Op.Sub => " -= ",
				Op.Mul => " *= ",
				Op.Div => " /= ",
				Op.DivFloor => " //= ",
				Op.Mod => " %= ",
				Op.Pow => " ^= ",
				Op.Concat => " ..= ",
				_ => default,
			});
			this.rhs.pretty(ref p);
		}
	}

	#endregion

	#region terminating statements

	public abstract class Term: Stmt { }

	public sealed class Return(Expr[] rets): NonTerm {
		public Expr[] rets = rets;

		public override bool is_long => false;

		public override void pretty(ref Pretty p) {
			p.dst.Append(this.rets.Length > 0 ? "return " : "return");
			p.delimited<Expr>(this.rets.AsSpan(), ", ");
		}
	}

	public sealed class Break: NonTerm {
		public static readonly Break brk = new();

		private Break() { }

		public override bool is_long => false;

		public override void pretty(ref Pretty p) => p.dst.Append("break");
	}

	public sealed class Continue: NonTerm {
		public static readonly Continue cont = new();

		private Continue() { }

		public override bool is_long => false;

		public override void pretty(ref Pretty p) => p.dst.Append("continue");
	}

	#endregion

	// todo: inspect exprs for multiline stuff
	public abstract bool is_long { get; }

	public abstract void pretty(ref Pretty p);

	// public abstract void compile();
}
