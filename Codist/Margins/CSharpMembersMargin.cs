using System;
using System.Windows;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist.Margins
{
	/// <summary>
	/// Implementation of a margin that show the structure of a code file.
	/// </summary>
	sealed class CSharpMembersMargin : IWpfTextViewMargin
	{
		public const string Name = "CodeMembers";

		readonly CSharpMembersMarginElement _structureMarginElement;
		bool _isDisposed;

		public CSharpMembersMargin(IWpfTextViewHost textViewHost, IVerticalScrollBar scrollBar, CSharpMembersMarginFactory factory) {
			_structureMarginElement = new CSharpMembersMarginElement(textViewHost.TextView, scrollBar, factory);
		}

		#region IWpfTextViewMargin Members
		/// <summary>
		/// The FrameworkElement that renders the margin.
		/// </summary>
		public FrameworkElement VisualElement {
			get {
				ThrowIfDisposed();
				return _structureMarginElement;
			}
		}
		#endregion

		#region ITextViewMargin Members
		public double MarginSize {
			get {
				ThrowIfDisposed();
				return _structureMarginElement.ActualWidth;
			}
		}

		public bool Enabled {
			get {
				ThrowIfDisposed();
				return _structureMarginElement.Enabled;
			}
		}

		public ITextViewMargin GetTextViewMargin(string marginName) {
			return string.Equals(marginName, Name, StringComparison.OrdinalIgnoreCase) ? this : null;
		}

		public void Dispose() {
			if (!_isDisposed) {
				_structureMarginElement.Dispose();
				GC.SuppressFinalize(this);
				_isDisposed = true;
			}
		}
		#endregion

		private void ThrowIfDisposed() {
			if (_isDisposed) {
				throw new ObjectDisposedException(Name);
			}
		}
	}
}
