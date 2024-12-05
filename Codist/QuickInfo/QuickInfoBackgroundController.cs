using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CLR;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text.Classification;

namespace Codist.QuickInfo
{
	sealed class QuickInfoBackgroundController : SingletonQuickInfoSource
	{
		#region Adaptive background brightness
		static readonly IClassificationFormatMap __ToolTipFormatMap = InitFormatMap();
		static bool __Init;

		static IClassificationFormatMap InitFormatMap() {
			var m = ServicesHelper.Instance.ClassificationFormatMap.GetClassificationFormatMap("tooltip");
			m.ClassificationFormatMappingChanged += (s, args) => AdaptBackgroundBrightness(__ToolTipFormatMap);
			return m;
		}

		static void AdaptBackgroundBrightness(IClassificationFormatMap formatMap) {
			QuickInfoConfig c = Config.Instance.QuickInfo;
			if (c.BackColor.A != 0
				&& formatMap.DefaultTextProperties.ForegroundBrush is SolidColorBrush b
				&& b.Color.IsDark() == c.BackColor.IsDark()) {
				c.BackColor = c.BackColor.InvertBrightness();
			}
		}
		#endregion

		SolidColorBrush _Background;

		public QuickInfoBackgroundController() {
			UpdateBackgroundBrush();
			Config.RegisterUpdateHandler(ConfigUpdated);
		}

		void ConfigUpdated(ConfigUpdatedEventArgs args) {
			if (args.UpdatedFeature.MatchFlags(Features.SuperQuickInfo)) {
				AdaptBackgroundBrightness(__ToolTipFormatMap);
				UpdateBackgroundBrush();
			}
		}

		void UpdateBackgroundBrush() {
			if (__Init == false) {
				__Init = true;
				AdaptBackgroundBrightness(__ToolTipFormatMap);
			}
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
