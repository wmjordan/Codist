using System;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using Codist.Controls;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist.NaviBar;

public abstract class NaviBar : ToolBar, INaviBar
{
	IWpfTextView _View;
	int _Views;

	protected NaviBar(IWpfTextView textView) {
		_View = textView;
		_View.Closed += View_Closed;
		_Views = 1;
		ViewOverlay = TextViewOverlay.GetOrCreate(textView);
		this.SetBackgroundForCrispImage(ThemeCache.TitleBackgroundColor);
		textView.Properties.AddProperty(nameof(NaviBar), this);
		Resources = SharedDictionaryManager.NavigationBar;
		UseLayoutRounding = true;
		SnapsToDevicePixels = true;
		if (CodistPackage.VsVersion.Major < 18) {
			SetResourceReference(BackgroundProperty, VsBrushes.CommandBarMenuBackgroundGradientKey);
			SetResourceReference(ForegroundProperty, VsBrushes.CommandBarTextInactiveKey);
		}
	}

	public abstract void ShowActiveItemMenu();
	public abstract void ShowRootItemMenu(int parameter);
	protected abstract void BindView(IWpfTextView view);
	protected abstract void BindExtraView(IWpfTextView view);
	protected abstract void LeaveView(IWpfTextView view);
	protected abstract void EnterView(IWpfTextView view);
	protected abstract void UnbindView(IWpfTextView view);

	protected IWpfTextView View => _View;
	protected int Views => _Views;
	internal TextViewOverlay ViewOverlay { get; private set; }

	protected override void OnPreviewMouseRightButtonUp(MouseButtonEventArgs e) {
		if ((e.Source as DependencyObject).GetParentOrSelf<DependencyObject>(o => o is IContextMenuHost) is IContextMenuHost h) {
			h.ShowContextMenu(e);
			e.Handled = true;
		}
	}

	protected override AutomationPeer OnCreateAutomationPeer() {
		return null;
	}

	public override void OnApplyTemplate() {
		base.OnApplyTemplate();
		if (CodistPackage.VsVersion.Major >= 18) {
			this.GetFirstVisualChild<StackPanel>()?.Background = default;
		}
	}

	void View_Closed(object sender, EventArgs e) {
		if (sender is IWpfTextView view) {
			view.Closed -= View_Closed;
			view.GotAggregateFocus -= SwitchViewOnFocus;

			UnbindView(view);
			if (--_Views == 0) {
				var visualParent = this.GetParent<FrameworkElement>();
				if (visualParent is Panel p) {
					p.Children.Remove(this);
				}
				else if (visualParent is ContentControl c) {
					c.Content = null;
				}
				this.DisposeCollection();
				DataContext = null;
				ViewOverlay = null;
			}
			else if (_View != view) {
				ViewOverlay = TextViewOverlay.GetOrCreate(_View);
			}
			view.Properties.RemoveProperty(nameof(NaviBar));
			view.Properties.RemoveProperty(typeof(TextViewOverlay));
		}
	}

	internal void AssociateExtraView(IWpfTextView view) {
		_Views++;
		_View.GotAggregateFocus -= SwitchViewOnFocus;
		_View.GotAggregateFocus += SwitchViewOnFocus;
		view.Properties.AddProperty(nameof(NaviBar), this);
		view.GotAggregateFocus += SwitchViewOnFocus;
		view.Closed += View_Closed;
		BindExtraView(view);
	}

	void SwitchViewOnFocus(object sender, EventArgs e) {
		var view = (IWpfTextView)sender;
		if (_View != view) {
			if (_View != null) {
				LeaveView(_View);
			}
			_View = view;
			ViewOverlay = TextViewOverlay.GetOrCreate(view);
			EnterView(view);
		}
		if (_Views == 0) {
			EnterView(view);
			view.GotAggregateFocus -= SwitchViewOnFocus;
		}
	}
}
