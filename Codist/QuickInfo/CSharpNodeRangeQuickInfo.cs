using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CLR;
using Codist.Controls;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text.Editor;
using R = Codist.Properties.Resources;

namespace Codist.QuickInfo
{
	sealed class CSharpNodeRangeQuickInfo : SingletonQuickInfoSource
	{
		protected override Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			SemanticContext context;
			return Config.Instance.QuickInfoOptions.HasAnyFlag(QuickInfoOptions.NodeRange | QuickInfoOptions.SyntaxNodePath)
				&& session.TextView is IWpfTextView view
				&& (context = SemanticContext.GetOrCreateSingletonInstance(view)) != null
				? InternalGetQuickInfoItemAsync(session, context, cancellationToken)
				: Task.FromResult<QuickInfoItem>(null);
		}

		async Task<QuickInfoItem> InternalGetQuickInfoItemAsync(IAsyncQuickInfoSession session, SemanticContext sc, CancellationToken cancellationToken) {
			await sc.UpdateAsync(session.GetSourceBuffer(out var triggerPoint), triggerPoint, cancellationToken).ConfigureAwait(false);
			var token = sc.Compilation.FindToken(triggerPoint, true);
			var node = token.Parent;
			if (node == null) {
				return null;
			}
			var option = Config.Instance.QuickInfoOptions;
			if (option.MatchFlags(QuickInfoOptions.NodeRange)) {
				var rangeNode = node.GetNodePurpose();
				session.Properties.AddProperty(typeof(Tag), sc.MapSourceSpan(rangeNode.Span));
				session.StateChanged += Session_StateChanged;
			}
			if (option.MatchFlags(QuickInfoOptions.SyntaxNodePath)) {
				var block = new BlockItem(IconIds.SyntaxNode, R.T_SyntaxPath, true)
					.AppendLine()
					.Append(SyntaxKindCache.Cache[token.Kind()]);
				do {
					block.Append(" < ").Append(SyntaxKindCache.Cache[node.Kind()]);
				}
				while (node.Kind().IsDeclaration() == false && (node = node.Parent) != null);
				await SyncHelper.SwitchToMainThreadAsync(cancellationToken);
				return QuickInfoOverride.CheckCtrlSuppression()
					? null
					: new QuickInfoItem(session.ApplicableToSpan, new GeneralInfoBlock(block));
			}
			return null;
		}

		void Session_StateChanged(object sender, QuickInfoSessionStateChangedEventArgs e) {
			var s = (IAsyncQuickInfoSession)sender;
			if (s.TextView is IWpfTextView view) {
				switch (e.NewState) {
					case QuickInfoSessionState.Dismissed:
						s.StateChanged -= Session_StateChanged;
						TextViewOverlay.Get(view)?.ClearRangeAdornments();
						break;
					case QuickInfoSessionState.Visible:
						TextViewOverlay.Get(view)?.SetRangeAdornment(s.Properties.GetProperty<Microsoft.VisualStudio.Text.SnapshotSpan>(typeof(Tag)));
						break;
				}
			}
		}

		struct Tag { }

		static class SyntaxKindCache
		{
			public static readonly Dictionary<SyntaxKind, string> Cache = InitCache();

			static Dictionary<SyntaxKind, string> InitCache() {
				var type = typeof(SyntaxKind);
				var cache = new Dictionary<SyntaxKind, string>();
				foreach (var field in type.GetFields()) {
					if (field.FieldType == type) {
						cache.Add((SyntaxKind)field.GetValue(null), field.Name);
					}
				}
				return cache;
			}
		}
	}
}
