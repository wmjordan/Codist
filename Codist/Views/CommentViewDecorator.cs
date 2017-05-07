using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

namespace Codist.Views
{
	internal sealed class CommentViewDecorator
	{
		static Dictionary<string, StyleBase> _classifications;

		readonly IClassificationFormatMap map;

		readonly IClassificationTypeRegistryService regService;

		bool isDecorating;

		public CommentViewDecorator(ITextView view, IClassificationFormatMap map, IClassificationTypeRegistryService service) {
			view.GotAggregateFocus += TextView_GotAggregateFocus;
			Config.Instance.ConfigUpdated += SettingsSaved;
			this.map = map;
			regService = service;

			if (_classifications == null) {
				var c = typeof(CommentStyles);
				var styleNames = Enum.GetNames(c);
				var cs = typeof(CodeStyles);
				var codeStyles = Enum.GetNames(cs);
				_classifications = new Dictionary<string, StyleBase>(styleNames.Length + codeStyles.Length);
				foreach (var styleName in styleNames) {
					var f = c.GetField(styleName);
					var d = f.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false);
					if (d.Length == 0) {
						continue;
					}
					var ct = service.GetClassificationType((d[0] as System.ComponentModel.DescriptionAttribute).Description);
					var cso = Config.Instance.Styles.Find(i => i.StyleID == (CommentStyles)f.GetValue(null));
					if (cso == null) {
						continue;
					}
					_classifications[ct.Classification] = cso;
				}
				foreach (var styleName in codeStyles) {
					var f = cs.GetField(styleName);
					var d = f.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false);
					if (d.Length == 0) {
						continue;
					}
					var ct = service.GetClassificationType((d[0] as System.ComponentModel.DescriptionAttribute).Description);
					var cso = Config.Instance.CodeStyles.Find(i => i.StyleID == (CodeStyles)f.GetValue(null));
					if (cso == null) {
						continue;
					}
					_classifications[ct.Classification] = cso;
				}
			}

			Decorate();
		}

		private void SettingsSaved(object sender, EventArgs eventArgs) {
			if (!isDecorating) {
				Decorate();
			}
		}

		private void Decorate() {
			try {
				isDecorating = true;
				DecorateClassificationTypes();
			}
			catch (Exception) {
			}
			finally {
				isDecorating = false;
			}
		}

		private void DecorateClassificationTypes() {
			map.BeginBatchUpdate();
			foreach (var item in map.CurrentPriorityOrder) {
				if (item == null) {
					continue;
				}
				Debug.Write(item.Classification);
				Debug.Write(' ');
				foreach (var type in item.BaseTypes) {
					Debug.Write('/');
					Debug.Write(type.Classification);
				}
				Debug.WriteLine('/');
				StyleBase style;
				if (_classifications.TryGetValue(item.Classification, out style)) {
					var p = map.GetExplicitTextProperties(item);
					if (p == null) {
						continue;
					}
					map.SetExplicitTextProperties(item, SetProperties(p, style));
				}
			}
			map.EndBatchUpdate();
		}

		private double GetEditorTextSize() {
			return map.GetTextProperties(regService.GetClassificationType("text")).FontRenderingEmSize;
		}

		private TextFormattingRunProperties SetProperties(TextFormattingRunProperties properties, StyleBase styleOption) {
			var settings = styleOption;
			double fontSize = GetEditorTextSize() + settings.FontSize;
			if (string.IsNullOrWhiteSpace(settings.Font) == false) {
				properties = properties.SetTypeface(new Typeface(settings.Font));
			}
			if (Math.Abs(fontSize - properties.FontRenderingEmSize) > 0.0) {
				properties = properties.SetFontRenderingEmSize(fontSize);
			}
			if (settings.Italic.HasValue) {
				properties = properties.SetItalic(settings.Italic.Value);
			}
			if (settings.ForeColor.A > 0 /* fore color is not transparent */) {
				properties = properties.SetForegroundOpacity(settings.ForeColor.A / 255.0);
				properties = properties.SetForeground(settings.ForeColor);
			}
			if (settings.BackColor.A > 0) {
				properties = properties.SetBackgroundOpacity(settings.BackColor.A / 255.0);
				properties = properties.SetBackground(settings.BackColor);
				//? have some fun with background color
				//properties = properties.SetBackgroundBrush(new LinearGradientBrush(Colors.White, settings.BackColor, 90));
			}
			if (settings.Bold.HasValue) {
				properties = properties.SetBold(settings.Bold.Value);
			}
			if (settings.Underline.HasValue || settings.StrikeThrough.HasValue) {
				var tdc = new TextDecorationCollection();
				if (settings.Underline.HasValue && settings.Underline.Value) {
					tdc.Add(TextDecorations.Underline);
				}
				if (settings.StrikeThrough.HasValue && settings.StrikeThrough.Value) {
					tdc.Add(TextDecorations.Strikethrough);
				}
				properties = properties.SetTextDecorations(tdc);
			}
			return properties;
		}

		private void TextView_GotAggregateFocus(object sender, EventArgs e) {
			ITextView view;
			if ((view = (sender as ITextView)) != null) {
				view.GotAggregateFocus -= TextView_GotAggregateFocus;
			}
			if (!isDecorating) {
				Decorate();
			}
		}
	}
}
