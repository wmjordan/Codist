using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Media;
using CLR;
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

		static readonly HashSet<Highlighter> __Highlighters = new HashSet<Highlighter>();

		static bool __HighlightEnabled;

		// caches IEditorFormatMap.Key and corresponding StyleBase
		// when accessed, the key should be provided by IClassificationFormatMap.GetEditorFormatMapKey
		static Dictionary<string, StyleBase> __SyntaxStyleCache = InitSyntaxStyleCache();

		static readonly Dictionary<IClassificationType, List<IClassificationType>> __ClassificationTypeStore = InitClassificationTypes(__SyntaxStyleCache.Keys);

		static readonly Dictionary<IClassificationType, bool> __ClassificationTypeFormattabilities = InitFormattableClassificationType();

		static readonly IClassificationFormatMap __DefaultClassificationFormatMap = ServicesHelper.Instance.ClassificationFormatMap.GetClassificationFormatMap(Constants.CodeText);
		#endregion

		/// <summary>
		/// Gets whether symbol source (user code or metadata code) is assigned with specific syntax style.
		/// </summary>
		internal static bool IdentifySymbolSource { get; private set; }

		internal static IClassificationFormatMap DefaultClassificationFormatMap => __DefaultClassificationFormatMap;

		internal static TextFormattingRunProperties EditorDefaultTextProperties => __DefaultClassificationFormatMap.DefaultTextProperties;

		/// <summary>
		/// Event for editor background color changes.
		/// Sender is <see cref="IFormatCache"/>.
		/// Parameter the new background color.
		/// </summary>
		internal static event EventHandler<EventArgs<Color>> EditorBackgroundChanged;

		/// <summary>
		/// Event for classification format map changes.
		/// Sender is <see cref="IClassificationFormatMap"/>.
		/// Parameter is a collection of changed classification types.
		/// </summary>
		internal static event EventHandler<EventArgs<IEnumerable<IClassificationType>>> ClassificationFormatMapChanged;

		/// <summary>
		/// Event for view default text properties changes.
		/// Sender is <see cref="IFormatCache"/>.
		/// Parameter is the default text format run properties.
		/// </summary>
		internal static event EventHandler<EventArgs<TextFormattingRunProperties>> DefaultTextPropertiesChanged;

		/// <summary>
		/// Event for format item changes.
		/// Sender is <see cref="IEditorFormatMap"/>.
		/// Parameter is changed format map keys.
		/// </summary>
		internal static event EventHandler<EventArgs<IReadOnlyList<string>>> FormatItemsChanged;

		public static void Highlight(IWpfTextView view) {
			Highlight(view.GetViewCategory());
		}

		static Highlighter Highlight(string category) {
			var efm = ServicesHelper.Instance.EditorFormatMap.GetEditorFormatMap(category);
			var cfm = ServicesHelper.Instance.ClassificationFormatMap.GetClassificationFormatMap(category);
			var highlighter = new Highlighter(category, efm, cfm);
			if (__Highlighters.Add(highlighter)) {
				// since both EFM and CFM are shared between views with the same category,
				//   we can avoid redundantly applying highlight styles to the same kinds of views
				//   by caching applied categories
				$"[{category}] highlighter created".Log();
				ServicesHelper.Instance.ClassificationTypeExporter.UpdateClassificationFormatMap(category);
				highlighter.SubscribeConfigUpdateHandler();
				highlighter.SubscribeFormatMappingChanges();
				highlighter.DetectThemeColorCompatibilityWithBackground();
				highlighter.Apply();
				highlighter.Refresh();
				return highlighter;
			}
			return null;
		}

		public static IFormatCache GetFormatCache(string category) {
			Highlight(category);
			foreach (var item in __Highlighters) {
				if (item.Category == category) {
					return item;
				}
			}
			return null;
		}

		public static bool IsFormattableClassificationType(this IClassificationType type) {
			return type != null
				&& (__ClassificationTypeFormattabilities.TryGetValue(type, out var f)
					? f
					: (__ClassificationTypeFormattabilities[type] = IsFormattable(type)));
		}

		/// <summary>
		/// Enforces syntax style precedencies.
		/// </summary>
		public static void Refresh() {
			foreach (var item in __Highlighters) {
				item.Refresh();
			}
		}
		public static StyleBase GetOrCreateStyle(IClassificationType classificationType, IClassificationFormatMap formatMap) {
			var c = formatMap.GetEditorFormatMapKey(classificationType);
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
		public static TextFormattingRunProperties GetRunPriorities(string classificationType) {
			var ct = __GetClassificationType(classificationType);
			return ct != null
				? __DefaultClassificationFormatMap.GetRunProperties(__DefaultClassificationFormatMap.GetEditorFormatMapKey(ct))
				: null;
		}

		public static void Reset() {
			foreach (var item in __Highlighters) {
				item.Reset();
			}
			__SyntaxStyleCache.Clear();
			IdentifySymbolSource = false;
		}

		public static void Reset(string classificationType) {
			foreach (var item in __Highlighters) {
				item.Reset(classificationType);
			}
		}

		static Dictionary<string, StyleBase> InitSyntaxStyleCache() {
			var cache = new Dictionary<string, StyleBase>(100, StringComparer.OrdinalIgnoreCase);
			__HighlightEnabled = Config.Instance.Features.MatchFlags(Features.SyntaxHighlight);
			LoadSyntaxStyleCache(cache, Config.Instance);
			Config.RegisterLoadHandler(ResetStyleCache);
			Config.RegisterUpdateHandler(FeatureToggle);
			ThemeHelper.ThemeChanged += (s, args) => {
				foreach (var item in __Highlighters) {
					item.Reset();
				}
			};
			return cache;
		}

		static void FeatureToggle(ConfigUpdatedEventArgs args) {
			bool enabled;
			if (args.UpdatedFeature == Features.SyntaxHighlight
				&& (enabled = Config.Instance.Features.MatchFlags(Features.SyntaxHighlight)) != __HighlightEnabled) {
				if (__HighlightEnabled = enabled) {
					foreach (var item in __Highlighters) {
						item.Apply();
						item.Refresh();
					}
				}
				else {
					foreach (var item in __Highlighters) {
						item.Reset();
					}
				}
			}
		}

		static Dictionary<IClassificationType, bool> InitFormattableClassificationType() {
			var u = new Dictionary<IClassificationType, bool>();
			foreach (var item in ServicesHelper.Instance.ClassificationFormatMap.GetClassificationFormatMap(Constants.CodeText).CurrentPriorityOrder) {
				if (item != null) {
					u.Add(item, IsFormattable(item));
				}
			}
			return u;
		}

		static bool IsFormattable(IClassificationType item) {
			string c;
			return (item != null)
				&& (c = item.Classification) != null
				&& c.StartsWith("Breakpoint", StringComparison.Ordinal) == false
				&& c.StartsWith("Logpoint", StringComparison.Ordinal) == false
				&& c.StartsWith("Tracepoint", StringComparison.Ordinal) == false
				&& c.StartsWith("Snappoint", StringComparison.Ordinal) == false
				&& c.StartsWith("Executing Thread IP", StringComparison.Ordinal) == false
				&& c.StartsWith("Current Statement", StringComparison.Ordinal) == false
				&& c.StartsWith("Call Return", StringComparison.Ordinal) == false
				&& c.EndsWith("{LegacyMarker}", StringComparison.Ordinal) == false
				&& c != "quickinfo-bold"
				&& c != "sighelp-documentation"
				&& c != "formal language"
				&& c != "natural language"
				&& c != "mismatched brace"
				&& c != Constants.CodeIdentifier // hack: workaround a VS bug that identifier takes abnormal precedency
				&& c.StartsWith("brace matching", StringComparison.Ordinal) == false;
		}

		static void ResetStyleCache(Config config) {
			lock (__SyncRoot) {
				var cache = new Dictionary<string, StyleBase>(__SyntaxStyleCache.Count);
				LoadSyntaxStyleCache(cache, config);
				__SyntaxStyleCache = cache;
			}
		}

		static void LoadSyntaxStyleCache(Dictionary<string, StyleBase> cache, Config config) {
			InitStyleClassificationCache<CodeStyleTypes, CodeStyle>(cache, config.GeneralStyles);
			InitStyleClassificationCache<CommentStyleTypes, CommentStyle>(cache, config.CommentStyles);
			InitStyleClassificationCache<CppStyleTypes, CppStyle>(cache, config.CppStyles);
			InitStyleClassificationCache<CSharpStyleTypes, CSharpStyle>(cache, config.CodeStyles);
			InitStyleClassificationCache<MarkdownStyleTypes, MarkdownStyle>(cache, config.MarkdownStyles);
			InitStyleClassificationCache<XmlStyleTypes, XmlCodeStyle>(cache, config.XmlCodeStyles);
			InitStyleClassificationCache<SymbolMarkerStyleTypes, SymbolMarkerStyle>(cache, config.SymbolMarkerStyles);
			var styles = config.Styles;
			if (styles != null) {
				var ct = ServicesHelper.Instance.ClassificationFormatMap.GetClassificationFormatMap(Constants.CodeText);
				foreach (var item in styles) {
					if (item == null || String.IsNullOrEmpty(item.ClassificationType)) {
						continue;
					}
					var c = __GetClassificationType(item.ClassificationType);
					if (c != null) {
						item.Key = ct.GetEditorFormatMapKey(c);
					}
					// WARN: compatible with previous versions
					cache[item.Key] = item;
				}
				config.Styles = null;
			}
			UpdateHighlightOptions(cache);
		}

		static void UpdateHighlightOptions(Dictionary<string, StyleBase> cache) {
			StyleBase style;
			IdentifySymbolSource = cache.TryGetValue(Constants.CSharpMetadataSymbol, out style) && style.IsSet
				|| cache.TryGetValue(Constants.CSharpUserSymbol, out style) && style.IsSet;
		}

		static void InitStyleClassificationCache<TStyleEnum, TCodeStyle>(Dictionary<string, StyleBase> styleCache, List<TCodeStyle> styles)
			where TCodeStyle : StyleBase {
			var cs = typeof(TStyleEnum);
			var cfm = ServicesHelper.Instance.ClassificationFormatMap.GetClassificationFormatMap(Constants.CodeText);
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
						styleCache[cfm.GetEditorFormatMapKey(ct)] = cso;
					}
					else {
						// WARN: compatible with previous versions
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

		sealed class Highlighter : IEquatable<Highlighter>, IFormatCache
		{
			readonly string _Category;
			readonly IEditorFormatMap _EditorFormatMap;
			readonly IClassificationFormatMap _ClassificationFormatMap;
			// traces changed IEditorFormatMap items;
			// key should obtain from IClassificationFormatMap.GetEditorFormatMapKey
			readonly Dictionary<string, ChangeTrace> _Traces = new Dictionary<string, ChangeTrace>();
			readonly Dictionary<IClassificationType, TextFormattingRunProperties> _PropertiesCache = new Dictionary<IClassificationType, TextFormattingRunProperties>(new ClassificationTypeComparer());
			readonly Dictionary<string, IClassificationType> _FormatClassificationTypes = new Dictionary<string, IClassificationType>();
			readonly Stack<string> _Formatters = new Stack<string>();
			readonly List<string> _ChangedFormatItems = new List<string>(7);
			readonly PendingChange _PendingChange = new PendingChange();
			FormatContext _Context;
			Color _ViewBackground;
			Typeface _DefaultTypeface;
			double _DefaultFontSize;
			int _Lock;

			public Highlighter(string category, IEditorFormatMap editorFormatMap, IClassificationFormatMap classificationFormatMap) {
				_Category = category;
				_EditorFormatMap = editorFormatMap;
				_ClassificationFormatMap = classificationFormatMap;
				_DefaultFontSize = classificationFormatMap.DefaultTextProperties.FontRenderingEmSize;
				_DefaultTypeface = classificationFormatMap.DefaultTextProperties.Typeface;
				_ViewBackground = editorFormatMap.GetBackgroundColor();
			}

			public string Category => _Category;

			public IClassificationFormatMap ClassificationFormatMap => _ClassificationFormatMap;
			public IEditorFormatMap EditorFormatMap => _EditorFormatMap;
			public TextFormattingRunProperties DefaultTextProperties => _ClassificationFormatMap.DefaultTextProperties;
			public Color ViewBackground => _ViewBackground;
			public Typeface ViewTypeface => _DefaultTypeface;

			public int FormattableItemCount {
				get {
					int c = 0;
					foreach (var item in _ClassificationFormatMap.CurrentPriorityOrder) {
						if (item.IsFormattableClassificationType()) {
							++c;
						}
					}
					return c;
				}
			}

			public TextFormattingRunProperties GetCachedProperty(IClassificationType classificationType) {
				return _PropertiesCache.TryGetValue(classificationType, out var property)
					? property
					: _PropertiesCache[classificationType] = _ClassificationFormatMap.GetTextProperties(classificationType);
			}

			public bool TryGetChanges(string formatMapKey, out ResourceDictionary original, out ResourceDictionary changes, out string note) {
				if (_Traces.TryGetValue(formatMapKey, out var trace)) {
					original = trace.Origin.Copy();
					changes = trace.Changes.Copy();
					note = trace.FormatChanges.ToString();
					return true;
				}
				changes = original = null;
				note = null;
				return false;
			}

			void LockEvent(string name) {
				++_Lock;
				_Formatters.Push(name);
			}

			void UnlockEvent() {
				_Formatters.Pop();
				if (--_Lock == 0 && _PendingChange.PendingEvents != EventKind.None) {
					_PendingChange.FireEvents(this);
					UpdateHighlightOptions(__SyntaxStyleCache);
					_ChangedFormatItems.Clear();
				}
			}

			// note: VS appears to have difficulty in merging semantic braces and some other styles.
			//   By explicitly iterating the CurrentPriorityOrder collection,
			//   calling GetTextProperties then SetTextProperties one by one,
			//   the underlying merging process will be called and the priority order is enforced.
			public void Refresh() {
				if (_ClassificationFormatMap.IsInBatchUpdate) {
					_PendingChange.PendEvent(EventKind.Refresh);
					return;
				}
				LockEvent(nameof(Refresh));
				_ClassificationFormatMap.BeginBatchUpdate();
				try {
					var formats = _ClassificationFormatMap.CurrentPriorityOrder;
					$"Refresh priority {formats.Count}".Log();
					_PropertiesCache.Clear();
					foreach (var item in formats) {
						if (item.IsFormattableClassificationType()) {
							var p = _ClassificationFormatMap.GetTextProperties(item);
							// C/C++ styles can somehow get reverted, here we forcefully reinforce our highlights 
							if (Highlight(item, out var newStyle) != FormatChanges.None) {
								p = newStyle.Value.MergeFormatProperties(p);
							}
							$"[{_Category}] refresh classification {item.Classification} ({p.Print()})".Log();
							_ClassificationFormatMap.SetTextProperties(item, p);
							_PropertiesCache[item] = p;
						}
					}
				}
				finally {
					_ClassificationFormatMap.EndBatchUpdate();
					_PendingChange.PendingEvents = _PendingChange.PendingEvents.SetFlags(EventKind.Refresh, false);
					UnlockEvent();
				}
			}

			public void Apply() {
				var formats = _ClassificationFormatMap.CurrentPriorityOrder;
				$"[{_Category}] apply priority {formats.Count}".Log();
				var newStyles = new List<KeyValuePair<string, ResourceDictionary>>(7);
				foreach (var item in formats) {
					if (item.IsFormattableClassificationType()
						&& Highlight(item, out var newStyle) != FormatChanges.None) {
						newStyles.Add(newStyle);
					}
				}

				if (newStyles.Count == 0) {
					return;
				}
				if (_EditorFormatMap.IsInBatchUpdate) {
					_PendingChange.PendEvent(EventKind.Apply);
					return;
				}
				_PropertiesCache.Clear();
				LockEvent(nameof(Apply));
				_EditorFormatMap.BeginBatchUpdate();
				$"[{_Category}] update formats {newStyles.Count}".Log();
				try {
					foreach (var item in newStyles) {
						$"[{_Category}] apply format {item.Key}".Log();
						_EditorFormatMap.SetProperties(item.Key, item.Value);
					}
				}
				finally {
					_EditorFormatMap.EndBatchUpdate();
					UnlockEvent();
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
						if ((_FormatClassificationTypes.TryGetValue(t, out var ct)
								|| (ct = __GetClassificationType(t)) != null)
							&& Highlight(ct, out var newStyle) != FormatChanges.None) {
							// remove the cache and let subsequent call to GetCachedProperty updates it
							_PropertiesCache.Remove(ct);
							_ClassificationFormatMap.BeginBatchUpdate();
							try {
								_EditorFormatMap.SetProperties(newStyle.Key, newStyle.Value);
							}
							finally {
								_ClassificationFormatMap.EndBatchUpdate();
							}
						}
					}
					else {
						$"[{_Category}] update theme after config change".Log();
						DetectThemeColorCompatibilityWithBackground();
						Apply();
					}
					_Context = FormatContext.None;
				}
			}

			public void SubscribeFormatMappingChanges() {
				_EditorFormatMap.FormatMappingChanged -= EditorFormatMappingChanged;
				_EditorFormatMap.FormatMappingChanged += EditorFormatMappingChanged;
				_ClassificationFormatMap.ClassificationFormatMappingChanged -= ClassificationFormatMappingChanged;
				_ClassificationFormatMap.ClassificationFormatMappingChanged += ClassificationFormatMappingChanged;
			}
			public void UnsubscribeFormatMappingChanges() {
				_EditorFormatMap.FormatMappingChanged -= EditorFormatMappingChanged;
				_ClassificationFormatMap.ClassificationFormatMappingChanged -= ClassificationFormatMappingChanged;
			}

			void ClassificationFormatMappingChanged(object sender, EventArgs e) {
				if (_PendingChange.FiringEvent == EventKind.None) {
					_PendingChange.PendEvent(EventKind.ClassificationFormat);
				}
				if (_Lock != 0) {
					return;
				}
				LockEvent("ClassificationFormatMappingChangedEvent");
				try {
					var currentTypeface = _ClassificationFormatMap.DefaultTextProperties.Typeface;
					if (AreTypefaceEqual(currentTypeface, _DefaultTypeface) == false) {
						$"[{_Category}] default font changed {_DefaultTypeface?.FontFamily.Source}->{currentTypeface.FontFamily.Source}".Log();
						if (_Lock == 1) {
							UpdateDefaultTypeface(currentTypeface);
							DefaultTextPropertiesChanged?.Invoke(this, new EventArgs<TextFormattingRunProperties>(_ClassificationFormatMap.DefaultTextProperties));
						}
						else {
							_PendingChange.PendEvent(EventKind.DefaultText);
						}
					}
				}
				finally {
					UnlockEvent();
				}
			}

			// VS somehow fails to update some formats after editor font changes
			// This method compensates the missing ones
			void UpdateDefaultTypeface(Typeface currentTypeface) {
				LockEvent("DefaultFontChangedEvent");
				_Context = FormatContext.Event;
				var startedEditorBatch = false;
				var startedClassifierBatch = false;
				var defaultFontFamily = _DefaultTypeface.FontFamily;
				if (_ClassificationFormatMap.IsInBatchUpdate == false) {
					_ClassificationFormatMap.BeginBatchUpdate();
					startedClassifierBatch = true;
				}
				if (_EditorFormatMap.IsInBatchUpdate == false) {
					_EditorFormatMap.BeginBatchUpdate();
					startedEditorBatch = true;
				}
				try {
					foreach (var item in _Traces) {
						var p = _EditorFormatMap.GetProperties(item.Key);
						if (p.GetTypeface()?.FontFamily.Equals(defaultFontFamily) == true
							&& item.Value.Changes.GetTypeface() == null) {
							p.SetTypeface(currentTypeface);
							_EditorFormatMap.SetProperties(item.Key, p);
						}
					}
				}
				finally {
					_Context = FormatContext.None;
					if (startedEditorBatch) {
						_EditorFormatMap.EndBatchUpdate();
					}
					if (startedClassifierBatch) {
						_ClassificationFormatMap.EndBatchUpdate();
					}
					UnlockEvent();
				}
				_DefaultTypeface = currentTypeface;
			}

			void EditorFormatMappingChanged(object sender, FormatItemsEventArgs e) {
				if (_PendingChange.FiringEvent == EventKind.None) {
					_ChangedFormatItems.AddRange(e.ChangedItems);
					_PendingChange.PendEvent(EventKind.EditorFormat);
				}
				if (_Lock != 0) {
					$"[{_Category}] format changed {String.Join(", ", e.ChangedItems)}, blocked by {String.Join(".", _Formatters)}".Log();
					return;
				}

				_Context = FormatContext.Event;
				LockEvent("FormatChangedEvent");
				var newStyles = new List<KeyValuePair<string, ResourceDictionary>>(7);
				bool startedBatch = false, needRefresh = false, bgInverted = false;
				var currentBg = _EditorFormatMap.GetBackgroundColor();
				var bgChanged = _ViewBackground != currentBg;
				if (bgChanged) {
					$"[{_Category}] background changed {_ViewBackground.ToHexString()}->{currentBg.ToHexString()}".Log();
					if (_Category == Constants.CodeText) {
						bgInverted = InvertColorOnBackgroundInverted(currentBg);
					}
					_ViewBackground = currentBg;
					_PendingChange.PendEvent(EventKind.Background);
				}
				var currentFontSize = _ClassificationFormatMap.DefaultTextProperties.FontRenderingEmSize;
				var fsChanged = _DefaultFontSize != currentFontSize;
				if (fsChanged) {
					$"[{_Category}] font size changed {_DefaultFontSize}->{currentFontSize}".Log();
					_DefaultFontSize = currentFontSize;
					_PendingChange.PendEvent(EventKind.DefaultText);
				}
				var dedup = new HashSet<IClassificationType>();
				// ChangedItems collection is dynamic
				// cache the changes to prevent it from changing during the enumerating procedure
				var changedItems = e.ChangedItems.ToList();
				$"[{_Category}] editor format changed: {changedItems.Count}".Log();
				foreach (var item in changedItems) {
					HighlightRecursive(__GetClassificationType(item), dedup, newStyles);
				}
				if (bgChanged || fsChanged) {
					foreach (var item in __SyntaxStyleCache) {
						if (bgChanged && item.Value.BackColor.A > 0
							|| fsChanged && item.Value.FontSize != 0) {
							HighlightRecursive(__GetClassificationType(item.Key), dedup, newStyles);
							needRefresh = true;
						}
					}
				}

				$"[{_Category}] overridden {newStyles.Count} styles".Log();
				try {
					if (newStyles.Count != 0) {
						if (_EditorFormatMap.IsInBatchUpdate == false) {
							_EditorFormatMap.BeginBatchUpdate();
							startedBatch = true;
						}
						foreach (var item in newStyles) {
							_EditorFormatMap.SetProperties(item.Key, item.Value);
						}
						if (needRefresh == false) {
							needRefresh = dedup.Contains(__GetClassificationType(Constants.CodeClassName));
						}
					}
				}
				finally {
					_Context = FormatContext.None;
					if (startedBatch) {
						_EditorFormatMap.EndBatchUpdate();
					}
					$"[{_Category}] after format mapping changes".Log();
					if (bgInverted) {
						Apply();
					}
					if (needRefresh) {
						Refresh();
					}
					UnlockEvent();
				}
			}

			/// <summary>
			/// This method counts major brightness of colors in a theme and compare it with the editor background. If the brightness does not match, it automatically inverts brightness of each colors in the theme.
			/// </summary>
			/// <remarks>Since syntax highlight colors are used majorly in primary document editor window, thus this method only works for the primary editor.</remarks>
			internal void DetectThemeColorCompatibilityWithBackground() {
				if (_Category != Constants.CodeText) {
					return;
				}
				var isBackgroundDark = _ViewBackground.IsDark();
				// color counter
				int brightness = 0;
				foreach (var item in GetStyles()) {
					var style = item.Value;
					CountColorBrightness(style.ForeColor, ref brightness);
					CountColorBrightness(style.BackColor, ref brightness);
				}
				if (brightness > 3 && isBackgroundDark == false || brightness < -3 && isBackgroundDark) {
					InvertColorBrightness();
				}

				void CountColorBrightness(Color c, ref int b) {
					if (c.A != 0) {
						b += c.IsDark() ? -1 : 1;
					}
				}
			}

			bool InvertColorOnBackgroundInverted(Color currentBg) {
				if (_ViewBackground.IsDark() ^ currentBg.IsDark()) {
					InvertColorBrightness();
					return true;
				}
				return false;
			}

			void InvertColorBrightness() {
				$"[{_Category}] invert color brightness".Log();
				foreach (var item in GetStyles()) {
					var style = item.Value;
					if (style.ForeColor.A != 0) {
						style.ForeColor = style.ForeColor.InvertBrightness();
					}
					if (style.BackColor.A != 0) {
						style.BackColor = style.BackColor.InvertBrightness();
					}
					if (style.HasLineColor) {
						style.LineColor = style.LineColor.InvertBrightness();
					}
				}

				var qi = Config.Instance.QuickInfo;
				if (qi.BackColor.A != 0) {
					qi.BackColor = qi.BackColor.InvertBrightness();
					Config.Instance.FireConfigChangedEvent(Features.SuperQuickInfo);
				}
				var sm = Config.Instance.SymbolReferenceMarkerSettings;
				if (sm.ReferenceMarker.A != 0 || sm.WriteMarker.A != 0 || sm.SymbolDefinition.A != 0) {
					if (sm.ReferenceMarker.A != 0) {
						sm.ReferenceMarker = sm.ReferenceMarker.InvertBrightness();
					}
					if (sm.WriteMarker.A != 0) {
						sm.WriteMarker = sm.WriteMarker.InvertBrightness();
					}
					if (sm.SymbolDefinition.A != 0) {
						sm.SymbolDefinition = sm.SymbolDefinition.InvertBrightness();
					}
				}
			}

			void HighlightRecursive(IClassificationType ct, HashSet<IClassificationType> dedup, List<KeyValuePair<string, ResourceDictionary>> updates) {
				if (ct.IsFormattableClassificationType() == false
					|| dedup.Add(ct) == false) {
					return;
				}
				var c = Highlight(ct, out var newStyle);
				if (c == FormatChanges.None) {
					return;
				}
				updates.Add(newStyle);
				if (c.MatchFlags(FormatChanges.FontSize | FormatChanges.BackgroundBrush)
					&& __ClassificationTypeStore.TryGetValue(ct, out var subTypes)) {
					foreach (var subType in subTypes) {
						HighlightRecursive(subType, dedup, updates);
					}
				}
			}

			FormatChanges Highlight(IClassificationType classification, out KeyValuePair<string, ResourceDictionary> newStyle) {
				if (classification is null) {
					newStyle = default;
					return FormatChanges.None;
				}
				var c = GetEditorFormatMapKey(classification);
				__SyntaxStyleCache.TryGetValue(c, out var style);
				LockEvent($"Highlight<{c}>");
				var current = _EditorFormatMap.GetProperties(c);
				var changes = FormatChanges.None;
				if (_Traces.TryGetValue(c, out var trace) == false) {
					if (style == null) {
						$"[{_Category}] skipped format {c}".Log();
						newStyle = default;
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
					changes = trace.Change(current, style, this);
					if (changes != FormatChanges.None) {
						$"[{_Category}] update format trace <{c}> ({trace})".Log();
					}
					else {
						newStyle = default;
						goto EXIT;
					}
				}
				else {
					$"[{_Category}] change format trace <{c}> ({trace})".Log();
					changes = trace.Change(current, style, this);
				}
				newStyle = new KeyValuePair<string, ResourceDictionary>(c, current);
			EXIT:
				UnlockEvent();
				return changes;
			}

			public void Reset() {
				LockEvent("Reset");
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
					UnlockEvent();
				}
			}

			public void Reset(string classificationType) {
				var t = __GetClassificationType(classificationType);
				if (t != null) {
					Reset(t);
				}
			}

			public void Reset(IClassificationType classificationType) {
				LockEvent($"Reset.{classificationType.Classification}");
				ResetTextProperties(_EditorFormatMap, classificationType);
				UnlockEvent();
			}

			public void Clear() {
				_PropertiesCache.Clear();
				_Traces.Clear();
			}

			void ResetTextProperties(IEditorFormatMap map, IClassificationType item) {
				var formatKey = GetEditorFormatMapKey(item);
				if (__SyntaxStyleCache.ContainsKey(formatKey) == false) {
					return;
				}

				var current = map.GetProperties(formatKey);
				var reset = Reset(formatKey, current);
				if (reset != current) {
					// remove the cache and let subsequent call to GetCachedProperty updates it
					_PropertiesCache.Remove(item);
					map.SetProperties(formatKey, reset);
				}
			}

			ResourceDictionary Reset(string editorFormatMapKey, ResourceDictionary format) {
				if (_Traces.TryGetValue(editorFormatMapKey, out var trace)) {
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

			string GetEditorFormatMapKey(IClassificationType classificationType) {
				var key = _ClassificationFormatMap.GetEditorFormatMapKey(classificationType);
				if (_FormatClassificationTypes.ContainsKey(key) == false) {
					_FormatClassificationTypes.Add(key, classificationType);
				}
				return key;
			}

			IEnumerable<IClassificationType> GetChangedClassificationTypes() {
				foreach (var item in _ChangedFormatItems) {
					if (_FormatClassificationTypes.TryGetValue(item, out var t)) {
						yield return t;
					}
				}
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

			sealed class PendingChange
			{
				public EventKind PendingEvents;
				public EventKind FiringEvent;

				public void FireEvents(Highlighter highlighter) {
					if (PendingEvents == EventKind.None) {
						return;
					}
					if (PendingEvents.MatchFlags(EventKind.Background)) {
						FiringEvent = EventKind.Background;
						EditorBackgroundChanged?.Invoke(highlighter, new EventArgs<Color>(highlighter.ViewBackground));
					}
					if (PendingEvents.MatchFlags(EventKind.DefaultText)) {
						FiringEvent = EventKind.DefaultText;
						DefaultTextPropertiesChanged?.Invoke(highlighter, new EventArgs<TextFormattingRunProperties>(highlighter.DefaultTextProperties));
					}
					if (PendingEvents.MatchFlags(EventKind.EditorFormat)) {
						FiringEvent = EventKind.EditorFormat;
						FormatItemsChanged?.Invoke(highlighter, new EventArgs<IReadOnlyList<string>>(highlighter._ChangedFormatItems));
					}
					if (PendingEvents.MatchFlags(EventKind.ClassificationFormat)) {
						FiringEvent = EventKind.ClassificationFormat;
						ClassificationFormatMapChanged?.Invoke(highlighter, new EventArgs<IEnumerable<IClassificationType>>(highlighter.GetChangedClassificationTypes()));
					}
					if (PendingEvents.MatchFlags(EventKind.Apply)) {
						FiringEvent = EventKind.Apply;
						highlighter.Apply();
					}
					if (PendingEvents.MatchFlags(EventKind.Refresh)) {
						FiringEvent = EventKind.Refresh;
						highlighter.Refresh();
					}
					PendingEvents = FiringEvent = EventKind.None;
				}

				public void PendEvent(EventKind eventKind) {
					if (FiringEvent == EventKind.None) {
						PendingEvents |= eventKind;
					}
				}
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
					var c = FormatChanges.None;
					ChangeBold(current, style, ref c);
					ChangeItalic(current, style, ref c);
					ChangeBrush(current, style, ref c);
					ChangeBackgroundBrush(current, style, highlighter, ref c);
					ChangeOpacity(current, style, ref c);
					ChangeBackgroundOpacity(current, style, ref c);
					ChangeFontSize(current, style, highlighter, ref c);
					ChangeTypeface(current, style, highlighter, ref c);
					ChangeTextDecorations(current, style, ref c);
					return c;
				}

				#region ResourceDictionary alteration methods
				void ChangeBold(ResourceDictionary current, StyleBase style, ref FormatChanges c) {
					bool? s;
					if (style?.Bold != null) {
						if ((s = style.Bold) != current.GetBold()) {
							_FormatChanges |= FormatChanges.Bold;
							Changes.SetBold(s);
							current.SetBold(s);
							c |= FormatChanges.Bold;
						}
					}
					else if (_FormatChanges.MatchFlags(FormatChanges.Bold)) {
						_FormatChanges ^= FormatChanges.Bold;
						Changes.SetBold(null);
						current.SetBold(Origin.GetBold());
						c |= FormatChanges.Bold;
					}
					else if ((s = current.GetBold()) != Origin.GetBold()) {
						Origin.SetBold(s);
						c |= FormatChanges.Bold;
					}
				}

				void ChangeItalic(ResourceDictionary current, StyleBase style, ref FormatChanges c) {
					bool? s;
					if (style?.Italic != null) {
						if ((s = style.Italic) != current.GetItalic()) {
							_FormatChanges |= FormatChanges.Italic;
							Changes.SetItalic(s);
							current.SetItalic(s);
							c |= FormatChanges.Italic;
						}
					}
					else if (_FormatChanges.MatchFlags(FormatChanges.Italic)) {
						_FormatChanges ^= FormatChanges.Italic;
						Changes.SetItalic(null);
						current.SetItalic(Origin.GetItalic());
						c |= FormatChanges.Italic;
					}
					else if ((s = current.GetItalic()) != Origin.GetItalic()) {
						Origin.SetItalic(s);
						c |= FormatChanges.Italic;
					}
				}

				void ChangeBrush(ResourceDictionary current, StyleBase style, ref FormatChanges c) {
					Brush b;
					if (style != null && style.ForeColor.A != 0) {
						if (AreBrushesEqual(b = style.MakeBrush(), current.GetBrush()) == false) {
							_FormatChanges |= FormatChanges.ForegroundBrush;
							Changes.SetBrush(b);
							current.SetBrush(b);
							c |= FormatChanges.ForegroundBrush;
						}
					}
					else if (_FormatChanges.MatchFlags(FormatChanges.ForegroundBrush)) {
						_FormatChanges ^= FormatChanges.ForegroundBrush;
						Changes.SetBrush(null);
						current.SetBrush(Origin.GetBrush());
						c |= FormatChanges.ForegroundBrush;
					}
					else if (AreBrushesEqual(b = current.GetBrush(), Origin.GetBrush()) == false) {
						Origin.SetBrush(b);
						c |= FormatChanges.ForegroundBrush;
					}
				}

				void ChangeBackgroundBrush(ResourceDictionary current, StyleBase style, Highlighter highlighter, ref FormatChanges c) {
					Brush b;
					if (style != null && style.BackColor.A != 0) {
						b = style.MakeBackgroundBrush(highlighter._ViewBackground);
						if (AreBrushesEqual(b, current.GetBackgroundBrush()) == false) {
							_FormatChanges |= FormatChanges.BackgroundBrush;
							Changes.SetBackgroundBrush(b);
							current.SetBackgroundBrush(b);
							c |= FormatChanges.BackgroundBrush;
						}
					}
					else if (_FormatChanges.MatchFlags(FormatChanges.BackgroundBrush)) {
						_FormatChanges ^= FormatChanges.BackgroundBrush;
						Changes.SetBackgroundBrush(null);
						current.SetBackgroundBrush(Origin.GetBackgroundBrush());
						c |= FormatChanges.BackgroundBrush;
					}
					else if (AreBrushesEqual(b = current.GetBackgroundBrush(), Origin.GetBackgroundBrush()) == false) {
						Origin.SetBackgroundBrush(b);
						c |= FormatChanges.BackgroundBrush;
					}
				}

				void ChangeOpacity(ResourceDictionary current, StyleBase style, ref FormatChanges c) {
					double o;
					if (style != null && style.ForegroundOpacity != 0) {
						if ((o = style.ForegroundOpacity / 255d) != current.GetOpacity()) {
							_FormatChanges |= FormatChanges.ForegroundOpacity;
							Changes.SetOpacity(o);
							current.SetOpacity(o);
							c |= FormatChanges.ForegroundOpacity;
						}
					}
					else if (_FormatChanges.MatchFlags(FormatChanges.ForegroundOpacity)) {
						_FormatChanges ^= FormatChanges.ForegroundOpacity;
						Changes.SetOpacity(0);
						current.SetOpacity(Origin.GetOpacity());
						c |= FormatChanges.ForegroundOpacity;
					}
					else if ((o = current.GetOpacity()) != Origin.GetOpacity()) {
						Origin.SetOpacity(o);
						c |= FormatChanges.ForegroundOpacity;
					}
				}

				void ChangeBackgroundOpacity(ResourceDictionary current, StyleBase style, ref FormatChanges c) {
					double o;
					if (style != null && style.BackgroundOpacity != 0) {
						if ((o = style.BackgroundOpacity / 255d) != current.GetBackgroundOpacity()) {
							_FormatChanges |= FormatChanges.BackgroundOpacity;
							Changes.SetBackgroundOpacity(o);
							current.SetBackgroundOpacity(o);
							c |= FormatChanges.BackgroundOpacity;
						}
					}
					else if (_FormatChanges.MatchFlags(FormatChanges.BackgroundOpacity)) {
						_FormatChanges ^= FormatChanges.BackgroundOpacity;
						Changes.SetBackgroundOpacity(0);
						current.SetBackgroundOpacity(Origin.GetBackgroundOpacity());
						c |= FormatChanges.BackgroundOpacity;
					}
					else if ((o = current.GetBackgroundOpacity()) != Origin.GetBackgroundOpacity()) {
						Origin.SetBackgroundOpacity(o);
						c |= FormatChanges.BackgroundOpacity;
					}
				}

				void ChangeFontSize(ResourceDictionary current, StyleBase style, Highlighter highlighter, ref FormatChanges c) {
					double? s;
					if (style != null && style.FontSize != 0) {
						s = highlighter._DefaultFontSize + style.FontSize;
						if (s != current.GetFontSize()) {
							_FormatChanges |= FormatChanges.FontSize;
							Changes.SetFontSize(s);
							current.SetFontSize(s);
							c |= FormatChanges.FontSize;
						}
					}
					else if (_FormatChanges.MatchFlags(FormatChanges.FontSize)) {
						_FormatChanges ^= FormatChanges.FontSize;
						Changes.SetFontSize(null);
						current.SetFontSize(Origin.GetFontSize());
						c |= FormatChanges.FontSize;
					}
					else if ((s = current.GetFontSize()) != Origin.GetFontSize()) {
						Origin.SetFontSize(s);
						c |= FormatChanges.FontSize;
					}
				}

				void ChangeTypeface(ResourceDictionary current, StyleBase style, Highlighter highlighter, ref FormatChanges c) {
					Typeface ct;
					if (style != null && String.IsNullOrEmpty(style.Font) == false) {
						ct = style.MakeTypeface();
						if (ct != null
							&& AreTypefaceEqual(current.GetTypeface(), ct) == false
							&& AreTypefaceEqual(ct, highlighter._DefaultTypeface) == false) {
							_FormatChanges |= FormatChanges.Typeface;
							Changes.SetTypeface(ct);
							current.SetTypeface(ct);
							c |= FormatChanges.Typeface;
						}
					}
					else if (_FormatChanges.MatchFlags(FormatChanges.Typeface)) {
						_FormatChanges ^= FormatChanges.Typeface;
						Changes.SetTypeface(null);
						current.SetTypeface(Origin.GetTypeface());
						c |= FormatChanges.Typeface;
					}
					else if ((ct = current.GetTypeface()) != Origin.GetTypeface()) {
						Origin.SetTypeface(ct);
						c |= FormatChanges.Typeface;
					}
				}

				void ChangeTextDecorations(ResourceDictionary current, StyleBase style, ref FormatChanges c) {
					TextDecorationCollection td;
					if (style?.HasLine == true) {
						var t = style.MakeTextDecorations();
						if (t != (td = current.GetTextDecorations())
							&& (td == null || t?.SequenceEqual(td) != true)) {
							_FormatChanges |= FormatChanges.TextDecorations;
							Changes.SetTextDecorations(t);
							current.SetTextDecorations(t);
							c |= FormatChanges.TextDecorations;
						}
					}
					else if (_FormatChanges.MatchFlags(FormatChanges.TextDecorations)) {
						_FormatChanges ^= FormatChanges.TextDecorations;
						Changes.SetTextDecorations(null);
						current.SetTextDecorations(Origin.GetTextDecorations());
						c |= FormatChanges.TextDecorations;
					}
					else if ((td = current.GetTextDecorations()) != Origin.GetTextDecorations()) {
						Origin.SetTextDecorations(td);
						c |= FormatChanges.TextDecorations;
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
							case EditorFormatDefinition.ForegroundBrushId:
								sb.Append(BrushToString(val)).Append("@f");
								break;
							case EditorFormatDefinition.ForegroundColorId:
								if (resource.Contains(EditorFormatDefinition.ForegroundBrushId)) {
									break;
								}
								goto case EditorFormatDefinition.ForegroundBrushId;
							case ClassificationFormatDefinition.IsBoldId:
								sb.Append((bool)val ? "+B" : "-B");
								break;
							case ClassificationFormatDefinition.IsItalicId:
								sb.Append((bool)val ? "+I" : "-I");
								break;
							case EditorFormatDefinition.BackgroundBrushId:
								sb.Append(BrushToString(val)).Append("@b");
								break;
							case EditorFormatDefinition.BackgroundColorId:
								if (resource.Contains(EditorFormatDefinition.BackgroundBrushId)) {
									break;
								}
								goto case EditorFormatDefinition.BackgroundBrushId;
							case ClassificationFormatDefinition.FontRenderingSizeId:
								sb.Append(val).Append('#');
								break;
							case ClassificationFormatDefinition.TypefaceId:
								sb.Append('\'').Append(((Typeface)val).FontFamily.ToString()).Append('\'');
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

					string BrushToString(object brush) {
						if (brush is Color c) {
							return c.ToHexString();
						}
						if (brush is SolidColorBrush sc) {
							return sc.Color.ToHexString();
						}
						if (brush is GradientBrush g) {
							return String.Join("|", g.GradientStops.Select(s => s.Color.ToHexString()));
						}
						return String.Empty;
					}
				}
			}

			sealed class ClassificationTypeComparer : IEqualityComparer<IClassificationType>
			{
				public bool Equals(IClassificationType x, IClassificationType y) {
					return x.Classification == y.Classification;
				}

				public int GetHashCode(IClassificationType obj) {
					return obj.Classification.GetHashCode();
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

			[Flags]
			enum EventKind
			{
				None,
				EditorFormat = 1,
				ClassificationFormat = 1 << 1,
				Background = 1 << 2,
				DefaultText = 1 << 3,
				Apply = 1 << 4,
				Refresh = 1 << 5
			}
		}
	}
}
