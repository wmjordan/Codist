using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
	// see: https://stackoverflow.com/questions/24404473/create-visual-studio-theme-specific-syntax-highlighting
	sealed class CodeViewDecorator
	{
		readonly IWpfTextView _TextView;
		readonly IClassificationFormatMap _ClassificationFormatMap;
		readonly IClassificationTypeRegistryService _RegService;
		readonly IEditorFormatMap _EditorFormatMap;

		Color _BackColor, _ForeColor;
		volatile int _IsDecorating;
		bool _IsViewActive;
		static bool _Initialized;

		public CodeViewDecorator(IWpfTextView view) {
			view.Closed += View_Closed;
			view.VisualElement.IsVisibleChanged += VisualElement_IsVisibleChanged;
			if (_Initialized == false) {
				view.VisualElement.IsVisibleChanged += MarkInitialized;
			}
			Config.Updated += SettingsUpdated;

			_ClassificationFormatMap = ServicesHelper.Instance.ClassificationFormatMap.GetClassificationFormatMap(view);
			_EditorFormatMap = ServicesHelper.Instance.EditorFormatMap.GetEditorFormatMap(view);
			//_ClassificationFormatMap.ClassificationFormatMappingChanged += FormatUpdated;
			_RegService = ServicesHelper.Instance.ClassificationTypeRegistry;
			_TextView = view;

			_IsViewActive = true;
			if (_Initialized) {
				Debug.WriteLine("Decorate known types");
				Decorate(FormatStore.ClassificationTypeStore.Keys, true);
				_EditorFormatMap.FormatMappingChanged += FormatUpdated;
			}
			else {
				_EditorFormatMap.FormatMappingChanged += BackupFormat;
				_ClassificationFormatMap.BeginBatchUpdate();
				foreach (var item in _ClassificationFormatMap.CurrentPriorityOrder) {
					if (item != null) {
						_ClassificationFormatMap.SetTextProperties(item, _ClassificationFormatMap.GetExplicitTextProperties(item));
					}
				}
				_ClassificationFormatMap.EndBatchUpdate();
			}
		}

		void VisualElement_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
			_IsViewActive = (bool)e.NewValue;
		}

		void MarkInitialized(object sender, DependencyPropertyChangedEventArgs e) {
			if ((bool)e.NewValue) {
				_TextView.VisualElement.IsVisibleChanged -= VisualElement_IsVisibleChanged;
				_TextView.VisualElement.IsVisibleChanged -= MarkInitialized;
				_EditorFormatMap.FormatMappingChanged -= BackupFormat;
				Debug.WriteLine("Decorator initialized");
				Decorate(FormatStore.ClassificationTypeStore.Keys, true);
				_Initialized = true;
				_EditorFormatMap.FormatMappingChanged -= FormatUpdated;
				_EditorFormatMap.FormatMappingChanged += FormatUpdated;
			}
		}

		void View_Closed(object sender, EventArgs e) {
			_IsViewActive = false;
			Config.Updated -= SettingsUpdated;
			//_ClassificationFormatMap.ClassificationFormatMappingChanged -= FormatUpdated;
			_TextView.VisualElement.IsVisibleChanged -= VisualElement_IsVisibleChanged;
			_TextView.VisualElement.IsVisibleChanged -= MarkInitialized;
			_EditorFormatMap.FormatMappingChanged -= FormatUpdated;
			_EditorFormatMap.FormatMappingChanged -= BackupFormat;
			_TextView.Closed -= View_Closed;
		}

		void SettingsUpdated(object sender, ConfigUpdatedEventArgs eventArgs) {
			if (eventArgs.UpdatedFeature.MatchFlags(Features.SyntaxHighlight)) {
				var t = eventArgs.Parameter as string;
				if (t != null) {
					Decorate(new[] { ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationType(t) }, true);
				}
				else {
					Decorate(_ClassificationFormatMap.CurrentPriorityOrder, false);
				}
			}
		}

		void BackupFormat(object sender, FormatItemsEventArgs e) {
			if (e.ChangedItems.Count > 0) {
				foreach (var item in e.ChangedItems) {
					var t = _RegService.GetClassificationType(item);
					if (t != null) {
						FormatStore.GetOrSaveBackupFormatting(t, true);
					}
				}
			}
		}

		void FormatUpdated(object sender, FormatItemsEventArgs e) {
			if (_IsDecorating == 0 && _IsViewActive && e.ChangedItems.Count > 0) {
				Decorate(e.ChangedItems.Select(_RegService.GetClassificationType), true);
			}
		}

		void Decorate(IEnumerable<IClassificationType> classifications, bool fullUpdate) {
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
				DecorateClassificationTypes(classifications, fullUpdate);
			}
			catch (Exception ex) {
				Debug.WriteLine("Decorator exception: ");
				Debug.WriteLine(ex);
			}
			finally {
				_IsDecorating = 0;
			}
		}

		void DecorateClassificationTypes(IEnumerable<IClassificationType> classifications, bool fullUpdate) {
			if (_ClassificationFormatMap.IsInBatchUpdate) {
				return;
			}
			var defaultSize = _ClassificationFormatMap.DefaultTextProperties.FontRenderingEmSize;
			var updated = new Dictionary<IClassificationType, TextFormattingRunProperties>();
			StyleBase style;
			TextFormattingRunProperties textFormatting;
			foreach (var item in classifications) {
				if (item == null
					|| (style = FormatStore.GetOrCreateStyle(item)) == null
					|| (textFormatting = FormatStore.GetOrSaveBackupFormatting(item, _Initialized == false)) == null) {
					continue;
				}
				var p = SetProperties(textFormatting, style, defaultSize);
				if (p != textFormatting || fullUpdate) {
					updated[item] = p;
				}
			}
			var refreshList = new List<(IClassificationType type, TextFormattingRunProperties property)>();
			foreach (var item in updated) {
				foreach (var subType in item.Key.GetSubTypes()) {
					if (updated.ContainsKey(subType) == false) {
						if ((style = FormatStore.GetOrCreateStyle(subType)) == null
							|| (textFormatting = FormatStore.GetBackupFormatting(subType)) == null) {
							continue;
						}
						refreshList.Add((subType, SetProperties(textFormatting, style, defaultSize)));
					}
				}
			}
			if (refreshList.Count > 0) {
				foreach (var item in refreshList) {
					updated[item.type] = item.property;
				}
			}
			if (updated.Count > 0) {
				_ClassificationFormatMap.BeginBatchUpdate();
				foreach (var item in updated) {
					_ClassificationFormatMap.SetTextProperties(item.Key, item.Value);
					Debug.WriteLine("Update format: " + item.Key.Classification);
				}
				_ClassificationFormatMap.EndBatchUpdate();
				Debug.WriteLine($"Decorated {updated.Count} formats");
			}
		}

		TextFormattingRunProperties UpdateFormattingMap(StyleBase style, TextFormattingRunProperties textFormatting, double defaultSize) {
			var p = SetProperties(textFormatting, style, defaultSize);
			return textFormatting != p ? p : null;
		}

		TextFormattingRunProperties SetProperties(TextFormattingRunProperties format, StyleBase styleOption, double textSize) {
			var settings = styleOption;
			var fontSize = textSize + settings.FontSize;
			if (fontSize < 1) {
				fontSize = 1;
			}
			if (string.IsNullOrWhiteSpace(settings.Font) == false) {
				format = format.SetTypeface(new Typeface(settings.Font));
			}
			if (settings.FontSize != 0) {
				if (format.FontRenderingEmSizeEmpty || fontSize != format.FontRenderingEmSize) {
					format = format.SetFontRenderingEmSize(fontSize);
				}
			}
			if (settings.Bold.HasValue) {
				if (format.BoldEmpty || settings.Bold != format.Bold) {
					format = format.SetBold(settings.Bold.Value);
				}
			}
			if (settings.Italic.HasValue) {
				if (format.ItalicEmpty || settings.Italic != format.Italic) {
					format = format.SetItalic(settings.Italic.Value);
				}
			}
			if (settings.ForegroundOpacity > 0) {
				format = format.SetForegroundOpacity(settings.ForegroundOpacity / 255.0);
			}
			if (settings.ForeColor.A > 0) {
				if (format.ForegroundBrushEmpty || (format.ForegroundBrush as SolidColorBrush)?.Color != settings.ForeColor) {
					format = format.SetForeground(settings.ForeColor);
				}
			}
			if (settings.BackColor.A > 0) {
				var bc = settings.BackColor.A > 0 ? settings.BackColor
				   : format.BackgroundBrushEmpty == false && format.BackgroundBrush is SolidColorBrush ? (format.BackgroundBrush as SolidColorBrush).Color
				   : Colors.Transparent;
				if (settings.BackgroundOpacity != 0) {
					format = format.SetBackgroundOpacity(settings.BackgroundOpacity / 255.0);
				}
				if (bc.A > 0) {
					var bb = format.BackgroundBrush as LinearGradientBrush;
					switch (settings.BackgroundEffect) {
						case BrushEffect.Solid:
							if (format.BackgroundBrushEmpty || (format.BackgroundBrush as SolidColorBrush)?.Color != bc) {
								format = format.SetBackground(bc);
							}
							break;
						case BrushEffect.ToBottom:
							if (bb == null || bb.StartPoint.Y > bb.EndPoint.Y || bb.GradientStops.Count != 2
								|| bb.GradientStops[0].Color != _BackColor || bb.GradientStops[1].Color != bc) {
								format = format.SetBackgroundBrush(new LinearGradientBrush(_BackColor, bc, 90));
							}
							break;
						case BrushEffect.ToTop:
							if (bb == null || bb.StartPoint.Y < bb.EndPoint.Y || bb.GradientStops.Count != 2
								|| bb.GradientStops[0].Color != bc || bb.GradientStops[1].Color != _BackColor) {
								format = format.SetBackgroundBrush(new LinearGradientBrush(bc, _BackColor, 90));
							}
							bb = new LinearGradientBrush(bc, _BackColor, 90);
							break;
						case BrushEffect.ToRight:
							if (bb == null || bb.StartPoint.X >= bb.EndPoint.X || bb.GradientStops.Count != 2
								|| bb.GradientStops[0].Color != _BackColor || bb.GradientStops[1].Color != bc) {
								format = format.SetBackgroundBrush(new LinearGradientBrush(_BackColor, bc, 0));
							}
							break;
						case BrushEffect.ToLeft:
							if (bb == null || bb.StartPoint.X >= bb.EndPoint.X || bb.GradientStops.Count != 2
								|| bb.GradientStops[0].Color != bc || bb.GradientStops[1].Color != _BackColor) {
								format = format.SetBackgroundBrush(new LinearGradientBrush(bc, _BackColor, 0));
							}
							break;
						default:
							throw new NotImplementedException("Background effect not supported: " + settings.BackgroundEffect.ToString());
					}
				}
			}
			else if (settings.BackColor.A > 0) {
				if (format.BackgroundBrushEmpty || (format.BackgroundBrush as SolidColorBrush)?.Color != settings.BackColor) {
					format = format.SetBackground(settings.BackColor);
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
				format = format.SetTextDecorations(tdc);
			}
			return format;
		}
	}
}
