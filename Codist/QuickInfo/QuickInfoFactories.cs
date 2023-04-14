using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Utilities;
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
				? textBuffer.Properties.GetOrCreateSingletonProperty(() => new ColorQuickInfo())
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
				? textBuffer.Properties.GetOrCreateSingletonProperty(() => new CSharpQuickInfo())
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
			return textBuffer.Properties.GetOrCreateSingletonProperty(() => new QuickInfoVisibilityController());
		}
	}

	/// <summary>Shows information about selections.</summary>
	[Export(typeof(IAsyncQuickInfoSourceProvider))]
	[Name(nameof(SelectionQuickInfoProvider))]
	[Order(After = CSharpQuickInfo.Name)]
	[ContentType(Constants.CodeTypes.Text)]
	sealed class SelectionQuickInfoProvider : IAsyncQuickInfoSourceProvider
	{
		public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer) {
			return Config.Instance.Features.MatchFlags(Features.SuperQuickInfo)
				? textBuffer.Properties.GetOrCreateSingletonProperty(() => new SelectionQuickInfo())
				: null;
		}
	}

	/// <summary>Provides size limitation, selectable text blocks, icon for warning messages, etc. to Quick Info.</summary>
	[Export(typeof(IAsyncQuickInfoSourceProvider))]
	[Name(nameof(QuickInfoOverrideController))]
	[Order(After = "Default Quick Info Presenter")]
	[ContentType(Constants.CodeTypes.Text)]
	sealed class QuickInfoOverrideProvider : IAsyncQuickInfoSourceProvider
	{
		public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer) {
			return Config.Instance.Features.MatchFlags(Features.SuperQuickInfo)
				? textBuffer.Properties.GetOrCreateSingletonProperty(() => new QuickInfoOverrideController())
				: null;
		}
	}

	/// <summary>Controls background of quick info.</summary>
	[Export(typeof(IAsyncQuickInfoSourceProvider))]
	[Name(nameof(QuickInfoBackgroundController))]
	[Order(After = "Default Quick Info Presenter")]
	[ContentType(Constants.CodeTypes.Text)]
	sealed class QuickInfoBackgroundControllerProvider : IAsyncQuickInfoSourceProvider
	{
		public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer) {
			return Config.Instance.Features.MatchFlags(Features.SuperQuickInfo)
				? textBuffer.Properties.GetOrCreateSingletonProperty(()=> new QuickInfoBackgroundController())
				: null;
		}
	}
}
