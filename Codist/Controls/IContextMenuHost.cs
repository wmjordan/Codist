using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Codist.Controls
{
	interface IContextMenuHost
	{
		void ShowContextMenu(RoutedEventArgs args);
	}
}
