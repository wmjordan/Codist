using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Codist
{
	static class CancellationHelper
	{
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
