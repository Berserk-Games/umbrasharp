namespace UmbraSharp.Compiler;

using System.Globalization;
using System.Text;
using UmbraSharp.Internal;

using TK = Token.Kind;

public readonly struct Token(Token.Kind kind, int offset, Str text, Val val = default) {
	public enum Kind {
		EOF,

		#region symbols

		LParen, RParen,
		LBracket, RBracket,
		LBrace, RBrace,

		Plus, PlusEq,
		Minus, MinusEq,
		Asterisk, AsteriskEq,
		Slash, SlashEq,
		SlashSlash, SlashSlashEq,
		Percent, PercentEq,
		Caret, CaretEq,

		TildeEq,

		Hash,

		Eq, EqEq,
		Lt, LtEq,
		Gt, GtEq,

		Dot,
		DotDot, DotDotEq,
		DotDotDot,
		Colon,
		ColonColon,

		Semicolon,
		Comma,

		#endregion

		#region luau

		// todo: luau symbols for type stuff
		Attr, AtLBracket,
		InterpSimple, InterpStart, InterpMid, InterpEnd,

		#endregion

		#region moonsharp

		DollarLBrace,
		BangEq,
		Pipe,

		#endregion

		#region keywords

		Kw_Function,
		Kw_Local,

		Kw_If,
		Kw_Then,
		Kw_Else,
		Kw_ElseIf,
		Kw_For,
		Kw_In,
		Kw_While,
		Kw_Repeat,
		Kw_Until,
		Kw_Do,
		Kw_End,

		Kw_Break,
		Kw_Continue,
		Kw_Return,
		Kw_Goto,

		Kw_Nil,
		Kw_True,
		Kw_False,

		Kw_Not,
		Kw_And,
		Kw_Or,

		#endregion

		Name,
		String,
		LongString,
		Number,
	}

	public static Token eof(int offset) => new(Kind.EOF, offset, "<eof>");

	public readonly Kind kind = kind;
	public readonly int offset = offset;
	public readonly Str text = text;
	public readonly Val val = val;

	public override readonly string ToString() => $"[{this.offset:D04}] => {this.kind} \"{this.text}\" ={this.val}";
}

public struct Lexer {
	private enum NumMode {
		Bin,
		Dec,
		Hex,
	}

	private enum StringQuote {
		Single,
		Double,
		Grave,
	}

	public Script.Config config;
	public Stream st;
	public Stack<bool>? brace_stack = null;
	private (Token tok, int offset)? cache = null;

	public Lexer(Script.Config config, Stream st) {
		this.config = config;
		this.st = st;
		if (st.peek == '#') st.skip_until('\n');
	}

	public Token next() {
		if (this.cache is var (cached_tok, cached_offset)) {
			this.cache = null;
			this.st.offset = cached_offset;
			return cached_tok;
		}

	redo:
		var offset = this.st.offset;
		if (this.st.next is not char ch) return Token.eof(offset);

		if (char.IsWhiteSpace(ch)) goto redo;
		switch (ch) {
			#region symbols

			case '(': return new(TK.LParen, offset, "(");
			case ')': return new(TK.RParen, offset, ")");
			case '[': {
					return this.skip_long_separator(true) switch {
						var borders and >= 0 => new(TK.LongString, offset, this.view(offset..this.st.offset), this.read_long_string(borders)),
						-1 => new(TK.LBracket, offset, "["),
						< -1 => throw new SyntaxError(offset, "invalid long string delimiter", this.view(offset..this.st.offset).ToString()),
					};
				}
			case ']': return new(TK.RBracket, offset, "]");
			case '{': {
					(this.brace_stack ??= new()).Push(false);
					return new(TK.LBrace, offset, "{");
				}
			case '}': {
					if (this.brace_stack!.Pop()) {
						var (seg, end) = this.read_string(StringQuote.Grave);
						return new(end ? TK.InterpEnd : TK.InterpMid, offset, seg);
					} else return new(TK.RBrace, offset, "}");
				}

			case '+': {
					if (this.st.take('=')) return new(TK.PlusEq, offset, "+=");
					return new(TK.Plus, offset, "+");
				}
			case '-': {
					if (this.st.take('-')) {
						this.read_comment();
						goto redo;
					}
					if (this.st.take('=')) return new(TK.MinusEq, offset, "-=");
					return new(TK.Minus, offset, "-");
				}
			case '*': {
					if (this.st.take('=')) return new(TK.AsteriskEq, offset, "*=");
					return new(TK.Asterisk, offset, "*");
				}
			case '/': {
					if (this.st.take('=')) return new(TK.SlashEq, offset, "/=");
					if (this.st.take('/')) {
						if (this.st.take('=')) return new(TK.SlashSlashEq, offset, "//=");
						return new(TK.SlashSlash, offset, "//");
					}
					return new(TK.Slash, offset, "/");
				}
			case '%': {
					if (this.st.take('=')) return new(TK.PercentEq, offset, "%=");
					return new(TK.Percent, offset, "%");
				}
			case '^': {
					if (this.st.take('=')) return new(TK.CaretEq, offset, "^=");
					return new(TK.Caret, offset, "^");
				}

			case '~': {
					if (this.st.take('=')) return new(TK.TildeEq, offset, "~=");
					goto default;
				}

			case '#': return new(TK.Hash, offset, "#");

			case '=': {
					if (this.st.take('=')) return new(TK.EqEq, offset, "==");
					return new(TK.Eq, offset, "=");
				}
			case '<': {
					if (this.st.take('=')) return new(TK.LtEq, offset, "<=");
					return new(TK.Lt, offset, "<");
				}
			case '>': {
					if (this.st.take('=')) return new(TK.GtEq, offset, ">=");
					return new(TK.Gt, offset, ">");
				}

			case '.': {
					if (this.st.take('.')) {
						if (this.st.take('=')) return new(TK.DotDotEq, offset, "..=");
						if (this.st.take('.')) return new(TK.DotDotDot, offset, "...");
						return new(TK.DotDot, offset, "..");
					}
					if (this.st.peek is (>= '0' and <= '9')) {
						this.st.offset--;
						return new(TK.Number, offset, this.view(offset..this.st.offset), this.read_number());
					}
					return new(TK.Dot, offset, ".");
				}
			case ':': {
					if ((this.config.goto_support || this.config.luau_support) && this.st.take(':')) return new(TK.ColonColon, offset, "::");
					return new(TK.Colon, offset, ":");
				}

			case ';': return new(TK.Semicolon, offset, ";");
			case ',': return new(TK.Comma, offset, ",");

			#endregion

			#region luau

			case '@' when this.config.luau_support: {
					if (this.st.take('[')) return new(TK.AtLBracket, offset, "@[");
					if (this.st.peek is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_') {
						var name = this.read_name(offset);
						return new(TK.Attr, offset, this.view(offset..this.st.offset), name);
					} else throw new SyntaxError(offset, "Attribute name missing");
				}
			case '`' when this.config.luau_support: {
					var (seg, end) = this.read_string(StringQuote.Grave);
					return new(end ? TK.InterpSimple : TK.InterpStart, offset, this.view(offset..this.st.offset), seg);
				}

			#endregion

			#region moonsharp

			case '$' when this.config.moonsharp_support: {
					// prime tables are. one of the choices of all time
					// went undocumented (and didnt roundtrip through bytecode) for years
					if (this.st.take('{')) {
						(this.brace_stack ??= new()).Push(false);
						return new(TK.DollarLBrace, offset, "${");
					}
					goto default;
				}
			case '!' when this.config.moonsharp_support: {
					// hey atleast glua documents its poor syntax extension choices!
					// https://wiki.facepunch.com/gmod/Specific_Operators
					// this one isnt documented! yay!
					if (this.st.take('=')) return new(TK.BangEq, offset, "!=");
					goto default;
				}
			case '|' when this.config.moonsharp_support: return new(TK.Pipe, offset, "|");

			#endregion

			case (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or '_': {
					var name = this.read_name(offset);
					var kind = name.span switch {
						"function" => TK.Kw_Function,
						"local" => TK.Kw_Local,

						"if" => TK.Kw_If,
						"then" => TK.Kw_Then,
						"else" => TK.Kw_Else,
						"elseif" => TK.Kw_ElseIf,
						"for" => TK.Kw_For,
						"in" => TK.Kw_In,
						"while" => TK.Kw_While,
						"repeat" => TK.Kw_Repeat,
						"until" => TK.Kw_Until,
						"do" => TK.Kw_Do,
						"end" => TK.Kw_End,

						"break" => TK.Kw_Break,
						"continue" => TK.Kw_Continue,
						"return" => TK.Kw_Return,
						"goto" when config.goto_support => TK.Kw_Goto,

						"nil" => TK.Kw_Nil,
						"true" => TK.Kw_True,
						"false" => TK.Kw_False,

						"not" => TK.Kw_Not,
						"and" => TK.Kw_And,
						"or" => TK.Kw_Or,

						_ => TK.Name,
					};
					return new(kind, offset, name, kind == TK.Name ? (Val)name : default);
				}
			case >= '0' and <= '9': {
					this.st.offset--;
					return new(TK.Number, offset, this.view(offset..this.st.offset), this.read_number());
				}
			case '\'': return new(TK.String, offset, this.view(offset..this.st.offset), this.read_string(StringQuote.Single).seg);
			case '"': return new(TK.String, offset, this.view(offset..this.st.offset), this.read_string(StringQuote.Double).seg);

			default: throw SyntaxError.unexpected_symbol(offset, ch.ToString());
		}
	}

	public Token peek {
		get {
			if (this.cache is null) {
				var saved = this.st.offset;
				try {
					this.cache = (this.next(), this.st.offset);
				} finally {
					this.st.offset = saved;
				}
			}

			return this.cache.Value.tok;
		}
	}

	public bool at_end => this.peek.kind == TK.EOF;

	private readonly Str view(Range range) => this.st.src[range];

	private void read_comment() {
		if (this.st.take('[')) {
			var borders = this.st.skip_all('=');
			if (this.st.take('[')) {
				do {
					if (this.st.at_end) throw SyntaxError.unfinished(this.st.offset, "long comment");
					this.st.skip_until(']');
					this.st.offset++;
				} while (this.skip_long_separator(false) != borders);

				return;
			}
		}
		this.st.skip_until('\n');
	}

	private Str read_name(int start) {
		while (this.st.peek is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_') this.st.offset++;
		return this.view(start..this.st.offset);
	}

	private Val.Num read_number() {
		var err_start = this.st.offset;
		var start = this.st.offset;
		var mode = NumMode.Dec;
		var floating = false;

		// find the boundary and info

		if (this.st.next == '0') {
			switch (this.st.peek) {
				case 'x': {
						mode = NumMode.Hex;
						start += 2;
						break;
					}
				case 'b' when this.config.luau_support: {
						mode = NumMode.Bin;
						start += 2;
						break;
					}
			}
		}

		while (
			this.st.peek
			is var ch
			and (
				(>= 'a' and <= 'z')
				or (>= 'A' and <= 'Z')
				or (>= '0' and <= '9')
				or '_'
				or '.'
			)
		) {
			this.st.offset++;
			if (ch == '.') floating = true;
		}

		var span = this.view(start..this.st.offset);

		// todo: https://www.lua.org/source/5.4/lobject.c.html#l_str2int / l_str2dec
		// todo: manual parsing (need thousands separators + exponent + floating)
		switch ((mode, floating)) {
			case (NumMode.Bin, false): {
					if (!long.TryParse(span, NumberStyles.AllowBinarySpecifier, CultureInfo.InvariantCulture, out var res)) goto default;
					return res;
				}
			case (NumMode.Bin, true): goto default;
			case (NumMode.Dec, false): {
					if (!long.TryParse(span, NumberStyles.AllowExponent | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var res)) goto default;
					return res;
				}
			case (NumMode.Dec, true): {
					if (!double.TryParse(span, NumberStyles.AllowExponent | NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var res)) goto default;
					return res;
				}
			case (NumMode.Hex, false): {
					if (!long.TryParse(span, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var res)) goto default;
					return res;
				}
			case (NumMode.Hex, true): throw new InternalError("todo: hexadecimal floats");

			default: throw new SyntaxError(err_start, "malformed number", this.view(err_start..this.st.offset).ToString());
		}
	}

	private static char? unescape(char escaped) => escaped switch {
		'a' => '\a',
		'b' => '\b',
		'f' => '\f',
		'n' => '\n',
		'r' => '\r',
		't' => '\t',
		'v' => '\v',
		_ => escaped,
	};
	private (Str seg, bool end) read_string(StringQuote mode) {
		static void next_hex_digit(ref Lexer lx, ref int code, int err_start) {
			if (lx.st.next is not char hex || !char.IsAsciiHexDigit(hex)) throw new SyntaxError(err_start, "hexadecimal digit expected", lx.view(err_start..lx.st.offset).ToString());
			code = (16 * code) + (char.IsAsciiDigit(hex) ? hex - '0' : (hex | ' ') - 'a' + 10);
		}
		var start = this.st.offset;
		StringBuilder? content = null;

		while (true) {
			if (this.st.at_end) throw SyntaxError.unfinished(this.st.offset, "string");
			switch (this.st.next) {
				case '{' when mode == StringQuote.Grave: {
						if (this.st.take('{')) throw new SyntaxError(this.st.offset - 1, "Expected identifier when parsing expression, got '{{', which is invalid (did you mean '\\{'?)");
						(this.brace_stack ??= new()).Push(true);
						if (content is not null) return (StringBuilderPool.release_tostring(content), false);
						else return (this.view(start..(this.st.offset - 1)), false);
						// goto case '`'; // doesnt work :(t, "s
					}

				case '\'' when mode == StringQuote.Single:
				case '"' when mode == StringQuote.Double:
				case '`' when mode == StringQuote.Grave: {
						if (content is not null) return (StringBuilderPool.release_tostring(content), true);
						else return (this.view(start..(this.st.offset - 1)), true);
					}

				case '\n':
					throw SyntaxError.unfinished(this.st.offset, "string", mode switch {
						StringQuote.Single => "'",
						StringQuote.Double => "\"",
						StringQuote.Grave => "`",
						_ => throw InternalError.invalid_enum("unknown quote type"),
					});

				case '\\': {
						if (this.st.at_end) throw SyntaxError.unfinished(this.st.offset, "string");
						if (content is null) {
							content = StringBuilderPool.acquire();
							content.Append(this.view(start..(this.st.offset - 1)).span);
						}
						switch (this.st.next) {
							case '\r': {
									if (this.st.peek == '\n') {
										this.st.offset++;
										goto case '\n';
									}
									break;
								}
							case '\n': {
									content.Append('\n');
									break;
								}
							case 'z': {
									while (this.st.peek is char esc && char.IsWhiteSpace(esc)) this.st.offset++;
									break;
								}
							case 'x': {
									var err_start = this.st.offset - 2;
									var code = 0;
									for (var i = 0; i < 2; i++) next_hex_digit(ref this, ref code, err_start);
									content.Append((char)code);
									break;
								}
							case 'u': {
									var err_start = this.st.offset - 2;
									if (this.st.next != '{') throw new SyntaxError(this.st.offset - 1, "missing '{'", this.view(err_start..this.st.offset).ToString());
									if (this.st.peek == '}') throw new SyntaxError(this.st.offset, "hexadecimal digit expected", this.view(err_start..(this.st.offset + 1)).ToString());
									var code = 0;
									for (var i = 0; i < 16; i++) {
										if (this.st.peek == '}') break;
										next_hex_digit(ref this, ref code, err_start);
									}
									content.Append(char.ConvertFromUtf32(code));
									break;
								}
							case >= '0' and <= '9': {
									var err_start = this.st.offset - 2;
									this.st.offset--;
									var code = 0;
									for (var i = 0; i < 3; i++) {
										if (this.st.at_end || !char.IsDigit(this.st.peek!.Value)) break;
										code = (10 * code) + (this.st.next.Value - '0');
									}
									if (code > byte.MaxValue) throw new SyntaxError(this.st.offset, "decimal escape too large", this.view(err_start..this.st.offset).ToString());
									content.Append((char)code);
									break;
								}
							case char esc: {
									content.Append(Lexer.unescape(esc));
									break;
								}
						}
						break;
					}

				case var ch: {
						content?.Append(ch);
						break;
					}
			}
		}
	}

	private Str read_long_string(int borders) {
		if (this.st.peek == '\r' && this.st.peekn(1) == '\n') this.st.offset += 2;
		else this.st.take('\n');

		var start = this.st.offset;
		int end;

		do {
			if (this.st.at_end) throw SyntaxError.unfinished(this.st.offset, "long string");
			this.st.skip_until(']');
			end = this.st.offset;
			this.st.offset++;
		} while (this.skip_long_separator(false) != borders);

		return this.view(start..end);
	}

	private int skip_long_separator(bool open) {
		var count = this.st.skip_all('=');

		if (this.st.peek == (open ? '[' : ']')) {
			this.st.offset++;
			return count;
		} else return -count - 1;
	}
}