using System;

namespace TestProject.Partial;

[ApiVersion(13)]
static class PartialProperty
{
	partial class C
	{
		// Declaring declaration
		public partial string Name { get; set; }
	}

	partial class C
	{
		// implementation declaration:
		private string _name;
		public partial string Name {
			get => _name;
			set => _name = value;
		}
	}

	internal static void Use() {
		var c = new C {
			Name = "123"
		};
	}
}

[ApiVersion(14)]
sealed partial class PartialConstructorAndEvent
{
	internal partial PartialConstructorAndEvent();
	protected partial event EventHandler DoWork;
}

internal partial class PartialConstructorAndEvent
{
	internal partial PartialConstructorAndEvent() {
		Console.WriteLine("Inside partial constructor");
	}
	EventHandler _DoWork;
	protected partial event EventHandler DoWork {
		add => _DoWork += value;
		remove => _DoWork -= value;
	}
}