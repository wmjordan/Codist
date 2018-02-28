using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;

namespace Codist.Classifiers
{
	public sealed class CSharpBlockTagger : ITagger<ICodeMemberTag>
	{
		ITextBuffer _buffer;
		int _refCount;
		CodeBlock _root;
		BackgroundScan _scan;

		internal CSharpBlockTagger(ITextBuffer buffer) {
			_buffer = buffer;
		}

		public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

		public void AddRef() {
			if (++_refCount == 1) {
				_buffer.Changed += OnChanged;
				ScanBuffer(_buffer.CurrentSnapshot);
			}
		}

		public IEnumerable<ITagSpan<ICodeMemberTag>> GetTags(NormalizedSnapshotSpanCollection spans) {
			CodeBlock root = _root;  //this.root could be set on a background thread, so get a snapshot.
			if (root != null) {
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
		}

		public void Release() {
			if (--_refCount == 0) {
				_buffer.Changed -= OnChanged;

				if (_scan != null) {
					//Stop and blow away the old scan (even if it didn't finish, the results are not interesting anymore).
					_scan.Cancel();
					_scan = null;
				}
				_root = null; //Allow the old root to be GC'd
			}
		}

		private static bool AnyTextChanges(ITextVersion oldVersion, ITextVersion currentVersion) {
			while (oldVersion != currentVersion) {
				if (oldVersion.Changes.Count > 0) {
					return true;
				}

				oldVersion = oldVersion.Next;
			}

			return false;
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

		private void OnChanged(object sender, TextContentChangedEventArgs e) {
			if (AnyTextChanges(e.Before.Version, e.After.Version)) {
				ScanBuffer(e.After);
			}
		}

		private void ScanBuffer(ITextSnapshot snapshot) {
			if (_scan != null) {
				//Stop and blow away the old scan (even if it didn't finish, the results are not interesting anymore).
				_scan.Cancel();
				_scan = null;
			}

			//The underlying buffer could be very large, meaning that doing the scan for all matches on the UI thread
			//is a bad idea. Do the scan on the background thread and use a callback to raise the changed event when
			//the entire scan has completed.
			_scan = new BackgroundScan(snapshot, (CodeBlock newRoot) => {
				//This delegate is executed on a background thread.
				_root = newRoot;
				TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(new SnapshotSpan(snapshot, 0, snapshot.Length)));
			});
		}
		public static async Task<CodeBlock> ParseAsync(ITextSnapshot snapshot, CancellationToken token) {
			CodeBlock parentCodeBlockNode = null;
			try {
				parentCodeBlockNode = await GetAndParseSyntaxNodeAsync(snapshot, token);
			}
			catch (TaskCanceledException) {
				//ignore the exception.
			}

			return parentCodeBlockNode;
		}

		static async Task<CodeBlock> GetAndParseSyntaxNodeAsync(ITextSnapshot snapshot, CancellationToken token) {
			var document = snapshot.GetOpenDocumentInCurrentContextWithChanges();
			var parentSyntaxNode = await document.GetSyntaxRootAsync(token).ConfigureAwait(false);
			var root = new CodeBlock(null, CodeMemberType.Root, null, new SnapshotSpan(snapshot, 0, snapshot.Length), 0);

			ParseSyntaxNode(snapshot, parentSyntaxNode, root, 0, token);

			return root;
		}

		static void ParseSyntaxNode(ITextSnapshot snapshot, SyntaxNode parentSyntaxNode, CodeBlock parentCodeBlockNode, int level, CancellationToken token) {
			if (token.IsCancellationRequested) {
				throw new TaskCanceledException();
			}

			foreach (var node in parentSyntaxNode.ChildNodes()) {
				CodeMemberType type = MatchDeclaration(node);
				if (type != CodeMemberType.Unknown) {
					var name = ((node as Microsoft.CodeAnalysis.CSharp.Syntax.BaseTypeDeclarationSyntax)?.Identifier ?? (node as Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax)?.Identifier);
					var child = new CodeBlock(parentCodeBlockNode, type, name?.Text, new SnapshotSpan(snapshot, node.SpanStart, node.Span.Length), level + 1);
					if (type > CodeMemberType.Type) {
						continue;
					}
					ParseSyntaxNode(snapshot, node, child, level + 1, token);
				}
				else {
					ParseSyntaxNode(snapshot, node, parentCodeBlockNode, level, token);
				}
			}
		}

		static CodeMemberType MatchDeclaration(SyntaxNode node) {
			switch (node.Kind()) {
				case SyntaxKind.ClassDeclaration:
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

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable")]
		sealed class BackgroundScan
		{
			public CancellationTokenSource CancellationSource = new CancellationTokenSource();

			/// <summary>
			/// Does a background scan in <paramref name="snapshot"/>. Call
			/// <paramref name="completionCallback"/> once the scan has completed.
			/// </summary>
			/// <param name="snapshot">Text snapshot in which to scan.</param>
			/// <param name="completionCallback">Delegate to call if the scan is completed (will be called on the UI thread).</param>
			/// <remarks>The constructor must be called from the UI thread.</remarks>
			public BackgroundScan(ITextSnapshot snapshot, CompletionCallback completionCallback) {
				Task.Run(async delegate {
					CodeBlock newRoot = await ParseAsync(snapshot, CancellationSource.Token);

					if ((newRoot != null) && !CancellationSource.Token.IsCancellationRequested) {
						completionCallback(newRoot);
					}
				});
			}

			public delegate void CompletionCallback(CodeBlock root);

			public void Cancel() {
				if (CancellationSource != null) {
					CancellationSource.Cancel();
					CancellationSource.Dispose();
				}
			}
		}
	}
}
