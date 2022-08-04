using System;
using System.Windows;
using System.Windows.Automation.Peers;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist.Margins
{
	abstract class MarginElementBase : FrameworkElement, IWpfTextViewMargin
	{
		public FrameworkElement VisualElement => this;
		public bool Enabled => true;

		public abstract string MarginName { get; }
		public abstract double MarginSize { get; }

		protected MarginElementBase() {
			IsHitTestVisible = false;
		}

		public abstract void Dispose();

		public ITextViewMargin GetTextViewMargin(string marginName) {
			return String.Equals(marginName, MarginName, StringComparison.OrdinalIgnoreCase) ? this : null;
		}

		protected override AutomationPeer OnCreateAutomationPeer() {
			return null;
		}
	}
}
