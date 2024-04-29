using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CLR;
using Codist.SyntaxHighlight;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using ThreadHelper = Microsoft.VisualStudio.Shell.ThreadHelper;

namespace Codist.Margins
{
	/// <summary>
	/// Helper class to handle the rendering of the members and symbol references margin.
	/// </summary>
	sealed class CSharpMargin : MarginElementBase, IWpfTextViewMargin
	{
		//todo user customizable opacity of markers
		const double MarkerSize = 3, Padding = 3, LineSize = 2, TypeLineSize = 1, TypeAlpha = 0.5, MemberAlpha = 0.5;

		IWpfTextView _View;
		CancellationTokenSource _Cancellation = new CancellationTokenSource();
		MemberMarker _MemberMarker;
		SymbolReferenceMarker _SymbolReferenceMarker;
		ITextBufferParser _Parser;

		/// <summary>
		/// Constructor for the <see cref="CSharpMargin"/>.
		/// </summary>
		/// <param name="textView">ITextView to which this <see cref="CSharpMargin"/> will be attached.</param>
		/// <param name="verticalScrollbar">Vertical scrollbar of the ITextViewHost that contains <paramref name="textView"/>.</param>
		public CSharpMargin(IWpfTextView textView, IVerticalScrollBar verticalScrollbar)
			: base(textView) {
			_View = textView;
			_MemberMarker = new MemberMarker(verticalScrollbar, this);
			_Parser = CSharpParser.GetOrCreate(textView).GetParser(textView.TextBuffer);
			_SymbolReferenceMarker = new SymbolReferenceMarker(verticalScrollbar, this);

			Config.RegisterUpdateHandler(UpdateCSharpMembersMarginConfig);
			UpdateCSharpMembersMarginConfig(new ConfigUpdatedEventArgs(null, Features.ScrollbarMarkers));
			if (Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.SymbolReference)) {
				_SymbolReferenceMarker.HookEvents();
			}
			Width = MarginSize;
		}

		[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
		async void ParserStateUpdated(object sender, EventArgs<SemanticState> e) {
			try {
				var ct = SyncHelper.CancelAndRetainToken(ref _Cancellation);
				await Task.Delay(400).ConfigureAwait(false);
				// the view could be closed here, thus we need to take caution, otherwise VS will crash
				if (_View?.IsClosed != false) {
					return;
				}
				if (Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.MemberDeclaration)) {
					var m = _MemberMarker;
					if (m != null) {
						await m.UpdateAsync(e.Data, ct).ConfigureAwait(false);
					}
				}
				if (Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.SymbolReference)) {
					var m = _SymbolReferenceMarker;
					if (m != null) {
						await m.UpdateAsync(e.Data, ct).ConfigureAwait(false);
					}
				}
				await SyncHelper.SwitchToMainThreadAsync(ct);
				InvalidateVisual();
			}
			catch (OperationCanceledException) {
				//ignore the exception.
			}
		}

		bool ITextViewMargin.Enabled => IsVisible;
		FrameworkElement IWpfTextViewMargin.VisualElement => this;
		public override string MarginName => nameof(CSharpMargin);
		public override double MarginSize => Padding + MarkerSize;

		/// <summary>
		/// Override for the FrameworkElement's OnRender. When called, redraw all markers.
		/// </summary>
		protected override void OnRender(DrawingContext drawingContext) {
			base.OnRender(drawingContext);
			if (Config.Instance.MarkerOptions.HasAnyFlag(MarkerOptions.MemberDeclaration | MarkerOptions.RegionDirective)) {
				_MemberMarker.Render(drawingContext);
			}
			if (Config.Instance.MarkerOptions.HasAnyFlag(MarkerOptions.SymbolReference)) {
				_SymbolReferenceMarker.Render(drawingContext);
			}
		}

		void UpdateCSharpMembersMarginConfig(ConfigUpdatedEventArgs e) {
			_Parser.StateUpdated -= ParserStateUpdated;
			if (e.UpdatedFeature.HasAnyFlag(Features.ScrollbarMarkers)
				&& Config.Instance.Features.MatchFlags(Features.ScrollbarMarkers)) {
				_Parser.StateUpdated += ParserStateUpdated;
				if (_Parser.TryGetSemanticState(_View.TextSnapshot, out var state)) {
					ParserStateUpdated(_Parser, new EventArgs<SemanticState>(state));
				}
				SymbolReferenceMarker.Refresh();
			}
			else {
				if (Visibility == Visibility.Visible) {
					InvalidateVisual();
				}
				return;
			}
			var setVisible = Config.Instance.MarkerOptions.HasAnyFlag(MarkerOptions.MemberMarginMask);
			var visible = Visibility == Visibility.Visible;
			if (setVisible == false && visible) {
				Visibility = Visibility.Collapsed;
				_SymbolReferenceMarker.UnhookEvents();
			}
			else if (setVisible && visible == false) {
				Visibility = Visibility.Visible;
				if (Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.SymbolReference)) {
					_SymbolReferenceMarker.HookEvents();
				}
			}
			if (Visibility == Visibility.Visible) {
				InvalidateVisual();
			}
		}

		public override void Dispose() {
			if (_View != null) {
				Config.UnregisterUpdateHandler(UpdateCSharpMembersMarginConfig);
				_MemberMarker.Dispose();
				_SymbolReferenceMarker.Dispose();
				SyncHelper.CancelAndDispose(ref _Cancellation, false);
				_Parser.Dispose();
				_Parser = null;
				_MemberMarker = null;
				_SymbolReferenceMarker = null;
				_View = null;
			}
		}

		sealed class MemberMarker
		{
			// a lazy cache for related brushes, which should has its fields initialized or updated only in the Main thread
			static PenStore __PenStore;

			IVerticalScrollBar _ScrollBar;
			CSharpMargin _Margin;
			CodeBlock _CodeBlock;
			List<DirectiveTriviaSyntax> _Regions;

			static MemberMarker() {
				FormatStore.ClassificationFormatMapChanged += FormatStore_ClassificationFormatMapChanged;
			}

			public MemberMarker(IVerticalScrollBar verticalScrollbar, CSharpMargin margin) {
				_ScrollBar = verticalScrollbar;
				_Margin = margin;
				if (__PenStore == null) {
					__PenStore = new PenStore();
				}
			}

			static void FormatStore_ClassificationFormatMapChanged(object sender, EventArgs<IEnumerable<IClassificationType>> e) {
				if (sender is IFormatCache c && c.Category == Constants.CodeText) {
					__PenStore = null;
				}
			}

			public async Task UpdateAsync(SemanticState state, CancellationToken ct) {
				var rootDoc = await state.Model.SyntaxTree.GetRootAsync(ct).ConfigureAwait(false);
				var snapshot = state.Snapshot;
				var root = new CodeBlock(null, CodeMemberType.Root, null, snapshot.ToSnapshotSpan(), 0);
				ParseSyntaxNode(snapshot, rootDoc, root, 0, ct);
				_CodeBlock = root;
				_Regions = Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.RegionDirective)
					? state.Model.SyntaxTree.GetCompilationUnitRoot(ct).GetDirectives(d => d.IsKind(SyntaxKind.RegionDirectiveTrivia))
					: null;
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

			internal void Dispose() {
				if (_Margin != null) {
					_ScrollBar = null;
					_Margin = null;
				}
			}

			internal void Render(DrawingContext drawingContext) {
				const int showMemberDeclarationThreshold = 30, longDeclarationLines = 50, labelSize = 8;
				var snapshot = _Margin._View.TextSnapshot;
				var regions = _Regions;
				var penStore = __PenStore ?? (__PenStore = new PenStore());
				if (regions != null && Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.RegionDirective)) {
					DrawRegions(drawingContext, penStore, labelSize, snapshot, regions);
				}

				var codeBlock = _CodeBlock;
				if (codeBlock != null && Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.MemberDeclaration)) {
					DrawCodeBlockLines(drawingContext, penStore, showMemberDeclarationThreshold, longDeclarationLines, labelSize, snapshot, codeBlock.GetDescendants());
				}
			}

			void DrawCodeBlockLines(DrawingContext drawingContext, PenStore penStore, int showMemberDeclarationThreshold, int longDeclarationLines, int labelSize, ITextSnapshot snapshot, IEnumerable<CodeBlock> codeBlocks) {
				var snapshotLength = snapshot.Length;
				var memberLevel = 0;
				var memberType = CodeMemberType.Root;
				SnapshotPoint rangeFrom = default, rangeTo = default;
				var dt = ImmutableArray.CreateBuilder<DrawText>();
				double y1, y2;
				FormattedText text;

				foreach (var block in codeBlocks) {
					if (_Margin._Cancellation?.IsCancellationRequested != false) {
						break;
					}
					var type = block.Type;
					if (type == CodeMemberType.Root) {
						continue;
					}
					// check line counts of the member and draw marker line if longer than predefined length
					var span = block.Span;
					if (span.End >= snapshotLength) {
						continue;
					}
					var end = new SnapshotPoint(snapshot, span.End);
					var start = new SnapshotPoint(snapshot, span.Start);
					var level = block.Level;
					Pen pen;
					if (Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.LongMemberDeclaration) && span.Length > 150 && IsMember(type)) {
						var lineCount = snapshot.GetLineNumberFromPosition(end) - snapshot.GetLineNumberFromPosition(start);
						y1 = _ScrollBar.GetYCoordinateOfBufferPosition(start);
						y2 = _ScrollBar.GetYCoordinateOfBufferPosition(end);
						pen = null;
						if (lineCount >= longDeclarationLines) {
							pen = penStore.GetPenForCodeMemberType(type);
							drawingContext.DrawLine(pen, new Point(level, y1), new Point(_Margin.ActualWidth, y1));
							drawingContext.DrawLine(pen, new Point(level, y1), new Point(level, y2));
							drawingContext.DrawLine(pen, new Point(level, y2), new Point(_Margin.ActualWidth, y2));
						}
						y2 -= y1;
						if (y2 > showMemberDeclarationThreshold && block.Name != null) {
							if (pen == null) {
								pen = penStore.GetPenForCodeMemberType(type);
							}
							if (pen.Brush != null) {
								text = WpfHelper.ToFormattedText(block.Name, labelSize, pen.Brush.Alpha(y2 / _Margin.ActualHeight * 0.5 + 0.5));
								dt.Add(new DrawText(text, y2, new Point(level + 2, y1 -= text.Height / 2)));
							}
						}
					}

					if (IsType(type)) {
						if (IsMember(memberType)) {
							// draw range for previous grouped members
							y1 = _ScrollBar.GetYCoordinateOfBufferPosition(rangeFrom);
							y2 = _ScrollBar.GetYCoordinateOfBufferPosition(rangeTo);
							drawingContext.DrawLine(penStore.GetPenForCodeMemberType(memberType), new Point(memberLevel, y1), new Point(memberLevel, y2));
						}
						// draw type declaration line
						pen = penStore.GetPenForCodeMemberType(type);
						y1 = _ScrollBar.GetYCoordinateOfBufferPosition(start);
						y2 = _ScrollBar.GetYCoordinateOfBufferPosition(end);
						drawingContext.DrawRectangle(pen.Brush.Alpha(1), pen, new Rect(level - (MarkerSize / 2), y1 - (MarkerSize / 2), MarkerSize, MarkerSize));
						drawingContext.DrawLine(pen, new Point(level, y1), new Point(level, y2));
						if (Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.TypeDeclaration) && block.Name != null) {
							// draw type name
							text = WpfHelper.ToFormattedText(block.Name, labelSize, pen.Brush.Alpha(1))
								.SetBold();
							if (level != 1) {
								text.SetFontStyle(FontStyles.Italic);
							}
							y2 -= y1;
							dt.Add(new DrawText(text, y2, new Point(level + 1, y1 -= text.Height / 2)));
						}
						// mark the beginning of the range
						memberType = type;
						rangeFrom = start;
						continue;
					}
					if (Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.MethodDeclaration)) {
						if (type == CodeMemberType.Method) {
							if (penStore.Method.Brush != null) {
								drawingContext.DrawRectangle(penStore.Method.Brush.Alpha(1), penStore.Method, new Rect(level - (MarkerSize / 2), _ScrollBar.GetYCoordinateOfBufferPosition(start) - (MarkerSize / 2), MarkerSize, MarkerSize));
							}
						}
						else if (type == CodeMemberType.Constructor) {
							if (penStore.Constructor.Brush != null) {
								drawingContext.DrawRectangle(penStore.Constructor.Brush.Alpha(1), penStore.Constructor, new Rect(level - (MarkerSize / 2), _ScrollBar.GetYCoordinateOfBufferPosition(start) - (MarkerSize / 2), MarkerSize, MarkerSize));
							}
						}
					}
					if (type == memberType) {
						// expand the range to the end of the tag
						rangeTo = end;
					}
					else {
						if (IsMember(memberType)) {
							// draw range for previous grouped members
							y1 = _ScrollBar.GetYCoordinateOfBufferPosition(rangeFrom);
							y2 = _ScrollBar.GetYCoordinateOfBufferPosition(rangeTo);
							drawingContext.DrawLine(penStore.GetPenForCodeMemberType(memberType), new Point(level, y1), new Point(level, y2));
						}
						memberType = type;
						rangeFrom = start;
						rangeTo = end;
						memberLevel = level;
					}
				}
				if (IsMember(memberType)) {
					// draw range for previous grouped members
					y1 = _ScrollBar.GetYCoordinateOfBufferPosition(rangeFrom);
					y2 = _ScrollBar.GetYCoordinateOfBufferPosition(rangeTo);
					drawingContext.DrawLine(penStore.GetPenForCodeMemberType(memberType), new Point(memberLevel, y1), new Point(memberLevel, y2));
				}
				// adjust and write text on scrollbar margins
				var tc = dt.Count;
				switch (tc) {
					case 0: return;
					case 1:
						drawingContext.DrawText(dt[0].Text, dt[0].Point);
						return;
				}
				DrawText t, tPrev = null;
				for (int i = tc - 1; i >= 0; i--) {
					t = dt[i];
					// not overlapped, otherwise use the larger one
					if (tPrev == null) {
						tPrev = t;
						t = dt[i - 1];
						if (t.Point.Y + t.Text.Height * 0.7 < tPrev.Point.Y || tPrev.YSpan < t.YSpan) {
							drawingContext.DrawText(tPrev.Text, tPrev.Point);
						}
					}
					else if (t.Point.Y + t.Text.Height * 0.7 < tPrev.Point.Y || tPrev.YSpan < t.YSpan) {
						drawingContext.DrawText(t.Text, t.Point);
						tPrev = t;
					}
					else if (i == 0) {
						drawingContext.DrawText(t.Text, new Point(t.Point.X, t.Point.Y - t.Text.Height * 0.3));
					}
				}
			}

			void DrawRegions(DrawingContext drawingContext, PenStore penStore, int labelSize, ITextSnapshot snapshot, List<DirectiveTriviaSyntax> regions) {
				foreach (var region in regions.OfType<RegionDirectiveTriviaSyntax>()) {
					var s = region.GetDeclarationSignature();
					if (s != null) {
						var text = WpfHelper.ToFormattedText(s, labelSize, penStore.RegionForeground);
						SnapshotPoint rp;
						try {
							rp = new SnapshotPoint(snapshot, region.SpanStart);
						}
						catch (ArgumentOutOfRangeException) {
							break;
						}
						var p = new Point(5, _ScrollBar.GetYCoordinateOfBufferPosition(rp) - text.Height / 2);
						if (penStore.RegionBackground != null) {
							drawingContext.DrawRectangle(penStore.RegionBackground, null, new Rect(p, new Size(text.Width, text.Height)));
						}
						drawingContext.DrawText(text, p);
					}
				}
			}

			static bool IsType(CodeMemberType type) {
				return type > CodeMemberType.Root && type < CodeMemberType.Member;
			}

			static bool IsMember(CodeMemberType type) {
				return type > CodeMemberType.Member && type < CodeMemberType.Other;
			}

			[DebuggerDisplay("{GetDebuggerString()}")]
			sealed class CodeBlock
			{
				public CodeBlock(CodeBlock parent, CodeMemberType type, string name, SnapshotSpan span, int level) {
					Parent = parent;
					parent?.Children.Add(this);
					Type = type;
					Name = name;
					Span = span;
					Level = level;
				}

				public CodeBlock Parent { get; }
				public IList<CodeBlock> Children { get; } = new List<CodeBlock>();
				public SnapshotSpan Span { get; }
				public CodeMemberType Type { get; }
				public int Level { get; }

				public string Name { get; }

				public IEnumerable<CodeBlock> GetDescendants() {
					foreach (var child in Children) {
						yield return child;
						foreach (var grandChild in child.GetDescendants()) {
							yield return grandChild;
						}
					}
				}

				string GetDebuggerString() {
					return $"{new string('.', Level)}{Type}: {Span.GetText()}";
				}
			}

			enum CodeMemberType
			{
				Root, Class, Interface, Struct, Type = Struct, Enum, Delegate, Member, Constructor, Property, Method, Field, Event, Other, Unknown
			}

			sealed class PenStore
			{
				internal readonly Pen Class, Interface, Struct, Enum, Event, Delegate, Constructor, Method, Property, Field, Region;
				internal readonly Brush RegionForeground, RegionBackground;

				public PenStore() {
					var formatMap = FormatStore.DefaultClassificationFormatMap;
					var f = SymbolFormatter.Instance;
					Class = new Pen(f.Class.Alpha(TypeAlpha).MakeFrozen(), TypeLineSize).MakeFrozen();
					Interface = new Pen(f.Interface.Alpha(TypeAlpha).MakeFrozen(), TypeLineSize).MakeFrozen();
					Struct = new Pen(f.Struct.Alpha(TypeAlpha).MakeFrozen(), TypeLineSize).MakeFrozen();
					Constructor = new Pen(formatMap.GetRunProperties(Constants.CSharpConstructorMethodName).ForegroundBrush.Alpha(MemberAlpha).MakeFrozen(), LineSize).MakeFrozen();
					Delegate = new Pen(f.Delegate.Alpha(MemberAlpha).MakeFrozen(), LineSize).MakeFrozen();
					Enum = new Pen(f.Enum.Alpha(TypeAlpha).MakeFrozen(), TypeLineSize).MakeFrozen();
					Event = new Pen(f.Event.Alpha(MemberAlpha).MakeFrozen(), LineSize).MakeFrozen();
					Field = new Pen(f.Field.Alpha(MemberAlpha).MakeFrozen(), LineSize).MakeFrozen();
					Method = new Pen(f.Method.Alpha(MemberAlpha).MakeFrozen(), LineSize).MakeFrozen();
					Property = new Pen(f.Property.Alpha(MemberAlpha).MakeFrozen(), LineSize).MakeFrozen();
					var p = formatMap.GetRunProperties(Constants.CodePreprocessorText);
					RegionForeground = p.ForegroundBrush.Clone().MakeFrozen();
					RegionBackground = p.BackgroundBrush.Alpha(TypeAlpha).MakeFrozen();
					Region = new Pen(RegionBackground ?? RegionForeground, TypeLineSize).MakeFrozen();
				}

				internal Pen GetPenForCodeMemberType(CodeMemberType memberType) {
					switch (memberType) {
						case CodeMemberType.Class: return Class;
						case CodeMemberType.Interface: return Interface;
						case CodeMemberType.Struct: return Struct;
						case CodeMemberType.Enum: return Enum;
						case CodeMemberType.Event: return Event;
						case CodeMemberType.Delegate: return Delegate;
						case CodeMemberType.Constructor: return Constructor;
						case CodeMemberType.Property: return Property;
						case CodeMemberType.Method: return Method;
						case CodeMemberType.Field: return Field;
					}
					return null;
				}
			}

			sealed class DrawText
			{
				public readonly FormattedText Text;
				public readonly Point Point;
				public readonly double YSpan;

				public DrawText(FormattedText text, double ySpan, Point point) {
					Text = text;
					Point = point;
					YSpan = ySpan;
				}

				public override string ToString() {
					return $"{Text.Text}@{Point.X},{Point.Y:0.00} H:{YSpan:0.00}";
				}
			}
		}

		sealed class SymbolReferenceMarker
		{
			const double MarkerMargin = 1;
			static PenStore __PenStore;
			IVerticalScrollBar _ScrollBar;
			CSharpMargin _Margin;
			List<ReferenceItem> _References;
			ISymbol _ActiveSymbol;
			CancellationTokenSource _Cancellation;

			public SymbolReferenceMarker(IVerticalScrollBar verticalScrollbar, CSharpMargin margin) {
				_ScrollBar = verticalScrollbar;
				_Margin = margin;
			}

			internal static void Refresh() {
				__PenStore = null;
			}

			internal void HookEvents() {
				var view = _Margin._View;
				view.Selection.SelectionChanged -= UpdateReferences;
				view.Selection.SelectionChanged += UpdateReferences;
			}
			internal void UnhookEvents() {
				_Margin._View.Selection.SelectionChanged -= UpdateReferences;
			}
			internal void Dispose() {
				if (_Margin != null) {
					UnhookEvents();
					SyncHelper.CancelAndDispose(ref _Cancellation, false);
					_ScrollBar = null;
					_Margin = null;
					_References = null;
					_ActiveSymbol = null;
				}
			}

			internal void Render(DrawingContext drawingContext) {
				var refs = _References;
				if (refs == null) {
					return;
				}
				var snapshot = _Margin._View.TextSnapshot;
				var snapshotLength = snapshot.Length;
				var penStore = __PenStore ?? (__PenStore = new PenStore());
				foreach (var item in refs) {
					if (_Margin._Cancellation?.IsCancellationRequested != false) {
						break;
					}
					if (item.Position >= snapshotLength) {
						continue;
					}
					SolidColorBrush b;
					Pen p = null;
					switch (item.Usage) {
						case SymbolUsageKind.Write:
							b = penStore.WriteMarker;
							break;
						case SymbolUsageKind.Write | SymbolUsageKind.SetNull:
							b = null;
							p = penStore.SetNullPen;
							break;
						case SymbolUsageKind.Usage:
							b = penStore.ReferenceMarker;
							p = penStore.DefinitionPen;
							break;
						default:
							b = penStore.ReferenceMarker;
							break;
					}
					var y = _ScrollBar.GetYCoordinateOfBufferPosition(new SnapshotPoint(snapshot, item.Position));
					drawingContext.DrawRectangle(b,
						p,
						item.Usage == SymbolUsageKind.Usage
							? new Rect(0, y - (MarkerSize / 2), MarkerSize + MarkerMargin, MarkerSize + MarkerMargin)
							: new Rect(MarkerMargin, y - (MarkerSize / 2), MarkerSize, MarkerSize));
				}
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void UpdateReferences(object sender, EventArgs e) {
				var ct = SyncHelper.CancelAndRetainToken(ref _Cancellation);
				try {
					if (await Task.Run(() => UpdateAsync(null, ct))) {
						await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(default);
						_Margin?.InvalidateVisual();
					}
				}
				catch (ObjectDisposedException) {
					// ignore exception
				}
				catch (OperationCanceledException) {
					// ignore canceled
				}
			}

			public async Task<bool> UpdateAsync(SemanticState state, CancellationToken cancellationToken) {
				var margin = _Margin;
				ISymbol symbol = null;
				if (state != null || margin._Parser.TryGetSemanticState(margin._View.TextSnapshot, out state)) {
					symbol = await state.GetSymbolAsync(margin._View.GetCaretPosition(), cancellationToken).ConfigureAwait(false);
				}
				if (ReferenceEquals(Interlocked.Exchange(ref _ActiveSymbol, symbol), symbol)) {
					return false;
				}
				if (symbol == null) {
					if (_References != null) {
						_References = null;
						return true;
					}
					else {
						return false;
					}
				}
				_References = GetReferenceItems(
					await SymbolFinder.FindReferencesAsync(symbol.GetAliasTarget(), state.Document.Project.Solution, ImmutableSortedSet.Create(state.Document), cancellationToken).ConfigureAwait(false),
					state.Model.SyntaxTree,
					state.GetCompilationUnit(cancellationToken),
					cancellationToken
					);
				return true;
			}

			static List<ReferenceItem> GetReferenceItems(IEnumerable<ReferencedSymbol> referencePoints, SyntaxTree syntaxTree, CompilationUnitSyntax compilationUnit, CancellationToken cancellationToken) {
				var r = new List<ReferenceItem>();
				foreach (var item in referencePoints) {
					if (cancellationToken.IsCancellationRequested) {
						break;
					}

					#region Add reference points
					var pu = CodeAnalysisHelper.GetPotentialUsageKinds(item.Definition);
					foreach (var loc in item.Locations) {
						var span = loc.Location.SourceSpan;
						r.Add(new ReferenceItem(span.Start, CodeAnalysisHelper.GetUsageKind(pu, compilationUnit.FindNode(span))));
					}
					#endregion

					if (item.Definition.ContainingAssembly.GetSourceType() == AssemblySource.Metadata) {
						continue;
					}

					#region Add declaration marker
					foreach (var loc in item.Definition.DeclaringSyntaxReferences) {
						if (loc.SyntaxTree == syntaxTree) {
							r.Add(new ReferenceItem(loc.Span.Start, SymbolUsageKind.Usage));
						}
					}
					#endregion
				}
				return r;
			}

			sealed class PenStore
			{
				public readonly SolidColorBrush WriteMarker, ReferenceMarker;
				public readonly Pen SetNullPen, DefinitionPen;

				public PenStore() {
					var config = Config.Instance.SymbolReferenceMarkerSettings;
					WriteMarker = new SolidColorBrush(config.WriteMarker).MakeFrozen();
					ReferenceMarker = new SolidColorBrush(config.ReferenceMarker).MakeFrozen();
					SetNullPen = new Pen(WriteMarker, 1).MakeFrozen();
					DefinitionPen = new Pen(new SolidColorBrush(config.SymbolDefinition).MakeFrozen(), 1).MakeFrozen();
				}
			}

			readonly struct ReferenceItem
			{
				public readonly int Position;
				public readonly SymbolUsageKind Usage;

				public ReferenceItem(int position, SymbolUsageKind usage) {
					Position = position;
					Usage = usage;
				}
			}
		}
	}
}
