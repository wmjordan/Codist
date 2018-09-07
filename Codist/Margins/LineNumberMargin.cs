using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace Codist.Margins
{
	/// <summary>
	/// Margin's canvas and visual definition including both size and content
	/// </summary>
	sealed class LineNumberMargin : Canvas, IWpfTextViewMargin
	{
		/// <summary>
		/// Margin name.
		/// </summary>
		public const string MarginName = nameof(LineNumberMargin);

		readonly LineNumberMarginElement _LineNumberMarginElement;
		readonly IWpfTextViewHost _TextView;
		bool isDisposed;

		/// <summary>
		/// Initializes a new instance of the <see cref="LineNumberMargin"/> class for a given <paramref name="textView"/>.
		/// </summary>
		/// <param name="textView">The <see cref="IWpfTextView"/> to attach the margin to.</param>
		public LineNumberMargin(IWpfTextViewHost textView, IVerticalScrollBar scrollBar, LineNumberMarginFactory container) {
			_LineNumberMarginElement = new LineNumberMarginElement(textView.TextView, scrollBar);
			textView.Closed += TextView_Closed;
			_TextView = textView;
		}

		public bool Enabled => true;

		public double MarginSize => _LineNumberMarginElement.ActualWidth;

		public FrameworkElement VisualElement => _LineNumberMarginElement;

		public void Dispose() {
			if (!isDisposed) {
				_TextView.Closed -= TextView_Closed;
				GC.SuppressFinalize(this);
				isDisposed = true;
			}
		}

		public ITextViewMargin GetTextViewMargin(string marginName) {
			return string.Equals(marginName, MarginName, StringComparison.OrdinalIgnoreCase) ? this : null;
		}

		void TextView_Closed(object sender, EventArgs e) {
			_LineNumberMarginElement.Dispose();
		}
	}
}
