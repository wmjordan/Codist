using System;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using GdiColor = System.Drawing.Color;
using WpfBrush = System.Windows.Media.SolidColorBrush;
using WpfColor = System.Windows.Media.Color;
using Microsoft.VisualStudio.Imaging;

namespace Codist
{
	static class ThemeHelper
	{
		public static GdiColor ToolWindowBackgroundColor { get; private set; }
		public static WpfColor TitleBackgroundColor { get; private set; }
		public static WpfBrush ToolTipTextBrush { get; private set; }
		public static WpfBrush ToolTipBackgroundBrush { get; private set; }
		public static WpfBrush ToolWindowTextBrush { get; private set; }
		public static WpfBrush ToolWindowBackgroundBrush { get; private set; }
		public static WpfBrush MenuTextBrush { get; private set; }
		public static WpfBrush MenuBackgroundBrush { get; private set; }
		public static WpfBrush MenuGlyphBackgroundBrush { get; private set; }
		public static WpfBrush TextBoxBrush { get; private set; }
		public static WpfBrush TextBoxBackgroundBrush { get; private set; }
		public static WpfBrush TextBoxBorderBrush { get; private set; }
		public static WpfColor SystemButtonFaceColor { get; private set; }
		public static WpfColor SystemThreeDFaceColor { get; private set; }

		public static GdiColor ToThemedGdiColor(this ThemeResourceKey resourceKey) {
			return VSColorTheme.GetThemedColor(resourceKey);
		}
		public static WpfColor ToThemedWpfColor(this ThemeResourceKey resourceKey) {
			return resourceKey.ToThemedGdiColor().ToWpfColor();
		}

		/// <summary>
		/// Gets a themed <see cref="Image"/> from a value defined in <see cref="KnownImageIds"/>
		/// </summary>
		/// <param name="imageId">The image id.</param>
		public static CrispImage GetImage(int imageId, int size = 0) {
			const int DEFAULT_SIZE = 16;
			var moniker = new ImageMoniker {
				Guid = KnownImageIds.ImageCatalogGuid,
				Id = imageId
			};
			if (size < 1) {
				size = DEFAULT_SIZE;
			}
			var image = new CrispImage {
				Moniker = moniker,
				Height = size,
				Width = size,
			};
			return image;
		}

		internal static void RefreshThemeCache() {
			ToolWindowBackgroundColor = EnvironmentColors.ToolWindowBackgroundColorKey.ToThemedGdiColor();
			TitleBackgroundColor = EnvironmentColors.MainWindowActiveCaptionColorKey.ToThemedWpfColor();
			ToolTipTextBrush = new WpfBrush(EnvironmentColors.ButtonTextBrushKey.ToThemedWpfColor());
			ToolTipBackgroundBrush = new WpfBrush(EnvironmentColors.ToolTipBrushKey.ToThemedWpfColor());
			ToolWindowTextBrush = new WpfBrush(EnvironmentColors.ToolWindowTextBrushKey.ToThemedWpfColor());
			ToolWindowBackgroundBrush = new WpfBrush(EnvironmentColors.ToolWindowBackgroundBrushKey.ToThemedWpfColor());
			MenuTextBrush = new WpfBrush(EnvironmentColors.SystemMenuTextBrushKey.ToThemedWpfColor());
			MenuBackgroundBrush = new WpfBrush(EnvironmentColors.SystemMenuBrushKey.ToThemedWpfColor());
			MenuGlyphBackgroundBrush = new WpfBrush(EnvironmentColors.CommandBarMenuGlyphBrushKey.ToThemedWpfColor());
			TextBoxBrush = new WpfBrush(CommonControlsColors.TextBoxTextBrushKey.ToThemedWpfColor());
			TextBoxBackgroundBrush = new WpfBrush(CommonControlsColors.TextBoxBackgroundBrushKey.ToThemedWpfColor());
			TextBoxBorderBrush = new WpfBrush(CommonControlsColors.TextBoxBorderBrushKey.ToThemedWpfColor());
			SystemButtonFaceColor = EnvironmentColors.SystemButtonFaceColorKey.ToThemedWpfColor();
			SystemThreeDFaceColor = EnvironmentColors.SystemThreeDFaceColorKey.ToThemedWpfColor();
		}
	}
}
