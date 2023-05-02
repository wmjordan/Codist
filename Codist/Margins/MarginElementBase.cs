using System;
using System.Windows;
using System.Windows.Automation.Peers;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Projection;

namespace Codist.Margins
{
	abstract class MarginElementBase : FrameworkElement, IWpfTextViewMargin
	{
		IElisionBuffer _VisualBuffer;

		public FrameworkElement VisualElement => this;
		public bool Enabled => true;

		public abstract string MarginName { get; }
		public abstract double MarginSize { get; }

		protected MarginElementBase(ITextView textView) {
			IsHitTestVisible = false;
			if (textView.Roles.Contains(PredefinedTextViewRoles.Structured)) {
				_VisualBuffer = textView.VisualSnapshot.TextBuffer as IElisionBuffer;
				if (_VisualBuffer != null) {
					_VisualBuffer.SourceSpansChanged += OnSourceSpansChanged;
				}
				textView.Closed += TextView_Closed;
			}
		}

		public abstract void Dispose();

		public ITextViewMargin GetTextViewMargin(string marginName) {
			return String.Equals(marginName, MarginName, StringComparison.OrdinalIgnoreCase) ? this : null;
		}

		protected override AutomationPeer OnCreateAutomationPeer() {
			return null;
		}

		void OnSourceSpansChanged(object sender, ElisionSourceSpansChangedEventArgs e) {
			InvalidateVisual();
		}

		void TextView_Closed(object sender, EventArgs e) {
			((ITextView)sender).Closed -= TextView_Closed;
			if (_VisualBuffer != null) {
				_VisualBuffer.SourceSpansChanged -= OnSourceSpansChanged;
				_VisualBuffer = null;
			}
		}
	}
}
