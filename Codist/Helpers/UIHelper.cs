using System;

namespace Codist
{
	static class UIHelper
	{
		public static bool IsShiftDown => NativeMethods.IsShiftDown();
		public static bool IsCtrlDown => NativeMethods.IsControlDown();
		public static bool IsAltDown => NativeMethods.IsAltDown();

		static class NativeMethods
		{
			[System.Runtime.InteropServices.DllImport("user32.dll")]
			static extern short GetAsyncKeyState(int vKey);

			public static bool IsShiftDown() {
				return GetAsyncKeyState(0x10) < 0;
			}

			public static bool IsControlDown() {
				return GetAsyncKeyState(0x11) < 0;
			}

			public static bool IsAltDown() {
				return GetAsyncKeyState(0x12) < 0;
			}
		}
	}
}
