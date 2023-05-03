using System;

namespace Codist
{
	sealed class EventArgs<TData> : EventArgs
	{
		public EventArgs(TData data) {
			Data = data;
		}

		public TData Data { get; }
	}
}
