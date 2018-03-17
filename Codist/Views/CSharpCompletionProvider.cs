using System.ComponentModel.Composition;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Utilities;

namespace Codist.Views
{
	internal sealed class CompletionTooltipCustomization : TextBlock
	{

		[Export(typeof(IUIElementProvider<Completion, ICompletionSession>))]
		[Name("SampleCompletionTooltipCustomization")]
		//Roslyn is the default Tooltip Provider. We must override it if we wish to use custom tooltips
		[Order(Before = "RoslynToolTipProvider")]
		[ContentType(Constants.CodeTypes.CSharp)]
		internal sealed class CompletionTooltipCustomizationProvider : IUIElementProvider<Completion, ICompletionSession>
		{
			public UIElement GetUIElement(Completion itemToRender, ICompletionSession context, UIElementType elementType) {
				return elementType == UIElementType.Tooltip
					? new CompletionTooltipCustomization(itemToRender)
					: null;
			}
		}

		/// <summary>
		/// Custom constructor enables us to modify the text values of the tooltip. In this case, we are just modifying the font style and size
		/// </summary>
		/// <param name="completion">The tooltip to be modified</param>
		internal CompletionTooltipCustomization(Completion completion) {
			Text = string.Format(CultureInfo.CurrentCulture, "{0}: {1}", completion.DisplayText, completion.Description);
			FontSize = 24;
			FontStyle = FontStyles.Italic;
		}

	}
}
