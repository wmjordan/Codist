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
		// note: the following fields are sequence-critical here
		internal static readonly IEditorFormatMap DefaultEditorFormatMap = ServicesHelper.Instance.EditorFormatMap.GetEditorFormatMap("text");
		internal static readonly IClassificationFormatMap DefaultClassificationFormatMap = ServicesHelper.Instance.ClassificationFormatMap.GetClassificationFormatMap("text");
		static Dictionary<string, StyleBase> _SyntaxStyleCache = InitSyntaxStyleCache();
		internal static readonly Dictionary<IClassificationType, List<IClassificationType>> ClassificationTypeStore = InitClassificationTypes(_SyntaxStyleCache.Keys);

		static Dictionary<IClassificationType, TextFormattingRunProperties> _BackupFormattings = LoadFormattings(new Dictionary<IClassificationType, TextFormattingRunProperties>(80));
		static TextFormattingRunProperties _DefaultFormatting;

		internal static bool IdentifySymbolSource { get; private set; }

		public static TextFormattingRunProperties GetBackupFormatting(IClassificationType classificationType) {
			lock (_syncRoot) {
				return _BackupFormattings.TryGetValue(classificationType, out var r) ? r : null;
			}
		}
		public static IEnumerable<IClassificationType> GetBaseTypes(this IClassificationType classificationType) {
			var h = new HashSet<IClassificationType>();

			return GetBaseTypes(classificationType, h);

			IEnumerable<IClassificationType> GetBaseTypes(IClassificationType type, HashSet<IClassificationType> dedup) {
				foreach (var item in type.BaseTypes) {
					if (dedup.Add(item)) {
						yield return item;
						foreach (var c in GetBaseTypes(item, dedup)) {
							yield return c;
						}
					}
				}
			}
		}
		public static TextFormattingRunProperties GetOrSaveBackupFormatting(IClassificationType classificationType) {
			lock (_syncRoot) {
				if (_BackupFormattings.TryGetValue(classificationType, out var r)) {
					return r;
				}
				else {
					r = DefaultClassificationFormatMap.GetExplicitTextProperties(classificationType);
					_BackupFormattings.Add(classificationType, r);
					return r;
				}
			}
		}

		public static StyleBase GetStyle(string classificationType) {
			lock (_syncRoot) {
				return _SyntaxStyleCache.TryGetValue(classificationType, out var r) ? r : null;
			}
		}
		public static StyleBase GetOrCreateStyle(IClassificationType classificationType) {
			var c = classificationType.Classification;
			lock (_syncRoot) {
				if (_SyntaxStyleCache.TryGetValue(c, out var r)) {
					return r;
				}
				else {
					r = new SyntaxStyle(c);
					_SyntaxStyleCache.Add(c, r);
					return r;
				}
			}
		}
		public static IEnumerable<KeyValuePair<string, StyleBase>> GetStyles() {
			return _SyntaxStyleCache;
		}

		/// <summary>
		/// Get descendant <see cref="IClassificationType"/>s of a given <paramref name="classificationType"/>.
		/// </summary>
		public static IEnumerable<IClassificationType> GetSubTypes(this IClassificationType classificationType) {
			return GetSubTypes(classificationType, new HashSet<IClassificationType>());

			IEnumerable<IClassificationType> GetSubTypes(IClassificationType ct, HashSet<IClassificationType> dedup) {
				if (ClassificationTypeStore.TryGetValue(ct, out var subTypes)) {
					foreach (var t in subTypes) {
						if (dedup.Add(t)) {
							yield return t;
						}
					}
					foreach (var t in subTypes) {
						foreach (var tt in GetSubTypes(t, dedup)) {
							yield return tt;
						}
					}
				}
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
				DefaultClassificationFormatMap.BeginBatchUpdate();
				foreach (var item in _BackupFormattings) {
					DefaultClassificationFormatMap.SetTextProperties(item.Key, item.Value);
				}
				DefaultClassificationFormatMap.EndBatchUpdate();
				var cache = new Dictionary<string, StyleBase>(_SyntaxStyleCache.Count, StringComparer.OrdinalIgnoreCase);
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
					var formattings = new Dictionary<IClassificationType, TextFormattingRunProperties>(_BackupFormattings.Count);
					LoadFormattings(formattings);
					_BackupFormattings = formattings;
					_DefaultFormatting = defaultFormat;
				}
			}
			lock (_syncRoot) {
				UpdateIdentifySymbolSource(_SyntaxStyleCache);
			}
		}

		static Dictionary<IClassificationType, TextFormattingRunProperties> LoadFormattings(Dictionary<IClassificationType, TextFormattingRunProperties> formattings) {
			var m = DefaultClassificationFormatMap;
			foreach (var item in m.CurrentPriorityOrder) {
				if (item != null && _SyntaxStyleCache.ContainsKey(m.GetEditorFormatMapKey(item))) {
					formattings[item] = m.GetExplicitTextProperties(item);
				}
			}
			return formattings;
		}

		static Dictionary<string, StyleBase> InitSyntaxStyleCache() {
			var cache = new Dictionary<string, StyleBase>(100, StringComparer.OrdinalIgnoreCase);
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
			foreach (var styleName in Enum.GetNames(cs)) {
				var f = cs.GetField(styleName);
				var cso = styles.Find(i => i.Id == (int)f.GetValue(null));
				if (cso == null) {
					continue;
				}
				foreach (var item in f.GetCustomAttributes<ClassificationTypeAttribute>(false)) {
					var n = item.ClassificationTypeNames;
					if (String.IsNullOrWhiteSpace(n)) {
						continue;
					}
					var ct = service.GetClassificationType(n);
					if (ct != null) {
						styleCache[DefaultClassificationFormatMap.GetEditorFormatMapKey(ct)] = cso;
					}
				}
			}
		}

		static Dictionary<IClassificationType, List<IClassificationType>> InitClassificationTypes(ICollection<string> syntaxStyleCache) {
			var d = new Dictionary<IClassificationType, List<IClassificationType>>(syntaxStyleCache.Count);
			var cts = ServicesHelper.Instance.ClassificationTypeRegistry;
			foreach (var item in syntaxStyleCache) {
				var i = cts.GetClassificationType(item);
				if (i == null) {
					continue;
				}
				AddSelfAndBase(d, i);
			}
			AddSelfAndBase(d, cts.GetClassificationType(Constants.CodeSpecialPunctuation));
			return d;

			void AddSelfAndBase(Dictionary<IClassificationType, List<IClassificationType>> store, IClassificationType type) {
				if (store.TryGetValue(type, out var s) == false) {
					store.Add(type, new List<IClassificationType>());
				}
				foreach (var bt in type.BaseTypes) {
					if (store.TryGetValue(bt, out s) == false) {
						store.Add(bt, new List<IClassificationType> { type });
					}
					else if (s.Contains(type) == false) {
						s.Add(type);
					}
				}
			}
		}
	}

}
