using System;

namespace Codist
{
	public sealed class EventArgs<TData> : EventArgs
	{
		public EventArgs(TData data) {
			Data = data;
		}

		public TData Data { get; }
	}
}
