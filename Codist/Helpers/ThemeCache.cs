using System;
using System.Collections.Generic;
using Codist.SyntaxHighlight;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Classification;
using GdiColor = System.Drawing.Color;
using WpfBrush = System.Windows.Media.SolidColorBrush;
using WpfColor = System.Windows.Media.Color;
using WpfFontFamily = System.Windows.Media.FontFamily;
using Task = System.Threading.Tasks.Task;

namespace Codist
{
	static class ThemeCache
	{
		static readonly IClassificationFormatMap __EditorFormatMap = ServicesHelper.Instance.ClassificationFormatMap.GetClassificationFormatMap(Constants.CodeText);
		static readonly IClassificationFormatMap __ToolTipFormatMap = ServicesHelper.Instance.ClassificationFormatMap.GetClassificationFormatMap("tooltip");

		static ThemeCache() {
			RefreshThemeCache();
			__EditorFormatMap.ClassificationFormatMappingChanged += UpdateEditorFormatMap;
			__ToolTipFormatMap.ClassificationFormatMappingChanged += UpdateToolTipFormatMap;
			ThemeHelper.ThemeChanged += OnThemeChanged;
		}

		public static GdiColor DocumentPageColor { get; private set; }
		public static WpfBrush DocumentPageBrush { get; private set; }
		public static GdiColor DocumentTextColor { get; private set; }
		public static WpfBrush DocumentTextBrush { get; private set; }
		public static WpfBrush HyperlinkBrush { get; private set; }
		public static WpfBrush FileTabProvisionalSelectionBrush { get; private set; }
		public static WpfColor ToolWindowBackgroundColor { get; private set; }
		public static WpfColor TitleBackgroundColor { get; private set; }
		public static WpfBrush TitleTextBrush { get; private set; }
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
		public static WpfBrush ToolTipTextBrush { get; private set; }

		public static WpfFontFamily CodeTextFont { get; private set; }
		public static WpfFontFamily ToolTipFont { get; private set; }
		public static double ToolTipFontSize { get; private set; }
		public static double QuickInfoLargeIconSize { get; private set; }

		static void OnThemeChanged(object sender, EventArgs<KeyValuePair<Guid, string>> e) {
			RefreshThemeCache();
		}

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

		public static string GetEditorFont() {
			GetFontSettings(Microsoft.VisualStudio.Shell.Interop.FontsAndColorsCategory.TextEditor, out var fontName, out _);
			return fontName;
		}
		public static string GetOutputWindowFont() {
			GetFontSettings(Microsoft.VisualStudio.Shell.Interop.FontsAndColorsCategory.Outputwindow, out var fontName, out _);
			return fontName;
		}
		static void GetFontSettings(string categoryGuid, out string fontName, out int fontSize) {
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

		#region Cache
		static void RefreshThemeCache() {
			if (ThreadHelper.CheckAccess()) {
				RefreshThemeCacheSync();
				return;
			}
			"Theme cache refresh not on UI thread".Log();
			RefreshThemeCacheAsync().FireAndForget();
		}

		static void RefreshThemeCacheSync() {
			ThreadHelper.ThrowIfNotOnUIThread();
			DocumentPageColor = CommonDocumentColors.PageColorKey.GetGdiColor();
			DocumentPageBrush = new WpfBrush(DocumentPageColor.ToWpfColor());
			DocumentTextColor = CommonDocumentColors.PageTextColorKey.GetGdiColor();
			DocumentTextBrush = new WpfBrush(DocumentTextColor.ToWpfColor());
			HyperlinkBrush = CommonDocumentColors.HyperlinkBrushKey.GetWpfBrush();
			FileTabProvisionalSelectionBrush = EnvironmentColors.FileTabProvisionalSelectedActiveBrushKey.GetWpfBrush();
			TitleBackgroundColor = EnvironmentColors.MainWindowActiveCaptionColorKey.GetWpfColor();
			TitleTextBrush = EnvironmentColors.MainWindowActiveCaptionTextBrushKey.GetWpfBrush();
			ToolTipBackgroundBrush = EnvironmentColors.ToolTipBrushKey.GetWpfBrush();
			ToolWindowTextBrush = EnvironmentColors.ToolWindowTextBrushKey.GetWpfBrush();
			ToolWindowBackgroundBrush = EnvironmentColors.ToolWindowBackgroundBrushKey.GetWpfBrush();
			ToolWindowBackgroundColor = EnvironmentColors.ToolWindowBackgroundColorKey.GetWpfColor();
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
			UpdateEditorFormatMap(null, EventArgs.Empty);
			UpdateToolTipFormatMap();
			"Theme cache refreshed".Log();
		}

		static async Task RefreshThemeCacheAsync() {
			await SyncHelper.SwitchToMainThreadAsync(default);
			RefreshThemeCacheSync();
		}

		static void UpdateEditorFormatMap(object sender, EventArgs e) {
			CodeTextFont = __EditorFormatMap.DefaultTextProperties.Typeface.FontFamily;
		}

		static void UpdateToolTipFormatMap(object sender, EventArgs e) {
			UpdateToolTipFormatMap();
		}

		static void UpdateToolTipFormatMap() {
			var formatMap = __ToolTipFormatMap.DefaultTextProperties;
			ToolTipTextBrush = formatMap.ForegroundBrush as WpfBrush;
			ToolTipFont = formatMap.Typeface.FontFamily;
			ToolTipFontSize = formatMap.FontRenderingEmSize;
			QuickInfoLargeIconSize = VsImageHelper.LargeIconSize * ToolTipFontSize / 12;
		}
		#endregion
	}
}
