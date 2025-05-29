using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using CLR;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
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
	[Order(After = nameof(QuickInfoVisibilityController))]
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
	[Order(After = nameof(QuickInfoVisibilityController))]
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
	[Order(After = nameof(QuickInfoVisibilityController))]
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
	[Order(After = nameof(QuickInfoVisibilityController))]
	[ContentType(Constants.CodeTypes.Text)]
	sealed class QuickInfoBackgroundControllerProvider : IAsyncQuickInfoSourceProvider
	{
		public IAsyncQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer) {
			return Config.Instance.Features.MatchFlags(Features.SuperQuickInfo)
				? textBuffer.GetOrCreateSingletonProperty<QuickInfoBackgroundController>()
				: null;
		}
	}

	/// <summary>
	/// A base class which prevents the same Quick Info Source from being created more than once for the same <see cref="IAsyncQuickInfoSession"/>.
	/// </summary>
	/// <remarks>In VS, there is a bug which can cause <see cref="IAsyncQuickInfoSource.GetQuickInfoItemAsync(IAsyncQuickInfoSession, CancellationToken)"/> being called twice in the C# Interactive window.</remarks>
	abstract class SingletonQuickInfoSource : IAsyncQuickInfoSource
	{
		Task<QuickInfoItem> IAsyncQuickInfoSource.GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			return Config.Instance.Features.MatchFlags(Features.SuperQuickInfo) == false
				|| QuickInfoOverride.CheckCtrlSuppression()
				|| session.Properties.ContainsProperty(GetType())
				? Task.FromResult<QuickInfoItem>(null)
				: InternalGetQuickInfoItemAsync(session, cancellationToken);
		}

		protected abstract Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken);

		async Task<QuickInfoItem> InternalGetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			QuickInfoItem item;
			try {
				item = await GetQuickInfoItemAsync(session, cancellationToken);
			}
			catch (OperationCanceledException) {
				throw;
			}
			catch (Exception ex) {
				await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
				Controls.MessageWindow.Error(ex, null, Properties.Resources.T_SuperQuickInfo, this);
				return null;
			}
			if (item != null) {
				session.Properties.AddProperty(GetType(), this);
			}
			return item;
		}

		public virtual void Dispose() { }
	}

	[Export(typeof(IViewElementFactory))]
	[Name("InfoBlock Quick Info Factory")]
	[TypeConversion(from: typeof(InfoBlock), to: typeof(System.Windows.UIElement))]
	[Order(Before = "Default object converter")]
	public class InfoBlockQuickInfoFactory : IViewElementFactory
	{
		public TView CreateViewElement<TView>(ITextView textView, object model)
			where TView : class {
			return model is InfoBlock data
				? data.ToUI().Tag() as TView
				: null;
		}
	}
}
