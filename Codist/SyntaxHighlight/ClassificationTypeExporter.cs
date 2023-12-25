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
		readonly List<Entry> _Entries = new List<Entry>();

		public ClassificationTypeExporter(IClassificationTypeRegistryService classifications, IContentTypeRegistryService contentTypes, IClassificationFormatMapService formatMaps) {
			_Classifications = classifications;
			_ContentTypes = contentTypes;
			_FormatMaps = formatMaps;
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

			UpdateClassificationFormatMap(_FormatMaps.GetClassificationFormatMap(Constants.CodeText), r.GetClassificationType);
		}

		void UpdateClassificationFormatMap(IClassificationFormatMap classificationFormatMap, Func<string, IClassificationType> getCt) {
			var m = classificationFormatMap;
			var cpo = new Dictionary<IClassificationType, int>();
			int p = 0;
			var orders = m.CurrentPriorityOrder;
			foreach (var item in orders) {
				if (item != null) {
					cpo[item] = ++p;
				}
			}
			var batch = m.IsInBatchUpdate;
			if (batch == false) {
				m.BeginBatchUpdate();
			}
			foreach (var entry in GetExportedEntriesOrderByDependency()) {
				var f = entry.GetTextFormattingRunProperties();
				var bp = 0;
				IClassificationType bct = null;
				var lower = false;
				if (entry.BaseNames != null) {
					foreach (var item in entry.BaseNames) {
						var ct = getCt(item);
						if (ct != null && cpo.TryGetValue(ct, out var ctp) && ctp > bp) {
							bp = ctp;
							bct = ct;
						}
					}
				}
				if (entry.Orders != null) {
					foreach (var (classificationType, before) in entry.Orders) {
						var ct = getCt(classificationType);
						if (ct != null && cpo.TryGetValue(ct, out var ctp) && ctp > bp) {
							bp = ctp;
							bct = ct;
							lower = before;
						}
						else if (classificationType == Priority.High) {
							goto HIGH;
						}
						else if (classificationType == Priority.Low) {
							goto LOW;
						}
					}
				}
				if (bp != 0) {
					AssignOrderForClassificationType(cpo, entry.ClassificationType, bp + (lower ? 0 : 1));
					m.AddExplicitTextProperties(entry.ClassificationType, f, bct);
					if (lower == false) {
						m.SwapPriorities(bct, entry.ClassificationType);
					}
					continue;
				}
			// note: orders are ignored if priority = High or Low
			//   since all classification types exported are defined in Codist (in control),
			//   and the aforementioned situation does not exist, so we just skip handling that
			HIGH:
				AssignOrderForClassificationType(cpo, entry.ClassificationType, ++p);
				m.AddExplicitTextProperties(entry.ClassificationType, f);
				continue;
			LOW:
				AssignOrderForClassificationType(cpo, entry.ClassificationType, 1);
				m.AddExplicitTextProperties(entry.ClassificationType, f, orders[0]);
			}
			if (batch == false) {
				m.EndBatchUpdate();
			}

			void AssignOrderForClassificationType(Dictionary<IClassificationType, int> od, IClassificationType ct, int order) {
#if DEBUG
				od.Add(ct, order);
#else
				cpo[ct] = order;
#endif
			}
		}

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

			public TextFormattingRunProperties GetTextFormattingRunProperties() {
				var f = TextFormattingRunProperties.CreateTextFormattingRunProperties();
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
