using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using AppHelpers;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

namespace Codist.SyntaxHighlight
{
	sealed class CodeViewDecorator
	{
		readonly IWpfTextView _TextView;
		readonly IClassificationFormatMap _ClassificationFormatMap;
		readonly IClassificationTypeRegistryService _RegService;
		readonly IEditorFormatMap _EditorFormatMap;

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
			_EditorFormatMap = ServicesHelper.Instance.EditorFormatMap.GetEditorFormatMap(view);
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
				var c = _EditorFormatMap.GetProperties(Constants.EditorProperties.Text)?[EditorFormatDefinition.ForegroundColorId];
				if (c is Color) {
					_ForeColor = (Color)c;
				}
				c = _EditorFormatMap.GetProperties(Constants.EditorProperties.TextViewBackground)?[EditorFormatDefinition.BackgroundColorId];
				if (c is Color) {
					_BackColor = ((Color)c).Alpha(0);
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
			var defaultFormat = _ClassificationFormatMap.DefaultTextProperties;
			if (TextEditorHelper.DefaultFormatting == null) {
				TextEditorHelper.DefaultFormatting = defaultFormat;
			}
			else if (TextEditorHelper.DefaultFormatting.ForegroundBrushSame(defaultFormat.ForegroundBrush) == false) {
				Debug.WriteLine("DefaultFormatting Changed");
				// theme changed
				TextEditorHelper.BackupFormattings.Clear();
				TextEditorHelper.DefaultFormatting = defaultFormat;
			}
			foreach (var item in _ClassificationFormatMap.CurrentPriorityOrder) {
				if (item == null) {
					continue;
				}
				StyleBase style;
				if (TextEditorHelper.SyntaxStyleCache.TryGetValue(item.Classification, out style)) {
					TextFormattingRunProperties cached;
					if (TextEditorHelper.BackupFormattings.TryGetValue(item.Classification, out cached) == false) {
						var p = _ClassificationFormatMap.GetExplicitTextProperties(item);
						if (p == null) {
							continue;
						}
						TextEditorHelper.BackupFormattings[item.Classification] = cached = p;
					}
					_ClassificationFormatMap.SetTextProperties(item, SetProperties(cached, style, defaultFormat.FontRenderingEmSize));
				}
			}
			_ClassificationFormatMap.EndBatchUpdate();
			Debug.WriteLine("Decorated");
		}

		TextFormattingRunProperties SetProperties(TextFormattingRunProperties properties, StyleBase styleOption, double textSize) {
			var settings = styleOption;
			var fontSize = textSize + settings.FontSize;
			if (fontSize < 2) {
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
				properties = properties.SetForegroundOpacity(settings.ForeColor.A / 255.0)
					.SetForeground(settings.ForeColor);
			}
			var bc = settings.BackColor.A > 0 ? settings.BackColor
				: properties.BackgroundBrushEmpty == false && properties.BackgroundBrush is SolidColorBrush ? (properties.BackgroundBrush as SolidColorBrush).Color
				: Colors.Transparent;
			if (bc.A > 0) {
				properties = properties.SetBackgroundOpacity(bc.A / 255.0);
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
