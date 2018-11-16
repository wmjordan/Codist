using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using AppHelpers;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

namespace Codist.SyntaxHighlight
{
	// see: Microsoft.VisualStudio.Text.Classification.Implementation.ClassificationFormatMap
	// see: Microsoft.VisualStudio.Text.Classification.Implementation.ViewSpecificFormatMap
	sealed class CodeViewDecorator
	{
		readonly IWpfTextView _TextView;
		readonly IClassificationFormatMap _ClassificationFormatMap;
		readonly IClassificationTypeRegistryService _RegService;

		Color _BackColor, _ForeColor;
		volatile int _IsDecorating;
		//bool _PendingRefresh;

		public CodeViewDecorator(IWpfTextView view) {
			view.Closed += View_Closed;
			//view.VisualElement.IsVisibleChanged += VisualElement_IsVisibleChanged;
			//view.GotAggregateFocus += TextView_GotAggregateFocus;
			Config.Updated += SettingsSaved;

			_ClassificationFormatMap = ServicesHelper.Instance.ClassificationFormatMap.GetClassificationFormatMap(view);
			_ClassificationFormatMap.ClassificationFormatMappingChanged += FormatUpdated;
			_RegService = ServicesHelper.Instance.ClassificationTypeRegistry;
			_TextView = view;

			Decorate();
		}

		//void VisualElement_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
		//	if (_PendingRefresh && _TextView.VisualElement.IsVisible) {
		//		SettingsSaved(sender, EventArgs.Empty);
		//	}
		//}

		void View_Closed(object sender, EventArgs e) {
			Config.Updated -= SettingsSaved;
			_ClassificationFormatMap.ClassificationFormatMappingChanged -= FormatUpdated;
			//_TextView.VisualElement.IsVisibleChanged -= VisualElement_IsVisibleChanged;
			_TextView.Closed -= View_Closed;
		}

		void SettingsSaved(object sender, ConfigUpdatedEventArgs eventArgs) {
			if (eventArgs.UpdatedFeature.MatchFlags(Features.SyntaxHighlight) == false) {
				return;
			}
			FormatUpdated(sender, eventArgs);
		}

		void FormatUpdated(object sender, EventArgs e) {
			if (_IsDecorating != 0) {
				return;
			}
			if (_TextView.VisualElement.IsVisible) {
				Decorate();
			}
		}

		void Decorate() {
			if (Interlocked.CompareExchange(ref _IsDecorating, 1, 0) != 0) {
				return;
			}
			try {
				var c = TextEditorHelper.DefaultEditorFormatMap.GetColor(Constants.EditorProperties.Text, EditorFormatDefinition.ForegroundColorId);
				if (c.A > 0) {
					if (c.Equals(_ForeColor) == false) {
						Debug.WriteLine("Fore color changed: " + _ForeColor.ToString() + "->" + c.ToString());
					}
					_ForeColor = c;
				}
				c = TextEditorHelper.DefaultEditorFormatMap.GetColor(Constants.EditorProperties.TextViewBackground, EditorFormatDefinition.BackgroundColorId);
				if (c.A > 0) {
					_BackColor = c.Alpha(0);
				}
				DecorateClassificationTypes();
			}
			catch (Exception ex) {
				Debug.WriteLine("Decorator exception: ");
				Debug.WriteLine(ex);
			}
			finally {
				_IsDecorating = 0;
			}
		}

		void DecorateClassificationTypes() {
			if (_ClassificationFormatMap.IsInBatchUpdate) {
				return;
			}
			_ClassificationFormatMap.BeginBatchUpdate();
			var defaultSize = _ClassificationFormatMap.DefaultTextProperties.FontRenderingEmSize;
			foreach (var item in _ClassificationFormatMap.CurrentPriorityOrder) {
				StyleBase style;
				TextFormattingRunProperties textFormatting;
				if (item == null
					|| (style = TextEditorHelper.GetStyle(item.Classification)) == null
					|| (textFormatting = TextEditorHelper.GetBackupFormatting(item.Classification)) == null) {
					continue;
				}
				_ClassificationFormatMap.SetTextProperties(item, SetProperties(textFormatting, style, defaultSize));
			}
			_ClassificationFormatMap.EndBatchUpdate();
			Debug.WriteLine("Decorated");
		}

		TextFormattingRunProperties SetProperties(TextFormattingRunProperties properties, StyleBase styleOption, double textSize) {
			var settings = styleOption;
			var fontSize = textSize + settings.FontSize;
			if (fontSize < 1) {
				fontSize = 1;
			}
			if (string.IsNullOrWhiteSpace(settings.Font) == false) {
				properties = properties.SetTypeface(new Typeface(settings.Font));
			}
			if (settings.FontSize != 0) {
				properties = properties.SetFontRenderingEmSize(fontSize);
			}
			if (settings.Bold.HasValue) {
				properties = properties.SetBold(settings.Bold.Value);
			}
			if (settings.Italic.HasValue) {
				properties = properties.SetItalic(settings.Italic.Value);
			}
			if (settings.ForeColor.A > 0) {
				if (settings.ForeColor.A == 255 && properties.ForegroundOpacityEmpty) {
					properties = properties.SetForeground(settings.ForeColor.Alpha(255));
				}
				else {
					properties = properties.SetForegroundOpacity(settings.ForeColor.A / 255.0)
					.SetForeground(settings.ForeColor);
				}
			}
			var bc = settings.BackColor.A > 0 ? settings.BackColor.Alpha(255)
				: properties.BackgroundBrushEmpty == false && properties.BackgroundBrush is SolidColorBrush ? (properties.BackgroundBrush as SolidColorBrush).Color
				: Colors.Transparent;
			if (bc.A > 0) {
				if (settings.BackColor.A < 255 || properties.BackgroundOpacityEmpty == false) {
					properties = properties.SetBackgroundOpacity(bc.A / 255.0);
				}
				switch (settings.BackgroundEffect) {
					case BrushEffect.Solid:
						properties = properties.SetBackground(bc);
						break;
					case BrushEffect.ToBottom:
						properties = properties.SetBackgroundBrush(new LinearGradientBrush(_BackColor, bc, 90));
						break;
					case BrushEffect.ToTop:
						properties = properties.SetBackgroundBrush(new LinearGradientBrush(bc, _BackColor, 90));
						break;
					case BrushEffect.ToRight:
						properties = properties.SetBackgroundBrush(new LinearGradientBrush(_BackColor, bc, 0));
						break;
					case BrushEffect.ToLeft:
						properties = properties.SetBackgroundBrush(new LinearGradientBrush(bc, _BackColor, 0));
						break;
					default:
						break;
				}
			}
			if (settings.Underline.HasValue || settings.Strikethrough.HasValue || settings.OverLine.HasValue) {
				var tdc = new TextDecorationCollection();
				if (settings.Underline.GetValueOrDefault()) {
					tdc.Add(TextDecorations.Underline);
				}
				if (settings.Strikethrough.GetValueOrDefault()) {
					tdc.Add(TextDecorations.Strikethrough);
				}
				if (settings.OverLine.GetValueOrDefault()) {
					tdc.Add(TextDecorations.OverLine);
				}
				properties = properties.SetTextDecorations(tdc);
			}
			return properties;
		}

		//void TextView_GotAggregateFocus(object sender, EventArgs e) {
		//	//ITextView view;
		//	//if ((view = (sender as ITextView)) != null) {
		//	//	view.GotAggregateFocus -= TextView_GotAggregateFocus;
		//	//}
		//	if (_IsDecorating == 0) {
		//		Decorate();
		//	}
		//}
	}
}
