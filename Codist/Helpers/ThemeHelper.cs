using System;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using GdiColor = System.Drawing.Color;
using WpfBrush = System.Windows.Media.SolidColorBrush;
using WpfColor = System.Windows.Media.Color;

namespace Codist
{
	static class ThemeHelper
	{
		static IVsUIShell5 _VsUIShell5;
		static ImageAttributes _ImageAttributes;
		static IVsImageService2 _ImageService;

		public static IVsUIShell5 VsShell5 => _VsUIShell5 ?? (_VsUIShell5 = ServiceProvider.GlobalProvider.GetService(typeof(SVsUIShell)) as IVsUIShell5);
		public static GdiColor ToolWindowBackgroundColor { get; private set; }
		public static GdiColor TitleBackgroundColor { get; private set; }
		public static WpfBrush ToolTipTextBrush { get; private set; }
		public static WpfBrush ToolTipBackgroundBrush { get; private set; }
		public static WpfBrush ToolWindowTextBrush { get; private set; }
		public static WpfBrush ToolWindowBackgroundBrush { get; private set; }

		public static GdiColor ToThemedGdiColor(this ThemeResourceKey resourceKey) {
			return VSColorTheme.GetThemedColor(resourceKey);
		}
		public static WpfColor ToThemedWpfColor(this ThemeResourceKey resourceKey) {
			return VsShell5.GetThemedWPFColor(resourceKey);
		}

		/// <summary>
		/// Gets a themed <see cref="System.Windows.Controls.Image"/> from a value defined in <see cref="Microsoft.VisualStudio.Imaging.KnownImageIds"/>
		/// </summary>
		/// <param name="imageId">The image id.</param>
		public static System.Windows.Controls.Image GetImage(int imageId) {
			var moniker = new ImageMoniker {
				Guid = Microsoft.VisualStudio.Imaging.KnownImageIds.ImageCatalogGuid,
				Id = imageId
			};
			object data;
			(_ImageService ?? (_ImageService = ServiceProvider.GlobalProvider.GetService(typeof(SVsImageService)) as IVsImageService2)).GetImage(moniker, _ImageAttributes).get_Data(out data);
			return new System.Windows.Controls.Image { Source = data as System.Windows.Media.Imaging.BitmapSource };
		}


		internal static void Refresh() {
			const int ICON_SIZE = 16;
			ToolWindowBackgroundColor = VSColorTheme.GetThemedColor(EnvironmentColors.ToolWindowBackgroundColorKey);
			TitleBackgroundColor = VSColorTheme.GetThemedColor(EnvironmentColors.MainWindowActiveCaptionColorKey);
			ToolTipTextBrush = new WpfBrush(EnvironmentColors.ButtonTextBrushKey.ToThemedWpfColor());
			ToolTipBackgroundBrush = new WpfBrush(EnvironmentColors.ToolTipBrushKey.ToThemedWpfColor());
			ToolWindowTextBrush = new WpfBrush(EnvironmentColors.ToolWindowTextBrushKey.ToThemedWpfColor());
			ToolWindowBackgroundBrush = new WpfBrush(EnvironmentColors.ToolWindowBackgroundBrushKey.ToThemedWpfColor());
			var v = TitleBackgroundColor.ToArgb();
			_ImageAttributes = new ImageAttributes {
				Flags = unchecked((uint)(_ImageAttributesFlags.IAF_RequiredFlags | _ImageAttributesFlags.IAF_Background)),
				ImageType = (uint)_UIImageType.IT_Bitmap,
				Format = (uint)_UIDataFormat.DF_WPF,
				StructSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(ImageAttributes)),
				LogicalHeight = ICON_SIZE,
				LogicalWidth = ICON_SIZE,
				Background = (uint)(v & 0xFFFFFF << 8 | v & 0xFF)
			};
		}
	}
}
