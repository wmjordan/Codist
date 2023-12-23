using System;
using System.Windows;
using System.Windows.Controls;
using R = Codist.Properties.Resources;

namespace Codist.Controls
{
	sealed class SearchScopeBox : UserControl
	{
		readonly ThemedToggleButton _ProjectFilter, _DocumentFilter;
		bool _UiLock;
		private ScopeType _Filter;

		public event EventHandler FilterChanged;

		public SearchScopeBox() {
			_DocumentFilter = CreateButton(IconIds.File, R.T_SearchCurrentDocument);
			_ProjectFilter = CreateButton(IconIds.Project, R.T_SearchCurrentProject);
			Margin = WpfHelper.SmallHorizontalMargin;
			Content = new ThemedControlGroup(_DocumentFilter, _ProjectFilter);
			_DocumentFilter.IsChecked = true;
		}

		public ScopeType Filter {
			get => _Filter;
			set {
				if (_Filter != value) {
					switch (value) {
						case ScopeType.ActiveDocument: _DocumentFilter.IsChecked = true;
							break;
						case ScopeType.ActiveProject: _ProjectFilter.IsChecked = true;
							break;
					}
				}
			}
		}

		public UIElementCollection Contents => ((StackPanel)((Border)Content).Child).Children;

		ThemedToggleButton CreateButton(int imageId, string toolTip) {
			var b = new ThemedToggleButton(imageId, toolTip).ClearSpacing();
			b.Checked += UpdateFilterValue;
			b.Unchecked += KeepChecked;
			return b;
		}

		void KeepChecked(object sender, RoutedEventArgs e) {
			if (_UiLock) {
				return;
			}
			_UiLock = true;
			(sender as ThemedToggleButton).IsChecked = true;
			e.Handled = true;
			_UiLock = false;
		}

		void UpdateFilterValue(object sender, RoutedEventArgs eventArgs) {
			if (_UiLock) {
				return;
			}
			_UiLock = true;
			_ProjectFilter.IsChecked = _DocumentFilter.IsChecked = false;
			(sender as ThemedToggleButton).IsChecked = true;
			_UiLock = false;
			var f = sender == _DocumentFilter ? ScopeType.ActiveDocument
				: sender == _ProjectFilter ? ScopeType.ActiveProject
				: ScopeType.Undefined;
			if (_Filter != f) {
				_Filter = f;
				FilterChanged?.Invoke(this, EventArgs.Empty);
			}
		}
	}
}
