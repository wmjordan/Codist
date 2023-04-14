using System;
using System.Threading;

namespace Codist.Options
{
	sealed class UiLock
	{
		int _Locked;

		public bool IsLocked => _Locked != 0;
		public Action CommonEventAction { get; set; }
		public Action PostEventAction { get; set; }
		public bool Lock() {
			return Interlocked.CompareExchange(ref _Locked, 1, 0) == 0;
		}
		public bool Unlock() {
			return Interlocked.CompareExchange(ref _Locked, 0, 1) == 1;
		}
		public EventHandler HandleEvent (Action action) {
			return (sender, args) => {
				if (Lock()) {
					try {
						CommonEventAction?.Invoke();
						action();
						PostEventAction?.Invoke();
					}
					finally {
						Unlock();
					}
				}
			};
		}
		public bool DoWithLock(Action action) {
			if (Lock()) {
				try {
					action();
				}
				finally {
					Unlock();
				}
				return true;
			}
			return false;
		}
	}
}
