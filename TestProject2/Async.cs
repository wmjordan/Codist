using System;
using System.Threading.Tasks;

namespace TestProject.Async
{
	[ApiVersion(8)]
	public class AsyncForEach
	{
		public async Task ProcessGroupsAsync() {
			await foreach (var i in this) { }
		}

		public AsyncEnumerator GetAsyncEnumerator(System.Threading.CancellationToken token = default) {
			throw null;
		}

		public sealed class AsyncEnumerator : System.IAsyncDisposable
		{
			public int Current { get => throw null; }
			public Task<bool> MoveNextAsync() => throw null;

			public ValueTask DisposeAsync() => throw null;
		}
	}

}
