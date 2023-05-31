using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace Codist.QuickInfo
{
	sealed class QuickInfoOverrideController : SingletonQuickInfoSource
	{
		const string QuickInfoPropertyKey = "Codist.UIOverride";

		protected override async Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
			if (QuickInfoOverride.CheckCtrlSuppression()) {
				return null;
			}

			var ui = QuickInfoOverride.CreateOverride(session).CreateControl(session);
			if (session.Options == QuickInfoSessionOptions.TrackMouse) {
				session.Properties.AddProperty(QuickInfoPropertyKey, ui);
				session.StateChanged += Session_StateChanged;
			}
			return new QuickInfoItem(null, ui);
		}

		void Session_StateChanged(object sender, QuickInfoSessionStateChangedEventArgs e) {
			var s = (IAsyncQuickInfoSession)sender;
			switch (e.NewState) {
				case QuickInfoSessionState.Dismissed:
					s.StateChanged -= Session_StateChanged;
					break;
				case QuickInfoSessionState.Visible:
					var ui = s.Properties.GetProperty<UIElement>(QuickInfoPropertyKey);
					var quickInfo = ui.GetParent<FrameworkElement>(n => n.GetType().Name == "PopupRoot");
					if (quickInfo?.Parent is Popup popup) {
						RepositionQuickInfoIfOverCursor(s, quickInfo, popup);
						quickInfo.SizeChanged += QuickInfo_SizeChanged;
						quickInfo.Unloaded += QuickInfo_Unloaded;
					}
					break;
			}
		}

		void QuickInfo_SizeChanged(object sender, SizeChangedEventArgs e) {
			var quickInfo = (FrameworkElement)sender;
			if (quickInfo.Parent is Popup popup) {
				popup.PlacementRectangle = new Rect(new Point(popup.PlacementRectangle.Left, popup.PlacementRectangle.Top - (e.NewSize.Height - e.PreviousSize.Height)), popup.RenderSize);
			}
		}

		void QuickInfo_Unloaded(object sender, RoutedEventArgs e) {
			var quickInfo = (FrameworkElement)sender;
			quickInfo.SizeChanged -= QuickInfo_SizeChanged;
			quickInfo.Unloaded -= QuickInfo_Unloaded;
		}

		// reposition the Quick Info to prevent it from hindering text selection with mouse cursor
		static void RepositionQuickInfoIfOverCursor(IAsyncQuickInfoSession s, FrameworkElement quickInfo, Popup popup) {
			if (s.TextView is Microsoft.VisualStudio.Text.Editor.IWpfTextView view) {
				var visibleLineTop = view.TextViewLines.FirstVisibleLine.Top;
				var mousePosition = System.Windows.Input.Mouse.GetPosition(popup.PlacementTarget);
				var cursorLine = view.TextViewLines.GetTextViewLineContainingYCoordinate(mousePosition.Y + visibleLineTop);
				var offsetLine = cursorLine.Top - visibleLineTop;
				// if the Quick Info popup is over the line with mouse cursor
				if (offsetLine + quickInfo.ActualHeight > view.ViewportHeight) {
					// move the Quick Info popup window on top of the line
					popup.PlacementRectangle = new Rect(new Point(popup.PlacementRectangle.Left, popup.PlacementRectangle.Top - quickInfo.ActualHeight * 100 / view.ZoomLevel - cursorLine.TextHeight), popup.RenderSize);
				}
			}
		}
	}
}
