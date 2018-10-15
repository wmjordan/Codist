using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Codist
{
	static class CancellationHelper
	{
		public static CancellationTokenSource CancelAndDispose(ref CancellationTokenSource tokenSource, bool resurrectCts) {
			var c = Interlocked.Exchange(ref tokenSource, resurrectCts ? new CancellationTokenSource() : null);
			if (c != null) {
				c.Cancel();
				c.Dispose();
			}
			return tokenSource;
		}
		public static CancellationToken GetToken(this CancellationTokenSource tokenSource) {
			return tokenSource != null ? tokenSource.Token : new CancellationToken(true);
		}
	}
}
