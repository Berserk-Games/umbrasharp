using UmbraSharp.Internal;

namespace UmbraSharp.Compiler;

using TK = Token.Kind;

// todo: luau

public struct Parser(Lexer lx) {
	private struct FunctionState {
		public bool vararg;
		public int loop_depth;
	}

	public Lexer lx = lx;
	private readonly StructStack<FunctionState> func_stack = StructStackPool.acquire<FunctionState>();

	#region shared

	private Expr.Function.Attr[] attrs() {
		var attrs = ListPool.acquire<Expr.Function.Attr>();

		while (this.lx.peek.kind is TK.Attr or TK.AtLBracket) {
			var start = this.lx.next();
			if (start.kind is TK.Attr) attrs.Add(new(start.offset, start.val.as_string, []));
			else {
				if (this.take(TK.RBracket) is Token bad_bracket) throw new SyntaxError(bad_bracket.offset, "attribute list cannot be empty");
				do {
					var name = this.expect(TK.Name, "attribute name");
					attrs.Add(new(
						name.offset,
						name.val.as_string,
						this.lx.peek.kind is TK.LParen or TK.LBrace or TK.String ? this.call_list().args : []
					));
				} while (this.take(TK.Comma) is not null);
				this.expect(TK.RBracket, "']'");
			}
		}

		return ListPool.release_toarray(attrs);
	}

	// todo: figure out return type
	private void function(bool has_self) {
		var par_list = ListPool.acquire<Str>();
		Str? vararg = null;
		while (true) {
			var param = this.lx.next();
			switch (param.kind) {
				case TK.Name: {
						par_list.Add(param.val.as_string);
						break;
					}

				case TK.DotDotDot: {
						if (this.lx.config.named_vararg_support && this.take(TK.Name) is Token name) vararg = name.val.as_string;
						else vararg = "";
						this.expect(TK.RParen);
						goto pars_done;
					}
			}

			var next = this.lx.next();
			switch (next.kind) {
				case TK.Comma: continue;

				case TK.RParen: goto pars_done;

				default: throw SyntaxError.expected(next.offset, "'<name>' or '...'", next.text.ToString());
			}
		}
	pars_done:
		var pars = ListPool.release_toarray(par_list);

		// todo: some context setup

		var block = this.block();

		// todo: some context teardown
	}

	#endregion

	#region stmt

	public Stmt.Block block() {
		return default;
	}

	public Stmt stmt() {
		return null!;
	}

	#endregion

	#region expr

	public Expr expr() => this.operator_expr(0);

	private Expr operator_expr(int priority_limit) {
		static Expr.Unary.Op? unary_op(TK kind) => kind switch {
			TK.Kw_Not => Expr.Unary.Op.Not,
			TK.Minus => Expr.Unary.Op.Unm,
			TK.Hash => Expr.Unary.Op.Len,
			_ => null,
		};

		static (Expr.Binary.Op, int lhs, int rhs)? binary_op(TK op) => op switch {
			// luau operator priorities
			TK.Plus => (Expr.Binary.Op.Add, 6, 6),
			TK.Minus => (Expr.Binary.Op.Sub, 6, 6),
			TK.Asterisk => (Expr.Binary.Op.Mul, 7, 7),
			TK.Slash => (Expr.Binary.Op.Div, 7, 7),
			TK.SlashSlash => (Expr.Binary.Op.DivFloor, 7, 7),
			TK.Percent => (Expr.Binary.Op.Mod, 7, 7),
			TK.Caret => (Expr.Binary.Op.Pow, 10, 9),
			TK.DotDot => (Expr.Binary.Op.Concat, 5, 4),
			TK.Eq => (Expr.Binary.Op.Eq, 3, 3),
			TK.TildeEq => (Expr.Binary.Op.Ne, 3, 3),
			TK.Lt => (Expr.Binary.Op.Lt, 3, 3),
			TK.LtEq => (Expr.Binary.Op.Le, 3, 3),
			TK.Gt => (Expr.Binary.Op.Gt, 3, 3),
			TK.GtEq => (Expr.Binary.Op.Ge, 3, 3),
			TK.Kw_And => (Expr.Binary.Op.And, 2, 2),
			TK.Kw_Or => (Expr.Binary.Op.Or, 1, 1),
			_ => null,
		};

		Expr expr;

		{
			if (unary_op(this.lx.peek.kind) is Expr.Unary.Op op) {
				var op_tok = this.lx.next();

				var sub_expr = this.operator_expr(8);

				expr = new Expr.Unary(op_tok.offset, op, sub_expr);
			} else expr = this.simple_expr();
		}

		{
			while (binary_op(this.lx.peek.kind) is var (op, lhs_priority, rhs_priority) && lhs_priority > priority_limit) {
				var op_tok = this.lx.next();

				expr = new Expr.Binary(op_tok.offset, expr, op, this.operator_expr(rhs_priority));
			}
		}

		return expr;
	}

	private Expr simple_expr() {
		switch (this.lx.peek.kind) {
			case TK.Kw_Nil: return new Expr.LiteralNil(this.lx.next().offset);
			case TK.Kw_True: return new Expr.LiteralBool(this.lx.next().offset, true);
			case TK.Kw_False: return new Expr.LiteralBool(this.lx.next().offset, false);

			case TK.Number: {
					var tok = this.lx.next();
					return new Expr.LiteralNumber(tok.offset, tok.val.as_number);
				}

			case TK.String or TK.LongString or TK.InterpSimple: {
					var tok = this.lx.next();
					return new Expr.LiteralString(tok.offset, tok.val.as_string);
				}
			case TK.InterpStart: {
					var prefix = this.lx.next();
					var segments = ListPool.acquire<Expr.InterpString.Segment>();

					Token tok;
					do {
						var expr = this.expr();
						tok = this.lx.next();
						if (tok.kind is not TK.InterpMid or TK.InterpEnd) throw new SyntaxError(tok.offset, "malformed interpolated string", tok.text.ToString());
						segments.Add(new(expr, tok.val.as_string));
					} while (tok.kind is TK.InterpMid);

					return new Expr.InterpString(prefix.offset, prefix.val.as_string, ListPool.release_toarray(segments));
				}

			case TK.LBrace or TK.DollarLBrace: {
					var open = this.lx.next();
					return null!;
				}

			case TK.Attr or TK.AtLBracket: {
					var attrs = this.attrs();
					var fn = this.lx.peek;
					if (fn.kind != TK.Kw_Function) throw SyntaxError.expected(fn.offset, "function declaration", fn.text.ToString());
					return null!;
				}

			case TK.Kw_Function: {
					// this.function();
					return null!;
				}

			case TK.Kw_If when this.lx.config.luau_support: {
					var offset = this.lx.next().offset;

					var if_cond = this.expr();
					this.expect(TK.Kw_Then);
					var if_case = new Expr.If.Case(if_cond, this.expr());

					var elseif_cases = ListPool.acquire<Expr.If.Case>();
					while (this.take(TK.Kw_ElseIf) is not null) {
						var elseif_cond = this.expr();
						this.expect(TK.Kw_Then);
						elseif_cases.Add(new Expr.If.Case(elseif_cond, this.expr()));
					}

					this.expect(TK.Kw_Else);
					var else_case = this.expr();

					return new Expr.If(offset, if_case, ListPool.release_toarray(elseif_cases), else_case);
				}

			case TK.DotDotDot: return new Expr.Var(this.lx.next().offset, false);

			default: return this.primary_expr();
		}
	}

	private Expr primary_expr() {
		var expr = this.prefix_expr();

		while (true) {
			switch (this.lx.peek.kind) {
				case TK.LParen or TK.String or TK.LongString or TK.LBrace or TK.DollarLBrace or TK.Colon: {
						Token? name = this.take(TK.Colon) is not null ? this.expect(TK.Name, "<name>") : null;

						var (tok, args) = this.call_list();

						this.expect(TK.LParen);

						expr = name is Token name_tok
							? new Expr.MethodCall(tok.offset, expr, name_tok.val.as_string, args, false)
							: new Expr.Call(tok.offset, expr, args, false);

						break;
					}

				case TK.LBracket: {
						var offset = this.lx.next().offset;
						var index = this.lx.config.moonsharp_support ? this.expr_list() : [this.expr()];
						this.expect(TK.RBracket, "']'");
						expr = new Expr.Index(offset, expr, index);
						break;
					}

				case TK.Dot: {
						var offset = this.lx.next().offset;
						var name = this.expect(TK.Name, "<name>");
						expr = new Expr.Field(offset, expr, name.val.as_string);
						break;
					}

				default: return expr;
			}
		}
	}

	private Expr prefix_expr() {
		var tok = this.lx.next();
		switch (tok.kind) {
			case TK.Name: return new Expr.Name(tok.offset, tok.val.as_string);

			case TK.LParen: {
					var expr = this.expr();
					if (expr is Expr.Adjustable adj) adj.adjust_results = true;
					this.expect(TK.RParen);
					return expr;
				}

			default: throw SyntaxError.unexpected_symbol(tok.offset, tok.text.ToString());
		}
	}

	#endregion

	#region expr components

	private Expr[] expr_list() {
		var list = ListPool.acquire<Expr>();
		list.Add(this.expr());
		while (this.take(TK.Comma) != null) list.Add(this.expr());
		return ListPool.release_toarray(list);
	}

	private (Token tok, Expr[] args) call_list() {
		var tok = this.lx.next();
		switch (tok.kind) {
			case TK.LParen: {
					var args = this.expr_list();
					this.expect(TK.RParen, "')'");
					return (tok, args);
				}

			case TK.String: return (tok, [new Expr.LiteralString(tok.offset, tok.val.as_string)]);

			case TK.LBrace or TK.DollarLBrace: {
					// todo
					return (tok, []);
				}

			default: throw SyntaxError.expected(tok.offset, "function arguments", tok.text.ToString());
		}
	}

	#endregion

	#region helpers

	private Token? take(TK kind) {
		if (this.lx.peek.kind == kind) return this.lx.next();
		else return null;
	}

	private Token expect(TK kind, string what) {
		var tok = this.lx.next();
		if (tok.kind != kind) throw SyntaxError.expected(tok.offset, what, tok.text.ToString());
		return tok;
	}

	private Token expect(TK kind) {
		var tok = this.lx.next();
		if (tok.kind != kind) throw SyntaxError.unexpected_symbol(tok.offset, tok.text.ToString());
		return tok;
	}

	#endregion
}