using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;

namespace Codist.Classifiers
{
	/// <summary>
	/// The background scanner which parse given <see cref="ITextSnapshot"/> and produce result of type <typeparamref name="TProduct"/>.
	/// </summary>
	/// <typeparam name="TProduct">The produced parse result by the background scanner.</typeparam>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable")]
	sealed class BackgroundScan<TProduct>
	{
		CancellationTokenSource _cancellationSource = new CancellationTokenSource();

		/// <summary>
		/// Does a background scan in <paramref name="snapshot"/>. Call
		/// <paramref name="completionCallback"/> once the scan has completed.
		/// </summary>
		/// <param name="snapshot">Text snapshot in which to scan.</param>
		/// <param name="parser">The parse method to be called within the scanner.</param>
		/// <param name="completionCallback">Delegate to call if the scan is completed (will be called on the UI thread).</param>
		/// <remarks>The constructor must be called from the UI thread.</remarks>
		public BackgroundScan(ITextSnapshot snapshot, Func<ITextSnapshot, CancellationToken, Task<TProduct>> parser, Action<TProduct> completionCallback) {
			Task.Run(async delegate {
				var token = _cancellationSource.Token;
				var newRoot = await parser(snapshot, token);

				if ((newRoot != null) && !token.IsCancellationRequested) {
					completionCallback(newRoot);
				}
			});
		}

		public void Cancel() {
			CancellationHelper.CancelAndDispose(ref _cancellationSource, false);
		}
	}
}
