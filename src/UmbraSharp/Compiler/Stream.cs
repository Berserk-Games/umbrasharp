namespace UmbraSharp.Compiler;

public struct Stream {
	public readonly Str src;
	public int offset;

	public Stream(Str src) {
		this.src = src;
		this.offset = 0;
		if (src.len > 0 && src[0] == 0xFFEF) this.offset = 1;
	}

	public readonly bool at_end => this.offset >= this.src.len;

	public readonly char? peek => this.at_end ? null : this.src[this.offset];
	public readonly char? peekn(int n) => this.offset + n >= this.src.len ? null : this.src[this.offset + n];

	public char? next => this.at_end ? null : this.src[this.offset++];

	public bool take(char ch) {
		if (this.peek == ch) {
			this.offset++;
			return true;
		} else return false;
	}

	public int skip_all(char needle) {
		var start = this.offset;
		while (this.peek == needle) this.offset++;
		return this.offset - start;
	}

	public void skip_until(char needle) {
		if (!this.at_end && this.src.span[this.offset..].IndexOf(needle) is >= 0 and var i) this.offset += i;
		else this.offset = this.src.len;
	}

	public void skip_until_any(Str needle) {
		if (!this.at_end && this.src.span[this.offset..].IndexOfAny<char>(needle) is >= 0 and var i) this.offset += i;
		else this.offset = this.src.len;
	}

	public void skip_until_any_except(Str needle) {
		if (!this.at_end && this.src.span[this.offset..].IndexOfAnyExcept<char>(needle) is >= 0 and var i) this.offset += i;
		else this.offset = this.src.len;
	}

	public void skip_whitespace() {
		while (this.peek is ' ' or '\t' or '\n' or '\r') this.offset++;
	}
}