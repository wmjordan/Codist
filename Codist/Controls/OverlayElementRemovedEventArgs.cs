using System.Windows;

namespace Codist.Controls
{
	public class OverlayElementRemovedEventArgs
	{
		public readonly UIElement RemovedElement;

		public OverlayElementRemovedEventArgs(UIElement removed) {
			RemovedElement = removed;
		}
	}
}
