using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ThreadHelper = Microsoft.VisualStudio.Shell.ThreadHelper;

namespace Codist
{
	static class SyncHelper
	{
		public static void RunSync(Func<Task> func) {
			try {
				ThreadHelper.JoinableTaskFactory.Run(func);
			}
			catch (OperationCanceledException) {
				// ignore
			}
		}
		public static TResult RunSync<TResult>(Func<Task<TResult>> func) {
			try {
				return ThreadHelper.JoinableTaskFactory.Run(func);
			}
			catch (OperationCanceledException) {
				return default;
			}
		}

		/// <summary>Starts a task and forget it.</summary>
		public static void FireAndForget(this Task task) {
			_ = ThreadHelper.JoinableTaskFactory.RunAsync(async () => {
				try {
#pragma warning disable VSTHRD003 // As a fire-and-forget continuation, deadlocks can't happen.
					await task.ConfigureAwait(false);
#pragma warning restore VSTHRD003
				}
				catch (Exception ex) {
					// ignore error
					Debug.WriteLine(ex.Message);
				}
			});
		}

		[DebuggerStepThrough]
		public static CancellationToken CancelAndRetainToken(ref CancellationTokenSource tokenSource) {
			CancelAndDispose(ref tokenSource, true);
			return tokenSource.GetToken();
		}

		[DebuggerStepThrough]
		public static void CancelAndDispose(ref CancellationTokenSource tokenSource, bool resurrect) {
			var c = Interlocked.Exchange(ref tokenSource, resurrect ? new CancellationTokenSource() : null);
			if (c != null) {
				try {
					c.Cancel();
				}
				catch (ObjectDisposedException) {
					// ignore
				}
				catch (AggregateException) {
					// ignore
				}
				c.Dispose();
			}
		}
		[DebuggerStepThrough]
		public static CancellationToken GetToken(this CancellationTokenSource tokenSource) {
			if (tokenSource == null) {
				return new CancellationToken(true);
			}
			try {
				return tokenSource.Token;
			}
			catch (ObjectDisposedException) {
				return new CancellationToken(true);
			}
		}
	}
}
