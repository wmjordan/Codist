using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

namespace Commentist.Views
{
	internal sealed class CommentViewDecorator
	{
		static Dictionary<string, CommentStyleOption> _classifications;

		public CommentViewDecorator(ITextView view, IClassificationFormatMap map, IClassificationTypeRegistryService service) {
			view.GotAggregateFocus += this.TextView_GotAggregateFocus;
			//Config.Instance.ConfigUpdated += this.SettingsSaved;
			this.map = map;
			this.regService = service;

			if (_classifications == null) {
				var t = typeof(CommentStyle);
				var styleNames = Enum.GetNames(t);
				_classifications = new Dictionary<string, CommentStyleOption>(styleNames.Length);
				foreach (var styleName in styleNames) {
					var f = t.GetField(styleName);
					var d = f.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false);
					if (d.Length == 0) {
						continue;
					}
					var ct = service.GetClassificationType((d[0] as System.ComponentModel.DescriptionAttribute).Description);
					var cso = Config.Instance.Styles.Find(i => i.StyleID == (CommentStyle)f.GetValue(null));
					if (cso == null) {
						continue;
					}
					_classifications[ct.Classification] = cso;
				}
			}

			this.Decorate();
		}

		//private void SettingsSaved(object sender, EventArgs eventArgs) {
		//	if (!this.isDecorating) {
		//		this.Decorate();
		//	}
		//}

		private void TextView_GotAggregateFocus(object sender, EventArgs e) {
			ITextView view;
			if ((view = (sender as ITextView)) != null) {
				view.GotAggregateFocus -= this.TextView_GotAggregateFocus;
			}
			if (!this.isDecorating) {
				this.Decorate();
			}
		}

		private void Decorate() {
			try {
				this.isDecorating = true;
				this.DecorateClassificationTypes();
			}
			catch (Exception) {
			}
			finally {
				this.isDecorating = false;
			}
		}

		private void DecorateClassificationTypes() {
			foreach (var item in map.CurrentPriorityOrder) {
				if (item == null) {
					continue;
				}
				System.Diagnostics.Debug.WriteLine(item.Classification);
				CommentStyleOption style;
				if (_classifications.TryGetValue(item.Classification, out style)) {
					var p = map.GetExplicitTextProperties(item);
					if (p == null) {
						continue;
					}
					map.SetExplicitTextProperties(item, SetProperties(p, style));
				}
			}
		}

		private TextFormattingRunProperties SetProperties(TextFormattingRunProperties properties, CommentStyleOption styleOption) {
			var settings = styleOption;
			double fontSize = this.GetEditorTextSize() + settings.FontSize;
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

		private double GetEditorTextSize() {
			return this.map.GetTextProperties(this.regService.GetClassificationType("text")).FontRenderingEmSize;
		}

		private bool isDecorating;

		private readonly IClassificationFormatMap map;

		private readonly IClassificationTypeRegistryService regService;

	}
}
