using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Formatting;
using GdiColor = System.Drawing.Color;
using WpfColor = System.Windows.Media.Color;
using WpfColors = System.Windows.Media.Colors;
using WpfBrush = System.Windows.Media.Brush;
using FontStyle = System.Drawing.FontStyle;

namespace Codist.SyntaxHighlight
{
	static class FormatStore
	{
		static readonly object _syncRoot = new object();
		internal static readonly IEditorFormatMap DefaultEditorFormatMap = ServicesHelper.Instance.EditorFormatMap.GetEditorFormatMap("text");
		internal static readonly IClassificationFormatMap DefaultClassificationFormatMap = ServicesHelper.Instance.ClassificationFormatMap.GetClassificationFormatMap("text");
		static Dictionary<string, StyleBase> _SyntaxStyleCache = InitSyntaxStyleCache();
		static Dictionary<string, TextFormattingRunProperties> _BackupFormattings = LoadFormattings(new Dictionary<string, TextFormattingRunProperties>(80));
		static TextFormattingRunProperties _DefaultFormatting;

		internal static bool IdentifySymbolSource { get; private set; }

		public static TextFormattingRunProperties GetBackupFormatting(string classificationType) {
			lock (_syncRoot) {
				return _BackupFormattings.TryGetValue(classificationType, out var r) ? r : null;
			}
		}

		public static StyleBase GetStyle(string classificationType) {
			lock (_syncRoot) {
				return _SyntaxStyleCache.TryGetValue(classificationType, out var r) ? r : null;
			}
		}


		internal static void MixStyle(this StyleBase style, out FontStyle fontStyle, out GdiColor forecolor, out GdiColor backcolor) {
			forecolor = style.ForegroundOpacity > 0 ? ThemeHelper.DocumentTextColor.Alpha(style.ForegroundOpacity) : ThemeHelper.DocumentTextColor;
			backcolor = style.BackgroundOpacity > 0 ? ThemeHelper.DocumentPageColor.Alpha(style.BackgroundOpacity) : ThemeHelper.DocumentPageColor;
			fontStyle = style.GetFontStyle();
			if (style.ClassificationType == null) {
				return;
			}
			var p = DefaultClassificationFormatMap.GetRunProperties(style.ClassificationType);
			if (p == null) {
				return;
			}
			SolidColorBrush colorBrush;
			if (style.ForeColor.A == 0) {
				colorBrush = p.ForegroundBrushEmpty ? null : p.ForegroundBrush as SolidColorBrush;
				if (colorBrush != null) {
					forecolor = (style.ForegroundOpacity > 0 ? colorBrush.Color.Alpha(style.ForegroundOpacity) : colorBrush.Color).ToGdiColor();
				}
			}
			else {
				forecolor = style.AlphaForeColor.ToGdiColor();
			}
			if (style.BackColor.A == 0) {
				colorBrush = p.BackgroundBrushEmpty ? null : p.BackgroundBrush as SolidColorBrush;
				if (colorBrush != null) {
					backcolor = (style.BackgroundOpacity > 0 ? colorBrush.Color.Alpha(style.BackgroundOpacity) : colorBrush.Color).ToGdiColor();
				}
			}
			else {
				backcolor = style.AlphaBackColor.ToGdiColor();
			}
			if (p.BoldEmpty == false && p.Bold && style.Bold != false) {
				fontStyle |= FontStyle.Bold;
			}
			if (p.ItalicEmpty == false && p.Italic && style.Italic != false) {
				fontStyle |= FontStyle.Italic;
			}
			if (p.TextDecorationsEmpty == false) {
				foreach (var decoration in p.TextDecorations) {
					if (decoration.Location == System.Windows.TextDecorationLocation.Underline && style.Underline != false) {
						fontStyle |= FontStyle.Underline;
					}
					else if (decoration.Location == System.Windows.TextDecorationLocation.Strikethrough && style.Strikethrough != false) {
						fontStyle |= FontStyle.Strikeout;
					}
				}
			}
		}

		internal static FontStyle GetFontStyle(this StyleBase activeStyle) {
			var f = FontStyle.Regular;
			if (activeStyle.Bold == true) {
				f |= FontStyle.Bold;
			}
			if (activeStyle.Italic == true) {
				f |= FontStyle.Italic;
			}
			if (activeStyle.Underline == true) {
				f |= FontStyle.Underline;
			}
			if (activeStyle.Strikethrough == true) {
				f |= FontStyle.Strikeout;
			}
			return f;
		}

		static void ResetStyleCache() {
			lock (_syncRoot) {
				var cache = new Dictionary<string, StyleBase>(_SyntaxStyleCache.Count);
				LoadSyntaxStyleCache(cache);
				_SyntaxStyleCache = cache;
			}
		}

		static void UpdateFormatCache(object sender, EventArgs args) {
			var defaultFormat = DefaultClassificationFormatMap.DefaultTextProperties;
			if (_DefaultFormatting == null) {
				_DefaultFormatting = defaultFormat;
			}
			else if (_DefaultFormatting.ForegroundBrushSame(defaultFormat.ForegroundBrush) == false) {
				Debug.WriteLine("DefaultFormatting Changed");
				// theme changed
				lock (_syncRoot) {
					var formattings = new Dictionary<string, TextFormattingRunProperties>(_BackupFormattings.Count);
					LoadFormattings(formattings);
					_BackupFormattings = formattings;
					_DefaultFormatting = defaultFormat;
				}
			}
			lock (_syncRoot) {
				UpdateIdentifySymbolSource(_SyntaxStyleCache);
			}
		}

		static Dictionary<string, TextFormattingRunProperties> LoadFormattings(Dictionary<string, TextFormattingRunProperties> formattings) {
			var m = DefaultClassificationFormatMap;
			foreach (var item in m.CurrentPriorityOrder) {
				if (item != null && _SyntaxStyleCache.ContainsKey(item.Classification)) {
					formattings[item.Classification] = m.GetExplicitTextProperties(item);
				}
			}
			return formattings;
		}

		static Dictionary<string, StyleBase> InitSyntaxStyleCache() {
			var cache = new Dictionary<string, StyleBase>(100);
			LoadSyntaxStyleCache(cache);
			Config.Loaded += (s, args) => ResetStyleCache();
			DefaultClassificationFormatMap.ClassificationFormatMappingChanged += UpdateFormatCache;
			return cache;
		}

		static void LoadSyntaxStyleCache(Dictionary<string, StyleBase> cache) {
			var service = ServicesHelper.Instance.ClassificationTypeRegistry;
			InitStyleClassificationCache<CodeStyleTypes, CodeStyle>(cache, service, Config.Instance.GeneralStyles);
			InitStyleClassificationCache<CommentStyleTypes, CommentStyle>(cache, service, Config.Instance.CommentStyles);
			InitStyleClassificationCache<CppStyleTypes, CppStyle>(cache, service, Config.Instance.CppStyles);
			InitStyleClassificationCache<CSharpStyleTypes, CSharpStyle>(cache, service, Config.Instance.CodeStyles);
			InitStyleClassificationCache<MarkdownStyleTypes, MarkdownStyle>(cache, service, Config.Instance.MarkdownStyles);
			InitStyleClassificationCache<XmlStyleTypes, XmlCodeStyle>(cache, service, Config.Instance.XmlCodeStyles);
			InitStyleClassificationCache<SymbolMarkerStyleTypes, SymbolMarkerStyle>(cache, service, Config.Instance.SymbolMarkerStyles);
			UpdateIdentifySymbolSource(cache);
		}

		static void UpdateIdentifySymbolSource(Dictionary<string, StyleBase> cache) {
			StyleBase style;
			IdentifySymbolSource = cache.TryGetValue(Constants.CSharpMetadataSymbol, out style) && style.IsSet
					|| cache.TryGetValue(Constants.CSharpUserSymbol, out style) && style.IsSet;
		}

		static void InitStyleClassificationCache<TStyleEnum, TCodeStyle>(Dictionary<string, StyleBase> styleCache, IClassificationTypeRegistryService service, List<TCodeStyle> styles)
			where TCodeStyle : StyleBase {
			var cs = typeof(TStyleEnum);
			var codeStyles = Enum.GetNames(cs);
			foreach (var styleName in codeStyles) {
				var f = cs.GetField(styleName);
				var cso = styles.Find(i => i.Id == (int)f.GetValue(null));
				if (cso == null) {
					continue;
				}
				var cts = f.GetCustomAttributes<ClassificationTypeAttribute>(false);
				foreach (var item in cts) {
					var n = item.ClassificationTypeNames;
					if (String.IsNullOrWhiteSpace(n)) {
						continue;
					}
					var ct = service.GetClassificationType(n);
					if (ct != null) {
						styleCache[ct.Classification] = cso;
					}
				}
			}
		}
	}

}
