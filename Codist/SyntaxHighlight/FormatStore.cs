using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Media;
using AppHelpers;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Utilities;

namespace Codist.SyntaxHighlight
{
	static class FormatStore
	{
		static readonly object __SyncRoot = new object();

		#region sequence-critical static fields
		static readonly Func<string, IClassificationType> __GetClassificationType = ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationType;

		static Dictionary<string, StyleBase> __SyntaxStyleCache = InitSyntaxStyleCache();

		static readonly Dictionary<IClassificationType, List<IClassificationType>> __ClassificationTypeStore = InitClassificationTypes(__SyntaxStyleCache.Keys);

		static readonly HashSet<Highlighter> __Highlighters = new HashSet<Highlighter>();
		#endregion

		internal static bool IdentifySymbolSource { get; private set; }

		public static void Highlight(IWpfTextView view) {
			var category = view.Options.GetOptionValue(DefaultWpfViewOptions.AppearanceCategory);
			var efm = ServicesHelper.Instance.EditorFormatMap.GetEditorFormatMap(category);
			var cfm = ServicesHelper.Instance.ClassificationFormatMap.GetClassificationFormatMap(category);
			var highlighter = new Highlighter(category, efm, cfm);
			if (__Highlighters.Add(highlighter)) {
				// since both EFM and CFM are shared between views with the same category,
				//   we can avoid redundantly applying highlight styles to the same kinds of views
				//   by caching applied categories
				$"[{category}] highlighter created".Log();
				highlighter.SubscribeConfigUpdateHandler();
				highlighter.SubscribeFormatMappingChanges();
				highlighter.Apply();
				highlighter.Refresh();
			}
		}

		public static void Refresh() {
			foreach (var item in __Highlighters) {
				item.Refresh();
			}
		}

		public static StyleBase GetOrCreateStyle(IClassificationType classificationType) {
			var c = classificationType.Classification;
			lock (__SyncRoot) {
				if (__SyntaxStyleCache.TryGetValue(c, out var r)) {
					return r;
				}
				r = new SyntaxStyle(c);
				__SyntaxStyleCache.Add(c, r);
				return r;
			}
		}
		public static IReadOnlyDictionary<string, StyleBase> GetStyles() {
			return __SyntaxStyleCache;
		}

		/// <summary>
		/// Get descendant <see cref="IClassificationType"/>s of a given <paramref name="classificationType"/>.
		/// </summary>
		public static IEnumerable<IClassificationType> GetSubTypes(this IClassificationType classificationType) {
			return GetSubTypes(classificationType, new HashSet<IClassificationType>());

			IEnumerable<IClassificationType> GetSubTypes(IClassificationType ct, HashSet<IClassificationType> dedup) {
				if (__ClassificationTypeStore.TryGetValue(ct, out var subTypes)) {
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

		public static void Reset() {
			foreach (var item in __Highlighters) {
				item.Reset();
			}
			ResetStyleCache();
		}

		public static void Reset(string classificationType) {
			foreach (var item in __Highlighters) {
				item.Reset(classificationType);
			}
		}

		static void ResetStyleCache() {
			lock (__SyncRoot) {
				var cache = new Dictionary<string, StyleBase>(__SyntaxStyleCache.Count, StringComparer.OrdinalIgnoreCase);
				LoadSyntaxStyleCache(cache);
				__SyntaxStyleCache = cache;
			}
		}

		static Dictionary<string, StyleBase> InitSyntaxStyleCache() {
			var cache = new Dictionary<string, StyleBase>(100, StringComparer.OrdinalIgnoreCase);
			LoadSyntaxStyleCache(cache);
			Config.RegisterLoadHandler((config) => ResetStyleCache());
			return cache;
		}

		static void LoadSyntaxStyleCache(Dictionary<string, StyleBase> cache) {
			InitStyleClassificationCache<CodeStyleTypes, CodeStyle>(cache, Config.Instance.GeneralStyles);
			InitStyleClassificationCache<CommentStyleTypes, CommentStyle>(cache, Config.Instance.CommentStyles);
			InitStyleClassificationCache<CppStyleTypes, CppStyle>(cache, Config.Instance.CppStyles);
			InitStyleClassificationCache<CSharpStyleTypes, CSharpStyle>(cache, Config.Instance.CodeStyles);
			InitStyleClassificationCache<MarkdownStyleTypes, MarkdownStyle>(cache, Config.Instance.MarkdownStyles);
			InitStyleClassificationCache<XmlStyleTypes, XmlCodeStyle>(cache, Config.Instance.XmlCodeStyles);
			InitStyleClassificationCache<SymbolMarkerStyleTypes, SymbolMarkerStyle>(cache, Config.Instance.SymbolMarkerStyles);
			var styles = Config.Instance.Styles;
			if (styles != null) {
				foreach (var item in styles) {
					if (item == null || String.IsNullOrEmpty(item.ClassificationType)) {
						continue;
					}
					var c = __GetClassificationType(item.ClassificationType);
					if (c != null) {
						cache[item.ClassificationType] = item;
					}
				}
				Config.Instance.Styles = null;
			}
			UpdateIdentifySymbolSource(cache);
		}

		static void UpdateIdentifySymbolSource(Dictionary<string, StyleBase> cache) {
			StyleBase style;
			IdentifySymbolSource = cache.TryGetValue(Constants.CSharpMetadataSymbol, out style) && style.IsSet
				|| cache.TryGetValue(Constants.CSharpUserSymbol, out style) && style.IsSet;
		}

		static void InitStyleClassificationCache<TStyleEnum, TCodeStyle>(Dictionary<string, StyleBase> styleCache, List<TCodeStyle> styles)
			where TCodeStyle : StyleBase {
			var cs = typeof(TStyleEnum);
			foreach (var f in cs.GetFields(BindingFlags.Public | BindingFlags.Static)) {
				var styleId = (int)f.GetValue(null);
				var cso = styles.Find(i => i.Id == styleId);
				if (cso == null) {
					continue;
				}
				foreach (var item in f.GetCustomAttributes<ClassificationTypeAttribute>(false)) {
					var n = item.ClassificationTypeNames;
					if (String.IsNullOrWhiteSpace(n)) {
						continue;
					}
					var ct = __GetClassificationType(n);
					if (ct != null) {
						styleCache[n] = cso;
					}
				}
			}
		}

		static Dictionary<IClassificationType, List<IClassificationType>> InitClassificationTypes(ICollection<string> syntaxStyleCache) {
			var d = new Dictionary<IClassificationType, List<IClassificationType>>(syntaxStyleCache.Count);
			foreach (var item in syntaxStyleCache) {
				var i = __GetClassificationType(item);
				if (i == null) {
					continue;
				}
				AddSelfAndBase(d, i);
			}
			AddSelfAndBase(d, __GetClassificationType(Constants.CodeBold));
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

		sealed class Highlighter : IEquatable<Highlighter>
		{
			readonly string _Category;
			readonly IEditorFormatMap _EditorFormatMap;
			readonly IClassificationFormatMap _ClassificationFormatMap;
			readonly Dictionary<string, ChangeTrace> _Traces = new Dictionary<string, ChangeTrace>();
			readonly Stack<string> _Formatters = new Stack<string>();
			FormatContext _Context;
			Color _EditorBackground;
			double _DefaultFontSize;
			int _Lock;

			public Highlighter(string category, IEditorFormatMap editorFormatMap, IClassificationFormatMap classificationFormatMap) {
				_Category = category;
				_EditorFormatMap = editorFormatMap;
				_ClassificationFormatMap = classificationFormatMap;
				_DefaultFontSize = classificationFormatMap.DefaultTextProperties.FontRenderingEmSize;
				_EditorBackground = editorFormatMap.GetProperties(Constants.EditorProperties.TextViewBackground).GetBackgroundColor();
			}

			public TextFormattingRunProperties DefaultTextProperties => _ClassificationFormatMap.DefaultTextProperties;

			public int FormattableItemCount {
				get {
					int c = 0;
					foreach (var item in _ClassificationFormatMap.CurrentPriorityOrder) {
						if (item != null) {
							++c;
						}
					}
					return c;
				}
			}

			// note: VS appears to have difficulty in merging semantic braces and some other styles
			//   by explicitly calling GetTextProperties then SetTextProperties,
			//   the underlying merging process will be called
			public void Refresh() {
				_Lock++;
				_Formatters.Push(nameof(Refresh));
				_EditorFormatMap.BeginBatchUpdate();
				try {
					foreach (var item in _ClassificationFormatMap.CurrentPriorityOrder) {
						if (item != null && _Traces.ContainsKey(item.Classification)) {
							_ClassificationFormatMap.SetTextProperties(item, _ClassificationFormatMap.GetTextProperties(item));
						}
					}
				}
				finally {
					_EditorFormatMap.EndBatchUpdate();
					_Formatters.Pop();
					_Lock--;
				}
			}

			public void Apply() {
				_Lock++;
				_Formatters.Push(nameof(Apply));
				_EditorFormatMap.BeginBatchUpdate();
				try {
					foreach (var item in _ClassificationFormatMap.CurrentPriorityOrder) {
						Highlight(item);
					}
				}
				finally {
					_EditorFormatMap.EndBatchUpdate();
					_Formatters.Pop();
					_Lock--;
				}
			}

			public void SwapPriority(string classificationX, string classificationY) {
				var x = __GetClassificationType(classificationX);
				var y = __GetClassificationType(classificationY);
				if (x != null && y != null) {
					_ClassificationFormatMap.SwapPriorities(x, y);
				}
			}

			public void SubscribeConfigUpdateHandler() {
				Config.RegisterUpdateHandler(UpdateHighlightAfterConfigurationChange);
			}
			public void UnsubscribeConfigUpdateHandler() {
				Config.UnregisterUpdateHandler(UpdateHighlightAfterConfigurationChange);
			}

			void UpdateHighlightAfterConfigurationChange(ConfigUpdatedEventArgs eventArgs) {
				if (eventArgs.UpdatedFeature.MatchFlags(Features.SyntaxHighlight)) {
					_Context = FormatContext.Config;
					if (eventArgs.Parameter is string t) {
						Highlight(t);
					}
					else {
						Apply();
					}
					_Context = FormatContext.None;
				}
			}

			public void SubscribeFormatMappingChanges() {
				_EditorFormatMap.FormatMappingChanged -= EditorFormatMappingChanged;
				_EditorFormatMap.FormatMappingChanged += EditorFormatMappingChanged;
			}
			public void UnsubscribeFormatMappingChanges() {
				_EditorFormatMap.FormatMappingChanged -= EditorFormatMappingChanged;
			}

			void EditorFormatMappingChanged(object sender, FormatItemsEventArgs e) {
				if (_Lock > 0) {
					$"[{_Category}] format changed {String.Join(", ", e.ChangedItems)}, blocked by {String.Join(".", _Formatters)}".Log();
					return;
				}
				_Formatters.Push("FormatChangedEvent");
				_Lock++;
				_Context = FormatContext.Event;
				var startedBatch = false;
				if (_EditorFormatMap.IsInBatchUpdate == false) {
					_EditorFormatMap.BeginBatchUpdate();
					startedBatch = true;
				}
				var currentBg = _EditorFormatMap.GetProperties(Constants.EditorProperties.TextViewBackground).GetBackgroundColor();
				var bgChanged = _EditorBackground != currentBg;
				if (bgChanged) {
					_EditorBackground = currentBg;
					$"[{_Category}] background changed".Log();
				}
				var currentFontSize = _ClassificationFormatMap.DefaultTextProperties.FontRenderingEmSize;
				var fsChanged = _DefaultFontSize != currentFontSize;
				if (fsChanged) {
					_DefaultFontSize = currentFontSize;
					$"[{_Category}] font size changed".Log();
				}
				bool needRefresh = false;
				try {
					var dedup = new HashSet<IClassificationType>();
					// ChangedItems collection is dynamic
					// cache the changes to prevent it from changing during the enumerating procedure
					foreach (var item in e.ChangedItems.ToList()) {
						HighlightRecursive(__GetClassificationType(item), dedup);
					}

					if (bgChanged || fsChanged) {
						foreach (var item in __SyntaxStyleCache) {
							if (bgChanged && item.Value.BackColor.A > 0
								|| fsChanged && item.Value.FontSize != 0) {
								HighlightRecursive(__GetClassificationType(item.Key), dedup);
								needRefresh = true;
							}
						}
					}

					if (needRefresh == false) {
						needRefresh = dedup.Contains(__GetClassificationType(Constants.CodeClassName));
					}
				}
				finally {
					_Context = FormatContext.None;
					if (startedBatch) {
						_EditorFormatMap.EndBatchUpdate();
					}
					_Formatters.Pop();
					if (needRefresh) {
						Refresh();
					}
					_Lock--;
				}
			}

			void HighlightRecursive(IClassificationType ct, HashSet<IClassificationType> dedup) {
				if (dedup.Add(ct)
					&& Highlight(ct).MatchFlags(FormatChanges.FontSize | FormatChanges.BackgroundBrush)
					&& __ClassificationTypeStore.TryGetValue(ct, out var subTypes)) {
					foreach (var subType in subTypes) {
						HighlightRecursive(subType, dedup);
					}
				}
			}

			FormatChanges Highlight(string classification) {
				if (classification is null) {
					return FormatChanges.None;
				}
				return Highlight(__GetClassificationType(classification));
			}

			FormatChanges Highlight(IClassificationType classification) {
				if (classification is null) {
					return FormatChanges.None;
				}
				var c = classification.Classification;
				__SyntaxStyleCache.TryGetValue(c, out var style);
				_Formatters.Push($"Highlight<{c}>");
				_Lock++;
				var current = _EditorFormatMap.GetProperties(_ClassificationFormatMap.GetEditorFormatMapKey(classification));
				var changes = FormatChanges.None;
				if (_Traces.TryGetValue(c, out var trace) == false) {
					if (style == null) {
						$"[{_Category}] skipped format {c}".Log();
						goto EXIT;
					}
					trace = new ChangeTrace(current);
					changes = trace.Change(current, style, this);
					if (changes != FormatChanges.None || trace.HasOriginalStyle) {
						$"[{_Category}] trace format <{c}> ({trace})".Log();
						_Traces[c] = trace;
					}
				}
				else if (_Context == FormatContext.Event) {
					// format mapping may change outside of Codist,
					// thus we should update the trace
					changes = trace.FormatChanges;
					// merge external changes to trace
					trace.Change(current, style, this);
					if (changes != trace.FormatChanges) {
						$"[{_Category}] update format trace <{c}> ({trace})".Log();
					}
				}
				else {
					$"[{_Category}] change format trace <{c}> ({trace})".Log();
					trace.Change(current, style, this);
				}
				_EditorFormatMap.SetProperties(_ClassificationFormatMap.GetEditorFormatMapKey(classification), current);
			EXIT:
				_Formatters.Pop();
				_Lock--;
				return changes;
			}

			public void Reset() {
				_Lock++;
				_Formatters.Push("Reset");
				var map = _EditorFormatMap;
				map.BeginBatchUpdate();
				try {
					foreach (var item in _ClassificationFormatMap.CurrentPriorityOrder) {
						if (item != null) {
							ResetTextProperties(map, item);
						}
					}
				}
				finally {
					map.EndBatchUpdate();
					_Formatters.Pop();
					_Lock--;
				}
			}

			public void Reset(string classificationType) {
				var t = __GetClassificationType(classificationType);
				if (t != null) {
					Reset(t);
				}
			}

			public void Reset(IClassificationType classificationType) {
				_Lock++;
				ResetTextProperties(_EditorFormatMap, classificationType);
				_Lock--;
			}

			void ResetTextProperties(IEditorFormatMap map, IClassificationType item) {
				var c = item.Classification;
				if (__SyntaxStyleCache.ContainsKey(c) == false) {
					return;
				}

				var formatKey = _ClassificationFormatMap.GetEditorFormatMapKey(item);
				var current = map.GetProperties(formatKey);
				var reset = Reset(c, current);
				if (reset != current) {
					map.SetProperties(formatKey, reset);
				}
			}

			ResourceDictionary Reset(string classification, ResourceDictionary format) {
				if (_Traces.TryGetValue(classification, out var trace)) {
					if (trace.FormatChanges == FormatChanges.None) {
						return format;
					}
					trace.Reset();
				}
				return trace?.Origin.Copy() ?? format;
			}

			bool IEquatable<Highlighter>.Equals(Highlighter other) {
				return other != null && _Category == other._Category;
			}

			public override int GetHashCode() {
				return _Category.GetHashCode();
			}

			public override bool Equals(object obj) {
				return obj is Highlighter other && _Category == other._Category;
			}

			public override string ToString() {
				return _Category;
			}

			static bool AreBrushesEqual(Brush x, Brush y) {
				if (x == y) {
					return true;
				}
				if (x == null || y == null) {
					return false;
				}
				if (x.Opacity == 0.0 && y.Opacity == 0.0) {
					return true;
				}

				if (x is SolidColorBrush bx && y is SolidColorBrush by) {
					return bx.Color.A == 0 && by.Color.A == 0
						|| bx.Color == by.Color && Math.Abs(bx.Opacity - by.Opacity) < 0.01;
				}
				return x.Equals(y);
			}

			static bool AreTypefaceEqual(Typeface x, Typeface y) {
				if (x == y) {
					return true;
				}
				return x != null && y != null
					&& x.FontFamily.ToString() == y.FontFamily.ToString()
					&& x.Stretch.Equals(y.Stretch)
					&& x.Weight.Equals(y.Weight)
					&& x.Style.Equals(y.Style);
			}

			/// <summary>
			/// Records what has been done to the format.
			/// </summary>
			sealed class ChangeTrace
			{
				FormatChanges _FormatChanges;
				/// <summary>
				/// Changes made by Codist
				/// </summary>
				public readonly ResourceDictionary Changes;
				/// <summary>
				/// External properties, set from outside of Codist
				/// </summary>
				public readonly ResourceDictionary Origin;

				public ChangeTrace(ResourceDictionary origin) {
					Origin = origin.Copy();
					Changes = new ResourceDictionary();
				}

				/// <summary>
				/// Gets a quick summary of changes made by Codist
				/// </summary>
				public FormatChanges FormatChanges => _FormatChanges;
				/// <summary>
				/// Check whether there's any property defined outside of Codist
				/// </summary>
				public bool HasOriginalStyle => Origin.Count > 0;

				/// <summary>
				/// <para>Changes properties of <paramref name="current"/>, according to <paramref name="style"/>.</para>
				/// <para>Change method takes the following pattern:</para>
				/// <list type="number">
				/// <item><para>change the format if it is specified from style, and record the changes</para></item>
				/// <item><para>if the format is not specified from style, but previously was, then revert it to the original and erase the changes</para></item>
				/// <item><para>if the format is not specified from style, but changed externally, update the format in Origin</para></item>
				/// </list>
				/// </summary>
				/// <param name="current">The properties to be changed.</param>
				/// <param name="style">The style customized from Codist.</param>
				/// <param name="highlighter">The highlighter providing global settings (background, default font size, etc.)</param>
				/// <returns>What's changed</returns>
				public FormatChanges Change(ResourceDictionary current, StyleBase style, Highlighter highlighter) {
					ChangeBold(current, style);
					ChangeItalic(current, style);
					ChangeBrush(current, style);
					ChangeBackgroundBrush(current, style, highlighter);
					ChangeOpacity(current, style);
					ChangeBackgroundOpacity(current, style);
					ChangeFontSize(current, style, highlighter);
					ChangeTypeface(current, style);
					ChangeTextDecorations(current, style);
					return _FormatChanges;
				}

				#region ResourceDictionary alteration methods
				void ChangeBold(ResourceDictionary current, StyleBase style) {
					bool? s;
					if (style?.Bold != null) {
						if ((s = style.Bold) != current.GetBold()) {
							_FormatChanges |= FormatChanges.Bold;
							Changes.SetBold(s);
							current.SetBold(s);
						}
					}
					else if (_FormatChanges.MatchFlags(FormatChanges.Bold)) {
						_FormatChanges ^= FormatChanges.Bold;
						Changes.SetBold(null);
						current.SetBold(Origin.GetBold());
					}
					else if ((s = current.GetBold()) != Origin.GetBold()) {
						Origin.SetBold(s);
					}
				}

				void ChangeItalic(ResourceDictionary current, StyleBase style) {
					bool? s;
					if (style?.Italic != null) {
						if ((s = style.Italic) != current.GetItalic()) {
							_FormatChanges |= FormatChanges.Italic;
							Changes.SetItalic(s);
							current.SetItalic(s);
						}
					}
					else if (_FormatChanges.MatchFlags(FormatChanges.Italic)) {
						_FormatChanges ^= FormatChanges.Italic;
						Changes.SetItalic(null);
						current.SetItalic(Origin.GetItalic());
					}
					else if ((s = current.GetItalic()) != Origin.GetItalic()) {
						Origin.SetItalic(s);
					}
				}

				void ChangeBrush(ResourceDictionary current, StyleBase style) {
					Brush b;
					if (style != null && style.ForeColor.A != 0) {
						if (AreBrushesEqual(b = style.MakeBrush(), current.GetBrush()) == false) {
							_FormatChanges |= FormatChanges.ForegroundBrush;
							Changes.SetBrush(b);
							current.SetBrush(b);
						}
					}
					else if (_FormatChanges.MatchFlags(FormatChanges.ForegroundBrush)) {
						_FormatChanges ^= FormatChanges.ForegroundBrush;
						Changes.SetBrush(null);
						current.SetBrush(Origin.GetBrush());
					}
					else if (AreBrushesEqual(b = current.GetBrush(), Origin.GetBrush()) == false) {
						Origin.SetBrush(b);
					}
				}

				void ChangeBackgroundBrush(ResourceDictionary current, StyleBase style, Highlighter highlighter) {
					Brush b;
					if (style != null && style.BackColor.A != 0) {
						b = style.MakeBackgroundBrush(highlighter._EditorFormatMap.GetColor(Constants.EditorProperties.TextViewBackground, EditorFormatDefinition.BackgroundColorId));
						if (AreBrushesEqual(b, current.GetBackgroundBrush()) == false) {
							_FormatChanges |= FormatChanges.BackgroundBrush;
							Changes.SetBackgroundBrush(b);
							current.SetBackgroundBrush(b);
						}
					}
					else if (_FormatChanges.MatchFlags(FormatChanges.BackgroundBrush)) {
						_FormatChanges ^= FormatChanges.BackgroundBrush;
						Changes.SetBackgroundBrush(null);
						current.SetBackgroundBrush(Origin.GetBackgroundBrush());
					}
					else if (AreBrushesEqual(b = current.GetBackgroundBrush(), Origin.GetBackgroundBrush()) == false) {
						Origin.SetBackgroundBrush(b);
					}
				}

				void ChangeOpacity(ResourceDictionary current, StyleBase style) {
					double o;
					if (style != null && style.ForegroundOpacity != 0) {
						if ((o = style.ForegroundOpacity / 255d) != current.GetOpacity()) {
							_FormatChanges |= FormatChanges.ForegroundOpacity;
							Changes.SetOpacity(o);
							current.SetOpacity(o);
						}
					}
					else if (_FormatChanges.MatchFlags(FormatChanges.ForegroundOpacity)) {
						_FormatChanges ^= FormatChanges.ForegroundOpacity;
						Changes.SetOpacity(0);
						current.SetOpacity(Origin.GetOpacity());
					}
					else if ((o = current.GetOpacity()) != Origin.GetOpacity()) {
						Origin.SetOpacity(o);
					}
				}

				void ChangeBackgroundOpacity(ResourceDictionary current, StyleBase style) {
					double o;
					if (style != null && style.BackgroundOpacity != 0) {
						if ((o = style.BackgroundOpacity / 255d) != current.GetBackgroundOpacity()) {
							_FormatChanges |= FormatChanges.BackgroundOpacity;
							Changes.SetBackgroundOpacity(o);
							current.SetBackgroundOpacity(o);
						}
					}
					else if (_FormatChanges.MatchFlags(FormatChanges.BackgroundOpacity)) {
						_FormatChanges ^= FormatChanges.BackgroundOpacity;
						Changes.SetBackgroundOpacity(0);
						current.SetBackgroundOpacity(Origin.GetBackgroundOpacity());
					}
					else if ((o = current.GetBackgroundOpacity()) != Origin.GetBackgroundOpacity()) {
						Origin.SetBackgroundOpacity(o);
					}
				}

				void ChangeFontSize(ResourceDictionary current, StyleBase style, Highlighter highlighter) {
					double? s;
					if (style != null && style.FontSize != 0) {
						s = highlighter._DefaultFontSize + style.FontSize;
						if (s != current.GetFontSize()) {
							_FormatChanges |= FormatChanges.FontSize;
							Changes.SetFontSize(s);
							current.SetFontSize(s);
						}
					}
					else if (_FormatChanges.MatchFlags(FormatChanges.FontSize)) {
						_FormatChanges ^= FormatChanges.FontSize;
						Changes.SetFontSize(null);
						current.SetFontSize(Origin.GetFontSize());
					}
					else if ((s = current.GetFontSize()) != Origin.GetFontSize()) {
						Origin.SetFontSize(s);
					}
				}

				void ChangeTypeface(ResourceDictionary current, StyleBase style) {
					Typeface ct;
					if (style != null && String.IsNullOrEmpty(style.Font) == false) {
						ct = style.MakeTypeface();
						if (ct != null && AreTypefaceEqual(current.GetTypeface(), ct) == false) {
							_FormatChanges |= FormatChanges.Typeface;
							Changes.SetTypeface(ct);
							current.SetTypeface(ct);
						}
					}
					else if (_FormatChanges.MatchFlags(FormatChanges.Typeface)) {
						_FormatChanges ^= FormatChanges.Typeface;
						Changes.SetTypeface(null);
						current.SetTypeface(Origin.GetTypeface());
					}
					else if ((ct = current.GetTypeface()) != Origin.GetTypeface()) {
						Origin.SetTypeface(ct);
					}
				}

				void ChangeTextDecorations(ResourceDictionary current, StyleBase style) {
					TextDecorationCollection td;
					if (style?.HasLine == true) {
						var t = style.MakeTextDecorations();
						if (t != (td = current.GetTextDecorations())
							&& (td == null || t?.SequenceEqual(td) != true)) {
							_FormatChanges |= FormatChanges.TextDecorations;
							Changes.SetTextDecorations(t);
							current.SetTextDecorations(t);
						}
					}
					else if (_FormatChanges.MatchFlags(FormatChanges.TextDecorations)) {
						_FormatChanges ^= FormatChanges.TextDecorations;
						Changes.SetTextDecorations(null);
						current.SetTextDecorations(Origin.GetTextDecorations());
					}
					else if ((td = current.GetTextDecorations()) != Origin.GetTextDecorations()) {
						Origin.SetTextDecorations(td);
					}
				}
				#endregion

				public void Reset() {
					Changes.Clear();
					_FormatChanges = 0;
				}

				public override string ToString() {
					using (var sbr = ReusableStringBuilder.AcquireDefault(30)) {
						var sb = sbr.Resource;
						Print(Origin, sb);
						if (Changes != null) {
							sb.Append("=>");
							Print(Changes, sb);
						}
						return sb.ToString();
					}
				}

				static void Print(ResourceDictionary resource, StringBuilder sb) {
					sb.Append('[');
					foreach (var key in resource.Keys) {
						var val = resource[key];
						switch (key.ToString()) {
							case ClassificationFormatDefinition.ForegroundBrushId:
								sb.Append(val).Append("@f");
								break;
							case ClassificationFormatDefinition.ForegroundColorId:
								if (resource.Contains(ClassificationFormatDefinition.ForegroundBrushId)) {
									break;
								}
								goto case ClassificationFormatDefinition.ForegroundBrushId;
							case ClassificationFormatDefinition.IsBoldId:
								sb.Append((bool)val ? "+B" : "-B");
								break;
							case ClassificationFormatDefinition.IsItalicId:
								sb.Append((bool)val ? "+I" : "-I");
								break;
							case ClassificationFormatDefinition.BackgroundBrushId:
								sb.Append(val).Append("@b");
								break;
							case ClassificationFormatDefinition.BackgroundColorId:
								if (resource.Contains(ClassificationFormatDefinition.BackgroundBrushId)) {
									break;
								}
								goto case ClassificationFormatDefinition.BackgroundBrushId;
							case ClassificationFormatDefinition.FontRenderingSizeId:
								sb.Append(val).Append('#');
								break;
							case ClassificationFormatDefinition.TypefaceId:
								sb.Append(((Typeface)val).FontFamily.ToString());
								break;
							case ClassificationFormatDefinition.ForegroundOpacityId:
								sb.Append('%').Append(((double)val).ToString("0.0#")).Append('f');
								break;
							case ClassificationFormatDefinition.BackgroundOpacityId:
								sb.Append('%').Append(((double)val).ToString("0.0#")).Append('b');
								break;
							case ClassificationFormatDefinition.TextDecorationsId:
								sb.Append("TD");
								break;
							default:
								sb.Append(key).Append('=').Append(resource[key]).Append(';');
								break;
						}
					}
					sb.Append(']');
				}
			}

			enum FormatContext
			{
				None,
				Init,
				Config,
				Event,
				Reset
			}

			[Flags]
			enum FormatChanges
			{
				None,
				Typeface = 1,
				FontSize = 1 << 1,
				Bold = 1 << 2,
				Italic = 1 << 3,
				ForegroundBrush = 1 << 4,
				ForegroundOpacity = 1 << 5,
				BackgroundBrush = 1 << 6,
				BackgroundOpacity = 1 << 7,
				TextDecorations = 1 << 8,
			}
		}
	}
}
