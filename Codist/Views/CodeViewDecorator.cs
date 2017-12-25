using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

namespace Codist.Views
{
	sealed class CodeViewDecorator
	{
		static readonly Dictionary<string, TextFormattingRunProperties> __InitialProperties = new Dictionary<string, TextFormattingRunProperties>(30);
		static Dictionary<string, StyleBase> __Styles;

		readonly IClassificationFormatMap _Map;
		readonly IClassificationTypeRegistryService _RegService;

		bool isDecorating;

		public CodeViewDecorator(ITextView view, IClassificationFormatMap map, IClassificationTypeRegistryService service) {
			view.GotAggregateFocus += TextView_GotAggregateFocus;
			Config.Instance.ConfigUpdated += SettingsSaved;
			_Map = map;
			_RegService = service;

			if (__Styles == null) {
				var c = typeof(CommentStyleTypes);
				var styleNames = Enum.GetNames(c);
				var cs = typeof(CodeStyleTypes);
				var codeStyles = Enum.GetNames(cs);
				__Styles = new Dictionary<string, StyleBase>(styleNames.Length + codeStyles.Length);
				foreach (var styleName in styleNames) {
					var f = c.GetField(styleName);
					var d = f.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>(false);
					if (d == null || String.IsNullOrWhiteSpace(d.Description)) {
						continue;
					}
					var ct = service.GetClassificationType(d.Description);
					var cso = Config.Instance.CommentStyles.Find(i => i.StyleID == (CommentStyleTypes)f.GetValue(null));
					if (cso == null) {
						continue;
					}
					__Styles[ct.Classification] = cso;
				}
				foreach (var styleName in codeStyles) {
					var f = cs.GetField(styleName);
					var d = f.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>(false);
					if (d == null || String.IsNullOrWhiteSpace(d.Description)) {
						continue;
					}
					var ct = service.GetClassificationType(d.Description);
					var cso = Config.Instance.CodeStyles.Find(i => i.StyleID == (CodeStyleTypes)f.GetValue(null));
					if (cso == null) {
						continue;
					}
					__Styles[ct.Classification] = cso;
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
			catch (Exception ex) {
				Debug.WriteLine("Decorator exception: ");
				Debug.WriteLine(ex);
			}
			finally {
				isDecorating = false;
			}
		}

		private void DecorateClassificationTypes() {
			_Map.BeginBatchUpdate();
			foreach (var item in _Map.CurrentPriorityOrder) {
				if (item == null) {
					continue;
				}
#if DEBUG
				Debug.Write(item.Classification);
				Debug.Write(' ');
				foreach (var type in item.BaseTypes) {
					Debug.Write('/');
					Debug.Write(type.Classification);
				}
				Debug.WriteLine('/');
#endif
				StyleBase style;
				if (__Styles.TryGetValue(item.Classification, out style)) {
					TextFormattingRunProperties initialProperty;
					if (__InitialProperties.TryGetValue(item.Classification, out initialProperty) == false) {
						var p = _Map.GetExplicitTextProperties(item);
						if (p == null) {
							continue;
						}
						__InitialProperties[item.Classification] = initialProperty = p;
					}
					_Map.SetTextProperties(item, SetProperties(initialProperty, style));
				}
			}
			_Map.EndBatchUpdate();
		}

		private double GetEditorTextSize() {
			return _Map.GetTextProperties(_RegService.GetClassificationType("text")).FontRenderingEmSize;
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
			if (settings.ForeColor.A > 0) {
				properties = properties.SetForegroundOpacity(settings.ForeColor.A / 255.0);
				properties = properties.SetForeground(settings.ForeColor);
			}
			if (settings.BackColor.A > 0) {
				properties = properties.SetBackgroundOpacity(settings.BackColor.A / 255.0);
				switch (settings.BackgroundEffect) {
					case BrushEffect.Solid:
						properties = properties.SetBackground(settings.BackColor);
						break;
					case BrushEffect.ToBottom:
						properties = properties.SetBackgroundBrush(new LinearGradientBrush(Colors.Transparent, settings.BackColor, 90));
						break;
					case BrushEffect.ToTop:
						properties = properties.SetBackgroundBrush(new LinearGradientBrush(settings.BackColor, Colors.Transparent, 90));
						break;
					case BrushEffect.ToRight:
						properties = properties.SetBackgroundBrush(new LinearGradientBrush(Colors.Transparent, settings.BackColor, 0));
						break;
					case BrushEffect.ToLeft:
						properties = properties.SetBackgroundBrush(new LinearGradientBrush(settings.BackColor, Colors.Transparent, 0));
						break;
					default:
						break;
				}
			}
			if (settings.Bold.HasValue) {
				properties = properties.SetBold(settings.Bold.Value);
			}
			if (settings.Underline.HasValue || settings.StrikeThrough.HasValue || settings.OverLine.HasValue) {
				var tdc = new TextDecorationCollection();
				if (settings.Underline.GetValueOrDefault()) {
					tdc.Add(TextDecorations.Underline);
				}
				if (settings.StrikeThrough.GetValueOrDefault()) {
					tdc.Add(TextDecorations.Strikethrough);
				}
				if (settings.OverLine.GetValueOrDefault()) {
					tdc.Add(TextDecorations.OverLine);
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
