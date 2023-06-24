using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.Imaging;

// This code file is taken from WPF Color Picker Construction Kit
//   by KenJohnson
//   https://www.codeproject.com/Articles/131708/WPF-Color-Picker-Construction-Kit
namespace Codist.Controls
{
	public abstract class ColorComponent
	{
		public static readonly ColorComponent Hue = new HueComponent();
		public static readonly ColorComponent Saturation = new SaturationComponent();
		public static readonly ColorComponent Brightness = new BrightnessComponent();

		/// <summary>
		/// The largest possible value for a component (value when slider at top)
		/// </summary>
		public abstract int MaxValue { get; }

		/// <summary>
		/// The smallest possible value for a component (value when slider at bottom)
		/// </summary>
		public abstract int MinValue { get; }

		/// <summary>
		/// The value of the component for a given color
		/// </summary>
		public abstract int Value(Color color);

		/// <summary>
		/// The name of the color component (used to avoid reflection)
		/// </summary>
		public abstract string Name { get; }

		/// <summary>
		/// Is the Normal bitmap independent of the specific color (false for all but Hue of HSB)
		/// </summary>
		public abstract bool IsNormalIndependentOfColor { get; }

		/// <summary>
		/// Updates the normal Bitmap (The bitmap with the slider)
		/// </summary>
		public abstract void UpdateNormalBitmap(WriteableBitmap bitmap, Color color);

		/// <summary>
		/// Updates the color plane bitmap (the bitmap where one selects the colors)
		/// </summary>
		public abstract void UpdateColorPlaneBitmap(WriteableBitmap bitmap, int normalComponentValue);

		/// <summary>
		/// Gets the color corresponding to a selected point (with 255 alpha)
		/// </summary>
		public abstract Color ColorAtPoint(Point selectionPoint, int colorComponentValue);

		/// <summary>
		/// Gets the point on the color plane that corresponds to the color (alpha ignored)
		/// </summary>
		public abstract Point PointFromColor(Color color);

		sealed class HueComponent : ColorComponent
		{
			public override int MinValue => 0;

			public override int MaxValue => 359;

			public override void UpdateNormalBitmap(WriteableBitmap bitmap, Color color) {
				unsafe {
					bitmap.Lock();
					int currentPixel = -1;
					byte* pStart = (byte*)(void*)bitmap.BackBuffer, o;
					const double iRowUnit = (double)360 / 256;
					double iRowCurrent = 359;
					for (int iRow = 0; iRow < bitmap.PixelHeight; iRow++) {
						Color hueColor = new HslColor(iRowCurrent, 1, 1).ToColor();
						for (int iCol = 0; iCol < bitmap.PixelWidth; iCol++) {
							currentPixel++;
							o = pStart + currentPixel * 3;
							*o = hueColor.B; //Blue
							*(o + 1) = hueColor.G; //Green 
							*(o + 2) = hueColor.R; //red
						}

						iRowCurrent -= iRowUnit;
					}

					bitmap.AddDirtyRect(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
					bitmap.Unlock();
				}
			}

			public override void UpdateColorPlaneBitmap(WriteableBitmap bitmap, int normalComponentValue) {
				unsafe {
					bitmap.Lock();
					byte* pStart = (byte*)(void*)bitmap.BackBuffer, o;
					int currentPixel = -1;
					const double iRowUnit = (double)1 / 256;
					const double iColUnit = (double)1 / 256;
					double iRowCurrent = 1;

					double r = 0;
					double g = 0;
					double b = 0;
					double hue = 359 - normalComponentValue;
					for (int iRow = 0; iRow < bitmap.PixelHeight; iRow++) {
						double iColCurrent = 0;
						for (int iCol = 0; iCol < bitmap.PixelWidth; iCol++) {
							double saturation = iColCurrent;
							double brightness = iRowCurrent;
							//Taken from HSBModel for speed purposes
							if (saturation == 0) {
								r = g = b = brightness;
							}
							else {
								// the color wheel consists of 6 sectors. Figure out which sector you're in.
								double sectorPos = hue / 60.0;
								int sectorNumber = (int)(Math.Floor(sectorPos));
								// get the fractional part of the sector
								double fractionalSector = sectorPos - sectorNumber;

								// calculate values for the three axes of the color. 
								double p = brightness * (1.0 - saturation);
								double q = brightness * (1.0 - (saturation * fractionalSector));
								double t = brightness * (1.0 - (saturation * (1 - fractionalSector)));

								// assign the fractional colors to r, g, and b based on the sector the angle is in.
								switch (sectorNumber) {
									case 0:
										r = brightness;
										g = t;
										b = p;
										break;
									case 1:
										r = q;
										g = brightness;
										b = p;
										break;
									case 2:
										r = p;
										g = brightness;
										b = t;
										break;
									case 3:
										r = p;
										g = q;
										b = brightness;
										break;
									case 4:
										r = t;
										g = p;
										b = brightness;
										break;
									case 5:
										r = brightness;
										g = p;
										b = q;
										break;
								}
							}

							currentPixel++;
							o = pStart + currentPixel * 3;
							*o = Convert.ToByte(g * 255); //Blue
							*(o + 1) = Convert.ToByte(b * 255); //Green 
							*(o + 2) = Convert.ToByte(r * 255); //red
							iColCurrent += iColUnit;
						}
						iRowCurrent -= iRowUnit;
					}
					bitmap.AddDirtyRect(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
					bitmap.Unlock();
				}
			}

			public override Color ColorAtPoint(Point selectionPoint, int colorComponentValue) {
				var hue = colorComponentValue;
				var brightness = (1 - selectionPoint.Y / 255);
				var saturation = (selectionPoint.X / 255);
				return new HslColor(hue, saturation, brightness).ToColor();
			}

			public override Point PointFromColor(Color color) {
				var hsl = HslColor.FromColor(color);
				int x = Convert.ToInt32(hsl.Saturation * 255);
				int y = 255 - Convert.ToInt32(hsl.Luminosity * 255);
				return new Point(x, y);
			}

			public override int Value(Color color) {
				return (int)HslColor.FromColor(color).Hue;
			}

			public override string Name => "HSB_Blue";

			public override bool IsNormalIndependentOfColor => true;
		}

		sealed class SaturationComponent : ColorComponent
		{
			const int MAX = 300;
			public override int MinValue => 0;

			public override int MaxValue => MAX;

			public override void UpdateNormalBitmap(WriteableBitmap bitmap, Color color) {
				unsafe {
					bitmap.Lock();
					int currentPixel = -1;
					byte* pStart = (byte*)(void*)bitmap.BackBuffer, o;
					const double iRowUnit = (double)1 / 256;
					double iRowCurrent = 1;
					var hsl = HslColor.FromColor(color);
					double hue = hsl.Hue;
					double brightness = hsl.Luminosity;
					for (int iRow = 0; iRow < bitmap.PixelHeight; iRow++) {
						Color hueColor = new HslColor(hue, iRowCurrent, brightness).ToColor();
						for (int iCol = 0; iCol < bitmap.PixelWidth; iCol++) {
							currentPixel++;
							o = pStart + currentPixel * 3;
							*o = hueColor.B; //Blue
							*(o+1) = hueColor.G; //Green 
							*(o + 2) = hueColor.R; //red
						}

						iRowCurrent -= iRowUnit;
					}

					bitmap.AddDirtyRect(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
					bitmap.Unlock();
				}
			}

			public override void UpdateColorPlaneBitmap(WriteableBitmap bitmap, int normalComponentValue) {
				unsafe {
					bitmap.Lock();
					byte* pStart = (byte*)(void*)bitmap.BackBuffer, o;
					int currentPixel = -1;
					const double iRowUnit = (double)1 / 256;
					const double iColUnit = (double)360 / 256;
					double iRowCurrent = 1;

					double r = 0;
					double g = 0;
					double b = 0;
					double saturation = (double)normalComponentValue / MAX;
					for (int iRow = 0; iRow < bitmap.PixelHeight; iRow++) {
						double iColCurrent = 359;
						for (int iCol = 0; iCol < bitmap.PixelWidth; iCol++) {
							double hue = iColCurrent;
							double brightness = iRowCurrent;
							//Taken from HSBModel for speed purposes

							if (saturation == 0) {
								r = g = b = brightness;
							}
							else {
								// the color wheel consists of 6 sectors. Figure out which sector you're in.
								double sectorPos = hue / 60.0;
								int sectorNumber = (int)(Math.Floor(sectorPos));
								// get the fractional part of the sector
								double fractionalSector = sectorPos - sectorNumber;

								// calculate values for the three axes of the color. 
								double p = brightness * (1.0 - saturation);
								double q = brightness * (1.0 - (saturation * fractionalSector));
								double t = brightness * (1.0 - (saturation * (1 - fractionalSector)));

								// assign the fractional colors to r, g, and b based on the sector the angle is in.
								switch (sectorNumber) {
									case 0:
										r = brightness;
										g = t;
										b = p;
										break;
									case 1:
										r = q;
										g = brightness;
										b = p;
										break;
									case 2:
										r = p;
										g = brightness;
										b = t;
										break;
									case 3:
										r = p;
										g = q;
										b = brightness;
										break;
									case 4:
										r = t;
										g = p;
										b = brightness;
										break;
									case 5:
										r = brightness;
										g = p;
										b = q;
										break;
								}
							}

							currentPixel++;
							o = pStart + currentPixel * 3;
							*o = Convert.ToByte(g * 255); //Blue
							*(o + 1) = Convert.ToByte(b * 255); //Green 
							*(o + 2) = Convert.ToByte(r * 255); //red
							iColCurrent -= iColUnit;
						}
						iRowCurrent -= iRowUnit;
					}
					bitmap.AddDirtyRect(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
					bitmap.Unlock();
				}
			}

			public override Color ColorAtPoint(Point selectionPoint, int colorComponentValue) {
				var hue = (359 * selectionPoint.X / 255);
				var brightness = (1 - selectionPoint.Y / 255);
				var saturation = (double)colorComponentValue / MAX;

				return new HslColor(hue, saturation, brightness).ToColor();
			}

			public override Point PointFromColor(Color color) {
				var hsl = HslColor.FromColor(color);
				int x = Convert.ToInt32(hsl.Hue * (255d / 359d));
				int y = 255 - Convert.ToInt32(hsl.Luminosity * 255);
				return new Point(x, y);
			}

			public override int Value(Color color) {
				return Convert.ToInt32(HslColor.FromColor(color).Saturation * MAX);
			}

			public override string Name => "HSB_Saturation";

			public override bool IsNormalIndependentOfColor => false;
		}

		sealed class BrightnessComponent : ColorComponent
		{
			const int MAX = 300;
			public override int MinValue => 0;

			public override int MaxValue => MAX;

			public override void UpdateNormalBitmap(WriteableBitmap bitmap, Color color) {
				unsafe {
					bitmap.Lock();
					int currentPixel = -1;
					byte* pStart = (byte*)(void*)bitmap.BackBuffer, o;
					const double iRowUnit = (double)1 / 256;
					double iRowCurrent = 1;
					var hsl = HslColor.FromColor(color);
					double hue = hsl.Hue;
					double saturation = hsl.Saturation;
					for (int iRow = 0; iRow < bitmap.PixelHeight; iRow++) {
						Color hueColor = new HslColor(hue, saturation, iRowCurrent).ToColor();
						for (int iCol = 0; iCol < bitmap.PixelWidth; iCol++) {
							currentPixel++;
							o = pStart + currentPixel * 3;
							*o = hueColor.B; //Blue
							*(o + 1) = hueColor.G; //Green 
							*(o + 2) = hueColor.R; //red
						}

						iRowCurrent -= iRowUnit;
					}

					bitmap.AddDirtyRect(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
					bitmap.Unlock();
				}
			}

			public override void UpdateColorPlaneBitmap(WriteableBitmap bitmap, int normalComponentValue) {
				unsafe {
					bitmap.Lock();
					byte* pStart = (byte*)(void*)bitmap.BackBuffer, o;
					int currentPixel = -1;
					const double iRowUnit = (double)1 / 256;
					const double iColUnit = (double)360 / 256;
					double iRowCurrent = 1;

					double r = 0;
					double g = 0;
					double b = 0;
					double brightness = (double)(normalComponentValue) / MAX;
					for (int iRow = 0; iRow < bitmap.PixelHeight; iRow++) {
						double iColCurrent = 359;
						for (int iCol = 0; iCol < bitmap.PixelWidth; iCol++) {
							double hue = iColCurrent;
							double saturation = iRowCurrent;
							//Taken from HSBModel for speed purposes

							if (saturation == 0) {
								r = g = b = brightness;
							}
							else {
								// the color wheel consists of 6 sectors. Figure out which sector you're in.
								double sectorPos = hue / 60.0;
								int sectorNumber = (int)(Math.Floor(sectorPos));
								// get the fractional part of the sector
								double fractionalSector = sectorPos - sectorNumber;

								// calculate values for the three axes of the color. 
								double p = brightness * (1.0 - saturation);
								double q = brightness * (1.0 - (saturation * fractionalSector));
								double t = brightness * (1.0 - (saturation * (1 - fractionalSector)));

								// assign the fractional colors to r, g, and b based on the sector the angle is in.
								switch (sectorNumber) {
									case 0:
										r = brightness;
										g = t;
										b = p;
										break;
									case 1:
										r = q;
										g = brightness;
										b = p;
										break;
									case 2:
										r = p;
										g = brightness;
										b = t;
										break;
									case 3:
										r = p;
										g = q;
										b = brightness;
										break;
									case 4:
										r = t;
										g = p;
										b = brightness;
										break;
									case 5:
										r = brightness;
										g = p;
										b = q;
										break;
								}
							}

							currentPixel++;
							o = pStart + currentPixel * 3;
							*o = Convert.ToByte(g * 255); //Blue
							*(o + 1) = Convert.ToByte(b * 255); //Green 
							*(o + 2) = Convert.ToByte(r * 255); //red
							iColCurrent -= iColUnit;
						}
						iRowCurrent -= iRowUnit;
					}
					bitmap.AddDirtyRect(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight));
					bitmap.Unlock();
				}
			}

			public override Color ColorAtPoint(Point selectionPoint, int colorComponentValue) {
				var hue = (359 * selectionPoint.X / 255);
				var brightness = (double)colorComponentValue / MAX;
				var saturation = (1 - (double)selectionPoint.Y / 255);
				return new HslColor(hue, saturation, brightness).ToColor();
			}

			public override Point PointFromColor(Color color) {
				var hsl = HslColor.FromColor(color);
				int x = Convert.ToInt32(hsl.Hue * (255d / 359d));
				int y = Convert.ToInt32(255 - hsl.Saturation * 255d);
				return new Point(x, y);
			}

			public override int Value(Color color) {
				return Convert.ToInt32(HslColor.FromColor(color).Luminosity * MAX);
			}

			public override string Name => "HSB_Brightness";

			public override bool IsNormalIndependentOfColor => false;
		}
	}
}
