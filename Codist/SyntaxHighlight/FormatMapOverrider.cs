using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using AppHelpers;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist.SyntaxHighlight
{
	sealed class FormatMapOverrider
	{
		readonly IWpfTextView _TextView;
		//readonly IClassificationFormatMap _ClassificationFormatMap;
		readonly IEditorFormatMap _EditorFormatMap;
		readonly IClassificationTypeRegistryService _RegService;
		volatile int _IsDecorating;

		public FormatMapOverrider(IWpfTextView view) {
			view.Closed += View_Closed;
			Config.Updated += SettingsSaved;

			_EditorFormatMap = ServicesHelper.Instance.EditorFormatMap.GetEditorFormatMap(view);
			_EditorFormatMap.FormatMappingChanged += FormatMappingChanged;
			_RegService = ServicesHelper.Instance.ClassificationTypeRegistry;
			_TextView = view;

			UpdateFormats();
		}
		void FormatMappingChanged(object sender, FormatItemsEventArgs e) {
			var normalSize = FormatStore.DefaultClassificationFormatMap.DefaultTextProperties.FontRenderingEmSize;
			foreach (var item in e.ChangedItems) {
				UpdateFormatProperties(_EditorFormatMap.GetProperties(item), FormatStore.GetStyle(item), normalSize);
			}
		}

		void UpdateFormats() {
			if (Interlocked.CompareExchange(ref _IsDecorating, 1, 0) != 0) {
				return;
			}
			if (_EditorFormatMap.IsInBatchUpdate) {
				return;
			}
			try {
				_EditorFormatMap.BeginBatchUpdate();
				var normalSize = FormatStore.DefaultClassificationFormatMap.DefaultTextProperties.FontRenderingEmSize;
				foreach (var item in FormatStore.GetStyles()) {
					var p = _EditorFormatMap.GetProperties(item.Key);
					var style = item.Value;
					var updated = UpdateFormatProperties(p, style, normalSize);
					if (updated) {
						_EditorFormatMap.SetProperties(item.Key, p);
					}
				}
			}
			finally {
				_EditorFormatMap.EndBatchUpdate();
				_IsDecorating = 0;
			}
		}

		static bool UpdateFormatProperties(ResourceDictionary p, StyleBase style, double normalSize) {
			var updated = false;
			if (style.ForeColor != p.GetColor()) {
				if (style.ForeColor.A > 0) {
					p.SetBrush(new SolidColorBrush(style.ForeColor.MakeOpaque()));
					p[EditorFormatDefinition.ForegroundColorId] = style.ForeColor.MakeOpaque();
					updated = true;
				}
				else if (p.Contains(EditorFormatDefinition.ForegroundBrushId) || p.Contains(EditorFormatDefinition.ForegroundColorId)) {
					p.Remove(EditorFormatDefinition.ForegroundBrushId);
					p.Remove(EditorFormatDefinition.ForegroundColorId);
					updated = true;
				}
			}
			if (style.ForegroundOpacity != p.GetOpacity()) {
				if (style.ForegroundOpacity != Byte.MaxValue && style.ForegroundOpacity != 0) {
					p[Constants.EditorFormatKeys.ForegroundOpacity] = style.ForegroundOpacity / 255.0;
					updated = true;
				}
				else if (p.Contains(Constants.EditorFormatKeys.ForegroundOpacity)) {
					p.Remove(Constants.EditorFormatKeys.ForegroundOpacity);
					updated = true;
				}
			}
			if (style.FontSize != p.GetFontSize()) {
				if (style.FontSize > 0) {
					p[Constants.EditorFormatKeys.FontRenderingSize] = style.FontSize + normalSize;
					updated = true;
				}
				else if (p.Contains(Constants.EditorFormatKeys.FontRenderingSize)) {
					p.Remove(Constants.EditorFormatKeys.FontRenderingSize);
					updated = true;
				}
			}

			return updated;
		}

		void View_Closed(object sender, EventArgs e) {
			Config.Updated -= SettingsSaved;
			//_ClassificationFormatMap.ClassificationFormatMappingChanged -= FormatUpdated;
			_EditorFormatMap.FormatMappingChanged -= FormatMappingChanged;
			_TextView.Closed -= View_Closed;
		}

		void SettingsSaved(object sender, ConfigUpdatedEventArgs eventArgs) {
			if (eventArgs.UpdatedFeature.MatchFlags(Features.SyntaxHighlight)) {
				FormatUpdated(sender, eventArgs);
			}
		}

		void FormatUpdated(object sender, EventArgs e) {
			if (_IsDecorating == 0 && _TextView.VisualElement.IsVisible) {
				UpdateFormats();
			}
		}

	}
}
