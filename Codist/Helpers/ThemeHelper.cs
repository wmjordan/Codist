using System;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using GdiColor = System.Drawing.Color;
using WpfBrush = System.Windows.Media.SolidColorBrush;
using WpfColor = System.Windows.Media.Color;
using Microsoft.VisualStudio.Imaging;
using System.Drawing;

namespace Codist
{
	static class ThemeHelper
	{
		static ThemeHelper() {
			RefreshThemeCache();
		}

		public static GdiColor DocumentPageColor { get; private set; }
		public static GdiColor DocumentTextColor { get; private set; }
		public static WpfBrush DocumentTextBrush { get; private set; }
		public static GdiColor ToolWindowBackgroundColor { get; private set; }
		public static WpfBrush TitleBackgroundBrush { get; private set; }
		public static WpfColor TitleBackgroundColor { get; private set; }
		public static WpfBrush TitleTextBrush { get; private set; }
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

		public static GdiColor GetGdiColor(this ThemeResourceKey resourceKey) {
			return VSColorTheme.GetThemedColor(resourceKey);
		}
		public static WpfColor GetWpfColor(this ThemeResourceKey resourceKey) {
			return resourceKey.GetGdiColor().ToWpfColor();
		}
		public static WpfBrush GetWpfBrush(this ThemeResourceKey resourceKey) {
			return new WpfBrush(resourceKey.GetWpfColor());
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
		public static void SetBackgroundForCrispImage(this System.Windows.DependencyObject target, WpfColor color) {
			ImageThemingUtilities.SetImageBackgroundColor(target, color);
		}

		internal static void RefreshThemeCache() {
			DocumentPageColor = CommonDocumentColors.PageColorKey.GetGdiColor();
			DocumentTextColor = CommonDocumentColors.PageTextColorKey.GetGdiColor();
			DocumentTextBrush = new WpfBrush(CommonDocumentColors.PageTextColorKey.GetWpfColor());
			ToolWindowBackgroundColor = EnvironmentColors.ToolWindowBackgroundColorKey.GetGdiColor();
			TitleBackgroundColor = EnvironmentColors.MainWindowActiveCaptionColorKey.GetWpfColor();
			TitleTextBrush = new WpfBrush(EnvironmentColors.MainWindowActiveCaptionTextBrushKey.GetWpfColor());
			TitleBackgroundBrush = new WpfBrush(TitleBackgroundColor);
			ToolTipTextBrush = new WpfBrush(EnvironmentColors.ButtonTextBrushKey.GetWpfColor());
			ToolTipBackgroundBrush = new WpfBrush(EnvironmentColors.ToolTipBrushKey.GetWpfColor());
			ToolWindowTextBrush = new WpfBrush(EnvironmentColors.ToolWindowTextBrushKey.GetWpfColor());
			ToolWindowBackgroundBrush = new WpfBrush(EnvironmentColors.ToolWindowBackgroundBrushKey.GetWpfColor());
			MenuTextBrush = new WpfBrush(EnvironmentColors.SystemMenuTextBrushKey.GetWpfColor());
			MenuBackgroundBrush = new WpfBrush(EnvironmentColors.SystemMenuBrushKey.GetWpfColor());
			MenuGlyphBackgroundBrush = new WpfBrush(EnvironmentColors.CommandBarMenuGlyphBrushKey.GetWpfColor());
			TextBoxBrush = new WpfBrush(CommonControlsColors.TextBoxTextBrushKey.GetWpfColor());
			TextBoxBackgroundBrush = new WpfBrush(CommonControlsColors.TextBoxBackgroundBrushKey.GetWpfColor());
			TextBoxBorderBrush = new WpfBrush(CommonControlsColors.TextBoxBorderBrushKey.GetWpfColor());
			SystemButtonFaceColor = EnvironmentColors.SystemButtonFaceColorKey.GetWpfColor();
			SystemThreeDFaceColor = EnvironmentColors.SystemThreeDFaceColorKey.GetWpfColor();
		}
	}
}
