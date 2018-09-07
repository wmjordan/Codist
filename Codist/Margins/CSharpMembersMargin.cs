using System;
using System.Windows;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist.Margins
{
	/// <summary>
	/// Implementation of a margin that show the member declaration spots within a C# code file.
	/// </summary>
	sealed class CSharpMembersMargin : IWpfTextViewMargin
	{
		public const string Name = nameof(CSharpMembersMargin);

		readonly CSharpMembersMarginElement _MemberMarginElement;
		bool _IsDisposed;

		public CSharpMembersMargin(IWpfTextViewHost textViewHost, IVerticalScrollBar scrollBar, CSharpMembersMarginFactory factory) {
			_MemberMarginElement = new CSharpMembersMarginElement(textViewHost.TextView, scrollBar, factory);
		}

		#region IWpfTextViewMargin Members
		/// <summary>
		/// The FrameworkElement that renders the margin.
		/// </summary>
		public FrameworkElement VisualElement {
			get {
				ThrowIfDisposed();
				return _MemberMarginElement;
			}
		}
		#endregion

		#region ITextViewMargin Members
		public double MarginSize {
			get {
				ThrowIfDisposed();
				return _MemberMarginElement.ActualWidth;
			}
		}

		public bool Enabled {
			get {
				ThrowIfDisposed();
				return _MemberMarginElement.Enabled;
			}
		}

		public ITextViewMargin GetTextViewMargin(string marginName) {
			return string.Equals(marginName, Name, StringComparison.OrdinalIgnoreCase) ? this : null;
		}

		public void Dispose() {
			if (!_IsDisposed) {
				_MemberMarginElement.Dispose();
				GC.SuppressFinalize(this);
				_IsDisposed = true;
			}
		}
		#endregion

		private void ThrowIfDisposed() {
			if (_IsDisposed) {
				throw new ObjectDisposedException(Name);
			}
		}
	}
}
