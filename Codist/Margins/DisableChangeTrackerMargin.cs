using System;
using System.Windows;
using System.Windows.Input;
using AppHelpers;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist.Margins
{
	sealed class DisableChangeTrackerMargin : FrameworkElement, IWpfTextViewMargin
	{
		internal const string MarginName = nameof(DisableChangeTrackerMargin);
		readonly IWpfTextViewMargin _MarginContainer;
		FrameworkElement _Tracker;
		bool _DisabledChangeTracker, _TrackerEnabled = true;

		public FrameworkElement VisualElement => this;
		public double MarginSize => 0;
		public bool Enabled => true;

		public DisableChangeTrackerMargin(IWpfTextViewMargin marginContainer) {
			marginContainer.VisualElement.MouseEnter += EnterMarginContainer;
			Config.RegisterUpdateHandler(ConfigUpdateHandler);
			_MarginContainer = marginContainer;
			_DisabledChangeTracker = Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.DisableChangeTracker);
		}

		void ConfigUpdateHandler(ConfigUpdatedEventArgs e) {
			if (e.UpdatedFeature.MatchFlags(Features.ScrollbarMarkers)) {
				ToggleTracker((_DisabledChangeTracker = e.Config.MarkerOptions.MatchFlags(MarkerOptions.DisableChangeTracker)) == false);
			}
		}

		void EnterMarginContainer(object sender, MouseEventArgs e) {
			ToggleTracker(_DisabledChangeTracker == false || Keyboard.Modifiers.MatchFlags(ModifierKeys.Control));
		}

		void ToggleTracker(bool enable) {
			if (enable == _TrackerEnabled) {
				return;
			}
			var t = _Tracker ?? (_Tracker = _MarginContainer.VisualElement.GetFirstVisualChild<FrameworkElement>(i => i.GetType().Name == "ChangeTrackingMarginElement"));
			if (t != null) {
				t.IsEnabled = _TrackerEnabled = enable;
			}
		}

		void IDisposable.Dispose() {
			_MarginContainer.VisualElement.MouseEnter -= EnterMarginContainer;
			Config.UnregisterUpdateHandler(ConfigUpdateHandler);
		}

		ITextViewMargin ITextViewMargin.GetTextViewMargin(string marginName) {
			return string.Equals(marginName, MarginName, StringComparison.OrdinalIgnoreCase) ? this : null;
		}
	}
}
