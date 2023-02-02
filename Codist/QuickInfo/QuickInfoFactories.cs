using System;
using System.ComponentModel.Composition;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text.Operations;
using AppHelpers;
using Microsoft.VisualStudio.Text;

namespace Codist.QuickInfo
{
	/// <summary>
	/// Provides quick info for named colors or #hex colors
	/// </summary>
	[Export(typeof(IAsyncQuickInfoSourceProvider))]
	[Name("Color Quick Info")]
	[Order(After = "Default Quick Info Presenter")]
	[ContentType(Constants.CodeTypes.Text)]
	sealed class ColorQuickInfoControllerProvider : IAsyncQuickInfoSourceProvider
	{
		public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer) {
			return Config.Instance.Features.MatchFlags(Features.SuperQuickInfo)
				? textBuffer.Properties.GetOrCreateSingletonProperty(() => new ColorQuickInfo(textBuffer))
				: null;
		}
	}

	[Export(typeof(IAsyncQuickInfoSourceProvider))]
	[Name(CSharpQuickInfo.Name)]
	[Order(After = "Default Quick Info Presenter")]
	[ContentType(Constants.CodeTypes.CSharp)]
	sealed class CSharpQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
	{
		public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer) {
			return Config.Instance.Features.MatchFlags(Features.SuperQuickInfo)
				? textBuffer.Properties.GetOrCreateSingletonProperty(() => new CSharpQuickInfo(textBuffer)).Reference()
				: null;
		}
	}

	/// <summary>
	/// <para>Controls whether quick info should be displayed.</para>
	/// <para>When activated, quick info will not be displayed unless Shift key is pressed.</para>
	/// <para>It is also used to suppress Quick Info when mouse is hovered on the SmartBar or NaviBar menu.</para>
	/// </summary>
	[Export(typeof(IAsyncQuickInfoSourceProvider))]
	[Name(nameof(QuickInfoVisibilityController))]
	[Order(Before = "Default Quick Info Presenter")]
	[ContentType(Constants.CodeTypes.Text)]
	sealed class QuickInfoVisibilityControllerProvider : IAsyncQuickInfoSourceProvider
	{
		public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer) {
			return textBuffer.Properties.GetOrCreateSingletonProperty(() => new QuickInfoVisibilityController(textBuffer));
		}
	}

	/// <summary>Shows information about selections.</summary>
	[Export(typeof(IAsyncQuickInfoSourceProvider))]
	[Name(Name)]
	[Order(After = CSharpQuickInfo.Name)]
	[ContentType(Constants.CodeTypes.Text)]
	sealed class SelectionQuickInfoProvider : IAsyncQuickInfoSourceProvider
	{
		const string Name = nameof(SelectionQuickInfoProvider);

		public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer) {
			return Config.Instance.Features.MatchFlags(Features.SuperQuickInfo)
				? textBuffer.Properties.GetOrCreateSingletonProperty(() => new SelectionQuickInfo(textBuffer))
				: null;
		}
	}

	/// <summary>Controls size of quick info.</summary>
	[Export(typeof(IAsyncQuickInfoSourceProvider))]
	[Name(Name)]
	[Order(After = "Default Quick Info Presenter")]
	[ContentType(Constants.CodeTypes.Text)]
	sealed class QuickInfoSizeControllerProvider : IAsyncQuickInfoSourceProvider
	{
		const string Name = nameof(QuickInfoSizeController);

		public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer) {
			return Config.Instance.Features.MatchFlags(Features.SuperQuickInfo)
				? textBuffer.Properties.GetOrCreateSingletonProperty(()=> new QuickInfoSizeController(textBuffer))
				: null;
		}
	}

	/// <summary>Controls background of quick info.</summary>
	[Export(typeof(IAsyncQuickInfoSourceProvider))]
	[Name(Name)]
	[Order(After = "Default Quick Info Presenter")]
	[ContentType(Constants.CodeTypes.Text)]
	sealed class QuickInfoBackgroundControllerProvider : IAsyncQuickInfoSourceProvider
	{
		const string Name = nameof(QuickInfoBackgroundController);

		public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer) {
			return Config.Instance.Features.MatchFlags(Features.SuperQuickInfo)
				? textBuffer.Properties.GetOrCreateSingletonProperty(()=> new QuickInfoBackgroundController(textBuffer))
				: null;
		}
	}
}
