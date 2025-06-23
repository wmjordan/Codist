using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CLR;
using Codist.Controls;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using R = Codist.Properties.Resources;

namespace Codist.QuickInfo
{
	sealed class CSharpNodeRangeQuickInfo : SingletonQuickInfoSource
	{
		protected override Task<QuickInfoItem> GetQuickInfoItemAsync(IAsyncQuickInfoSession session, CancellationToken cancellationToken) {
			SemanticContext context;
			ITextBuffer textBuffer;
			return Config.Instance.QuickInfoOptions.HasAnyFlag(QuickInfoOptions.NodeRange | QuickInfoOptions.SyntaxNodePath)
				&& session.TextView is IWpfTextView view
				&& (textBuffer = session.GetSourceBuffer(out var triggerPoint)) != null
				&& triggerPoint.Position < textBuffer.CurrentSnapshot.Length
				&& (context = SemanticContext.GetOrCreateSingletonInstance(view)) != null
				? InternalGetQuickInfoItemAsync(session, context, textBuffer, triggerPoint, cancellationToken)
				: Task.FromResult<QuickInfoItem>(null);
		}

		async Task<QuickInfoItem> InternalGetQuickInfoItemAsync(IAsyncQuickInfoSession session, SemanticContext sc, ITextBuffer textBuffer, SnapshotPoint triggerPoint, CancellationToken cancellationToken) {
			if (await sc.UpdateAsync(textBuffer, triggerPoint, cancellationToken).ConfigureAwait(false) == false
				|| sc.Compilation is null) {
				return null;
			}
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
					.Append(token, triggerPoint.Snapshot, SyntaxKindCache.Cache[token.Kind()], false);
				var pNode = node.GetNodePurpose();
				do {
					block.Append(" < ").Append(node, triggerPoint.Snapshot, SyntaxKindCache.Cache[node.Kind()], node == pNode);
				}
				while (node.Kind().IsDeclaration() == false && (node = node.Parent) != null);
				return new QuickInfoItem(token.Span.CreateSnapshotSpan(triggerPoint.Snapshot).ToTrackingSpan(), new GeneralInfoBlock(block));
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
