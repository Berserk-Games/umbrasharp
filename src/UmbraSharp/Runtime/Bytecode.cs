using System.Text;

using U = UmbraSharp.Runtime.Bytecode.Inst.OperandUsage;

namespace UmbraSharp.Runtime;

// todo: <summary>-ify all the comments
// c# will understand Normal Doc Comments one day. trust

public class Bytecode {
	public readonly struct Inst {
		public readonly struct Registers {
			public readonly int start;
			public readonly int len;

			public Registers(int index) : this(index, index + 1) { }

			public Registers(int start, int len) {
				if (len < 0) throw new ArgumentOutOfRangeException(nameof(len), "registers len < 0");
				this.start = start;
				this.len = len;
			}

			public readonly bool is_const => this.start < 0;

			public override string ToString() =>
				this.len != 1
				? (this.is_const ? $"%{this.start}+{this.len}" : $"&{-this.start}+{this.len}")
				: (this.is_const ? $"%{this.start}" : $"&{-this.start}");
		}

		// todo: finalize instruction set v1
		// todo: number all instructions after instruction set is done
		public enum Opcode: byte {
			#region base

			/// immediately throw an error using format string $a
			Halt = 0x0,
			/// send a signal created using format string $a to the debugger, ignored when no debugger attached
			Debug,

			#endregion
			#region register manipulation

			/// close $rx* (disconnect any upvalue links to the register(s) and set value with nil)
			Drop,
			/// copy $rx*- to $ry*
			Copy,

			#endregion
			#region loads

			/// $rx = $a
			LoadConst,
			/// $rx = nil
			LoadNil,
			/// $rx = false
			LoadFalse,
			/// $rx = true
			LoadTrue,
			/// $ry = format string $a
			LoadFmt,
			/// $rx = closure($a)
			LoadFn,
			/// $rx = { $ry*- } (array part init from $ry*- + varret, hash part allocate with $a capacity)
			LoadTable,
			/// load varargs into $rx* (+ varret if $alt)
			LoadVararg,

			#endregion
			#region table init

			/// in $rx, init keys $ry*- with values $rz*-
			/// for (k, v) in ($ry*-).zip($rz*-) { $rx[k] = v }
			Init,

			#endregion
			#region upvalues

			/// $rx = upv[$a]
			UpvLd,
			/// upv[$a] = $rx-
			UpvSt,
			/// $ry = upv[$a][$rx-]
			UpvIndex,
			/// upv[$a][$rx-] = $ry-
			UpvIndexSet,

			#endregion
			#region control flow

			/// jump to address $a relative to instruction pointer
			Jump,
			/// jump to address in $rx relative to instruction pointer
			JumpDyn,
			/// return $rx*-, ...varret
			Ret,

			#endregion
			#region calls

			/// call function $a in the bytecode's function list, passing $rx*- + varret, returns written to $ry* (+ varret if $alt)
			/// if $alt { $ry*, varret } else { $ry* } = funcs[$a]($rx*-)
			Call,
			/// call function in $rx, passing $ry*- + varret, returns written to $rz* (+ varret if $alt)
			/// if $alt { $rz*, varret } else { $rz* } = ($rx)($ry*-)
			CallDyn,
			/// equivalent to `call`, except $ry* and $alt are omitted, since they are inherited due to the nature of tail calls
			/// return funcs[$a]($rx*-)
			TailCall,
			/// equivalent to `call.dyn`, except $rz* and $alt are omitted, since they are inherited due to the nature of tail calls
			/// return ($rx)($ry*-)
			TailCallDyn,

			#endregion
			#region operators

			#endregion
			#region iterators

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
			RXEnd = 1 << 3,
			/// instruction accepts negative indices for $rx- (indicating constants)
			RXConst = 1 << 4,

			/// instruction takes a register index for $ry
			RY = 1 << 5,
			/// instruction takes the length of registers for $ry*
			RYEnd = 1 << 6,
			/// instruction accepts negative indices for $ry- (indicating constants)
			RYConst = 1 << 7,

			/// instruction takes a register index for $rz
			RZ = 1 << 8,
			/// instruction takes the length of registers for $rz*
			RZEnd = 1 << 9,
			/// instruction accepts negative indices for $rz- (indicating constants)
			RZConst = 1 << 10,
		}

		public static string name(Opcode op) => op switch {
			Opcode.Halt => "halt",
			Opcode.Debug => "debug",

			Opcode.Drop => "drop",
			Opcode.Copy => "copy",

			Opcode.LoadConst => "ld.const",
			Opcode.LoadNil => "ld.nil",
			Opcode.LoadFalse => "ld.false",
			Opcode.LoadTrue => "ld.true",
			Opcode.LoadFmt => "ld.fmt",
			Opcode.LoadFn => "ld.fn",
			Opcode.LoadTable => "ld.table",
			Opcode.LoadVararg => "ld.vararg",

			Opcode.Init => "init",

			Opcode.UpvLd => "upv.ld",
			Opcode.UpvSt => "upv.st",
			Opcode.UpvIndex => "upv.index",
			Opcode.UpvIndexSet => "upv.index.set",

			Opcode.Jump => "jmp",
			Opcode.JumpDyn => "jmp.dyn",
			Opcode.Ret => "ret",

			Opcode.Call => "call",
			Opcode.CallDyn => "call.dyn",
			Opcode.TailCall => "call.tail",
			Opcode.TailCallDyn => "call.tail.dyn",

			_ => throw new ArgumentOutOfRangeException($"unknown opcode {op} (0x{(int)op:02x})"),
		};

		public static OperandUsage usage(Opcode op) {
#pragma warning disable IDE0047 // unnecessary parentheses around RX/RY/RZ + modifier groups
			return op switch {
				Opcode.Halt => U.None,
				Opcode.Debug => U.A,

				Opcode.Drop => (U.RX | U.RXEnd),
				Opcode.Copy => (U.RX | U.RXEnd | U.RXConst) | (U.RY | U.RYEnd),

				Opcode.LoadConst => U.A | (U.RX),
				Opcode.LoadNil => (U.RX),
				Opcode.LoadFalse => (U.RX),
				Opcode.LoadTrue => (U.RX),
				Opcode.LoadFmt => U.A | (U.RX),
				Opcode.LoadFn => U.A | (U.RX),
				Opcode.LoadTable => U.A | (U.RX) | (U.RY | U.RYEnd | U.RYConst),
				Opcode.LoadVararg => U.Alt | (U.RX | U.RXEnd),

				Opcode.Init => (U.RX) | (U.RY | U.RYEnd | U.RYConst) | (U.RZ | U.RZEnd | U.RZConst),

				Opcode.UpvLd => U.A | (U.RX),
				Opcode.UpvSt => U.A | (U.RX | U.RXConst),
				Opcode.UpvIndex => U.A | (U.RX | U.RXConst) | (U.RY),
				Opcode.UpvIndexSet => U.A | (U.RX | U.RXConst) | (U.RY | U.RYConst),

				Opcode.Jump => U.A,
				Opcode.JumpDyn => U.RX,
				Opcode.Ret => (U.RX | U.RXEnd | U.RXConst),

				Opcode.Call => U.Alt | U.A | (U.RX | U.RXEnd) | (U.RY | U.RYEnd),
				Opcode.CallDyn => U.Alt | (U.RX | U.RXEnd) | (U.RY | U.RYEnd),
				Opcode.TailCall => U.A | (U.RX | U.RXEnd),
				Opcode.TailCallDyn => (U.RX | U.RXEnd),

				_ => throw new ArgumentOutOfRangeException($"unknown opcode {op} (0x{(int)op:02x})"),
			};
#pragma warning restore IDE0047
		}

		/// opcode for this instruction
		public readonly required Opcode opcode { get; init; }

		/// boolean flag (dependent on the instruction) $alt
		public readonly bool alt { get; init; }
		/// integer value $a
		public readonly int a { get; init; }
		/// register index/span $rx
		public readonly Registers rx { get; init; }
		/// register index/span $ry
		public readonly Registers ry { get; init; }
		/// register index/span $ry
		public readonly Registers rz { get; init; }

		/// arbitrary source offset
		public readonly uint? src { get; init; }

		private static readonly StringBuilder tostring_buf = new(32);
		public override string ToString() {
			Inst.tostring_buf.Clear();
			Inst.tostring_buf.Append(Inst.name(this.opcode));
			var usage = Inst.usage(this.opcode);
			if ((usage & OperandUsage.Alt) != 0 && this.alt) Inst.tostring_buf.Append(".*");
			if ((usage & OperandUsage.A) != 0) {
				Inst.tostring_buf.Append(' ');
				Inst.tostring_buf.Append(this.a);
			}
			if ((usage & OperandUsage.RX) != 0) {
				Inst.tostring_buf.Append(' ');
				Inst.tostring_buf.Append(this.rx);
			}
			if ((usage & OperandUsage.RY) != 0) {
				Inst.tostring_buf.Append(' ');
				Inst.tostring_buf.Append(this.ry);
			}
			if ((usage & OperandUsage.RZ) != 0) {
				Inst.tostring_buf.Append(' ');
				Inst.tostring_buf.Append(this.rz);
			}
			return Inst.tostring_buf.ToString();
		}
	}

	public class LuaFnProto: FnProto {
		/// the bytecode this function is from
		public required Bytecode bytecode { get; init; }
		/// whether this function is dumpable
		public required bool dumpable { get; init; }
		/// entrypoint of the function
		public required int addr { get; init; }
		/// length of the function (used for dumps)
		public required int len { get; init; }
		/// friendly name for the function
		public required string name { get; init; }

		/// the number of args the function expectes
		public required int args { get; init; }
		/// whether the function takes varargs
		public required bool varargs { get; init; }
		/// the number of extra non-arg registers the function uses
		public required int extra_regs { get; init; }
		/// the register indices in the parent function that correspond to the upvalues
		public required int[] upvalues { get; init; }

		public override string debug_name => this.name;
	}

	public readonly record struct FormatString(string prefix, FormatString.Segment segments) {
		public readonly record struct Segment(int reg, string? fmt, string suffix);
	}

	/// name of the root chunk
	public required string chunk_name { get; init; }
	/// all the instructions
	public required Inst[] instructions { get; init; }
	/// constants referenced in this chunk
	public required Val[] constants { get; init; }
	/// format strings referenced in this chunk
	public required FormatString[] format_strings { get; init; }
	/// any functions this chunk references
	public required LuaFnProto[] prototypes { get; init; }
	// todo: add when compiler is real (ensure that we can still read source info from bytecode! not the source itself, but having real line numbers/etc would be neat)
	// public required LuaSource[]? sources { get; init; }
	public bool validated { get; internal set; } = false;

	/// validates that the bytecode does not violate certain contracts, throwing an error if it fails, or setting validated to true if it passes
	public void validate() {
		// todo: ensure operand usage is correct, no constant/formatstring/function references or jumps are known to be out of bounds
		this.validated = true;
	}
}