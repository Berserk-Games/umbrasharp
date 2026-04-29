namespace UmbraSharp;

public sealed class Config {
	// todo
}

internal static class StaticConfig {
	public const int STACK_SIZE = 1 << 7;
	public const int MAX_STACK_SIZE = 1 << 14;

	public const int CALL_STACK_SIZE = 1 << 5;
	public const int MAX_CALL_STACK_SIZE = 1 << 12;
}
