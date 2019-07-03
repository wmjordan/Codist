using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Codist
{
	static class SyncHelper
	{
		public static void RunSync(Func<Task> func) {
			Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.Run(func);
		}
		public static TResult RunSync<TResult>(Func<Task<TResult>> func) {
			return Microsoft.VisualStudio.Shell.ThreadHelper.JoinableTaskFactory.Run(func);
		}

		[DebuggerStepThrough]
		public static CancellationToken CancelAndRetainToken(ref CancellationTokenSource tokenSource) {
			return CancelAndDispose(ref tokenSource, true).GetToken();
		}

		[DebuggerStepThrough]
		public static CancellationTokenSource CancelAndDispose(ref CancellationTokenSource tokenSource, bool resurrect) {
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
			return tokenSource;
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
