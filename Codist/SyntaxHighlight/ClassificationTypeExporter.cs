using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Utilities;

namespace Codist.SyntaxHighlight
{
	/// <summary>
	/// This class exports classification type definitions for styles used for syntax highlighting
	/// </summary>
	sealed class ClassificationTypeExporter
	{
		readonly IClassificationTypeRegistryService _Classifications;
		readonly IContentTypeRegistryService _ContentTypes;
		readonly IClassificationFormatMapService _FormatMaps;
		readonly IEditorFormatMapService _EditorFormatMaps;
		readonly List<Entry> _Entries = new List<Entry>();

		public ClassificationTypeExporter(IClassificationTypeRegistryService classifications, IContentTypeRegistryService contentTypes, IClassificationFormatMapService formatMaps, IEditorFormatMapService editorFormatMaps) {
			_Classifications = classifications;
			_ContentTypes = contentTypes;
			_FormatMaps = formatMaps;
			_EditorFormatMaps = editorFormatMaps;
		}

		public void RegisterClassificationTypes<TStyle>() where TStyle : Enum {
			var t = typeof(TStyle);
			var r = _Classifications;

			// skip classification types which do not have a corresponding content type
			var c = t.GetCustomAttribute<CategoryAttribute>();
			if (c != null && _ContentTypes.GetContentType(c.Category) == null) {
				return;
			}

			foreach (var field in t.GetFields()) {
				var name = field.GetCustomAttribute<ClassificationTypeAttribute>()?.ClassificationTypeNames;
				if (String.IsNullOrEmpty(name)
					|| r.GetClassificationType(name) != null
					|| field.GetCustomAttribute<InheritanceAttribute>() != null) {
					continue;
				}
				var baseNames = new List<string>();
				var orders = new List<(string, bool)>();
				StyleAttribute style = null;
				foreach (var attr in field.GetCustomAttributes()) {
					if (attr is BaseDefinitionAttribute baseDef
						&& String.IsNullOrWhiteSpace(baseDef.BaseDefinition) == false) {
						baseNames.Add(baseDef.BaseDefinition);
					}
					else if (attr is OrderAttribute order) {
						if (String.IsNullOrWhiteSpace(order.Before) == false) {
							orders.Add((order.Before, true));
						}
						if (String.IsNullOrWhiteSpace(order.After) == false) {
							orders.Add((order.After, false));
						}
					}
					else if (attr is StyleAttribute s) {
						style = s;
					}
				}
				_Entries.Add(new Entry(name,
					baseNames.Count != 0 ? baseNames : null,
					orders.Count != 0 ? orders : null,
					style));
			}
		}

		public void ExportClassificationTypes() {
			int e = 0, lastExported;
			var r = _Classifications;
			do {
				lastExported = e;
				foreach (var item in _Entries) {
					if (item.Exported || r.GetClassificationType(item.Name) != null) {
						continue;
					}
					if (item.BaseNames == null) {
						item.MarkExported(r.CreateClassificationType(item.Name, Enumerable.Empty<IClassificationType>()));
						e++;
					}
					else {
						var cts = item.BaseNames.ConvertAll(r.GetClassificationType);
						cts.RemoveAll(i => i == null);
						if (cts.Count != 0) {
							e++;
							item.MarkExported(r.CreateClassificationType(item.Name, cts));
						}
					}
				}
			}
			while (e < _Entries.Count && lastExported != e);
		}

		public void UpdateClassificationFormatMap(string category) {
			UpdateClassificationFormatMap(_FormatMaps.GetClassificationFormatMap(category), _EditorFormatMaps.GetEditorFormatMap(category), _Classifications.GetClassificationType);
		}

		// This method creates `TextFormattingRunProperties` for `IClassificationType`s defined
		//   in Codist. The `TextFormattingRunProperties` instances are inserted to `IClassificationFormatMap`
		//   with consideration of [BaseDefinition] and [Order] attributes.
		//   However, only [OrderAttribute(After)] is handled, [Order(Before)] is not treated as purposed.
		// Note: there is a bug in VS that it sometimes reorders `TextFormattingRunProperties` added to
		//   `IClassificationFormatMap`for some unknown reasons after Codist arranges them in this method.
		void UpdateClassificationFormatMap(IClassificationFormatMap classificationFormatMap, IEditorFormatMap editorFormatMap, Func<string, IClassificationType> getCt) {
			var m = classificationFormatMap;
			var typePriorities = new Dictionary<IClassificationType, int>(100);
			var priorityGroups = new Dictionary<int, Chain<IClassificationType>>(19);
			int priority = 0;
			var orders = m.CurrentPriorityOrder;
			foreach (var item in orders) {
				if (item != null) {
					typePriorities[item] = ++priority;
				}
			}
			var batch = m.IsInBatchUpdate;
			if (batch == false) {
				m.BeginBatchUpdate();
			}
			foreach (var entry in GetExportedEntriesOrderByDependency()) {
				var f = entry.GetTextFormattingRunProperties(editorFormatMap.GetProperties(m.GetEditorFormatMapKey(entry.ClassificationType)));
				var p = 0;
				int tp;
				IClassificationType bct = null, ct;
				var lower = false;
				if (entry.BaseNames != null) {
					// gets highest priority of base types
					foreach (var item in entry.BaseNames) {
						ct = getCt(item);
						if (ct != null && typePriorities.TryGetValue(ct, out tp) && tp > p) {
							p = tp;
							bct = ct;
						}
					}
				}
				if (entry.Orders != null) {
					// gets highest priority of dependency-ordered types
					foreach (var (classificationType, before) in entry.Orders) {
						ct = getCt(classificationType);
						if (ct != null && typePriorities.TryGetValue(ct, out tp) && tp > p) {
							p = tp;
							bct = ct;
							lower = before;
						}
						else if (classificationType == Priority.High) {
							goto HIGH;
						}
						else if (classificationType == Priority.Low) {
							p = 1;
							bct = orders[0];
							lower = true;
							break;
						}
					}
				}
				if (p != 0) {
					// note: since GetExportedEntriesOrderByDependency has sorted entries by dependency,
					//   and in Codist, only [BaseDefinition] and [Order] After priorities are coded
					//   for extended `IClassificationType`s, we give higher precedency for later populated
					//   `IClassificationType`s
					#region Assign priority order to new IClassificationType
					if (priorityGroups.TryGetValue(p, out var chain)) {
						bct = chain.Last;
					}
					else {
						priorityGroups[p] = chain = new Chain<IClassificationType>(bct);
					}
					chain.Add(entry.ClassificationType);
					AssignOrderForClassificationType(typePriorities, entry.ClassificationType, p);
					#endregion
					m.AddExplicitTextProperties(entry.ClassificationType, f, bct);
					if (lower == false) {
						m.SwapPriorities(bct, entry.ClassificationType);
					}
					continue;
				}
			// note: orders are ignored if priority = High
			//   since all classification types exported are defined in Codist (in control),
			//   and the aforementioned situation does not exist, so we just skip handling that
			HIGH:
				AssignOrderForClassificationType(typePriorities, entry.ClassificationType, ++priority);
				m.AddExplicitTextProperties(entry.ClassificationType, f);
			}
			if (batch == false) {
				m.EndBatchUpdate();
			}

			void AssignOrderForClassificationType(Dictionary<IClassificationType, int> od, IClassificationType ct, int order) {
#if DEBUG
				od.Add(ct, order);
#else
				od[ct] = order;
#endif
			}
		}

		// This method returns a List that considering dependencies defined by [BaseDefinition] and [Order]
		// Independent entries will be returned first.
		// The most dependent entries will be returned at the end of the list.
		List<Entry> GetExportedEntriesOrderByDependency() {
			var r = _Entries.FindAll(i => i.Exported && i.BaseNames == null && i.Orders == null);
			int e = r.Count, lastExported;
			var exported = new Dictionary<string, bool>();
			bool o;
			foreach (var item in _Entries) {
				if (item.Exported) {
					exported.Add(item.Name, item.BaseNames == null && item.Orders == null);
				}
			}
			do {
				lastExported = e;
				foreach (var entry in _Entries) {
					if (entry.Exported == false
						|| entry.BaseNames == null && entry.Orders == null
						|| exported.TryGetValue(entry.Name, out o) && o) {
						goto NEXT;
					}

					if (entry.BaseNames != null) {
						foreach (var baseName in entry.BaseNames) {
							if (exported.TryGetValue(baseName, out o) == false // not defined in Codist
								|| o // exported
								) {
								continue;
							}
							goto NEXT;
						}
					}

					if (entry.Orders != null) {
						foreach (var (classificationType, before) in entry.Orders) {
							if (exported.TryGetValue(classificationType, out o) == false
								|| o
								|| classificationType == Priority.High || classificationType == Priority.Low) {
								continue;
							}
							goto NEXT;
						}
					}

					exported[entry.Name] = true;
					r.Add(entry);
					++e;
				NEXT:;
				}
			} while (e < _Entries.Count && lastExported != e);
			foreach (var item in _Entries) {
				if (item.Exported
					&& item.Orders != null
					&& exported.TryGetValue(item.Name, out o)
					&& o == false) {
					r.Add(item);
				}
			}
			return r;
		}

		sealed class Entry
		{
			public readonly string Name;
			public readonly List<string> BaseNames;
			public readonly List<(string classificationType, bool before)> Orders;
			public readonly StyleAttribute Style;
			public IClassificationType ClassificationType;
			public bool Exported => ClassificationType != null;

			public Entry(string name, List<string> baseNames, List<(string, bool)> orders, StyleAttribute style) {
				Name = name;
				BaseNames = baseNames;
				Orders = orders;
				Style = style;
			}

			public TextFormattingRunProperties GetTextFormattingRunProperties(System.Windows.ResourceDictionary resourceDictionary) {
				var f = resourceDictionary.AsFormatProperties();
				var s = Style;
				if (s != null) {
					if (s.Bold) {
						f = f.SetBold(true);
					}
					if (s.Italic) {
						f = f.SetItalic(true);
					}
					if (s.ForeColor.A != 0) {
						f = f.SetForeground(s.ForeColor).SetForegroundBrush(new SolidColorBrush(s.ForeColor));
					}
					if (s.BackColor.A != 0) {
						f = f.SetBackground(s.BackColor).SetBackgroundBrush(new SolidColorBrush(s.BackColor));
					}
					if (s.Size != 0) {
						f = f.SetFontHintingEmSize(s.Size);
					}
				}
				return f;
			}

			public void MarkExported(IClassificationType classificationType) {
				ClassificationType = classificationType;
				$"Export classification type: {Name}".Log();
			}

			public override string ToString() {
				return $"{Name} ({(Exported ? "E" : "?")})";
			}
		}
	}
}
