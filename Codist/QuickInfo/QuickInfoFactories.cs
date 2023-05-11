using System;
using System.ComponentModel.Composition;
using AppHelpers;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace Codist.QuickInfo
{
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
			return textBuffer.GetOrCreateSingletonProperty<QuickInfoVisibilityController>();
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
				? textBuffer.GetOrCreateSingletonProperty<QuickInfoOverrideController>()
				: null;
		}
	}

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
				? textBuffer.GetOrCreateSingletonProperty<ColorQuickInfo>()
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
				? textBuffer.GetOrCreateSingletonProperty<CSharpQuickInfo>()
				: null;
		}
	}

	/// <summary>Highlight range of hovered C# node in code editor.</summary>
	[Export(typeof(IAsyncQuickInfoSourceProvider))]
	[Name(nameof(CSharpNodeRangeQuickInfoSourceProvider))]
	[Order(After = CSharpQuickInfo.Name)]
	[ContentType(Constants.CodeTypes.CSharp)]
	sealed class CSharpNodeRangeQuickInfoSourceProvider : IAsyncQuickInfoSourceProvider
	{
		public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer) {
			return Config.Instance.Features.MatchFlags(Features.SuperQuickInfo)
				? textBuffer.GetOrCreateSingletonProperty<CSharpNodeRangeQuickInfo>()
				: null;
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
				? textBuffer.GetOrCreateSingletonProperty<SelectionQuickInfo>()
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
				? textBuffer.GetOrCreateSingletonProperty<QuickInfoBackgroundController>()
				: null;
		}
	}
}
