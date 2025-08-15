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
		static SolidColorBrush __Background;

		static IClassificationFormatMap InitFormatMap() {
			var m = ServicesHelper.Instance.ClassificationFormatMap.GetClassificationFormatMap("tooltip");
			m.ClassificationFormatMappingChanged += (s, args) => UpdateBackgroundBrush();
			return m;
		}

		static void AdaptBrightnessOfBackgroundColor(ref Color bc) {
			if (bc.A != 0
					&& __ToolTipFormatMap.DefaultTextProperties.ForegroundBrush is SolidColorBrush b
					&& b.Color.IsDark() == bc.IsDark()) {
				Config.Instance.QuickInfo.BackColor = bc = bc.InvertBrightness();
			}
		}
		#endregion

		public QuickInfoBackgroundController() {
			UpdateBackgroundBrush();
			Config.RegisterUpdateHandler(ConfigUpdated);
		}

		void ConfigUpdated(ConfigUpdatedEventArgs args) {
			if (args.UpdatedFeature.MatchFlags(Features.SuperQuickInfo)) {
				UpdateBackgroundBrush();
			}
		}

		static void UpdateBackgroundBrush() {
			var bc = Config.Instance.QuickInfo.BackColor;
			var o = bc.A;
			if (o != 0) {
				AdaptBrightnessOfBackgroundColor(ref bc);

				var b = new SolidColorBrush(bc.Alpha(Byte.MaxValue));
				if (o > 0) {
					b.Opacity = o / 255d;
				}
				b.Freeze();
				__Background = b;
			}
			else {
				__Background = null;
			}
		}

		protected override Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			var result = __Background != null
				? new QuickInfoItem(null, new BackgroundControllerInfoBlock(__Background))
				: null;
			return Task.FromResult(result);
		}

		public override void Dispose() {
			Config.UnregisterUpdateHandler(ConfigUpdated);
		}

		sealed class BackgroundControllerInfoBlock : InfoBlock
		{
			public BackgroundControllerInfoBlock(Brush background) {
				Background = background;
			}

			public Brush Background { get; }

			public override UIElement ToUI() {
				return new BackgroundController(Background).Tag();
			}
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
