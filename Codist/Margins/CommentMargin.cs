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
	sealed class CommentMargin : Canvas, IWpfTextViewMargin
	{
		/// <summary>
		/// Margin name.
		/// </summary>
		public const string MarginName = nameof(CommentMargin);

		readonly CommentMarginElement _CodeMarginElement;
		readonly ITagAggregator<ClassificationTag> _CodeTagAggregator;
		readonly IWpfTextViewHost _TextView;
		bool isDisposed;

		/// <summary>
		/// Initializes a new instance of the <see cref="CommentMargin"/> class for a given <paramref name="textView"/>.
		/// </summary>
		/// <param name="textView">The <see cref="IWpfTextView"/> to attach the margin to.</param>
		public CommentMargin(IWpfTextViewHost textView, IVerticalScrollBar scrollBar, CommentMarginFactory container) {
			_TextView = textView ?? throw new ArgumentNullException(nameof(textView));
			_CodeTagAggregator = container.ViewTagAggregatorFactoryService.CreateTagAggregator<ClassificationTag>(textView.TextView);
			_CodeMarginElement = new CommentMarginElement(textView.TextView, container, _CodeTagAggregator, scrollBar);
			textView.Closed += TextView_Closed;
		}

		private void TextView_Closed(object sender, EventArgs e) {
			_CodeTagAggregator.Dispose();
			_CodeMarginElement.Dispose();
		}

		#region IWpfTextViewMargin
		/// <summary>
		/// Gets the <see cref="FrameworkElement"/> that implements the visual representation of the margin.
		/// </summary>
		/// <exception cref="ObjectDisposedException">The margin is disposed.</exception>
		public FrameworkElement VisualElement {
			// Since this margin implements Canvas, this is the object which renders
			// the margin.
			get {
				ThrowIfDisposed();
				return _CodeMarginElement;
			}
		}

		#endregion

		#region ITextViewMargin

		/// <summary>
		/// Gets the size of the margin.
		/// </summary>
		/// <remarks>
		/// For a horizontal margin this is the height of the margin,
		/// since the width will be determined by the <see cref="ITextView"/>.
		/// For a vertical margin this is the width of the margin,
		/// since the height will be determined by the <see cref="ITextView"/>.
		/// </remarks>
		/// <exception cref="ObjectDisposedException">The margin is disposed.</exception>
		public double MarginSize {
			get {
				ThrowIfDisposed();

				return _CodeMarginElement.ActualWidth;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the margin is enabled.
		/// </summary>
		/// <exception cref="ObjectDisposedException">The margin is disposed.</exception>
		public bool Enabled {
			get {
				ThrowIfDisposed();

				// The margin should always be enabled
				return true;
			}
		}

		/// <summary>
		/// Gets the <see cref="ITextViewMargin"/> with the given <paramref name="marginName"/> or null if no match is found
		/// </summary>
		/// <param name="marginName">The name of the <see cref="ITextViewMargin"/></param>
		/// <returns>The <see cref="ITextViewMargin"/> named <paramref name="marginName"/>, or null if no match is found.</returns>
		/// <remarks>
		/// A margin returns itself if it is passed its own name. If the name does not match and it is a container margin, it
		/// forwards the call to its children. Margin name comparisons are case-insensitive.
		/// </remarks>
		/// <exception cref="ArgumentNullException"><paramref name="marginName"/> is null.</exception>
		public ITextViewMargin GetTextViewMargin(string marginName) {
			return string.Equals(marginName, MarginName, StringComparison.OrdinalIgnoreCase) ? this : null;
		}

		/// <summary>
		/// Disposes an instance of <see cref="CommentMargin"/> class.
		/// </summary>
		public void Dispose() {
			if (!isDisposed) {
				_TextView.Closed -= TextView_Closed;
				GC.SuppressFinalize(this);
				isDisposed = true;
			}
		}

		#endregion

		/// <summary>
		/// Checks and throws <see cref="ObjectDisposedException"/> if the object is disposed.
		/// </summary>
		private void ThrowIfDisposed() {
			if (isDisposed) {
				throw new ObjectDisposedException(MarginName);
			}
		}
	}
}
