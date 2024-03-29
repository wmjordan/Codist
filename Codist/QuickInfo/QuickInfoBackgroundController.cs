﻿using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Language.Intellisense;

namespace Codist.QuickInfo
{
	sealed class QuickInfoBackgroundController : SingletonQuickInfoSource
	{
		SolidColorBrush _Background;

		public QuickInfoBackgroundController() {
			UpdateBackgroundBrush();
			Config.RegisterUpdateHandler(ConfigUpdated);
		}

		void ConfigUpdated(ConfigUpdatedEventArgs obj) {
			UpdateBackgroundBrush();
		}

		void UpdateBackgroundBrush() {
			var bc = Config.Instance.QuickInfo.BackgroundColor;
			if (String.IsNullOrEmpty(bc) == false && bc != Constants.EmptyColor) {
				UIHelper.ParseColor(bc, out var c, out var o);
				_Background = new SolidColorBrush(c);
				if (o > 0) {
					_Background.Opacity = o / 255d;
				}
			}
			else {
				_Background = null;
			}
		}

		protected override async Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
			return QuickInfoOverride.CheckCtrlSuppression() == false && _Background != null
				? new QuickInfoItem(null, new BackgroundController(_Background).Tag())
				: null;
		}

		public override void Dispose() {
			Config.UnregisterUpdateHandler(ConfigUpdated);
		}

		sealed class BackgroundController : UserControl
		{
			readonly Brush _Brush;

			public BackgroundController(Brush brush) {
				_Brush = brush;
			}

			protected override void OnVisualParentChanged(DependencyObject oldParent) {
				base.OnVisualParentChanged(oldParent);
				var p = this.GetParent<UserControl>(n => n.GetType().Name == "WpfToolTipControl");
				if (p != null && _Brush != null) {
					p.Background = _Brush;
				}
				var b = this.GetParent<Border>();
				if (b != null) {
					b.Visibility = Visibility.Collapsed;
				}
			}
		}
	}
}
