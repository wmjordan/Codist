using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Formatting;

namespace Codist.SyntaxHighlight
{
	static class FormatStore
	{
		static readonly object _syncRoot = new object();

		#region sequence-critical static fields
		internal static readonly IEditorFormatMap DefaultEditorFormatMap = ServicesHelper.Instance.EditorFormatMap.GetEditorFormatMap(Constants.CodeText);

		internal static readonly IClassificationFormatMap DefaultClassificationFormatMap = ServicesHelper.Instance.ClassificationFormatMap.GetClassificationFormatMap(Constants.CodeText);

		static Dictionary<string, StyleBase> _SyntaxStyleCache = InitSyntaxStyleCache();

		internal static readonly Dictionary<IClassificationType, List<IClassificationType>> ClassificationTypeStore = InitClassificationTypes(_SyntaxStyleCache.Keys); 
		#endregion

		static Dictionary<IClassificationType, TextFormattingRunProperties> _BackupFormattings = new Dictionary<IClassificationType, TextFormattingRunProperties>(80);

		static TextFormattingRunProperties __DefaultFormatting;

		internal static bool IdentifySymbolSource { get; private set; }
		internal static int BackupFormatCount => _BackupFormattings.Count;

		public static TextFormattingRunProperties GetBackupFormatting(this IClassificationType classificationType) {
			lock (_syncRoot) {
				return _BackupFormattings.TryGetValue(classificationType, out var r) ? r : null;
			}
		}
		public static TextFormattingRunProperties GetOrSaveBackupFormatting(IClassificationType classificationType, bool update) {
			lock (_syncRoot) {
				if (update == false && _BackupFormattings.TryGetValue(classificationType, out var r)) {
					return r;
				}
				// hack: workaround for https://github.com/wmjordan/Codist/issues/199
				if (classificationType.Classification?.Contains("Breakpoint") == true) {
					return null;
				}

				r = DefaultClassificationFormatMap.GetExplicitTextProperties(classificationType);
				Debug.WriteLine($"Backup format: {classificationType.Classification} {(r.ForegroundBrushEmpty ? "<empty>" : r.ForegroundBrush.ToString())}");
				_BackupFormattings[classificationType] = r;
				return r;
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
			if (__DefaultFormatting == null) {
				__DefaultFormatting = defaultFormat;
			}
			else if (__DefaultFormatting.ForegroundBrushSame(defaultFormat.ForegroundBrush) == false) {
				Debug.WriteLine("DefaultFormatting changed");
				// theme changed
				lock (_syncRoot) {
					var formattings = new Dictionary<IClassificationType, TextFormattingRunProperties>(_BackupFormattings.Count);
					LoadFormattings(formattings);
					_BackupFormattings = formattings;
					__DefaultFormatting = defaultFormat;
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
			Config.RegisterLoadHandler((config) => ResetStyleCache());
			DefaultClassificationFormatMap.ClassificationFormatMappingChanged += UpdateFormatCache;
			VSColorTheme.ThemeChanged += _ => {
				if (_BackupFormattings != null) {
					_BackupFormattings?.Clear();
					LoadFormattings(_BackupFormattings);
				}
			};
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
			var styles = Config.Instance.Styles;
			if (styles != null) {
				foreach (var item in styles) {
					if (item == null || String.IsNullOrEmpty(item.ClassificationType)) {
						continue;
					}
					var c = service.GetClassificationType(item.ClassificationType);
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

		static void InitStyleClassificationCache<TStyleEnum, TCodeStyle>(Dictionary<string, StyleBase> styleCache, IClassificationTypeRegistryService service, List<TCodeStyle> styles)
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
			AddSelfAndBase(d, cts.GetClassificationType(Constants.CodeBold));
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
