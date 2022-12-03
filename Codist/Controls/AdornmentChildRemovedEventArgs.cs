using System.Windows;

namespace Codist.Controls
{
	public class AdornmentChildRemovedEventArgs
	{
		public readonly UIElement RemovedElement;

		public AdornmentChildRemovedEventArgs(UIElement removed) {
			RemovedElement = removed;
		}
	}
}
