using System.Diagnostics;
using System.Runtime.CompilerServices;
using UmbraSharp.Internal;

namespace UmbraSharp;

static file class Helpers {
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int lmod(int val, int size) {
		Debug.Assert((size & (size - 1)) == 0);
		return val & (size - 1);
	}

	public static int log2(int v) {
		var r = 0;

		while (v >= (2 << r))
			r++;

		return r;
	}

	public static int ceillog2(int v) {
		return v == 1 ? 0 : log2(v - 1) + 1;
	}

	public static T[] array_alloc<T>(int len) {
		// dont bother allocating empty arrays, they're immutable, so just use the shared empty array
		if (len == 0) return [];
		Statistics.trace_alloc_internal($"alloc -> table internal {typeof(T).Name}[{len}]");
		return new T[len];
	}

	public static T[] array_realloc<T>(T[] arr, int len) {
		// dont bother allocating empty arrays, they're immutable, so just use the shared empty array
		if (len == 0) return [];
		if (arr.Length == len) {
			Statistics.trace($"ignoring table internal realloc (same len) {typeof(T).Name}[{len}]");
			return arr;
		}
		Statistics.trace_alloc_internal($"alloc -> table internal realloc {typeof(T).Name}[{len}]");
		Array.Resize(ref arr, len);
		return arr;
	}

	public static T[] array_clone<T>(T[] arr) {
		// dont bother cloning empty arrays, they're immutable, so just use the shared empty array
		if (arr.Length == 0) return arr;
		Statistics.trace_alloc_internal($"alloc -> table internal clone {typeof(T).Name}[{arr.Length}]");
		return (T[])arr.Clone();
	}

	public static int? integral(Val key) {
		if (key.is_long) return checked((int)key.assume_long_bits);
		if (!key.is_number) return null;
		var num = key.assume_double_bits;
		var integral = (int)num;
		return num == integral ? integral : null;
	}

	public static bool is_valid_integral_index(Val key, out int integral) {
		if (Helpers.integral(key) is int k && k > 0 && k <= Table.MAX_SIZE) {
			integral = k;
			return true;
		}
		integral = default;
		return false;
	}
}

file ref struct OptionRef<T> {
	public bool present;
	public ref T reference;

	public OptionRef(ref T reference) {
		this.present = true;
		this.reference = ref reference;
	}
}

// can't use a Dictionary, since we can't implement `next`, so we're reimplementing luau's table implementation

public sealed class Table {
	internal const int MAX_BITS = 26;
	internal const int MAX_SIZE = 1 << 26;

	/// metamethods that we'd like to avoid a metamethod lookup for if they aren't present
	internal enum FastMeta: byte {
		__index = 0,
		__newindex = 1,
		__len = 2,
		__eq = 3,
		__iter = 4,
		__iterator = 5,
	}

	private record struct Entry(uint key_hash, Val key, Val val, int next_offset) {
		public static implicit operator Pair(Entry entry) => new(entry.key, entry.val);
	}

	public readonly record struct Pair(Val key, Val val);

	// public struct Enumerator: IEnumerator<Pair>, IEnumerable<Pair> {
	// 	public Pair Current =>
	// }

	public bool frozen = false;

	public Table? metatable;

	private int? boundary = 0;

	/// a cache of which metamethods are missing, optimizing for metamethods *not* being present
	/// when a metamethod lookup misses, it's added to this cache so further attempts dont do lookups
	/// then this cache is invalidated when the table is modified
	private byte missing_meta = (byte)0xff;

	public int raw_len {
		get {
			if (this.boundary.HasValue) return this.boundary.Value;
			return 0;
		}
	}

	public Table() : this(0, 0) { }

	public Table(int array, int hash) {
		Statistics.trace_alloc_table();
		this.ap_resize(array);
		this.hp_resize(hash);
	}

	public Val raw_get(Val key) {
		if (this.index(key) is not int idx) return default;
		return idx < 0 ? this.array[-idx - 1] : this.hash[idx].val;
	}

	public void raw_set(Val key, Val val) {
		this.assert_mutable();
		this.missing_meta = 0;

		if (this.index(key) is int idx) {
			if (idx < 0) this.array[-idx - 1] = val;
			else this.hash[idx].val = val;
			return;
		}

		this.new_key(key, val);
	}

	public int internal_index_find(Val key) {
		if (key.is_nil) return -1;
		if (this.index(key) is not int idx) throw new Exception("invalid key to 'next'");
		return idx < 0 ? -idx - 1 : idx + this.array.Length;
	}

	// public Val? internal_index_next(int idx) {}

	public Pair next(Val key) {
		var i = this.internal_index_find(key);
		for (i++; i < this.array.Length; i++) {
			var val = this.array[i];
			if (!val.is_nil) return new(i + 1, val);
		}
		for (i -= this.array.Length; i < this.hash.Length; i++) {
			var entry = this.hash[i];
			if (!entry.val.is_nil) return entry;
		}
		return default;
	}

	public void clear() {
		this.assert_mutable();
		this.missing_meta = 0xff;
		this.boundary = null;
		this.array.AsSpan().Clear();
		this.hash.AsSpan().Clear();
		this.hp_last_free = 0;
	}

	public Table clone() => new() {
		metatable = this.metatable,
		// todo
		boundary = this.boundary,
	};

	internal Val fast_meta(FastMeta meta) {
		var bits = (byte)(1 << (byte)meta);
		if ((this.missing_meta & bits) != 0) return default;
		var val = this.raw_get(meta.ToString());
		if (val.is_nil) this.missing_meta |= bits;
		return val;
	}

	#region internal

	private void assert_mutable() {
		if (this.frozen) throw new Exception("attempt to modify a readonly table");
	}

	/// <summary>returns the internal index of a key (zero or positive for hash index (this.hash[idx]), less than zero for negated array index (this.array[-idx - 1])) or null if not found</summary>
	private int? index(Val key) {
		if (this.ap_idx(key) is int ap) return -ap;
		if (this.hash.Length == 0) return null;
		var i = this.hp_main_position(key.GetHashCode());
		var hash = this.hash;
		while (true) {
			ref var entry = ref hash[i];
			if (entry.key.raw_eq(key)) return i;
			if (entry.next_offset == 0) return null;
			i += entry.next_offset;
		}
	}

	private void rehash(Val extra) {
		Statistics.trace($"rehashing table + with extra key {extra}");
		Span<int> nums = stackalloc int[Table.MAX_BITS + 1];
		nums.Clear();
		var array = this.ap_num_used(nums);
		var total = array + this.hp_num_used(nums, ref array) + 1;

		if (Helpers.is_valid_integral_index(extra, out var k)) {
			nums[Helpers.ceillog2(k)]++;
			array++;
		}

		// compute array size
		var num_to_array = this.ap_compute_sizes(nums, ref array);
		var hash = total - num_to_array;

		// adjust array sie
		var adjusted = this.ap_adjust_size(array, extra);

		var array_extra = adjusted - array;

		if (array_extra != 0) {
			hash -= array_extra;

			array = this.ap_adjust_size(adjusted + array_extra, extra);
		}

		this.resize(array, hash);
	}

	private void resize(int array, int hash) {
		Statistics.trace($"resizing table to {array},{hash}");
		if (array > Table.MAX_SIZE || hash > Table.MAX_SIZE) Errors.table_overflow();
		var old_array = this.array;
		var old_hash = this.hash;

		this.hp_resize(hash);
		var new_hash = this.hash;
		this.ap_resize(array);
		var new_array = this.array;

		if (array < old_array.Length)
			for (var i = array; i < old_array.Length; i++) {
				var val = old_array[i];
				if (!val.is_nil) this.new_key(i + 1, val);
			}

		foreach (var entry in old_hash) {
			if (!entry.val.is_nil) this.array_or_new_key(entry.key, entry.val);
		}

		Debug.Assert(this.hash == new_hash);
		Debug.Assert(this.array == new_array);
	}

	private void new_key(Val key, Val val) {
		Statistics.trace($"table newkey {key} = {val} // {this.array.Length},{this.hash.Length}");
		if (Helpers.integral(key) is int integral && integral == this.array.Length + 1) {
			Statistics.trace($"- 1 + array, growing");
			goto grow;
		}

		var hash = this.hash;
		if (hash.Length == 0) {
			Statistics.trace($"- hash empty, growing");
			goto grow;
		}
		var cur_mp = this.hp_main_position(key.GetHashCode());
		ref var cur = ref hash[cur_mp];
		if (!cur.val.is_nil) {
			while (this.hp_last_free > 0) {
				if (hash[--this.hp_last_free].val.is_nil) goto found;
			}
			Statistics.trace($"- no free nodes, growing");
			goto grow;
		found:
			var free_mp = this.hp_last_free;
			ref var free = ref hash[free_mp];

			var colliding_mp = this.hp_main_position(cur.key.GetHashCode());
			if (colliding_mp != cur_mp) {
				Statistics.trace($"- colliding node ({cur_mp}) is not in main position, evicting to free node ({free_mp})");
				ref var colliding = ref hash[colliding_mp];

				while (colliding_mp + colliding.next_offset != cur_mp)
					colliding_mp += colliding.next_offset;

				colliding.next_offset = free_mp - colliding_mp;

				free = cur;
				if (cur.next_offset != 0) {
					free.next_offset += cur_mp - free_mp;
					cur.next_offset = 0;
				}
			} else {
				Statistics.trace($"- colliding node ({cur_mp}) is in main position, using free node ({free_mp})");
				if (cur.next_offset != 0) free.next_offset = cur_mp + cur.next_offset - free_mp;
				else Debug.Assert(free.next_offset == 0, "free next != 0");
				cur.next_offset = free_mp - cur_mp;
				cur = ref free;
			}
		} else Debug.Assert(cur.next_offset == 0, "cur next != 0");
		cur.key = key;
		cur.val = val;
		return;

	grow:
		this.rehash(key);
		this.array_or_new_key(key, val);
	}

	private void array_or_new_key(Val key, Val val) {
		if (this.ap_idx(key) is int i) this.array[i - 1] = val;
		else this.new_key(key, val);
	}

	#region array part

	private Val[] array = [];

	private int? ap_idx(Val key) => (
		Helpers.integral(key) is int integral
		&& integral > 0
		&& integral <= this.array.Length
	) ? integral : null;

	private int ap_num_used(Span<int> nums) {
		var arr = this.array;
		var cap = arr.Length;

		var total = 0;
		var i = 1;
		for (var segment = 0; segment <= Table.MAX_BITS; segment++) {
			var count = 0;
			var lim = 1 << segment;
			if (lim > cap) {
				lim = cap;
				if (i > lim) break;
			}
			for (; i <= lim; i++)
				if (!arr[i - 1].is_nil)
					count++;
			nums[segment] += count;
			total += count;
		}
		return total;
	}

	private int ap_compute_sizes(Span<int> nums, ref int array) {
		var num_to_array = 0;
		var num_lt_twotoi = 0;
		var optimal_array_size = 0;

		var limit = (int)Math.Log2(array);
		for (var i = 0; i <= limit; i++) {
			if (nums[i] > 0) {
				num_lt_twotoi += nums[i];
				var twotoi = 1 << i;
				if (num_lt_twotoi > twotoi / 2) {
					optimal_array_size = twotoi;
					num_to_array = num_lt_twotoi;
				}
			}
			if (num_lt_twotoi == array) break;
		}
		array = optimal_array_size;
		return num_to_array;
	}

	private int ap_adjust_size(int array, Val extra) {
		var bound = this.hash.Length > 0 || array < this.array.Length;
		var extra_idx = extra.is_number ? Helpers.integral(extra) ?? -1 : -1;
		while (array + 1 == extra_idx || (bound && !this.raw_get(array + 1).is_nil)) array++;
		return array;
	}

	private void ap_resize(int size) {
		if (size > Table.MAX_SIZE) Errors.table_overflow();
		this.array = Helpers.array_realloc<Val>(this.array, size);
	}

	#endregion

	#region hash part

	private Entry[] hash = [];
	private int hp_last_free = 0;

	private int hp_main_position(int key_hash) => Helpers.lmod(key_hash, this.hash.Length);

	private void hp_resize(int size) {
		if (size == 0) this.hash = [];
		else {
			var lsize = Helpers.ceillog2(size);
			if (lsize > Table.MAX_BITS) Errors.table_overflow();
			size = 1 << lsize;
			this.hash = new Entry[size];
			this.hp_last_free = size;
		}
	}

	private int hp_num_used(Span<int> nums, ref int numeric) {
		var total = 0;

		var entries = this.hash;
		for (var i = entries.Length - 1; i >= 0; i--) {
			ref var entry = ref entries[i];
			if (entry.val.is_nil) continue;
			if (entry.key.is_number && Helpers.is_valid_integral_index(entry.key, out var k)) {
				nums[Helpers.ceillog2(k)]++;
				numeric++;
			}
			total++;
		}

		return total;
	}

	#endregion

	#endregion
}
