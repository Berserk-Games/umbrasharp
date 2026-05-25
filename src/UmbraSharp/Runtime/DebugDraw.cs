using UmbraSharp.Runtime.VirtualMachine;
using VectSharp;

namespace UmbraSharp.Runtime;

internal class Dbg {
	private static readonly Brush BACKGROUND = new SolidColourBrush(Colour.FromRgb(16, 16, 16));
	private static readonly Brush REGISTER = new SolidColourBrush(Colour.FromRgb(225, 225, 225));
	private static readonly Brush LUA_FRAME = new SolidColourBrush(Colour.FromRgb(127, 127, 127));
	private static readonly Brush NATIVE_FRAME = new SolidColourBrush(Colour.FromRgb(127, 64, 255));
	private static readonly Brush POP = new SolidColourBrush(Colour.FromRgba(255, 64, 64, 192));
	private static readonly Brush MODIFY = new SolidColourBrush(Colour.FromRgba(64, 255, 64, 192));
	private static readonly Brush COPY = new SolidColourBrush(Colour.FromRgba(255, 0, 192, 192));
	private static readonly Brush EVENT = new SolidColourBrush(Colour.FromRgb(0, 192, 255));
	private static readonly Font FONT = new(FontFamily.ResolveFontFamily(FontFamily.StandardFontFamilies.Courier), fontSize: 30);
	private static readonly Font SMALL_FONT = new(FontFamily.ResolveFontFamily(FontFamily.StandardFontFamilies.Courier), fontSize: 20);

	private readonly VM vm;
	public readonly Page render;
	public int row = 0;

	public Dbg(VM vm, Page render) {
		this.vm = vm;
		this.render = render;

		this.render.Graphics.FillRectangle(new(), new(10000, 10000), Dbg.BACKGROUND);
	}

	private Point center(int col, int row_offset = 0) => new(
		192 + (col * 128),
		192 + ((this.row + row_offset) * (256 + 64))
	);

	public void advance() => this.row++;

	public void advance(string evt) {
		this.advance();
		this.notify_event(evt);
		this.debug_stack();
	}

	// notify

	public void notify_pop(int dst) {
		var src_pt = this.center(dst) + new Point(0, 96);
		var dst_pt = src_pt + new Point(0, 16);
		this.line(src_pt, dst_pt, Dbg.POP, 8);
	}

	public void notify_set(int dst) {
		var dst_pt = this.center(dst, 1) + new Point(0, -96);
		var src_pt = dst_pt + new Point(0, -16);
		this.line(src_pt, dst_pt, Dbg.MODIFY, 8);
	}

	public void notify_copy(int src, int dst) {
		var src_pt = this.center(src) + new Point(0, 96);
		var dst_pt = this.center(dst, 1) + new Point(0, -96);
		this.line(src_pt, dst_pt, Dbg.COPY, 8);
	}

	public void notify_copy(StackSpan src, StackSpan dst) {
		if (src.len != dst.len) throw new ArgumentOutOfRangeException(nameof(dst), "src and dst length mismatch");

		for (var i = 1; i < src.len; i++) this.notify_copy(src.start + i, dst.start + i);
	}

	public void notify_event(string evt) {
		this.render.Graphics.FillText(this.center(0) - new Point(160, 128 + 12), evt, Dbg.FONT, Dbg.EVENT);
	}

	public void debug_stack() {
		const int REG_SIZE = 96;
		const int REG_OFFSET = REG_SIZE / 2;

		for (var i = 0; i < this.vm.regs.top; i++) {
			var center = this.center(i);
			this.render.Graphics.StrokeRectangle(
				center - new Point(REG_OFFSET, REG_OFFSET),
				new(REG_SIZE, REG_SIZE), Dbg.REGISTER,
				4,
				LineCaps.Round,
				LineJoins.Round
			);
			this.render.Graphics.FillText(
				center - new Point(36, 7),
				this.vm.regs.data[i].ToString(),
				Dbg.SMALL_FONT,
				Dbg.REGISTER
			);
		}

		const int CS_SIZE = 128;
		const int CS_OFFSET = CS_SIZE / 2;
		const int REDUCE_VAR_SIZE = 16;

		Point offset = new(CS_OFFSET, CS_OFFSET);

		var depth = 0;
		foreach (var frame in this.vm.call_stack.enumerate_downwards()) {
			var col = frame.fn is ByteCode.LuaFnProto ? Dbg.LUA_FRAME : Dbg.NATIVE_FRAME;

			var bp = this.center(frame.bp);

			this.line(
				bp - offset,
				bp - offset + new Point(-Math.Max((frame.varargs * CS_SIZE) - REDUCE_VAR_SIZE, 0), 0),
				col,
				8
			);
			var used_regs = 0;
			switch (frame.fn) {
				case NativeFnProto fn:
					this.line(
						bp - offset,
						bp - offset + new Point(0, CS_SIZE),
						col,
						8
					);
					break;
				case ByteCode.LuaFnProto fn:
					used_regs = fn.args + fn.extra_regs;
					this.render.Graphics.StrokeRectangle(
						bp - offset,
						new Size(CS_SIZE * used_regs, CS_SIZE),
						col,
						8,
						LineCaps.Round,
						LineJoins.Round
					);
					var split = this.center(frame.bp + fn.args);
					this.line(
						split - offset,
						split - offset + new Point(0, CS_SIZE),
						col,
						8
					);
					break;
			}
			this.render.Graphics.FillText(
				bp - offset - new Point(8, 30),
				frame.fn.debug_name,
				Dbg.FONT,
				col
			);
			if (depth++ == 0) {
				var varret = this.vm.regs.top - (frame.bp + used_regs);
				this.line(
					bp - offset + new Point(used_regs * CS_SIZE, CS_SIZE),
					bp - offset + new Point((used_regs * CS_SIZE) + Math.Max((varret * CS_SIZE) - REDUCE_VAR_SIZE, 0), CS_SIZE),
					col,
					8
				);
			}
		}
	}

	private void line(Point src, Point dst, Brush brush, int width) => this.render.Graphics.StrokePath(
		new GraphicsPath().MoveTo(src).LineTo(dst),
		brush,
		width,
		LineCaps.Round,
		LineJoins.Round
	);
}