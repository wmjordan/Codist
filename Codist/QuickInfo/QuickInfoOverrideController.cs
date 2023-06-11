using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text.Editor;

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
					ui.GetParent<System.Windows.Controls.Border>().Collapse();
					var quickInfo = ui.GetParent<FrameworkElement>(n => n.GetType().Name == "PopupRoot");
					if (quickInfo?.Parent is Popup popup) {
						popup.CustomPopupPlacementCallback = null;
						popup.Placement = PlacementMode.Bottom;
						quickInfo.UpdateLayout();
						new QuickInfoPositioner(s, quickInfo, popup).Reposition(true);
					}
					break;
			}
		}

		sealed class QuickInfoPositioner
		{
			IAsyncQuickInfoSession _Session;
			FrameworkElement _QuickInfo;
			Popup _Popup;

			public QuickInfoPositioner(IAsyncQuickInfoSession session, FrameworkElement quickInfo, Popup popup) {
				_Session = session;
				_QuickInfo = quickInfo;
				_Popup = popup;
			}

			// reposition the Quick Info to prevent it from hindering text selection with mouse cursor
			public void Reposition(bool attachEventsOnDemand) {
				if (_Session.TextView is IWpfTextView view == false
					|| _Session.ApplicableToSpan == null
					|| view.VisualElement.IsVisible == false) {
					return;
				}
				var viewLines = view.TextViewLines;
				var visibleLineTop = viewLines.FirstVisibleLine.Top;
				#region positioning fix for VS 2017
				if (_Popup.PlacementTarget == null) {
					_Popup.PlacementTarget = view.VisualElement;
				}
				#endregion
				var mousePosition = System.Windows.Input.Mouse.GetPosition(_Popup.PlacementTarget);
				var cursorLine = viewLines.GetTextViewLineContainingBufferPosition(_Session.GetTriggerPoint(view.TextSnapshot).Value);
				var offsetLine = cursorLine.TextTop - view.ViewportTop;
				var textSpan = cursorLine.Extent.Intersection(_Session.GetTriggerSpan()).Value;
				var textBound = viewLines.GetTextMarkerGeometry(textSpan).Bounds;
				textBound.Offset(-view.ViewportLeft, -view.ViewportTop);
				var left = mousePosition.X - 40;
				var zoom = view.ZoomLevel / 100;
				var quickInfoHeight = _QuickInfo.ActualHeight / zoom;
				var top = view.VisualElement.PointFromScreen(new Point(0, 0)).Y;
				var bottom = view.VisualElement.PointFromScreen(new Point(0, WpfHelper.GetActiveScreenSize().Height)).Y;
				if (offsetLine + cursorLine.TextHeight + quickInfoHeight > bottom) {
					// Quick Info popup is over the line with mouse cursor
					if (view.VisualElement.PointToScreen(new Point(left, offsetLine - quickInfoHeight - cursorLine.TextHeight)).Y >= 0) {
						goto SHOW_ON_TOP;

					}
					if (textBound.Bottom + quickInfoHeight >= bottom) {
						// find the max vertical room to place the popup,
						// resize if popup can go off screen
						if (offsetLine - top >= bottom - textBound.Bottom) {
							goto SHOW_ON_TOP;
						}
					}
				}
				_QuickInfo.MaxHeight = (bottom - textBound.Bottom) * zoom;
				_Popup.PlacementRectangle = new Rect(new Point(left, offsetLine), textBound.Size);
				if (attachEventsOnDemand) {
					_QuickInfo.SizeChanged += QuickInfo_SizeChanged;
					_QuickInfo.Unloaded += QuickInfo_Unloaded;
				}
				return;
				SHOW_ON_TOP:
				_QuickInfo.MaxHeight = (offsetLine - top) * zoom;
				_Popup.Placement = PlacementMode.Top;
				_Popup.PlacementRectangle = new Rect(new Point(left, offsetLine), textBound.Size);
			}

			void QuickInfo_SizeChanged(object sender, SizeChangedEventArgs e) {
				if (e.NewSize.Height != 0 && e.NewSize.Height != e.PreviousSize.Height) {
					Reposition(false);
				}
			}

			void QuickInfo_Unloaded(object sender, RoutedEventArgs e) {
				var quickInfo = (FrameworkElement)sender;
				quickInfo.SizeChanged -= QuickInfo_SizeChanged;
				quickInfo.Unloaded -= QuickInfo_Unloaded;
				_Session = null;
				_QuickInfo = null;
				_Popup = null;
			}
		}
	}
}
