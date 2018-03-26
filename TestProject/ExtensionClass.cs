namespace TestProject
{
	static class ExtensionClass // static class
	{
		public static void Log(this string text) {
			var cc = new ConcreteClass(0);
			cc.VirtualMethod(); // call overriden method
			var ac = cc as AbstractClass;
			ac.VirtualMethod(); // call virtual method
		} // static method
	}
}
