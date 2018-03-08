using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
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

		readonly IWpfTextView _TextView;
		readonly IClassificationFormatMap _Map;
		readonly IClassificationTypeRegistryService _RegService;
		readonly IEditorFormatMap _FormatMap;

		Color _BackColor, _ForeColor;
		volatile int _IsDecorating;
		//bool _PendingRefresh;

		public CodeViewDecorator(IWpfTextView view, IClassificationFormatMap map, IClassificationTypeRegistryService service, IEditorFormatMap formatMap) {
			view.Closed += View_Closed;
			//view.VisualElement.IsVisibleChanged += VisualElement_IsVisibleChanged;
			map.ClassificationFormatMappingChanged += SettingsSaved;
			//view.GotAggregateFocus += TextView_GotAggregateFocus;
			Config.Updated += SettingsSaved;

			_Map = map;
			_RegService = service;
			_FormatMap = formatMap;
			_TextView = view;

			if (__Styles == null) {
				CacheStyles(service);
			}

			Decorate();
		}

		//void VisualElement_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
		//	if (_PendingRefresh && _TextView.VisualElement.IsVisible) {
		//		SettingsSaved(sender, EventArgs.Empty);
		//	}
		//}

		void View_Closed(object sender, EventArgs e) {
			Config.Updated -= SettingsSaved;
			_Map.ClassificationFormatMappingChanged -= SettingsSaved;
			//_TextView.VisualElement.IsVisibleChanged -= VisualElement_IsVisibleChanged;
			_TextView.Closed -= View_Closed;
		}

		static void CacheStyles(IClassificationTypeRegistryService service) {
			__Styles = new Dictionary<string, StyleBase>(100);
			InitStyleClassificationCache<CommentStyleTypes, CommentStyle>(service, Config.Instance.CommentStyles);
			InitStyleClassificationCache<CodeStyleTypes, CodeStyle>(service, Config.Instance.CodeStyles);
			InitStyleClassificationCache<XmlStyleTypes, XmlCodeStyle>(service, Config.Instance.XmlCodeStyles);
		}

		static void InitStyleClassificationCache<TStyleEnum, TCodeStyle>(IClassificationTypeRegistryService service, List<TCodeStyle> styles)
			where TCodeStyle : StyleBase {
			var cs = typeof(TStyleEnum);
			var codeStyles = Enum.GetNames(cs);
			foreach (var styleName in codeStyles) {
				var f = cs.GetField(styleName);
				var cso = styles.Find(i => i.Id == (int)f.GetValue(null));
				if (cso == null) {
					continue;
				}
				var cts = f.GetCustomAttributes<ClassificationTypeAttribute>(false);
				foreach (var item in cts) {
					var n = item.ClassificationTypeNames;
					if (String.IsNullOrWhiteSpace(n)) {
						continue;
					}
					var ct = service.GetClassificationType(n);
					if (ct != null) {
						__Styles[ct.Classification] = cso;
					}
				}
			}
		}

		void SettingsSaved(object sender, EventArgs eventArgs) {
			if (_IsDecorating != 0) {
				return;
			}
			if (_TextView.VisualElement.IsVisible) {
				//if (_PendingRefresh) {
					CacheStyles(_RegService);
					Decorate();
					//_PendingRefresh = false;
					Debug.WriteLine("Unset pending refresh");
				//}
			}
			//else {
			//	_PendingRefresh = true;
			//	Debug.WriteLine("Set pending refresh");
			//}
		}

		void Decorate() {
			if (Interlocked.CompareExchange(ref _IsDecorating, 1, 0) != 0) {
				return;
			}
			try {
				var c = _FormatMap.GetProperties(Constants.EditorProperties.Text)?[EditorFormatDefinition.ForegroundColorId];
				if (c is Color) {
					_ForeColor = (Color)c;
				}
				c = _FormatMap.GetProperties(Constants.EditorProperties.TextViewBackground)?[EditorFormatDefinition.BackgroundColorId];
				if (c is Color) {
					_BackColor = (Color)c;
					_BackColor = Color.FromArgb(0x00, _BackColor.R, _BackColor.G, _BackColor.B);
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
			if (_Map.IsInBatchUpdate) {
				return;
			}
			_Map.BeginBatchUpdate();
			var textProperty = _Map.GetTextProperties(_RegService.GetClassificationType("text"));
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
					_Map.SetTextProperties(item, SetProperties(initialProperty, style, textProperty.FontRenderingEmSize));
				}
			}
			_Map.EndBatchUpdate();
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
			if (settings.BackColor.A > 0) {
				properties = properties.SetBackgroundOpacity(settings.BackColor.A / 255.0);
				switch (settings.BackgroundEffect) {
					case BrushEffect.Solid:
						properties = properties.SetBackground(settings.BackColor);
						break;
					case BrushEffect.ToBottom:
						properties = properties.SetBackgroundBrush(new LinearGradientBrush(_BackColor, settings.BackColor, 90));
						break;
					case BrushEffect.ToTop:
						properties = properties.SetBackgroundBrush(new LinearGradientBrush(settings.BackColor, _BackColor, 90));
						break;
					case BrushEffect.ToRight:
						properties = properties.SetBackgroundBrush(new LinearGradientBrush(_BackColor, settings.BackColor, 0));
						break;
					case BrushEffect.ToLeft:
						properties = properties.SetBackgroundBrush(new LinearGradientBrush(settings.BackColor, _BackColor, 0));
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
