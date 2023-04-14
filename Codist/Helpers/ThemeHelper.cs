using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Controls;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Classification;
using GdiColor = System.Drawing.Color;
using WpfBrush = System.Windows.Media.SolidColorBrush;
using WpfColor = System.Windows.Media.Color;
using WpfFontFamily = System.Windows.Media.FontFamily;

namespace Codist
{
	static class ThemeHelper
	{
		internal const int DefaultIconSize = 16;
		internal const int MiddleIconSize = 24;
		internal const int LargeIconSize = 32;
		internal const int XLargeIconSize = 48;

		static readonly IClassificationFormatMap __ToolTipFormatMap = ServicesHelper.Instance.ClassificationFormatMap.GetClassificationFormatMap("tooltip");
		static readonly IClassificationFormatMap __TextFormatMap = ServicesHelper.Instance.ClassificationFormatMap.GetClassificationFormatMap(Constants.CodeText);
		static ThemeHelper() {
			RefreshThemeCache();
			__ToolTipFormatMap.ClassificationFormatMappingChanged += UpdateToolTipFormatMap;
			__TextFormatMap.ClassificationFormatMappingChanged += UpdateTextFormatMap;
			VSColorTheme.ThemeChanged += _ => {
				System.Diagnostics.Debug.WriteLine("Theme changed.");
				RefreshThemeCache();
			};
		}

		public static System.Windows.Media.Brush EditorBackground => ServicesHelper.Instance.EditorFormatMap.GetEditorFormatMap("text").GetProperties(Constants.EditorProperties.TextViewBackground).GetBrush(EditorFormatDefinition.BackgroundBrushId);
		public static WpfFontFamily CodeTextFont { get; private set; }
		public static GdiColor DocumentPageColor { get; private set; }
		public static WpfBrush DocumentPageBrush { get; private set; }
		public static GdiColor DocumentTextColor { get; private set; }
		public static WpfBrush DocumentTextBrush { get; private set; }
		public static WpfBrush FileTabProvisionalSelectionBrush { get; private set; }
		public static GdiColor ToolWindowBackgroundColor { get; private set; }
		public static WpfColor TitleBackgroundColor { get; private set; }
		public static WpfBrush TitleTextBrush { get; private set; }
		public static WpfBrush ToolTipTextBrush { get; private set; }
		public static WpfBrush ToolTipBackgroundBrush { get; private set; }
		public static WpfBrush ToolWindowTextBrush { get; private set; }
		public static WpfBrush ToolWindowBackgroundBrush { get; private set; }
		public static WpfBrush MenuTextBrush { get; private set; }
		public static WpfBrush MenuBackgroundBrush { get; private set; }
		public static WpfBrush MenuHoverBorderBrush { get; private set; }
		public static WpfBrush MenuHoverBackgroundBrush { get; private set; }
		public static WpfColor MenuHoverBackgroundColor { get; private set; }
		public static WpfBrush MenuGlyphBackgroundBrush { get; private set; }
		public static WpfBrush TextBoxBrush { get; private set; }
		public static WpfBrush TextBoxBackgroundBrush { get; private set; }
		public static WpfBrush TextBoxBorderBrush { get; private set; }
		public static WpfBrush TextSelectionHighlightBrush { get; private set; }
		public static WpfColor SystemButtonFaceColor { get; private set; }
		public static WpfColor SystemThreeDFaceColor { get; private set; }
		public static WpfBrush SystemGrayTextBrush { get; private set; }
		public static WpfFontFamily ToolTipFont { get; private set; }
		public static double ToolTipFontSize { get; private set; }

		#region Colors and brushes
		public static System.Windows.Media.Brush GetAnyBrush(this IEditorFormatMap formatMap, params string[] formatNames) {
			foreach (var item in formatNames) {
				var r = formatMap.GetProperties(item);
				var b = r.GetBrush();
				if (b != null) {
					return b;
				}
			}
			return null;
		}
		public static GdiColor GetGdiColor(this ThemeResourceKey resourceKey) {
			return VSColorTheme.GetThemedColor(resourceKey);
		}
		public static WpfColor GetWpfColor(this ThemeResourceKey resourceKey) {
			return resourceKey.GetGdiColor().ToWpfColor();
		}
		public static WpfBrush GetWpfBrush(this ThemeResourceKey resourceKey) {
			return new WpfBrush(resourceKey.GetWpfColor());
		}
		#endregion

		public static void GetFontSettings(string categoryGuid, out string fontName, out int fontSize) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var storage = ServicesHelper.Get<IVsFontAndColorStorage, SVsFontAndColorStorage>();
			if (storage == null) {
				goto EXIT;
			}
			var pLOGFONT = new LOGFONTW[1];
			var pInfo = new FontInfo[1];

			ErrorHandler.ThrowOnFailure(storage.OpenCategory(new Guid(categoryGuid), (uint)(__FCSTORAGEFLAGS.FCSF_LOADDEFAULTS | __FCSTORAGEFLAGS.FCSF_PROPAGATECHANGES)));
			try {
				if (ErrorHandler.Succeeded(storage.GetFont(pLOGFONT, pInfo))) {
					fontName = pInfo[0].bstrFaceName;
					fontSize = pInfo[0].wPointSize;
					return;
				}
			}
			finally {
				storage.CloseCategory();
			}
			EXIT:
			fontName = null;
			fontSize = 0;
		}

		#region CrispImage
		/// <summary>
		/// Gets a themed <see cref="Image"/> from a value defined in <see cref="KnownImageIds"/>
		/// </summary>
		/// <param name="imageId">The image id.</param>
		public static CrispImage GetImage(int imageId, int size = 0) {
			var moniker = new ImageMoniker {
				Guid = KnownImageIds.ImageCatalogGuid,
				Id = imageId
			};
			if (size < 1) {
				size = DefaultIconSize;
			}
			return new CrispImage {
				Moniker = moniker,
				Height = size,
				Width = size,
			};
		}
		public static CrispImage GetImage(string monikerName, int size = 0) {
			return GetImage(KnownMonikerNameMap.Map.TryGetValue(monikerName, out int i) ? i : KnownImageIds.Blank, size);
		}
		public static void SetBackgroundForCrispImage(this System.Windows.DependencyObject target, WpfColor color) {
			ImageThemingUtilities.SetImageBackgroundColor(target, color);
		}

		public static TControl ReferenceCrispImageBackground<TControl>(this TControl target, ThemeResourceKey colorKey) where TControl : System.Windows.FrameworkElement {
			target.SetResourceReference(ImageThemingUtilities.ImageBackgroundColorProperty, colorKey);
			return target;
		}
		#endregion

		#region Cache
		internal static void RefreshThemeCache() {
			DocumentPageColor = CommonDocumentColors.PageColorKey.GetGdiColor();
			DocumentPageBrush = new WpfBrush(DocumentPageColor.ToWpfColor());
			DocumentTextColor = CommonDocumentColors.PageTextColorKey.GetGdiColor();
			DocumentTextBrush = new WpfBrush(DocumentTextColor.ToWpfColor());
			FileTabProvisionalSelectionBrush = EnvironmentColors.FileTabProvisionalSelectedActiveBrushKey.GetWpfBrush();
			ToolWindowBackgroundColor = EnvironmentColors.ToolWindowBackgroundColorKey.GetGdiColor();
			TitleBackgroundColor = EnvironmentColors.MainWindowActiveCaptionColorKey.GetWpfColor();
			TitleTextBrush = EnvironmentColors.MainWindowActiveCaptionTextBrushKey.GetWpfBrush();
			ToolTipBackgroundBrush = EnvironmentColors.ToolTipBrushKey.GetWpfBrush();
			ToolWindowTextBrush = EnvironmentColors.ToolWindowTextBrushKey.GetWpfBrush();
			ToolWindowBackgroundBrush = EnvironmentColors.ToolWindowBackgroundBrushKey.GetWpfBrush();
			MenuTextBrush = EnvironmentColors.SystemMenuTextBrushKey.GetWpfBrush();
			MenuBackgroundBrush = EnvironmentColors.SystemMenuBrushKey.GetWpfBrush();
			MenuHoverBorderBrush = EnvironmentColors.CommandBarMenuItemMouseOverBrushKey.GetWpfBrush();
			MenuHoverBackgroundBrush = EnvironmentColors.CommandBarMenuItemMouseOverBorderBrushKey.GetWpfBrush();
			MenuHoverBackgroundColor = EnvironmentColors.CommandBarMenuItemMouseOverBorderColorKey.GetWpfColor();
			MenuGlyphBackgroundBrush = EnvironmentColors.CommandBarMenuGlyphBrushKey.GetWpfBrush();
			TextBoxBrush = CommonControlsColors.TextBoxTextBrushKey.GetWpfBrush();
			TextBoxBackgroundBrush = CommonControlsColors.TextBoxBackgroundBrushKey.GetWpfBrush();
			TextBoxBorderBrush = CommonControlsColors.TextBoxBorderBrushKey.GetWpfBrush();
			TextSelectionHighlightBrush = CommonControlsColors.ComboBoxTextInputSelectionBrushKey.GetWpfBrush().Alpha(0.6);
			SystemButtonFaceColor = EnvironmentColors.SystemButtonFaceColorKey.GetWpfColor();
			SystemThreeDFaceColor = EnvironmentColors.SystemThreeDFaceColorKey.GetWpfColor();
			SystemGrayTextBrush = EnvironmentColors.SystemGrayTextBrushKey.GetWpfBrush();
			UpdateToolTipFormatMap(null, EventArgs.Empty);
			UpdateTextFormatMap(null, EventArgs.Empty);
		}

		static void UpdateToolTipFormatMap(object sender, EventArgs e) {
			var formatMap = __ToolTipFormatMap.DefaultTextProperties;
			ToolTipTextBrush = formatMap.ForegroundBrush as WpfBrush;
			ToolTipFont = formatMap.Typeface.FontFamily;
			ToolTipFontSize = formatMap.FontRenderingEmSize;
		}

		static void UpdateTextFormatMap(object sender, EventArgs e) {
			var formatMap = __TextFormatMap.DefaultTextProperties;
			CodeTextFont = formatMap.Typeface.FontFamily;
		}
		#endregion

		static class KnownMonikerNameMap
		{
			internal static readonly Dictionary<string, int> Map = CreateMap();

			static Dictionary<string, int> CreateMap() {
				var d = new Dictionary<string, int>(3760);
				var intType = typeof(int);
				foreach (var item in typeof(KnownImageIds).GetFields()) {
					if (item.FieldType == intType) {
						d.Add(item.Name, (int)item.GetValue(null));
					}
				}
				return d;
			}
		}
	}
}
