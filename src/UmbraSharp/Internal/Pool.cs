using System.Collections.Concurrent;
using System.Text;

namespace UmbraSharp.Internal;

static file class P<T> {
	public static readonly ConcurrentBag<T> POOL = [];
}

public static class StringBuilderPool {
	public static StringBuilder acquire() {
		if (!P<StringBuilder>.POOL.TryTake(out var builder)) {
			Statistics.trace_alloc_internal("alloc -> StringBuilder");
			builder = new(128);
		}
		return builder;
	}

	public static void release(StringBuilder builder) {
		builder.Clear();
		P<StringBuilder>.POOL.Add(builder);
	}

	public static string release_tostring(StringBuilder builder) {
		var str = builder.ToString();
		StringBuilderPool.release(builder);
		return str;
	}
}

public static class ListPool {
	public static List<T> acquire<T>() {
		if (!P<List<T>>.POOL.TryTake(out var list)) {
			Statistics.trace_alloc_internal($"alloc -> List<{typeof(T).Name}>");
			list = new(32);
		}
		return list;
	}

	public static void release<T>(List<T> list) {
		list.Clear();
		P<List<T>>.POOL.Add(list);
	}

	public static T[] release_toarray<T>(List<T> list) {
		T[] vals = [.. list];
		ListPool.release(list);
		return vals;
	}
}

public static class StructStackPool {
	public static StructStack<T> acquire<T>() {
		if (!P<StructStack<T>>.POOL.TryTake(out var stack)) {
			Statistics.trace_alloc_internal($"alloc -> StructStack<{typeof(T).Name}>");
			stack = new(32);
		}
		return stack;
	}

	public static void release<T>(StructStack<T> stack) {
		stack.clear();
		P<StructStack<T>>.POOL.Add(stack);
	}
}