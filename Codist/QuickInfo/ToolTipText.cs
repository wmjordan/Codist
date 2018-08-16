using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.CodeAnalysis;

namespace Codist.QuickInfo
{
	sealed class ToolTipText : TextBlock
	{
		public ToolTipText() {
			Foreground = ThemeHelper.ToolTipTextBrush;
			TextWrapping = TextWrapping.Wrap;
		}
		public ToolTipText(string text) : this() {
			Inlines.Add(text);
		}
		public ToolTipText(string text, bool bold) : this() {
			this.Append(text, true);
		}
	}
}
