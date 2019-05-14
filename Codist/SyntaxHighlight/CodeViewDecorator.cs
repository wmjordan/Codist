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
	sealed partial class CodeViewDecorator
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
			if (eventArgs.UpdatedFeature.MatchFlags(Features.SyntaxHighlight)) {
				FormatUpdated(sender, eventArgs);
			}
		}

		void FormatUpdated(object sender, EventArgs e) {
			if (_IsDecorating == 0 && _TextView.VisualElement.IsVisible) {
				Decorate();
			}
		}

		void Decorate() {
			if (Interlocked.CompareExchange(ref _IsDecorating, 1, 0) != 0) {
				return;
			}
			try {
				var c = FormatStore.DefaultEditorFormatMap.GetColor(Constants.EditorProperties.Text, EditorFormatDefinition.ForegroundColorId);
				if (c.A > 0) {
					if (c != _ForeColor) {
						Debug.WriteLine("Fore color changed: " + _ForeColor.ToString() + "->" + c.ToString());
					}
					_ForeColor = c;
				}
				c = FormatStore.DefaultEditorFormatMap.GetColor(Constants.EditorProperties.TextViewBackground, EditorFormatDefinition.BackgroundColorId);
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
					|| (style = FormatStore.GetStyle(item.Classification)) == null
					|| (textFormatting = FormatStore.GetBackupFormatting(item.Classification)) == null) {
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
			if (settings.ForegroundOpacity > 0) {
				if (settings.ForeColor.A > 0) {
					properties = properties.SetForeground(settings.ForeColor);
				}
				if (settings.ForegroundOpacity != Byte.MaxValue || properties.ForegroundOpacityEmpty == false) {
					properties = properties.SetForegroundOpacity(settings.ForegroundOpacity / 255.0);
				}
			}
			else if (settings.ForeColor.A > 0) {
				properties = properties.SetForeground(settings.ForeColor);
			}
			if (settings.BackColor.A > 0) {
				var bc = settings.BackColor.A > 0 ? settings.BackColor
				   : properties.BackgroundBrushEmpty == false && properties.BackgroundBrush is SolidColorBrush ? (properties.BackgroundBrush as SolidColorBrush).Color
				   : Colors.Transparent;
				if (settings.BackgroundOpacity != Byte.MaxValue && settings.BackgroundOpacity != 0 || properties.BackgroundOpacityEmpty == false) {
					properties = properties.SetBackgroundOpacity(settings.BackgroundOpacity / 255.0);
				}
				if (bc.A > 0) {
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
							throw new NotImplementedException("Background effect not supported: " + settings.BackgroundEffect.ToString());
					}
				}
			}
			else if (settings.BackColor.A > 0) {
				properties = properties.SetBackground(settings.BackColor);
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
	}
}
