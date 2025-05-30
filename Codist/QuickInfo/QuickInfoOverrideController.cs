﻿using System;
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

		protected override Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			return Task.FromResult(new QuickInfoItem(null, new OverrideControllerInfoBlock(session)));
		}

		sealed class OverrideControllerInfoBlock : InfoBlock, IInteractiveQuickInfoContent
		{
			public OverrideControllerInfoBlock(IAsyncQuickInfoSession session) {
				Session = session;
			}

			public IAsyncQuickInfoSession Session { get; }
			public bool KeepQuickInfoOpen { get; set; }
			public bool IsMouseOverAggregated { get; set; }

			public override UIElement ToUI() {
				var session = Session;
				var ui = QuickInfoOverride.CreateOverride(session).CreateControl(session, SetHolder);
				if (session.Options == QuickInfoSessionOptions.TrackMouse) {
					session.Properties.AddProperty(QuickInfoPropertyKey, ui);
					session.StateChanged += Session_StateChanged;
				}
				return ui;
			}

			void SetHolder(bool hold) {
				IsMouseOverAggregated = hold;
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
				var session = _Session;
				Popup popup;
				IWpfTextView view;
				if (session == null
					|| session.ApplicableToSpan == null
					|| (view = session.TextView as IWpfTextView)?.VisualElement.IsVisible != true
					|| (popup = _Popup) == null) {
					return;
				}
				var viewLines = view.TextViewLines;
				var visibleLineTop = viewLines.FirstVisibleLine.Top;
				#region positioning fix for VS 2017
				if (popup.PlacementTarget == null) {
					popup.PlacementTarget = view.VisualElement;
				}
				#endregion
				var qi = _QuickInfo;
				if (qi == null) {
					return;
				}
				var mousePosition = System.Windows.Input.Mouse.GetPosition(popup.PlacementTarget);
				var cursorLine = viewLines.GetTextViewLineContainingBufferPosition(session.GetTriggerPoint(view.TextSnapshot).Value);
				if (cursorLine == null) {
					return;
				}
				var offsetLine = cursorLine.TextTop - view.ViewportTop;
				var textSpan = cursorLine.Extent.Intersection(session.GetTriggerSpan()).Value;
				if (textSpan.IsEmpty) {
					return;
				}
				var textBound = viewLines.GetTextMarkerGeometry(textSpan).Bounds;
				textBound.Offset(-view.ViewportLeft, -view.ViewportTop);
				var left = mousePosition.X - 40;
				var zoom = view.ZoomLevel / 100;
				var quickInfoHeight = qi.ActualHeight / zoom;
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
				qi.MaxHeight = (bottom - textBound.Bottom) * zoom;
				popup.PlacementRectangle = new Rect(new Point(left, offsetLine), textBound.Size);
				if (attachEventsOnDemand) {
					qi.SizeChanged += QuickInfo_SizeChanged;
					qi.Unloaded += QuickInfo_Unloaded;
				}
				return;
				SHOW_ON_TOP:
				qi.MaxHeight = (offsetLine - top) * zoom;
				popup.Placement = PlacementMode.Top;
				popup.PlacementRectangle = new Rect(new Point(left, offsetLine), textBound.Size);
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
