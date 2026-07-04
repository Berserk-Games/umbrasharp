using UmbraSharp.Internal;

namespace UmbraSharp.Runtime.VirtualMachine;

using System.Runtime.InteropServices;
using System.Text;
using OpCode = ByteCode.Inst.OpCode;

internal sealed partial class VM {
	private readonly ref struct ValSpan {
		[StructLayout(LayoutKind.Explicit)]
		private readonly ref struct Repr {
			[FieldOffset(0)] public readonly ReadOnlySpan<Val> as_vals;
			[FieldOffset(0)] public readonly ReadOnlySpan<Reg> as_regs;

			public Repr(ReadOnlySpan<Val> v) : this() => this.as_vals = v;
			public Repr(ReadOnlySpan<Reg> r) : this() => this.as_regs = r;
		}

		private readonly bool is_const;
		private readonly Repr repr;

		public ValSpan(ReadOnlySpan<Val> v) {
			this.is_const = true;
			this.repr = new(v);
		}

		public ValSpan(ReadOnlySpan<Reg> r) {
			this.is_const = false;
			this.repr = new(r);
		}

		public readonly int len => this.is_const ? this.repr.as_vals.Length : this.repr.as_regs.Length;

		public readonly void copy_to(Span<Reg> dst) {
			for (var i = 0; i < dst.Length; i++) dst[i].value = this[i];
		}

		public readonly void copy_to_new(Span<Reg> dst) {
			for (var i = 0; i < dst.Length; i++) dst[i] = new(this[i]);
		}

		public readonly Val this[int i] => this.is_const ? this.repr.as_vals[i] : this.repr.as_regs[i].value;
	}

	// todo: real error handling (unwinding)
	public Yield? execution_loop() {
	different_function:
		ref var frame = ref this.call_stack.peek;
		Statistics.trace($"different function: {frame.fn.debug_name}");
		if (frame.fn is not ByteCode.LuaFnProto proto) return null;
		var bytecode = proto.bytecode;

		while (true) {
			var ip = frame.ip++;
			ref var inst = ref bytecode.instructions[ip];

			Statistics.trace($"{VM.dbg_span(this.ex_vararg)} // {VM.dbg_span(this.regs.data.AsSpan()[frame.bp..this.base_top])} // {VM.dbg_span(this.ex_varret)}");
			Statistics.trace_inst(frame.fn.debug_name, ip, inst);

			switch (inst.opcode) {
				case OpCode.Halt: {
						throw new Exception(this.ex_fmt(bytecode, inst.a).ToString());
					}
				case OpCode.Debug: {
						// todo: debugger
						Console.Error.WriteLine($"debug >> {this.ex_fmt(bytecode, inst.a)}");
						break;
					}

				case OpCode.Drop: {
						if (inst.alt) this.ex_regs(inst.rx).Clear();
						else
							foreach (ref var reg in this.ex_regs(inst.rx))
								if (reg.closed_over)
									reg = new(reg.value);
						break;
					}
				case OpCode.CloseMark: {
						foreach (ref var reg in this.ex_regs(inst.rx))
							if (!reg.to_be_closed) {
								reg.to_be_closed = true;
							}
						break;
					}
				case OpCode.Close: {
						var regs = this.ex_regs(inst.rx);
						if (frame.close_index >= regs.Length) {
							frame.close_index = 0;
							break;
						}
						ref var reg = ref regs[frame.close_index];
						reg.to_be_closed = false;
						// revisit this instruction until nothing left to close
						// this is a pretty simple way to do this
						frame.ip--;

						var meta = reg.value.meta(MetaMethod.__close);
						if (meta.is_nil) throw new Exception("variable 'a' got a non-closable value");
						if (!meta.is_function) RuntimeError.attempt_to_what<None>("call", meta.ty.name(), "metamethod 'close'");
						this.begin_call(meta.assume_function, this.ex_r2ss(inst.rx), false, default, false, false);
						goto different_function;
					}
				case OpCode.Copy: {
						this.ex_regs_or_consts(inst.ry).copy_to(this.ex_regs(inst.rx));
						break;
					}

				case OpCode.LdNil: {
						this.ex_reg(inst.rx).value = default;
						break;
					}
				case OpCode.LdFalse: {
						this.ex_reg(inst.rx).value = false;
						break;
					}
				case OpCode.LdTrue: {
						this.ex_reg(inst.rx).value = true;
						break;
					}
				case OpCode.LdInt: {
						this.ex_reg(inst.rx).value = inst.a;
						break;
					}
				case OpCode.LdFmt: {
						this.ex_reg(inst.rx).value = this.ex_fmt(bytecode, inst.a);
						break;
					}
				case OpCode.LdFn: {
						this.ex_reg(inst.rx).value = bytecode.funcs[inst.a];
						break;
					}

				case OpCode.TableNew: {
						var array = this.ex_regs_or_consts(inst.ry);
						var varret = this.ex_varret;
						var tbl = new Table(array.len + varret.Length, inst.a);
						for (var i = 0; i < array.len; i++) tbl.raw_set(1 + i, array[i]);
						for (var i = 0; i < varret.Length; i++) tbl.raw_set(1 + array.len + i, array[i]);
						this.ex_reg(inst.rx).value = tbl;
						break;
					}
				case OpCode.TableInit: {
						var tbl = this.ex_reg(inst.rx).value.as_table;
						if (tbl.metatable != null) throw new Exception("table.init: cannot init a table with a metatable");
						var keys = this.ex_regs_or_consts(inst.ry);
						var values = this.ex_regs_or_consts(inst.rz);
						if (keys.len != values.len) throw new Exception("table.init: keys and values length differ");
						for (var i = 0; i < keys.len; i++) tbl.raw_set(keys[i], values[i]);
						break;
					}

				case OpCode.VarLoad: {
						var dst = this.ex_regs(inst.rx);
						var varargs = this.ex_vararg;
						for (var i = 0; i < dst.Length; i++) dst[i].value = i < varargs.Length ? varargs[i].value : default;
						if (inst.alt) {
							this.regs.reserve(varargs.Length - dst.Length);
							for (var i = dst.Length; i < varargs.Length; i++) this.regs.push_reserved(varargs[i].value);
						}
						break;
					}
				case OpCode.VarIndex: {
						ref var dst = ref this.ex_reg(inst.rx);
						var idx = this.ex_val(inst.ry);
						var varargs = this.ex_vararg;
						if (idx.is_string && idx.assume_string == "n") dst.value = varargs.Length;
						else if (idx.is_number) {
							if (idx.assume_number.as_long_try is not long i || i < 1 || i > varargs.Length) dst.value = default;
							else dst.value = varargs[(int)i - 1].value;
						} else dst.value = default;
						break;
					}

				case OpCode.UpvLd: {
						this.ex_reg(inst.rx).value = ((Slot[])frame.extra!)[inst.a].value;
						break;
					}
				case OpCode.UpvSt: {
						((Slot[])frame.extra!)[inst.a].value = this.ex_val(inst.rx);
						break;
					}
				case OpCode.UpvIndex: {
						throw new Exception("todo");
						break;
					}
				case OpCode.UpvIndexSet: {
						throw new Exception("todo");
						break;
					}

				case OpCode.Jmp: {
						frame.ip += inst.a - 1;
						break;
					}
				case OpCode.JmpDyn: {
						frame.ip += (int)this.ex_val(inst.rx).as_number.as_long_trunc - 1;
						break;
					}
				case OpCode.JmpNil: {
						if (this.ex_val(inst.rx).is_nil) frame.ip += inst.a - 1;
						break;
					}
				case OpCode.JmpTrue: {
						if (this.ex_val(inst.rx).is_truthy) frame.ip += inst.a - 1;
						break;
					}
				case OpCode.JmpFalse: {
						if (!this.ex_val(inst.rx).is_truthy) frame.ip += inst.a - 1;
						break;
					}
				case OpCode.Ret: {
						this.lua_ret(this.ex_r2ss(inst.rx), frame);

						this.dbg.advance(inst.ToString());
						goto different_function;
					}

				case OpCode.Call: {
						this.begin_call(bytecode.funcs[inst.a], this.ex_r2ss(inst.rx), true, this.ex_r2ss(inst.ry), inst.alt, true);

						this.dbg.advance(inst.ToString());
						goto different_function;
					}
				case OpCode.CallDyn: {
						var fn = this.ex_reg(inst.rx).value;
						// todo: __call
						if (!fn.is_function) throw new Exception($"attempt to call a {fn.ty.name()} value ({this.ex_fmt(bytecode, inst.a)})");
						this.begin_call(fn.assume_function, this.ex_r2ss(inst.ry), true, this.ex_r2ss(inst.rz), inst.alt, true);

						this.dbg.advance(inst.ToString());
						goto different_function;
					}
				case OpCode.TailCall: {
						this.begin_tail_call(bytecode.funcs[inst.a], this.ex_r2ss(inst.rx), true);

						this.dbg.advance(inst.ToString());
						goto different_function;
					}
				case OpCode.TailCallDyn: {
						var fn = this.ex_reg(inst.rx).value;
						// todo: __call
						if (!fn.is_function) throw new Exception($"attempt to call a {fn.ty.name()} value ({this.ex_fmt(bytecode, inst.a)})");
						this.begin_tail_call(fn.assume_function, this.ex_r2ss(inst.ry), true);

						this.dbg.advance(inst.ToString());
						goto different_function;
					}

				case OpCode.Add:
				case OpCode.Sub:
				case OpCode.Mul:
				case OpCode.Div:
				case OpCode.DivFloor:
				case OpCode.Mod:
				case OpCode.Pow:
				case OpCode.BitAnd:
				case OpCode.BitOr:
				case OpCode.BitXor:
				case OpCode.BitShl:
				case OpCode.BitShr: {
						var lhs = this.ex_val(inst.ry);
						var rhs = this.ex_val(inst.rz);
						// todo: logic that figures out whether we need to call a metamethod (userdata metamethods included!!)
						// if we dont, perform the calculation and assign immediately
						// if we do, do a nested call
						this.ex_reg(inst.rx).value = inst.opcode switch {
						};
						break;
					}
				case OpCode.Unm:
				case OpCode.BitNot:
					throw new Exception("todo");
					break;

				case OpCode.Concat:
					throw new Exception("todo");
					break;
				case OpCode.Len:
					throw new Exception("todo");
					break;

				case OpCode.Eq:
					throw new Exception("todo");
					break;
				case OpCode.Lt:
					throw new Exception("todo");
					break;
				case OpCode.Le:
					throw new Exception("todo");
					break;

				case OpCode.Index:
					throw new Exception("todo");
					break;
				case OpCode.IndexSet:
					throw new Exception("todo");
					break;

				case OpCode.ForPrep: {
						var limit = this.ex_val(inst.ry);
						if (!limit.is_number) RuntimeError.expected_what<None>(Val.Ty.Number.name(), limit.ty.name(), "bad 'for' limit");
						var step = this.ex_val(inst.rz);
						if (!step.is_number) RuntimeError.expected_what<None>(Val.Ty.Number.name(), step.ty.name(), "bad 'for' step");
						var initial_value = this.ex_reg(inst.rx).value;
						if (!initial_value.is_number) RuntimeError.expected_what<None>(Val.Ty.Number.name(), initial_value.ty.name(), "bad 'for' initial value");
						break;
					}
				case OpCode.ForStep: {
						// todo: longs?
						ref var iter = ref this.ex_reg(inst.rx);
						var limit = this.ex_val(inst.ry).as_number.as_double;
						var step = this.ex_val(inst.rz).as_number.as_double;
						var val = iter.value.as_number.as_double + step;
						iter = new(new(val));
						if ((val > 0) ? val <= limit : val >= limit) frame.ip += inst.a - 1;
						break;
					}
				case OpCode.IterPrep: {
						var data = this.ex_regs(inst.rx);

						var iterable = data[0].value;

						{
							var iter = Val.NIL;
							if (bytecode.script.config.luau_support && iterable.meta(MetaMethod.__iter) is Val meta_iter && !meta_iter.is_nil) {
								iter = meta_iter;
								if (!iter.is_function) RuntimeError.attempt_to_what<None>("call", iter.ty.name(), "metamethod 'iter'");
							} else if (bytecode.script.config.moonsharp_support && iterable.meta(MetaMethod.__iterator) is Val meta_iterator && !meta_iterator.is_nil) {
								iter = meta_iterator;
								if (!iter.is_function) RuntimeError.attempt_to_what<None>("call", iter.ty.name(), "metamethod 'iterator'");
							}

							if (!iter.is_nil) {
								var span = this.ex_r2ss(inst.rx);
								this.begin_call(iter.assume_function, span, false, span, false, false);
								goto different_function;
							}
						}

						if (
							!iterable.is_table
							|| !iterable.meta(MetaMethod.__call).is_nil
							|| bytecode.script.config.default_generalized_iteration == Script.Config.GeneralizedIteration.None
						) break;

						data[0].value = Val.light_userdata(-1, Table.generalized_iteration_marker, null);
						data[1].value = iterable;

						break;

					}
				case OpCode.IterStep: {
						// span.start: iter, span.start + 1: context, span.start + 2: index, span.start + 3: value1, ..., span.end: valueN
						// |- min span ---------------------------------------------------|
						// |- __call args ------------------------------------------------| span.sub(0, 3)
						//                   |- function args ----------------------------| span.sub(1, 3)
						//                                            |- ret_dst --------------------------------------------------------| span.sub(2)
						var data = this.ex_regs(inst.rx);

						var fn = data[0].value;
						if (fn.is_lightuserdata && object.ReferenceEquals(fn.assume_lightuserdata_heap, Table.generalized_iteration_marker)) {
							var (i, pair) = data[1].value.assume_table.internal_next((int)fn.assume_lightuserdata_stack);
							Console.WriteLine($"builtin iteration, idx {fn.assume_lightuserdata_stack} -> {i}, [{pair.key}] = {pair.val}");
							data[0].value = Val.light_userdata(i, Table.generalized_iteration_marker, null);
							switch (bytecode.script.config.default_generalized_iteration) {
								case Script.Config.GeneralizedIteration.None:
									RuntimeError.attempt_to<None>("call", data[1].value.ty.name());
									break;
								case Script.Config.GeneralizedIteration.Luau:
									data[2] = new(pair.key);
									if (data.Length <= 4) data[3] = new(pair.val);
									if (data.Length <= 5) data[4..].Clear();
									break;
								case Script.Config.GeneralizedIteration.MoonSharp:
									data[2] = new(pair.val);
									if (data.Length <= 4) data[3..].Clear();
									break;
							}
							break;
						}
						var span = this.ex_r2ss(inst.rx);
						if (fn.is_function) {
							this.begin_call(fn.assume_function, span.sub(1, 3), false, span.sub(2), false, false);
							goto different_function;
						} else {
							var meta = fn.meta(MetaMethod.__call);
							if (meta.is_nil) RuntimeError.attempt_to<None>("call", fn.ty.name());
							if (!meta.is_function) RuntimeError.attempt_to_what<None>("call", meta.ty.name(), "metamethod 'call'");
							this.begin_call(meta.assume_function, span.sub(0, 3), false, span.sub(2), false, false);
							goto different_function;
						}
					}
					// default:
					// 	throw new InvalidOperationException($"unknown opcode {inst.opcode} (0x{(int)inst.opcode:02x})");
			}

			this.dbg.advance(inst.ToString());
		}
	}

	private static string dbg_span(ReadOnlySpan<Reg> span) {
		var b = StringBuilderPool.acquire();
		b.Append('[');
		var comma = false;
		foreach (var reg in span) {
			if (!comma) comma = true;
			b.Append(", ");
			b.Append(reg);
		}
		b.Append(']');
		return StringBuilderPool.release_tostring(b);
	}

	private Val ex_val(ByteCode.MultiReg idx) {
		var frame = this.call_stack.peek;
		var fn = (ByteCode.LuaFnProto)frame.fn;
		if (idx.is_const) {
			if (-idx.start > fn.bytecode.constants.Length) throw new IndexOutOfRangeException("out of bounds constant reference");
			return fn.bytecode.constants.AsSpan()[-idx.start - 1];
		} else {
			if (idx.start >= fn.args + fn.extra_regs) throw new IndexOutOfRangeException("out of bounds register reference");
			return this.regs.data[frame.bp + idx.start].value;
		}
	}

	private ref Reg ex_reg(ByteCode.MultiReg r) {
		var frame = this.call_stack.peek;
		var fn = (ByteCode.LuaFnProto)frame.fn;
		if (r.start >= fn.args + fn.extra_regs) throw new IndexOutOfRangeException("out of bounds register reference");
		return ref this.regs.data[frame.bp + r.start];
	}

	private Span<Reg> ex_regs(ByteCode.MultiReg r) => this.regs.data.AsSpan()[(Range)this.ex_r2ss(r)];
	private ReadOnlySpan<Val> ex_consts(ByteCode.MultiReg r) {
		var frame = this.call_stack.peek;
		var bytecode = ((ByteCode.LuaFnProto)frame.fn).bytecode;
		var start = -r.start - 1;
		return bytecode.constants.AsSpan()[start..(start + r.len)];
	}
	private ValSpan ex_regs_or_consts(ByteCode.MultiReg r) => r.is_const ? new(this.ex_consts(r)) : new(this.ex_regs(r));

	private StackSpan ex_r2ss(ByteCode.MultiReg r) {
		var frame = this.call_stack.peek;
		var fn = (ByteCode.LuaFnProto)frame.fn;
		if (r.start + r.len > fn.args + fn.extra_regs) throw new IndexOutOfRangeException("out of bounds register reference");
		var start = frame.bp + r.start;
		return start..(start + r.len);
	}

	private ReadOnlySpan<Reg> ex_vararg {
		get {
			var frame = this.call_stack.peek;
			return this.regs.data.AsSpan()[(frame.bp - frame.varargs)..frame.bp];
		}
	}

	private ReadOnlySpan<Reg> ex_varret => this.regs.data.AsSpan()[this.base_top..this.regs.top];

	private Str ex_fmt(ByteCode bc, int a) {
		// todo: call metas, probably make the return nullable (to indicate that we're calling a metamethod), then use the same repeat-this-instruction-until-done pattern as closing vars to append to a stringbuilder stored in a lightuserdata
		var fmt = bc.format_strings[a];
		if (fmt.segments.Length == 0) return fmt.prefix;

		var buf = StringBuilderPool.acquire();
		buf.Append(fmt.prefix.span);
		foreach (var seg in fmt.segments) {
			// todo: format specifiers
			// standard string.format specifiers, plus .., which coerces like .. does
			buf.Append(this.ex_val(seg.reg));
			buf.Append(seg.suffix.span);
		}
		var res = buf.ToString();
		buf.Clear();

		return res;
	}
}