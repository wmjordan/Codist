using System;
using System.Windows;
using System.Windows.Input;
using CLR;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist.Margins
{
	sealed class DisableChangeTrackerMargin : FrameworkElement, IWpfTextViewMargin
	{
		readonly IWpfTextViewMargin _MarginContainer;
		FrameworkElement _Tracker;
		bool _DisabledChangeTracker, _TrackerEnabled = true;

		FrameworkElement IWpfTextViewMargin.VisualElement => this;
		bool ITextViewMargin.Enabled => true;
		double ITextViewMargin.MarginSize => 0;

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
			ToggleTracker(_DisabledChangeTracker == false || UIHelper.IsCtrlDown);
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

		public void Dispose() {
			_MarginContainer.VisualElement.MouseEnter -= EnterMarginContainer;
			Config.UnregisterUpdateHandler(ConfigUpdateHandler);
		}

		ITextViewMargin ITextViewMargin.GetTextViewMargin(string marginName) {
			return String.Equals(marginName, nameof(DisableChangeTrackerMargin), StringComparison.OrdinalIgnoreCase) ? this : null;
		}
	}
}
