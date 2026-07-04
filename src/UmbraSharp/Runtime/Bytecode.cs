using System.Text;
using UmbraSharp.Internal;
using U = UmbraSharp.Runtime.ByteCode.Inst.OperandUsage;

namespace UmbraSharp.Runtime;

// todo: <summary>-ify all the comments
// c# will understand Normal Doc Comments one day. trust

public class ByteCode {
	public readonly struct SingleReg(int index) {
		public readonly int index = index;

		public readonly bool is_const => this.index < 0;

		public SingleReg not_const() => !this.is_const ? this : throw new ArgumentOutOfRangeException("attempt to coerce const SingleReg to non-const");

		public override string ToString() => ((MultiReg)this).ToString();

		public static explicit operator SingleReg(MultiReg r) {
			if (r.len != 1) throw new ArgumentOutOfRangeException(nameof(r), "attempt to coerce MultiReg with length != 1 into SingleReg");
			return new(r.start);
		}
		public static implicit operator SingleReg(int r) => new(r);
	}

	public readonly struct MultiReg {
		public readonly int start;
		public readonly int len;

		public MultiReg(int index) : this(index, 1) { }

		public MultiReg(int start, int len) {
			if (len < 0) throw new ArgumentOutOfRangeException(nameof(len), "MultiReg len < 0");
			this.start = start;
			this.len = len;
		}

		public readonly bool is_const => this.start < 0;

		public MultiReg not_const() => !this.is_const ? this : throw new ArgumentOutOfRangeException("attempt to coerce const MultiReg to non-const");

		public override string ToString() =>
			this.len != 1
			? (this.is_const ? $"%{-this.start}..{-this.start + this.len}" : $"&{this.start}..{this.start + this.len}")
			: (this.is_const ? $"%{-this.start}" : $"&{this.start}");

		public static implicit operator MultiReg(SingleReg r) => new(r.index, 1);
		public static implicit operator MultiReg(Range r) => new(r.Start.Value, r.End.Value);
	}

	public struct Inst(Inst.OpCode opcode) {
		// todo: finalize instruction set v1
		// todo: number all instructions after instruction set is done
		public enum OpCode: byte {
			#region base

			/// immediately throw an error using format string $a
			Halt = 0x0,
			/// send a signal created using format string $a to the debugger, ignored when no debugger attached
			Debug,

			#endregion
			#region register manipulation

			/// disconnect any upvalue links to $rx* (and set values to nil if $alt)
			Drop,
			/// mark $rx* as to-be-closed
			CloseMark,
			/// close $rx*
			Close,
			/// copy $ry*- to $rx*
			/// $rx* = $ry*-
			Copy,

			#endregion
			#region loads

			/// $rx = nil
			LdNil,
			/// $rx = false
			LdFalse,
			/// $rx = true
			LdTrue,
			/// interpret $a as an int bitwise, then cast to long
			/// $rx = $a
			LdInt,
			/// $rx = vec($ry*-[0], $ry*-[1], $ry*-[2])
			LdVec,
			/// $ry = format_strings[$a].construct()
			LdFmt,
			/// $rx = funcs[$a]
			LdFn,
			/// $rx = closures[$a].construct()
			LdClosure,

			#endregion
			#region table

			/// load $rx with a new table, array part initialized with $ry*- + varret, hash part allocated with $a capacity
			/// $rx = { $ry*- + varret }
			TableNew,
			/// in $rx, init keys $ry*- with values $rz*-
			/// for (k, v) in ($ry*-).zip($rz*-) { $rx[k] = v }
			TableInit,

			#endregion
			#region vararg

			/// load varargs into $rx* (+ varret if $alt)
			VarLoad,
			/// load a vararg index $ry- into $rx, the index can also be "n", which will return the number of varargs
			/// this is an optimization that avoids constructing a actual table when the only usages of named vararg tables is indexing
			/// $rx = varargs[$ry-]
			VarIndex,

			#endregion
			#region upvalues

			/// $rx = upv[$a]
			UpvLd,
			/// upv[$a] = $rx-
			UpvSt,
			/// $rx = upv[$a][$ry*-]
			UpvIndex,
			/// upv[$a][$ry*-] = $rx-
			UpvIndexSet,

			#endregion
			#region control flow

			/// jump to relative address $a
			Jmp,
			/// jump to relative address in $rx
			JmpDyn,
			/// jump to relative address $a if $rx == nil
			JmpNil,
			/// jump to relative address $a if $rx
			JmpTrue,
			/// jump to relative address $a if not $rx
			JmpFalse,
			/// return $rx*, ...varret
			Ret,

			#endregion
			#region calls

			/// call function $a in the bytecode's function list, passing $rx* + varret, returns written to $ry* (+ varret if $alt)
			/// if $alt { $ry*, varret } else { $ry* } = funcs[$a]($rx*-)
			Call,
			/// call function in $rx, passing $ry* + varret, returns written to $rz* (+ varret if $alt)
			/// if the value is not a function, $a is the format string used for error context
			/// if $alt { $rz*, varret } else { $rz* } = ($rx)($ry*-)
			CallDyn,
			/// equivalent to `call`, except $ry* and $alt are omitted, since they are inherited due to the nature of tail calls
			/// return funcs[$a]($rx*-)
			TailCall,
			/// equivalent to `call.dyn`, except $rz* and $alt are omitted, since they are inherited due to the nature of tail calls
			/// if the value is not a function, $a is the format string used for error context
			/// return ($rx)($ry*-)
			TailCallDyn,

			#endregion
			#region operators

			/// $rx = $ry- + $rz-
			Add,
			/// $rx = $ry- - $rz-
			Sub,
			/// $rx = $ry- * $rz-
			Mul,
			/// $rx = $ry- / $rz-
			Div,
			/// $rx = $ry- // $rz-
			DivFloor,
			/// $rx = $ry- % $rz-
			Mod,
			/// $rx = $ry- ^ $rz-
			Pow,
			/// $rx = -$ry
			Unm,

			/// $rx = $ry- & $rz-
			BitAnd,
			/// $rx = $ry- | $rz-
			BitOr,
			/// $rx = $ry- ^ $rz-
			BitXor,
			/// $rx = $ry- << $rz-
			BitShl,
			/// $rx = $ry- >> $rz-
			BitShr,
			/// $rx = ~$ry
			BitNot,

			/// $rx = $ry*
			Concat,
			/// $rx = #$ry
			Len,

			/// $rx = not $ry
			Not,

			/// $rx = $ry- == $rz-
			Eq,
			/// $rx = $ry- < $rz-
			Lt,
			/// $rx = $ry- <= $rz-
			Le,

			/// $rx = $ry[$rz*-]
			Index,
			/// $ry[$rz*-] = $rx-
			IndexSet,

			#endregion
			#region iterators

			/// prepare numeric iteration, ensuring that $rx (initial), $ry- (max), and $rz- (step) are all numbers
			ForPrep,
			/// do a numeric iteration step, advancing the iterator in $rx by $rz- and relative jumping to $a if the loop should continue (<= $ry-)
			ForStep,
			/// prepare generic iteration, ensuring that $rx*[0] (iterator) is a function, in order:
			/// - if $rx*[0] has the __iter metamethod (if Luau support) or __iterator metamethod (if MoonSharp support), call that with $rx*[0..3] and assign its results to $rx*[0..3]
			/// - if $rx*[0] is not a table, has the __call metamethod, or generalized iteration is disabled, do nothing
			/// - else, copy $rx*[0] to $rx*[1], set $rx*[0] to the generalized iteration marker
			IterPrep,
			/// do a generic iteration step
			/// - if $rx*[0] is the generalized iteration marker, do the default_generalized_iteration behavior, writing results to $rx*[2..]
			/// - if $rx*[0] is a function or has the __call metamethod, call it with $rx*[0..3], writing results to $rx*[2..]
			/// - else error
			IterStep,

			#endregion
		}

		[Flags]
		public enum OperandUsage: ushort {
			/// no operands
			None = 0,

			/// instruction takes a boolean flag $alt (encoded as the MSB of the opcode)
			Alt = 1 << 0,

			/// instruction takes an integer $a
			A = 1 << 1,

			/// instruction takes a register index for $rx
			RX = 1 << 2,
			/// instruction takes the length of registers for $rx*
			RXMulti = 1 << 3,
			/// instruction accepts negative indices for $rx- (indicating constants)
			RXConst = 1 << 4,

			/// instruction takes a register index for $ry
			RY = 1 << 5,
			/// instruction takes the length of registers for $ry*
			RYMulti = 1 << 6,
			/// instruction accepts negative indices for $ry- (indicating constants)
			RYConst = 1 << 7,

			/// instruction takes a register index for $rz
			RZ = 1 << 8,
			/// instruction takes the length of registers for $rz*
			RZMulti = 1 << 9,
			/// instruction accepts negative indices for $rz- (indicating constants)
			RZConst = 1 << 10,
		}

		public static class Builder {
			public static Inst halt(int a_fmtstr) => new(OpCode.Halt) { a = a_fmtstr };
			public static Inst debug(int a_fmtstr) => new(OpCode.Debug) { a = a_fmtstr };

			public static Inst drop(bool alt_nil, MultiReg rx_regs) => new(OpCode.Drop) { alt = alt_nil, rx = rx_regs.not_const() };
			public static Inst close_mark(MultiReg rx_regs) => new(OpCode.CloseMark) { rx = rx_regs.not_const() };
			public static Inst close(MultiReg rx_regs) => new(OpCode.Close) { rx = rx_regs.not_const() };
			public static Inst copy(MultiReg rx_dst, MultiReg ry_src) => new(OpCode.Copy) { rx = rx_dst.not_const(), ry = ry_src };

			public static Inst ld_nil(SingleReg rx_dst) => new(OpCode.LdNil) { rx = rx_dst.not_const() };
			public static Inst ld_false(SingleReg rx_dst) => new(OpCode.LdFalse) { rx = rx_dst.not_const() };
			public static Inst ld_true(SingleReg rx_dst) => new(OpCode.LdTrue) { rx = rx_dst.not_const() };
			public static Inst ld_int(int a_int, SingleReg rx_dst) => new(OpCode.LdInt) { a = a_int, rx = rx_dst.not_const() };
			public static Inst ld_vec(SingleReg rx_dst, MultiReg ry_components) => new(OpCode.LdVec) { rx = rx_dst.not_const(), ry = ry_components };
			public static Inst ld_fmt(int a_fmtstr, SingleReg rx_dst) => new(OpCode.LdFmt) { a = a_fmtstr, rx = rx_dst.not_const() };
			public static Inst ld_fn(int a_fn, SingleReg rx_dst) => new(OpCode.LdFn) { a = a_fn, rx = rx_dst.not_const() };
			public static Inst ld_closure(int a_closure, SingleReg rx_dst) => new(OpCode.LdFn) { a = a_closure, rx = rx_dst.not_const() };

			public static Inst table_new(int a_hash_capacity, SingleReg rx_dst, MultiReg ry_array_part) => new(OpCode.TableNew) { a = a_hash_capacity, rx = rx_dst.not_const(), ry = ry_array_part };
			public static Inst table_init(SingleReg rx_dst, MultiReg ry_keys, MultiReg rz_values) => new(OpCode.TableInit) { rx = rx_dst.not_const(), ry = ry_keys, rz = rz_values };

			public static Inst var_load(bool alt_varret, MultiReg rx_dst) => new(OpCode.VarLoad) { alt = alt_varret, rx = rx_dst.not_const() };
			public static Inst var_index(SingleReg rx_dst, SingleReg ry_index) => new(OpCode.VarIndex) { rx = rx_dst.not_const(), ry = ry_index };

			public static Inst upv_ld(int a_upv, SingleReg rx_dst) => new(OpCode.UpvLd) { a = a_upv, rx = rx_dst.not_const() };
			public static Inst upv_st(int a_upv, SingleReg rx_src) => new(OpCode.UpvSt) { a = a_upv, rx = rx_src };
			public static Inst upv_index(int a_upv, SingleReg rx_dst, MultiReg ry_index) => new(OpCode.UpvIndex) { a = a_upv, rx = rx_dst.not_const(), ry = ry_index };
			public static Inst upv_index_set(int a_upv, SingleReg rx_src, MultiReg ry_index) => new(OpCode.UpvIndexSet) { a = a_upv, rx = rx_src, ry = ry_index };

			public static Inst jmp(int a_dst_rel) => new(OpCode.Jmp) { a = a_dst_rel };
			public static Inst jmp_dyn(SingleReg rx_dst_rel) => new(OpCode.JmpDyn) { rx = rx_dst_rel };
			public static Inst jmp_nil(int a_dst_rel, SingleReg rx_test) => new(OpCode.JmpNil) { a = a_dst_rel, rx = rx_test.not_const() };
			public static Inst jmp_true(int a_dst_rel, SingleReg rx_test) => new(OpCode.JmpTrue) { a = a_dst_rel, rx = rx_test.not_const() };
			public static Inst jmp_false(int a_dst_rel, SingleReg rx_test) => new(OpCode.JmpFalse) { a = a_dst_rel, rx = rx_test.not_const() };
			public static Inst ret(MultiReg rx_rets) => new(OpCode.Ret) { rx = rx_rets.not_const() };

			public static Inst call(bool alt_accept_varret, int a_fn, MultiReg rx_args, MultiReg ry_rets) => new(OpCode.Call) { alt = alt_accept_varret, a = a_fn, rx = rx_args.not_const(), ry = ry_rets.not_const() };
			public static Inst call_dyn(bool alt_accept_varret, int a_err_ctx, SingleReg rx_fn, MultiReg ry_args, MultiReg rz_rets) => new(OpCode.CallDyn) { alt = alt_accept_varret, a = a_err_ctx, rx = rx_fn.not_const(), ry = ry_args.not_const(), rz = rz_rets.not_const() };
			public static Inst tail_call(int a_fn, MultiReg rx_args) => new(OpCode.TailCall) { a = a_fn, rx = rx_args.not_const() };
			public static Inst tail_call_dyn(int a_err_ctx, SingleReg rx_fn, MultiReg ry_args) => new(OpCode.TailCallDyn) { a = a_err_ctx, rx = rx_fn.not_const(), ry = ry_args.not_const() };

			public static Inst add(SingleReg rx_dst, SingleReg ry_lhs, SingleReg rz_rhs) => new(OpCode.Add) { rx = rx_dst.not_const(), ry = ry_lhs, rz = rz_rhs };
			public static Inst sub(SingleReg rx_dst, SingleReg ry_lhs, SingleReg rz_rhs) => new(OpCode.Sub) { rx = rx_dst.not_const(), ry = ry_lhs, rz = rz_rhs };
			public static Inst mul(SingleReg rx_dst, SingleReg ry_lhs, SingleReg rz_rhs) => new(OpCode.Mul) { rx = rx_dst.not_const(), ry = ry_lhs, rz = rz_rhs };
			public static Inst div(SingleReg rx_dst, SingleReg ry_lhs, SingleReg rz_rhs) => new(OpCode.Div) { rx = rx_dst.not_const(), ry = ry_lhs, rz = rz_rhs };
			public static Inst div_floor(SingleReg rx_dst, SingleReg ry_lhs, SingleReg rz_rhs) => new(OpCode.DivFloor) { rx = rx_dst.not_const(), ry = ry_lhs, rz = rz_rhs };
			public static Inst mod(SingleReg rx_dst, SingleReg ry_lhs, SingleReg rz_rhs) => new(OpCode.Mod) { rx = rx_dst.not_const(), ry = ry_lhs, rz = rz_rhs };
			public static Inst pow(SingleReg rx_dst, SingleReg ry_lhs, SingleReg rz_rhs) => new(OpCode.Pow) { rx = rx_dst.not_const(), ry = ry_lhs, rz = rz_rhs };
			public static Inst unm(SingleReg rx_dst, SingleReg ry_operand) => new(OpCode.Unm) { rx = rx_dst.not_const(), ry = ry_operand.not_const() };

			public static Inst bit_and(SingleReg rx_dst, SingleReg ry_lhs, SingleReg rz_rhs) => new(OpCode.BitAnd) { rx = rx_dst.not_const(), ry = ry_lhs, rz = rz_rhs };
			public static Inst bit_or(SingleReg rx_dst, SingleReg ry_lhs, SingleReg rz_rhs) => new(OpCode.BitOr) { rx = rx_dst.not_const(), ry = ry_lhs, rz = rz_rhs };
			public static Inst bit_xor(SingleReg rx_dst, SingleReg ry_lhs, SingleReg rz_rhs) => new(OpCode.BitXor) { rx = rx_dst.not_const(), ry = ry_lhs, rz = rz_rhs };
			public static Inst bit_shl(SingleReg rx_dst, SingleReg ry_lhs, SingleReg rz_rhs) => new(OpCode.BitShl) { rx = rx_dst.not_const(), ry = ry_lhs, rz = rz_rhs };
			public static Inst bit_shr(SingleReg rx_dst, SingleReg ry_lhs, SingleReg rz_rhs) => new(OpCode.BitShr) { rx = rx_dst.not_const(), ry = ry_lhs, rz = rz_rhs };
			public static Inst bit_not(SingleReg rx_dst, SingleReg ry_operand) => new(OpCode.BitNot) { rx = rx_dst.not_const(), ry = ry_operand.not_const() };

			public static Inst concat(SingleReg rx_dst, MultiReg ry_operands) => new(OpCode.Concat) { rx = rx_dst.not_const(), ry = ry_operands.not_const() };
			public static Inst len(SingleReg rx_dst, SingleReg ry_operand) => new(OpCode.Len) { rx = rx_dst.not_const(), ry = ry_operand.not_const() };

			public static Inst not(SingleReg rx_dst, SingleReg ry_operand) => new(OpCode.Not) { rx = rx_dst.not_const(), ry = ry_operand.not_const() };

			public static Inst eq(SingleReg rx_dst, SingleReg ry_lhs, SingleReg rz_rhs) => new(OpCode.Eq) { rx = rx_dst.not_const(), ry = ry_lhs, rz = rz_rhs };
			public static Inst lt(SingleReg rx_dst, SingleReg ry_lhs, SingleReg rz_rhs) => new(OpCode.Lt) { rx = rx_dst.not_const(), ry = ry_lhs, rz = rz_rhs };
			public static Inst le(SingleReg rx_dst, SingleReg ry_lhs, SingleReg rz_rhs) => new(OpCode.Le) { rx = rx_dst.not_const(), ry = ry_lhs, rz = rz_rhs };

			public static Inst index(SingleReg rx_dst, SingleReg ry_indexee, MultiReg rz_index) => new(OpCode.Index) { rx = rx_dst.not_const(), ry = ry_indexee.not_const(), rz = rz_index };
			public static Inst index_set(SingleReg rx_src, SingleReg ry_indexee, MultiReg rz_index) => new(OpCode.IndexSet) { rx = rx_src, ry = ry_indexee.not_const(), rz = rz_index };

			public static Inst for_prep(SingleReg rx_initial, SingleReg ry_limit, SingleReg rz_step) => new(OpCode.ForPrep) { rx = rx_initial.not_const(), ry = ry_limit, rz = rz_step };
			public static Inst for_step(int a_dst_rel, SingleReg rx_value, SingleReg ry_limit, SingleReg rz_step) => new(OpCode.ForStep) { a = a_dst_rel, rx = rx_value.not_const(), ry = ry_limit, rz = rz_step };
			public static Inst iter_prep(MultiReg rx_vars) => new(OpCode.IterPrep) { rx = rx_vars.not_const() };
			public static Inst iter_step(MultiReg rx_vars) => new(OpCode.IterStep) { rx = rx_vars.not_const() };
		}

		public static string name(OpCode op) => op switch {
			OpCode.Halt => "halt",
			OpCode.Debug => "debug",

			OpCode.Drop => "drop",
			OpCode.CloseMark => "close.mark",
			OpCode.Close => "close",
			OpCode.Copy => "copy",

			OpCode.LdNil => "ld.nil",
			OpCode.LdFalse => "ld.false",
			OpCode.LdTrue => "ld.true",
			OpCode.LdInt => "ld.int",
			OpCode.LdVec => "ld.vec",
			OpCode.LdFmt => "ld.fmt",
			OpCode.LdFn => "ld.fn",
			OpCode.LdClosure => "ld.closure",

			OpCode.TableNew => "table.new",
			OpCode.TableInit => "table.init",

			OpCode.VarLoad => "var.load",
			OpCode.VarIndex => "var.index",

			OpCode.UpvLd => "upv.ld",
			OpCode.UpvSt => "upv.st",
			OpCode.UpvIndex => "upv.index",
			OpCode.UpvIndexSet => "upv.index.set",

			OpCode.Jmp => "jmp",
			OpCode.JmpDyn => "jmp.dyn",
			OpCode.JmpNil => "jmp.nil",
			OpCode.JmpTrue => "jmp.true",
			OpCode.JmpFalse => "jmp.false",
			OpCode.Ret => "ret",

			OpCode.Call => "call",
			OpCode.CallDyn => "call.dyn",
			OpCode.TailCall => "call.tail",
			OpCode.TailCallDyn => "call.tail.dyn",

			OpCode.Add => "op.add",
			OpCode.Sub => "op.sub",
			OpCode.Mul => "op.mul",
			OpCode.Div => "op.div",
			OpCode.DivFloor => "op.div.floor",
			OpCode.Mod => "op.mod",
			OpCode.Pow => "op.pow",
			OpCode.Unm => "op.unm",

			OpCode.BitAnd => "bit.and",
			OpCode.BitOr => "bit.or",
			OpCode.BitXor => "bit.xor",
			OpCode.BitShl => "bit.shl",
			OpCode.BitShr => "bit.shr",
			OpCode.BitNot => "bit.not",

			OpCode.Concat => "op.concat",
			OpCode.Len => "op.len",

			OpCode.Not => "op.not",

			OpCode.Eq => "cmp.eq",
			OpCode.Lt => "cmp.lt",
			OpCode.Le => "cmp.le",

			OpCode.Index => "index",
			OpCode.IndexSet => "index.set",

			OpCode.ForPrep => "for.prep",
			OpCode.ForStep => "for.step",
			OpCode.IterPrep => "iter.prep",
			OpCode.IterStep => "iter.step",

			_ => throw InternalError.invalid_enum($"unknown opcode {op} (0x{(int)op:02x})"),
		};

		public static U usage(OpCode op) {
#pragma warning disable IDE0047 // unnecessary parentheses around RX/RY/RZ + modifier groups
			return op switch {
				OpCode.Halt => U.A,
				OpCode.Debug => U.A,

				OpCode.Drop => U.Alt | (U.RX | U.RXMulti),
				OpCode.CloseMark => (U.RX | U.RXMulti),
				OpCode.Close => (U.RX | U.RXMulti),
				OpCode.Copy => (U.RX | U.RXMulti) | (U.RY | U.RYMulti | U.RYConst),

				OpCode.LdNil => (U.RX),
				OpCode.LdFalse => (U.RX),
				OpCode.LdTrue => (U.RX),
				OpCode.LdInt => U.A | (U.RX),
				OpCode.LdVec => (U.RX) | (U.RY | U.RYMulti | U.RYConst),
				OpCode.LdFmt => U.A | (U.RX),
				OpCode.LdFn => U.A | (U.RX),
				OpCode.LdClosure => U.A | (U.RX),

				OpCode.TableNew => U.A | (U.RX) | (U.RY | U.RYMulti | U.RYConst),
				OpCode.TableInit => (U.RX) | (U.RY | U.RYMulti | U.RYConst) | (U.RZ | U.RZMulti | U.RZConst),

				OpCode.VarLoad => U.Alt | (U.RX | U.RXMulti),
				OpCode.VarIndex => (U.RX) | (U.RY | U.RYConst),

				OpCode.UpvLd => U.A | (U.RX),
				OpCode.UpvSt => U.A | (U.RX | U.RXConst),
				OpCode.UpvIndex => U.A | (U.RX) | (U.RY | U.RYMulti | U.RYConst),
				OpCode.UpvIndexSet => U.A | (U.RX | U.RXConst) | (U.RY | U.RYMulti | U.RYConst),

				OpCode.Jmp => U.A,
				OpCode.JmpDyn => (U.RX),
				OpCode.JmpNil => U.A | (U.RX),
				OpCode.JmpTrue => U.A | (U.RX),
				OpCode.JmpFalse => U.A | (U.RX),
				OpCode.Ret => (U.RX | U.RXMulti),

				OpCode.Call => U.Alt | U.A | (U.RX | U.RXMulti) | (U.RY | U.RYMulti),
				OpCode.CallDyn => U.Alt | (U.RX) | (U.RY | U.RYMulti) | (U.RZ | U.RZMulti),
				OpCode.TailCall => U.A | (U.RX | U.RXMulti),
				OpCode.TailCallDyn => (U.RX) | (U.RY | U.RYMulti),

				OpCode.Add => (U.RX) | (U.RY | U.RYConst) | (U.RZ | U.RZConst),
				OpCode.Sub => (U.RX) | (U.RY | U.RYConst) | (U.RZ | U.RZConst),
				OpCode.Mul => (U.RX) | (U.RY | U.RYConst) | (U.RZ | U.RZConst),
				OpCode.Div => (U.RX) | (U.RY | U.RYConst) | (U.RZ | U.RZConst),
				OpCode.DivFloor => (U.RX) | (U.RY | U.RYConst) | (U.RZ | U.RZConst),
				OpCode.Mod => (U.RX) | (U.RY | U.RYConst) | (U.RZ | U.RZConst),
				OpCode.Pow => (U.RX) | (U.RY | U.RYConst) | (U.RZ | U.RZConst),
				OpCode.Unm => (U.RX) | (U.RY),

				OpCode.BitAnd => (U.RX) | (U.RY | U.RYConst) | (U.RZ | U.RZConst),
				OpCode.BitOr => (U.RX) | (U.RY | U.RYConst) | (U.RZ | U.RZConst),
				OpCode.BitXor => (U.RX) | (U.RY | U.RYConst) | (U.RZ | U.RZConst),
				OpCode.BitShl => (U.RX) | (U.RY | U.RYConst) | (U.RZ | U.RZConst),
				OpCode.BitShr => (U.RX) | (U.RY | U.RYConst) | (U.RZ | U.RZConst),
				OpCode.BitNot => (U.RX) | (U.RY),

				OpCode.Concat => (U.RX) | (U.RY | U.RYMulti),
				OpCode.Len => (U.RX) | (U.RY),

				OpCode.Not => (U.RX) | (U.RY),

				OpCode.Eq => (U.RX) | (U.RY | U.RYConst) | (U.RZ | U.RZConst),
				OpCode.Lt => (U.RX) | (U.RY | U.RYConst) | (U.RZ | U.RZConst),
				OpCode.Le => (U.RX) | (U.RY | U.RYConst) | (U.RZ | U.RZConst),

				OpCode.Index => (U.RX) | (U.RY) | (U.RZ | U.RZMulti | U.RZConst),
				OpCode.IndexSet => (U.RX | U.RXConst) | (U.RY) | (U.RZ | U.RZMulti | U.RZConst),

				OpCode.ForPrep => (U.RX) | (U.RY | U.RYConst) | (U.RZ | U.RZConst),
				OpCode.ForStep => U.A | (U.RX) | (U.RY | U.RYConst) | (U.RZ | U.RZConst),
				OpCode.IterPrep => (U.RX | U.RXMulti),
				OpCode.IterStep => (U.RX | U.RXMulti),

				_ => throw InternalError.invalid_enum($"unknown opcode {op} (0x{(int)op:02x})"),
			};
#pragma warning restore IDE0047
		}

		/// opcode for this instruction
		public readonly OpCode opcode = opcode;

		/// boolean flag (dependent on the instruction) $alt
		public readonly bool alt { get; init; }
		/// integer value $a
		public readonly int a { get; init; }
		/// register index/span $rx
		public readonly MultiReg rx { get; init; }
		/// register index/span $ry
		public readonly MultiReg ry { get; init; }
		/// register index/span $ry
		public readonly MultiReg rz { get; init; }

		/// cache for indexing
		public int? cache;

		/// arbitrary source offset
		public readonly uint? src { get; init; }

		public override readonly string ToString() {
			var b = StringBuilderPool.acquire();
			b.Append(Inst.name(this.opcode));
			var usage = Inst.usage(this.opcode);
			if ((usage & U.Alt) != 0 && this.alt) b.Append(".*");
			if ((usage & U.A) != 0) {
				b.Append(' ');
				b.Append(this.a);
			}
			if ((usage & U.RX) != 0) {
				b.Append(' ');
				b.Append(this.rx);
			}
			if ((usage & U.RY) != 0) {
				b.Append(' ');
				b.Append(this.ry);
			}
			if ((usage & U.RZ) != 0) {
				b.Append(' ');
				b.Append(this.rz);
			}
			return StringBuilderPool.release_tostring(b);
		}
	}

	public class LuaFnProto: FnProto {
		/// the bytecode this function is from
		public required ByteCode bytecode { get; init; }
		/// whether this function is dumpable
		public required bool dumpable { get; init; }
		/// entrypoint of the function
		public required int addr { get; init; }
		/// length of the function (used for dumps)
		public required int len { get; init; }
		/// friendly name for the function
		public required Str name { get; init; }

		/// the number of args the function expectes
		public required int args { get; init; }
		/// whether the function takes varargs
		public required bool varargs { get; init; }
		/// the number of extra non-arg registers the function uses
		public required int extra_regs { get; init; }
		/// the register indices in the parent function that correspond to the upvalues
		public required int[] upvalues { get; init; }

		public override Str debug_name => this.name;
		public override bool hide_from_trace => false;
	}

	public readonly record struct FormatString(Str prefix, FormatString.Segment[] segments) {
		public readonly record struct Segment(Str? fmt, SingleReg reg, Str suffix);
	}

	/// the script this bytecode belongs to
	public required Script script { get; init; }
	/// name of the root chunk
	public required Str chunk_name { get; init; }
	/// all the instructions
	public required Inst[] instructions { get; init; }
	/// constants referenced in this chunk
	public required Val[] constants { get; init; }
	/// format strings referenced in this chunk
	public required FormatString[] format_strings { get; init; }
	/// any functions this chunk references
	public required Fn[] funcs { get; init; }
	/// any functions this chunk references
	public required LuaFnProto[] closures { get; init; }
	// todo: add when compiler is real (ensure that we can still read source info from bytecode! not the source itself, but having real line numbers/etc would be neat)
	// public required LuaSource[]? sources { get; init; }

	/// validates that the bytecode does not violate certain contracts, returning any errors
	public string[] validate() {
		var errors = ListPool.acquire<string>();
		void fail(int i, Inst inst, string msg) => errors.Add($"[[{i:X4}] {inst}] {msg}");
		void regcheck(int i, Inst inst, char name, U usage, MultiReg rn, U rn_base, U rn_multi, U rn_const, int const_count) {
			if ((usage & rn_base) != 0) {
				if ((usage & rn_multi) == 0 && rn.len != 1) fail(i, inst, "usage: $r{name} has been passed a number of registers != 1");
				if (rn.is_const) {
					if ((usage & rn_const) == 0) fail(i, inst, "usage: $r{name} references constants");
					else if (-rn.start > const_count) fail(i, inst, "usage: $r{name} references constants out of the constants range");
				}
			} else if (rn.start != default || rn.len != default) fail(i, inst, "usage: $rx is set");
		}
		for (var i = 0; i < this.instructions.Length; i++) {
			var inst = this.instructions[i];
			var usage = Inst.usage(inst.opcode);
			if ((usage & U.Alt) == 0 && inst.alt != default) fail(i, inst, "usage: $flag is set");
			if ((usage & U.A) == 0 && inst.a != default) fail(i, inst, "usage: $a is set");
			regcheck(i, inst, 'x', usage, inst.rx, U.RX, U.RXMulti, U.RXConst, this.constants.Length);
			regcheck(i, inst, 'y', usage, inst.ry, U.RY, U.RYMulti, U.RYConst, this.constants.Length);
			regcheck(i, inst, 'z', usage, inst.rz, U.RZ, U.RZMulti, U.RZConst, this.constants.Length);

			switch (inst.opcode) {
				case Inst.OpCode.Halt:
				case Inst.OpCode.Debug:
				case Inst.OpCode.LdFmt:
					if (inst.a < 0 && inst.a >= this.format_strings.Length) fail(i, inst, "operand: $a refers to an out of bounds format string");
					break;

				case Inst.OpCode.LdVec:
					if (inst.ry.len > 3) fail(i, inst, "constraint: ld.vec $ry must have 0-3 regs");
					break;

				case Inst.OpCode.LdFn:
				case Inst.OpCode.Call:
				case Inst.OpCode.TailCall:
					if (inst.a < 0 && inst.a >= this.funcs.Length) fail(i, inst, "operand: $a refers to an out of bounds function");
					break;

				case Inst.OpCode.LdClosure:
					if (inst.a < 0 && inst.a >= this.closures.Length) fail(i, inst, "operand: $a refers to an out of bounds closure");
					break;

				case Inst.OpCode.Jmp:
				case Inst.OpCode.JmpNil:
				case Inst.OpCode.JmpTrue:
				case Inst.OpCode.JmpFalse:
				case Inst.OpCode.ForStep:
					var dst = i + inst.a;
					if (dst < 0 && dst >= this.instructions.Length) fail(i, inst, "operand: $a jumps out of bounds");
					break;

				case Inst.OpCode.IterPrep:
					if (inst.rx.len < 3) fail(i, inst, "constraint: iter.prep $rx must have exactly 3 regs");
					break;

				case Inst.OpCode.IterStep:
					if (inst.rx.len < 3) fail(i, inst, "constraint: iter.step $rx must have at least 3 regs");
					break;
			}
		}
		if (errors.Count > 0) return ListPool.release_toarray(errors);
		else {
			ListPool.release(errors);
			return [];
		}
	}
}