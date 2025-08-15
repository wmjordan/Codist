using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using CLR;
using GdiColor = System.Drawing.Color;
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;
using WpfColors = System.Windows.Media.Colors;

namespace Codist
{
	static class UIHelper
	{
		public static bool IsShiftDown => NativeMethods.IsShiftDown();
		public static bool IsCtrlDown => NativeMethods.IsControlDown();
		public static bool IsAltDown => NativeMethods.IsAltDown();

		public static GdiColor Alpha(this GdiColor color, byte alpha) {
			return GdiColor.FromArgb(alpha, color.R, color.G, color.B);
		}
		public static int GetArgbValue(this WpfColor color) {
			return color.A << 24 | color.R << 16 | color.G << 8 | color.B;
		}
		public static string ToText(this int value) {
			return value.ToString(CultureInfo.InvariantCulture);
		}
		public static string ToHexString(this GdiColor color) {
			return HexBinCache.WriteHexBinColor(color);
		}
		public static void ParseColor(string colorText, out WpfColor color, out byte opacity) {
			if (String.IsNullOrEmpty(colorText) || colorText[0] != '#') {
				goto EXIT;
			}
			var l = colorText.Length;
			if (!l.CeqAny(7, 9, 4, 3)) {
				goto EXIT;
			}
			try {
				byte a, r, g, b;
				switch (l) {
					case 3:
						if (ParseByte(colorText, 1, out a)) {
							opacity = a;
							color = WpfColors.Transparent;
							return;
						}
						break;
					case 4:
						if (ParseSingleByte(colorText, 1, out r)
							&& ParseSingleByte(colorText, 2, out g)
							&& ParseSingleByte(colorText, 3, out b)) {
							color = WpfColor.FromRgb(r, g, b);
							opacity = 0;
							return;
						}
						break;
					case 7:
						if (ParseByte(colorText, 1, out r)
							&& ParseByte(colorText, 3, out g)
							&& ParseByte(colorText, 5, out b)) {
							color = WpfColor.FromRgb(r, g, b);
							opacity = 0;
							return;
						}
						break;
					case 9:
						if (ParseByte(colorText, 1, out a)
							&& ParseByte(colorText, 3, out r)
							&& ParseByte(colorText, 5, out g)
							&& ParseByte(colorText, 7, out b)) {
							if (a == 0) {
								goto EXIT;
							}
							color = WpfColor.FromRgb(r, g, b);
							opacity = a;
							return;
						}
						break;
				}
			}
			catch (Exception ex) {
				ex.Log();
			}
		EXIT:
			color = WpfColors.Transparent;
			opacity = 0;
		}

		static bool ParseSingleByte(string text, int index, out byte value) {
			switch (text[index]) {
				case '0': value = 0; break;
				case '1': value = 0x11; break;
				case '2': value = 0x22; break;
				case '3': value = 0x33; break;
				case '4': value = 0x44; break;
				case '5': value = 0x55; break;
				case '6': value = 0x66; break;
				case '7': value = 0x77; break;
				case '8': value = 0x88; break;
				case '9': value = 0x99; break;
				case 'A':
				case 'a':
					value = 0xAA; break;
				case 'B':
				case 'b':
					value = 0xBB; break;
				case 'C':
				case 'c':
					value = 0xCC; break;
				case 'D':
				case 'd':
					value = 0xDD; break;
				case 'E':
				case 'e':
					value = 0xEE; break;
				case 'F':
				case 'f':
					value = 0xFF; break;
				default:
					value = 0;
					return false;
			}
			return true;
		}

		public static string ToHexString(this WpfColor color) {
			return HexBinCache.WriteHexBinColor(color);
		}
		public static GdiColor ToGdiColor(this WpfColor color) {
			return GdiColor.FromArgb(color.A, color.R, color.G, color.B);
		}
		public static WpfColor ToWpfColor(this GdiColor color) {
			return WpfColor.FromArgb(color.A, color.R, color.G, color.B);
		}
		public static WpfColor Alpha(this WpfColor color, byte a) {
			return WpfColor.FromArgb(a, color.R, color.G, color.B);
		}
		public static WpfColor MakeOpaque(this WpfColor color) {
			return WpfColor.FromArgb(Byte.MaxValue, color.R, color.G, color.B);
		}
		/// <summary>
		/// Returns a new clone of <see cref="WpfBrush"/> which has a new <paramref name="opacity"/> as <see cref="WpfBrush.Opacity"/>.
		/// </summary>
		public static TBrush Alpha<TBrush>(this TBrush brush, double opacity)
			where TBrush : WpfBrush {
			if (brush != null) {
				brush = brush.Clone() as TBrush;
				if (brush is System.Windows.Media.SolidColorBrush cb) {
					cb.Color = cb.Color.Alpha((byte)(opacity * 255));
				}
				else {
					brush.Opacity = opacity;
				}
				brush.Freeze();
			}
			return brush;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		static bool IsHexBinChar(int ch) {
			const long Mask = 0b111_1110_00000000_00000000_00000000_01111110_00000011_11111111;
			var n = unchecked((ushort)(ch - '0'));
			return (Mask & (1L << n) & ~((63 - n) >> 63)) != 0;
		}
		static bool ParseByte(string text, int offset, out byte value) {
			int high = text[offset];
			int low = text[offset + 1];

			if (!IsHexBinChar(high) || !IsHexBinChar(low)) {
				value = 0;
				return false;
			}

			high = (high & 0xF) + ((high >> 6) * 9);
			low = (low & 0xF) + ((low >> 6) * 9);

			value = (byte)((high << 4) | low);
			return true;
		}

		static class HexBinCache
		{
			static readonly char[] __ArgbBuffer = new char[9], __RgbBuffer = new char[7];
			static readonly int[] __HexBin = InitHexBinCache();

			static int[] InitHexBinCache() {
				__ArgbBuffer[0] = __RgbBuffer[0] = '#';
				int[] doubleChars = new int[256];
				for (int i = 0; i < 256; i++) {
					var a = i.ToString("X2").ToCharArray();
					doubleChars[i] = Op.Cast<char, int>(ref a[0]);
				}
				return doubleChars;
			}

			public static string WriteHexBinColor(GdiColor color) {
				if (color.A == 0xFF) {
					WriteHexBin(color.R, ref __RgbBuffer[1]);
					WriteHexBin(color.G, ref __RgbBuffer[3]);
					WriteHexBin(color.B, ref __RgbBuffer[5]);
					return new string(__RgbBuffer);
				}
				WriteHexBin(color.A, ref __ArgbBuffer[1]);
				WriteHexBin(color.R, ref __ArgbBuffer[3]);
				WriteHexBin(color.G, ref __ArgbBuffer[5]);
				WriteHexBin(color.B, ref __ArgbBuffer[7]);
				return new string(__ArgbBuffer);
			}
			public static string WriteHexBinColor(WpfColor color) {
				if (color.A == 0xFF) {
					WriteHexBin(color.R, ref __RgbBuffer[1]);
					WriteHexBin(color.G, ref __RgbBuffer[3]);
					WriteHexBin(color.B, ref __RgbBuffer[5]);
					return new string(__RgbBuffer);
				}
				WriteHexBin(color.A, ref __ArgbBuffer[1]);
				WriteHexBin(color.R, ref __ArgbBuffer[3]);
				WriteHexBin(color.G, ref __ArgbBuffer[5]);
				WriteHexBin(color.B, ref __ArgbBuffer[7]);
				return new string(__ArgbBuffer);
			}
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			static void WriteHexBin(byte value, ref char target) {
				Op.Cast<char, int>(ref target) = __HexBin[value];
			}
		}

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
