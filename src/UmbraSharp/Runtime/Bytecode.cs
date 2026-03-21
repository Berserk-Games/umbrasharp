namespace UmbraSharp.Runtime;

public readonly struct Inst {
	public readonly struct Registers {
		public readonly int start;
		public readonly int end;

		public Registers(int index) : this(index, index + 1) { }

		public Registers(int start, int end) {
			if (end < start) throw new ArgumentOutOfRangeException(nameof(end), "RegSpan end is before start");
			this.start = start;
			this.end = end;
		}

		public override string ToString() => this.start == this.end ? $"&regs[{this.start}]" : $"&regs[{this.start}..{this.end}]";
	}

	// todo: finalize instruction set v1
	// todo: number all instructions after instruction set is done
	public enum Opcode: byte {
		/// immediately throw an error
		Halt = 0x0,

		/// arbitrary debugger info, ignored when no debugger attached
		Debug,
		/// debugger trace log, ignored when no debugger attached
		DebugTrace,

		/// zero and close registers $rx*
		Drop,
		/// copy $rx* to $ry*
		Copy,

		/// $ry = format string $a interleaved with values starting at $rx*
		Format,

		/// jump to address $a
		Jump,
		/// jump to address in $rx
		JumpDyn,

		/// $rx = closure($a)
		Fn,
		/// close $rx*
		Close,

		/// load varargs into $rx* (+ varret if $flag)
		Vararg,

		/// call function $x in the current function's prototype list, passing $rx* + varret, returns written to $ry* (+ varret if $flag)
		Call,
		/// call function at $rx, passing $rx* (excl. $rx), returns written to $ry*
		CallDyn,
		/// equivalent to `call`, except $ry* and $flag are omitted, since they are inherited due to the nature of tail calls
		TailCall,
		/// equivalent to `call.dyn`, except $ry* and $flag are omitted, since they are inherited due to the nature of tail calls
		TailCallDyn,

		/// return $rx* + varret
		Ret,
	}

	[Flags]
	public enum OperandUsage: byte {
		/// no operands
		None = 0,

		/// instruction takes a boolean flag (encoded as the MSB of the opcode)
		Flag = 1 << 0,
		/// instruction takes an integer
		A = 1 << 1,
		/// instruction takes a register index for $rx
		RX = 1 << 2,
		/// instruction takes the length of registers for $rx*
		RXEnd = 1 << 3,
		/// instruction takes a register index for $ry
		RY = 1 << 4,
		/// instruction takes the length of registers for $ry*
		RYEnd = 1 << 5,
	}

	public static OperandUsage usage(Opcode op) => op switch {
		_ => throw new NotImplementedException($"opcode {op} (0x{(int)op:02x}) does not have usage defined yet"),
	};

	/// opcode for this instruction
	public readonly required Opcode opcode { get; init; }

	/// boolean flag (dependent on the instruction) $flag
	public readonly bool flag { get; init; }
	/// integer value $a
	public readonly int a { get; init; }
	/// register index/span $rx
	public readonly Registers rx { get; init; }
	/// register index/span $ry
	public readonly Registers ry { get; init; }

	/// arbitrary source offset
	public readonly uint? src { get; init; }
}

public class LuaFn: Fn {
	public required Bytecode bytecode { get; init; }
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
	/// prototypes this chunk references
	public required LuaFn[] prototypes { get; init; }

	public override string debug_name => this.name;
}

public class Bytecode {
	public required string chunk_name { get; init; }
	public required Inst[] instructions { get; init; }

	// todo: add when compiler is real (ensure that we can still read source info from bytecode! not the source itself, but having real line numbers/ect would be neat)
	// public required LuaSource[]? sources { get; init; }
}