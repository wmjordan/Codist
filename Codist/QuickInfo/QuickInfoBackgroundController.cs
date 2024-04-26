using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CLR;
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

		void ConfigUpdated(ConfigUpdatedEventArgs args) {
			if (args.UpdatedFeature.MatchFlags(Features.SuperQuickInfo)) {
				UpdateBackgroundBrush();
			}
		}

		void UpdateBackgroundBrush() {
			var bc = Config.Instance.QuickInfo.BackColor;
			var o = bc.A;
			if (o != 0) {
				if (_Background == null) {
					_Background = new SolidColorBrush(bc.Alpha(Byte.MaxValue));
				}
				else {
					_Background.Color = bc.Alpha(Byte.MaxValue);
				}
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
