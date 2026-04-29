namespace UmbraSharp;

public abstract class UserData(int uservalue_cap) {
	private Val[]? uservalues = null;
	private readonly int uservalue_cap = uservalue_cap;

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

public sealed class AnyUserData<T>(): UserData(1) {
	// todo: userdata wrapper for any type, will need work on method resolution once that's implemented for normal userdata
}