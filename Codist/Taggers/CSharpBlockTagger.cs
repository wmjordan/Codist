using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace Codist.Taggers
{
	sealed class CSharpBlockTagger : ITagger<ICodeMemberTag>, IReusableTagger
	{
		ITextBuffer _Buffer;
		int _RefCount;
		CodeBlock _Root;
		CancellationTokenSource _Cancellation;

		internal CSharpBlockTagger(ITextBuffer buffer) {
			_Buffer = buffer;
			_Buffer.ContentTypeChanged += TextBuffer_ContentTypeChanged;
		}

		public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

		public void AddRef() {
			if (++_RefCount == 1) {
				_Buffer.Changed += OnBufferChanged;
				_ = ScanBufferAsync(_Buffer.CurrentSnapshot);
			}
		}

		public IEnumerable<ITagSpan<ICodeMemberTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
			var root = _Root;  //this.root could be set on a background thread, so get a snapshot.
			if (root == null) {
				yield break;
			}

			if (root.Span.Snapshot != spans[0].Snapshot) {
				//There is a version skew between when the parse was done and what is being asked for.
				var translatedSpans = new List<SnapshotSpan>(spans.Count);
				foreach (var span in spans) {
					translatedSpans.Add(span.TranslateTo(root.Span.Snapshot, SpanTrackingMode.EdgeInclusive));
				}
				spans = new NormalizedSnapshotSpanCollection(translatedSpans);
			}

			foreach (var child in root.Children) {
				foreach (var tag in GetTags(child, spans)) {
					yield return tag;
				}
			}
		}

		public void Release() {
			if (--_RefCount == 0) {
				ReleaseResources();
			}
		}

		void ReleaseResources() {
			_Buffer.ContentTypeChanged -= TextBuffer_ContentTypeChanged;
			_Buffer.Changed -= OnBufferChanged;
			_Buffer.Properties.RemoveProperty(typeof(CSharpBlockTaggerProvider));
			_Buffer = null;

			//Stop and blow away the old scan (even if it didn't finish, the results are not interesting anymore).
			SyncHelper.CancelAndDispose(ref _Cancellation, false);
			_Root = null; //Allow the old root to be GC'd
		}

		void TextBuffer_ContentTypeChanged(object sender, ContentTypeChangedEventArgs e) {
			if (e.AfterContentType.IsOfType(Constants.CodeTypes.CSharp) == false) {
				ReleaseResources();
				_RefCount = 0;
			}
		}

		static async Task<CodeBlock> ParseAsync(ITextSnapshot snapshot, CancellationToken token) {
			try {
				return await GetAndParseSyntaxNodeAsync(snapshot, token);
			}
			catch (OperationCanceledException) {
				//ignore the exception.
				return null;
			}
		}

		static async Task<CodeBlock> GetAndParseSyntaxNodeAsync(ITextSnapshot snapshot, CancellationToken token) {
			var document = snapshot.GetOpenDocumentInCurrentContextWithChanges();
			var parentSyntaxNode = await document.GetSyntaxRootAsync(token).ConfigureAwait(false);
			var root = new CodeBlock(null, CodeMemberType.Root, null, new SnapshotSpan(snapshot, 0, snapshot.Length), 0);

			ParseSyntaxNode(snapshot, parentSyntaxNode, root, 0, token);

			return root;
		}

		static IEnumerable<ITagSpan<ICodeMemberTag>> GetTags(CodeBlock block, NormalizedSnapshotSpanCollection spans) {
			if (spans.IntersectsWith(new NormalizedSnapshotSpanCollection(block.Span))) {
				yield return new TagSpan<ICodeMemberTag>(block.Span, block);

				foreach (var child in block.Children) {
					foreach (var tag in GetTags(child, spans)) {
						yield return tag;
					}
				}
			}
		}

		static CodeMemberType MatchDeclaration(SyntaxNode node) {
			switch (node.Kind()) {
				case SyntaxKind.ClassDeclaration:
				case CodeAnalysisHelper.RecordDeclaration:
					return CodeMemberType.Class;
				case SyntaxKind.InterfaceDeclaration:
					return CodeMemberType.Interface;
				case SyntaxKind.StructDeclaration:
					return CodeMemberType.Struct;
				case SyntaxKind.EnumDeclaration:
					return CodeMemberType.Enum;
				case SyntaxKind.ConstructorDeclaration:
				case SyntaxKind.DestructorDeclaration:
					return CodeMemberType.Constructor;
				case SyntaxKind.MethodDeclaration:
				case SyntaxKind.OperatorDeclaration:
				case SyntaxKind.ConversionOperatorDeclaration:
					return CodeMemberType.Method;
				case SyntaxKind.IndexerDeclaration:
				case SyntaxKind.PropertyDeclaration:
					return CodeMemberType.Property;
				case SyntaxKind.FieldDeclaration:
					return CodeMemberType.Field;
				case SyntaxKind.EventDeclaration:
				case SyntaxKind.EventFieldDeclaration:
					return CodeMemberType.Event;
				case SyntaxKind.DelegateDeclaration:
					return CodeMemberType.Delegate;
				default:
					return CodeMemberType.Unknown;
			}
		}

		static void ParseSyntaxNode(ITextSnapshot snapshot, SyntaxNode parentSyntaxNode, CodeBlock parentCodeBlockNode, int level, CancellationToken token) {
			if (token.IsCancellationRequested) {
				throw new TaskCanceledException();
			}

			foreach (var node in parentSyntaxNode.ChildNodes()) {
				var type = MatchDeclaration(node);
				if (type == CodeMemberType.Unknown) {
					ParseSyntaxNode(snapshot, node, parentCodeBlockNode, level, token);
					continue;
				}

				var name = (node as BaseTypeDeclarationSyntax)?.Identifier ?? (node as MethodDeclarationSyntax)?.Identifier;
				var child = new CodeBlock(parentCodeBlockNode, type, name?.Text, new SnapshotSpan(snapshot, node.SpanStart, node.Span.Length), level + 1);
				if (type > CodeMemberType.Type) {
					continue;
				}
				ParseSyntaxNode(snapshot, node, child, level + 1, token);
			}
		}

		[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
		async void OnBufferChanged(object sender, TextContentChangedEventArgs e) {
			try {
				if (TextEditorHelper.AnyTextChanges(e.Before.Version, e.After.Version)) {
					await ScanBufferAsync(e.After);
				}
			}
			catch (OperationCanceledException) {
				// ignores cancellation
			}
		}

		async Task ScanBufferAsync(ITextSnapshot snapshot) {
			//Stop and blow away the old scan (even if it didn't finish, the results are not interesting anymore).
			SyncHelper.CancelAndDispose(ref _Cancellation, true);
			var cancellationToken = _Cancellation.GetToken();

			//The underlying buffer could be very large, meaning that doing the scan for all matches on the UI thread
			//is a bad idea. Do the scan on the background thread and use a callback to raise the changed event when
			//the entire scan has completed.
			_Root = await ParseAsync(snapshot, cancellationToken);

			//This delegate is executed on a background thread.
			await Task.Run(() => TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length))), cancellationToken);
		}
	}
}
