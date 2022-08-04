using System;
using System.Windows;
using System.Windows.Input;
using AppHelpers;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist.Margins
{
	sealed class DisableChangeTrackerMargin : MarginElementBase, IWpfTextViewMargin
	{
		readonly IWpfTextViewMargin _MarginContainer;
		FrameworkElement _Tracker;
		bool _DisabledChangeTracker, _TrackerEnabled = true;

		public override string MarginName => nameof(DisableChangeTrackerMargin);
		public override double MarginSize => 0;

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

		public override void Dispose() {
			_MarginContainer.VisualElement.MouseEnter -= EnterMarginContainer;
			Config.UnregisterUpdateHandler(ConfigUpdateHandler);
		}
	}
}
