using System.Windows;
using System.Windows.Controls;

namespace Codist.Controls
{
	public class SymbolItemTemplateSelector : DataTemplateSelector
	{
		public override DataTemplate SelectTemplate(object item, DependencyObject container) {
			var c = container as FrameworkElement;
			return item is SymbolItem s && (s.Symbol != null || s.SyntaxNode != null || s.Location != null)
				? c.FindResource("SymbolItemTemplate") as DataTemplate
				: c.FindResource("LabelTemplate") as DataTemplate;
		}
	}
}
