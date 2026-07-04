namespace UmbraSharp;

public sealed class ClassDefinition {
	public readonly Str name;

	public readonly Val[] static_members;

	internal readonly Dictionary<Str, int> name_to_offset;
	internal readonly Str[] offset_to_name;

	public Table class_metatable;

	public Table instance_metatable;

	public int num_instance_members => this.offset_to_name.Length - this.static_members.Length;

	public ClassInstance create() => new(new Val[this.num_instance_members], this);

	public int? static_member_offset(Str name) {
		if (!this.name_to_offset.TryGetValue(name, out var offset)) return null;
		if (offset < this.num_instance_members) return null;
		return offset - this.num_instance_members;
	}

	public int? instance_member_offset(Str name) {
		if (!this.name_to_offset.TryGetValue(name, out var offset)) return null;
		if (offset >= this.num_instance_members) return null;
		return offset;
	}
}

public readonly struct ClassInstance {
	public readonly Val[] members;
	public readonly ClassDefinition cls;

	internal ClassInstance(Val[] members, ClassDefinition cls) {
		this.members = members;
		this.cls = cls;
	}
}