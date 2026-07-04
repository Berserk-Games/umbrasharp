using UmbraSharp.Internal;

namespace UmbraSharp;

public abstract class UserData(Table? metatable) {
	public Table? metatable = metatable;

	public Val[]? uservalues = null;
	public virtual int uservalue_cap => 1;

	public Val get_uservalue(int i) => this.uservalues?[i] ?? default;
	public void set_uservalue(int i, Val val) {
		if (this.uservalues is null) {
			if (this.uservalue_cap > 0) {
				Statistics.trace_alloc_internal($"alloc -> {this.uservalue_cap} uservalues");
				this.uservalues = new Val[this.uservalue_cap];
			} else this.uservalues = [];
		}
		this.uservalues[i] = val;
	}

	// todo: methods
}

public sealed class AnyUserData<T>(Table? metatable): UserData(metatable) {
	// todo: userdata wrapper for any type, will need work on method resolution once that's implemented for normal userdata
}