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
	// The syntax highlight in VS is implemented in Microsoft.VisualStudio.Platform.VSEditor.dll
	// see: Microsoft.VisualStudio.Text.Classification.Implementation.ClassificationFormatMap
	// see: Microsoft.VisualStudio.Text.Classification.Implementation.ViewSpecificFormatMap
	// see: Microsoft.VisualStudio.Text.Classification.Implementation.EditorFormatMap
	// see: Microsoft.VisualStudio.Text.Formatting.Implementation.NormalizedSpanGenerator
	// see: https://stackoverflow.com/questions/24404473/create-visual-studio-theme-specific-syntax-highlighting
	// The difficulties of the implementation are:
	// 1. override TextFormattingRunProperties to change the display styles of classified type;
	// 2. can revert to original styles;
	// 3. detect when theme changes and still satisify 1 and 2 afterwards;
	// 4. work in all text view;
	// 5. good performance, don't do anything redundantly
	sealed class HighlightDecorator
	{
		static readonly IClassificationType __BraceMatchingClassificationType = ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationType(Constants.CodeBraceMatching);
		static bool __Initialized;
		static FontFamily __DefaultFontFamily;
		static double __DefaultFontSize;

		IWpfTextView _TextView;
		IClassificationFormatMap _ClassificationFormatMap;
		IClassificationTypeRegistryService _RegService;
		IEditorFormatMap _EditorFormatMap;

		Color _BackColor, _ForeColor;
		volatile int _IsDecorating;
		bool _IsViewVisible;

		public HighlightDecorator(IWpfTextView view) {
			view.Closed += View_Closed;
			view.VisualElement.IsVisibleChanged += VisualElement_IsVisibleChanged;
			Config.RegisterUpdateHandler(UpdateSyntaxHighlightConfig);

			_ClassificationFormatMap = ServicesHelper.Instance.ClassificationFormatMap.GetClassificationFormatMap(view);
			_EditorFormatMap = ServicesHelper.Instance.EditorFormatMap.GetEditorFormatMap(view);
			_ClassificationFormatMap.ClassificationFormatMappingChanged += FormatUpdated;
			_RegService = ServicesHelper.Instance.ClassificationTypeRegistry;
			_TextView = view;

			_IsViewVisible = true;
			if (view.TextBuffer.ContentType.IsOfType(Constants.CodeTypes.Output) == false) {
				Decorate(FormatStore.ClassificationTypeStore.Keys, __Initialized == false);
				Debug.WriteLine("Attached highlight decorator for " + view.TextBuffer.ContentType);
				if (__Initialized == false) {
					__Initialized = true;
				}
			}
			else {
				Decorate(_ClassificationFormatMap.CurrentPriorityOrder, false);
			}
			_EditorFormatMap.FormatMappingChanged += FormatUpdated;
		}

		void VisualElement_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
			_IsViewVisible = (bool)e.NewValue;
		}

		void View_Closed(object sender, EventArgs e) {
			_IsViewVisible = false;
			Config.UnregisterUpdateHandler(UpdateSyntaxHighlightConfig);
			_ClassificationFormatMap.ClassificationFormatMappingChanged -= FormatUpdated;
			_TextView.VisualElement.IsVisibleChanged -= VisualElement_IsVisibleChanged;
			_TextView.Properties.RemoveProperty(typeof(HighlightDecorator));
			_EditorFormatMap.FormatMappingChanged -= FormatUpdated;
			_TextView.Closed -= View_Closed;
			_ClassificationFormatMap = null;
			_EditorFormatMap = null;
			_RegService = null;
			_TextView = null;
		}

		void UpdateSyntaxHighlightConfig(ConfigUpdatedEventArgs eventArgs) {
			if (eventArgs.UpdatedFeature.MatchFlags(Features.SyntaxHighlight)) {
				if (eventArgs.Parameter is string t) {
					Decorate(new[] { ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationType(t) }, true);
				}
				else {
					Decorate(_ClassificationFormatMap.CurrentPriorityOrder, false);
				}
			}
		}


		void FormatUpdated(object sender, EventArgs e) {
			Debug.WriteLine("ClassificationFormatMapping changed.");
			var defaultProperties = _ClassificationFormatMap.DefaultTextProperties;
			if (__DefaultFontFamily == defaultProperties.Typeface.FontFamily && __DefaultFontSize == defaultProperties.FontRenderingEmSize) {
				return;
			}
			Debug.WriteLineIf(__DefaultFontFamily != null, "Default text properties changed.");
			__DefaultFontFamily = defaultProperties.Typeface.FontFamily;
			__DefaultFontSize = defaultProperties.FontRenderingEmSize;
			// hack: it is weird that this property is not in sync with the Text editor format, we have to force that
			_EditorFormatMap.GetProperties(Constants.EditorProperties.PlainText)
				.SetTypeface(defaultProperties.Typeface);
			if (_IsDecorating != 0) {
				Debug.WriteLine("Cancelled formatMap update.");
				return;
			}
			var updated = new Dictionary<IClassificationType, TextFormattingRunProperties>();
			foreach (var item in FormatStore.GetStyles()) {
				// explicitly update formats when font name or font size is changed in font options
				if (item.Value.Stretch.HasValue && String.IsNullOrWhiteSpace(item.Value.Font)
					|| item.Value.FontSize != 0) {
					var key = _RegService.GetClassificationType(item.Key);
					if (key == null) {
						continue;
					}
					updated[key] = SetProperties(_ClassificationFormatMap.GetTextProperties(key), item.Value, __DefaultFontSize);
				}
			}
			if (updated.Count > 0) {
				Debug.WriteLine("Decorate updated format: " + updated.Count);
				Decorate(updated.Keys, true);
			}
		}

		void FormatUpdated(object sender, FormatItemsEventArgs e) {
			if (_IsDecorating == 0 && _IsViewVisible && e.ChangedItems.Count > 0) {
				Debug.WriteLine("Format updated: " + e.ChangedItems.Count);
				Decorate(e.ChangedItems.Select(_RegService.GetClassificationType), true);
			}
		}

		void Decorate(IEnumerable<IClassificationType> classifications, bool fullUpdate) {
			if (_ClassificationFormatMap.IsInBatchUpdate || Interlocked.CompareExchange(ref _IsDecorating, 1, 0) != 0) {
				return;
			}
			try {
				var c = _EditorFormatMap.GetColor(Constants.EditorProperties.Text, EditorFormatDefinition.ForegroundColorId);
				if (c.A > 0) {
					if (c != _ForeColor) {
						Debug.WriteLine("Fore color changed: " + _ForeColor.ToString() + "->" + c.ToString());
					}
					_ForeColor = c;
				}
				c = _EditorFormatMap.GetColor(Constants.EditorProperties.TextViewBackground, EditorFormatDefinition.BackgroundColorId);
				if (c.A > 0) {
					_BackColor = c.Alpha(0);
				}
				DecorateClassificationTypes(classifications, fullUpdate);
			}
			catch (Exception ex) {
				Debug.WriteLine("Decorator exception: ");
				Debug.WriteLine(ex);
				if (Debugger.IsAttached) {
					Debugger.Break();
				}
			}
			finally {
				_IsDecorating = 0;
			}
		}

		void DecorateClassificationTypes(IEnumerable<IClassificationType> classifications, bool fullUpdate) {
			var defaultSize = _ClassificationFormatMap.DefaultTextProperties.FontRenderingEmSize;
			var updated = new Dictionary<IClassificationType, TextFormattingRunProperties>();
			StyleBase style;
			TextFormattingRunProperties textFormatting;
			foreach (var item in classifications) {
				if (item == null
					|| (style = FormatStore.GetOrCreateStyle(item)) == null
					|| (textFormatting = FormatStore.GetOrSaveBackupFormatting(item, __Initialized == false)) == null) {
					continue;
				}

				var p = SetProperties(textFormatting, style, defaultSize);
				if (p != textFormatting || fullUpdate) {
					updated[item] = p;
				}
			}
			if (updated.Count == 0) {
				return;
			}
			var refreshList = new List<(IClassificationType type, TextFormattingRunProperties property)>();
			foreach (var item in updated) {
				// hack: we have to update the sub-classficationTypes in order to make C# Braces and Parentheses highlighting work properly
				foreach (var subType in item.Key.GetSubTypes()) {
					if (updated.ContainsKey(subType) == false) {
						if ((style = FormatStore.GetOrCreateStyle(subType)) == null
							|| (textFormatting = subType.GetBackupFormatting()) == null) {
							continue;
						}
						refreshList.Add((subType, SetProperties(textFormatting, style, defaultSize)));
					}
				}
			}
			if (refreshList.Count > 0) {
				foreach (var (type, property) in refreshList) {
					updated[type] = property;
				}
			}
			if (updated.Count > 0) {
				_ClassificationFormatMap.BeginBatchUpdate();
				foreach (var item in updated) {
					if (item.Key == __BraceMatchingClassificationType) {
						continue;
					}
					try {
						_ClassificationFormatMap.SetTextProperties(item.Key, item.Value);
					}
					catch (Exception ex) {
						// hack Weird bug in VS: NullReferenceException can occur here even if item.Key is not null
						Debug.WriteLine($"Update format {item.Key.Classification} error: {ex}");
					}
					Debug.WriteLine("Update format: " + item.Key.Classification);
				}
				_ClassificationFormatMap.EndBatchUpdate();
				Debug.WriteLine($"Decorated {updated.Count} formats");
			}
		}

		TextFormattingRunProperties SetProperties(TextFormattingRunProperties format, StyleBase styleOption, double textSize) {
			var settings = styleOption;
			var fontSize = textSize + settings.FontSize;
			if (fontSize < 1) {
				fontSize = 1;
			}
			if (string.IsNullOrWhiteSpace(settings.Font) == false || settings.Stretch.HasValue) {
				format = format.SetTypeface(new Typeface(
					string.IsNullOrWhiteSpace(settings.Font) == false ? new FontFamily(settings.Font) : __DefaultFontFamily,
					FontStyles.Normal,
					FontWeights.Normal,
					settings.Stretch.HasValue ? FontStretch.FromOpenTypeStretch(settings.Stretch.Value) : FontStretches.Normal));
			}
			if (settings.FontSize != 0) {
				if (format.FontRenderingEmSizeEmpty || fontSize != format.FontRenderingEmSize) {
					format = format.SetFontRenderingEmSize(fontSize);
				}
			}
			else if (format.FontRenderingEmSizeEmpty == false) {
				format = format.ClearFontRenderingEmSize();
				if (format.FontRenderingEmSize != fontSize) {
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
				   : format.BackgroundBrushEmpty == false && format.BackgroundBrush is SolidColorBrush b ? b.Color
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
			if (settings.Underline.HasValue || settings.Strikethrough.HasValue || settings.OverLine.HasValue || settings.LineColor.A > 0) {
				var tdc = new TextDecorationCollection();
				if (settings.Underline.GetValueOrDefault() || settings.LineColor.A > 0) {
					if (settings.LineColor.A > 0) {
						tdc.Add(GetLineDecoration(settings, TextDecorationLocation.Underline));
					}
					else {
						tdc.Add(TextDecorations.Underline);
					}
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

		static TextDecoration GetLineDecoration(StyleBase settings, TextDecorationLocation location) {
			var d = new TextDecoration {
				Location = location,
				Pen = new Pen {
					Brush = new SolidColorBrush(settings.LineOpacity == 0 ? settings.LineColor : settings.LineColor.Alpha(settings.LineOpacity))
				}
			};
			if (settings.LineOffset > 0) {
				d.PenOffset = settings.LineOffset;
				d.PenOffsetUnit = TextDecorationUnit.Pixel;
			}
			if (settings.LineThickness > 0) {
				d.Pen.Thickness = settings.LineThickness + 1;
				d.PenThicknessUnit = TextDecorationUnit.Pixel;
			}
			if (settings.LineStyle != LineStyle.Solid) {
				switch (settings.LineStyle) {
					case LineStyle.Dot: d.Pen.DashStyle = new DashStyle(new double[] { 2, 2 }, 0);
						break;
					case LineStyle.Dash: d.Pen.DashStyle = new DashStyle(new double[] { 4, 4 }, 0);
						break;
					case LineStyle.DashDot: d.Pen.DashStyle = new DashStyle(new double[] { 4, 4, 2, 4 }, 0);
						break;
					default:
						break;
				}
			}
			d.Freeze();
			return d;
		}
	}
}
