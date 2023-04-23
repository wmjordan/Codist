using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using AppHelpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Task = System.Threading.Tasks.Task;

namespace Codist.Margins
{
	/// <summary>
	/// Helper class to handle the rendering of the members margin.
	/// </summary>
	sealed class CSharpMembersMargin : MarginElementBase, IWpfTextViewMargin
	{
		//todo user customizable opacity of markers
		const double MarkerSize = 3, Padding = 3, LineSize = 2, TypeLineSize = 1, TypeAlpha = 0.5, MemberAlpha = 0.5;

		CancellationTokenSource _Cancellation = new CancellationTokenSource();
		MemberMarker _MemberMarker;
		SymbolReferenceMarker _SymbolReferenceMarker;
		IEditorFormatMap _FormatMap;
		SemanticContext _SemanticContext;
		Pen _ClassPen, _InterfacePen, _StructPen, _EnumPen, _EventPen, _DelegatePen, _ConstructorPen, _MethodPen, _PropertyPen, _FieldPen, _RegionPen;
		Brush _RegionForeground, _RegionBackground;

		/// <summary>
		/// Constructor for the <see cref="CSharpMembersMargin"/>.
		/// </summary>
		/// <param name="textView">ITextView to which this <see cref="CSharpMembersMargin"/> will be attached.</param>
		/// <param name="verticalScrollbar">Vertical scrollbar of the ITextViewHost that contains <paramref name="textView"/>.</param>
		public CSharpMembersMargin(IWpfTextView textView, IVerticalScrollBar verticalScrollbar) {
			_MemberMarker = new MemberMarker(textView, verticalScrollbar, this);
			_SymbolReferenceMarker = new SymbolReferenceMarker(textView, verticalScrollbar, this);
			_FormatMap = ServicesHelper.Instance.EditorFormatMap.GetEditorFormatMap(textView);
			_SemanticContext = SemanticContext.GetOrCreateSingletonInstance(textView);
			IsVisibleChanged += IsVisibleChangedHandler;

			Config.RegisterUpdateHandler(UpdateCSharpMembersMarginConfig);
			UpdateCSharpMembersMarginConfig(new ConfigUpdatedEventArgs(null, Features.ScrollbarMarkers));
			if (Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.SymbolReference)) {
				_SymbolReferenceMarker.HookEvents();
			}

			Width = MarginSize;
		}

		bool ITextViewMargin.Enabled => IsVisible;
		FrameworkElement IWpfTextViewMargin.VisualElement => this;
		public override string MarginName => nameof(CSharpMembersMargin);
		public override double MarginSize => Padding + MarkerSize;

		/// <summary>
		/// Override for the FrameworkElement's OnRender. When called, redraw all markers 
		/// </summary>
		protected override void OnRender(DrawingContext drawingContext) {
			base.OnRender(drawingContext);
			_MemberMarker.Render(drawingContext);
			if (Config.Instance.MarkerOptions.HasAnyFlag(MarkerOptions.SymbolReference)) {
				_SymbolReferenceMarker.Render(drawingContext);
			}
		}

		void UpdateCSharpMembersMarginConfig(ConfigUpdatedEventArgs e) {
			if (e.UpdatedFeature.HasAnyFlag(Features.SyntaxHighlight | Features.ScrollbarMarkers) == false) {
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
				if (e.UpdatedFeature.MatchFlags(Features.SyntaxHighlight)) {
					UpdateSyntaxColors();
				}
				InvalidateVisual();
			}
		}

		Pen GetPenForCodeMemberType(CodeMemberType memberType) {
			switch (memberType) {
				case CodeMemberType.Class: return _ClassPen;
				case CodeMemberType.Interface: return _InterfacePen;
				case CodeMemberType.Struct: return _StructPen;
				case CodeMemberType.Enum: return _EnumPen;
				case CodeMemberType.Event: return _EventPen;
				case CodeMemberType.Delegate: return _DelegatePen;
				case CodeMemberType.Constructor: return _ConstructorPen;
				case CodeMemberType.Property: return _PropertyPen;
				case CodeMemberType.Method: return _MethodPen;
				case CodeMemberType.Field: return _FieldPen;
			}

			return null;
		}

		void UpdateSyntaxColors() {
			_ClassPen = new Pen(_FormatMap.GetAnyBrush(Constants.CodeClassName, Constants.CodePlainText).Alpha(TypeAlpha), TypeLineSize);
			_InterfacePen = new Pen(_FormatMap.GetAnyBrush(Constants.CodeInterfaceName, Constants.CodePlainText).Alpha(TypeAlpha), TypeLineSize);
			_StructPen = new Pen(_FormatMap.GetAnyBrush(Constants.CodeStructName, Constants.CodePlainText).Alpha(TypeAlpha), TypeLineSize);
			_ConstructorPen = new Pen(_FormatMap.GetAnyBrush(Constants.CSharpConstructorMethodName, Constants.CSharpMethodName, Constants.CodeMethodName).Alpha(MemberAlpha), LineSize);
			_DelegatePen = new Pen(_FormatMap.GetAnyBrush(Constants.CodeDelegateName).Alpha(MemberAlpha), LineSize);
			_EnumPen = new Pen(_FormatMap.GetAnyBrush(Constants.CodeEnumName).Alpha(TypeAlpha), TypeLineSize);
			_EventPen = new Pen(_FormatMap.GetAnyBrush(Constants.CSharpEventName, Constants.CodeEventName).Alpha(MemberAlpha), LineSize);
			_FieldPen = new Pen(_FormatMap.GetAnyBrush(Constants.CSharpFieldName, Constants.CodeFieldName).Alpha(MemberAlpha), LineSize);
			_MethodPen = new Pen(_FormatMap.GetAnyBrush(Constants.CSharpMethodName, Constants.CodeMethodName).Alpha(MemberAlpha), LineSize);
			_PropertyPen = new Pen(_FormatMap.GetAnyBrush(Constants.CSharpPropertyName, Constants.CodePropertyName).Alpha(MemberAlpha), LineSize);
			_RegionForeground = _FormatMap.GetAnyBrush(Constants.CodePreprocessorText);
			_RegionBackground = _FormatMap.GetProperties(Constants.CodePreprocessorText).GetBackgroundBrush().Alpha(TypeAlpha);
			_RegionPen = new Pen(_RegionBackground ?? _RegionForeground, TypeLineSize);
		}

		void IsVisibleChangedHandler(object sender, DependencyPropertyChangedEventArgs e) {
			if ((bool)e.NewValue) {
				_MemberMarker.Activate();
			}
			else {
				_MemberMarker.Deactivate();
			}
		}

		public override void Dispose() {
			if (Interlocked.Exchange(ref _SemanticContext, null) != null) {
				IsVisibleChanged -= IsVisibleChangedHandler;
				Config.UnregisterUpdateHandler(UpdateCSharpMembersMarginConfig);
				_MemberMarker.Dispose();
				_SymbolReferenceMarker.Dispose();
				SyncHelper.CancelAndDispose(ref _Cancellation, false);
				_MemberMarker = null;
				_SymbolReferenceMarker = null;
				_FormatMap = null;
			}
		}

		sealed class MemberMarker
		{
			IWpfTextView _TextView;
			IVerticalScrollBar _ScrollBar;
			CSharpMembersMargin _Element;

			List<IMappingTagSpan<ICodeMemberTag>> _Tags;
			List<DirectiveTriviaSyntax> _Regions;
			ITagAggregator<ICodeMemberTag> _CodeMemberTagger;

			public MemberMarker(IWpfTextView textView, IVerticalScrollBar verticalScrollbar, CSharpMembersMargin element) {
				_TextView = textView;
				_ScrollBar = verticalScrollbar;
				_Element = element;
			}

			internal void Dispose() {
				if (_TextView != null) {
					if (_CodeMemberTagger != null) {
						_CodeMemberTagger.BatchedTagsChanged -= OnTagsChanged;
						_CodeMemberTagger = null;
					}
					_TextView = null;
					_ScrollBar.TrackSpanChanged -= OnTagsChanged;
					_ScrollBar = null;
					_Element = null;
				}
			}

			internal void Activate() {
				//todo refresh the margin when format mapping or syntax highlight style is changed
				//Hook up to the various events we need to keep the margin current.
				_ScrollBar.TrackSpanChanged += OnTagsChanged;

				_Element.UpdateSyntaxColors();

				_CodeMemberTagger = ServicesHelper.Instance.ViewTagAggregatorFactory.CreateTagAggregator<ICodeMemberTag>(_TextView);
				_CodeMemberTagger.BatchedTagsChanged += OnTagsChanged;

				//Force the margin to be re-rendered since things might have changed while the margin was hidden.
				_Element.InvalidateVisual();
			}

			internal void Deactivate() {
				_ScrollBar.TrackSpanChanged -= OnTagsChanged;
				_CodeMemberTagger.BatchedTagsChanged -= OnTagsChanged;
				_CodeMemberTagger.Dispose();
				_CodeMemberTagger = null;
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void OnTagsChanged(object sender, EventArgs e) {
				try {
					var ct = SyncHelper.CancelAndRetainToken(ref _Element._Cancellation);
					await Task.Run(TagDocument, ct).ConfigureAwait(false);
					_Regions = TagRegions();
					if (ct.IsCancellationRequested) {
						return;
					}
					await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(ct);
					_Element.InvalidateVisual();
				}
				catch (ObjectDisposedException) {
					return;
				}
				catch (OperationCanceledException) {
					return;
				}
			}

			void TagDocument() {
				var tagger = _CodeMemberTagger;
				if (_TextView.IsClosed || tagger == null || Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.MemberDeclaration) == false) {
					return;
				}
				var snapshot = _TextView.TextSnapshot;
				_Tags = new List<IMappingTagSpan<ICodeMemberTag>>(tagger.GetTags(new SnapshotSpan(snapshot, 0, snapshot.Length)));
			}

			List<DirectiveTriviaSyntax> TagRegions() {
				return Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.RegionDirective)
					? _Element._SemanticContext.Compilation?.GetDirectives(d => d.IsKind(SyntaxKind.RegionDirectiveTrivia))
					: null;
			}

			internal void Render(DrawingContext drawingContext) {
				const int showMemberDeclarationThreshold = 30, longDeclarationLines = 50, labelSize = 8;
				if (Config.Instance.MarkerOptions.HasAnyFlag(MarkerOptions.MemberDeclaration | MarkerOptions.RegionDirective) == false
					|| _TextView.IsClosed) {
					return;
				}
				var snapshot = _TextView.TextSnapshot;
				var regions = _Regions;
				if (regions != null && Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.RegionDirective)) {
					DrawRegions(drawingContext, labelSize, snapshot, regions);
				}
				var tags = _Tags;
				if (tags == null || _CodeMemberTagger == null || Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.MemberDeclaration) == false) {
					return;
				}
				var snapshotLength = snapshot.Length;
				var memberLevel = 0;
				var memberType = CodeMemberType.Root;
				SnapshotPoint rangeFrom = default, rangeTo = default;
				var dt = ImmutableArray.CreateBuilder<DrawText>();
				double y1, y2;
				FormattedText text;

				foreach (var tag in tags) {
					if (_Element._Cancellation?.IsCancellationRequested != false) {
						break;
					}
					var tagType = tag.Tag.Type;
					if (tagType == CodeMemberType.Root) {
						continue;
					}
					// check line counts of the member and draw marker line if longer than predefined length
					var span = tag.Tag.Span;
					if (span.End >= snapshotLength) {
						continue;
					}
					var end = new SnapshotPoint(snapshot, span.End);
					var start = new SnapshotPoint(snapshot, span.Start);
					var level = tag.Tag.Level;
					Pen pen;
					if (Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.LongMemberDeclaration) && span.Length > 150 && tagType.IsMember()) {
						var lineCount = snapshot.GetLineNumberFromPosition(end) - snapshot.GetLineNumberFromPosition(start);
						y1 = _ScrollBar.GetYCoordinateOfBufferPosition(start);
						y2 = _ScrollBar.GetYCoordinateOfBufferPosition(end);
						pen = null;
						if (lineCount >= longDeclarationLines) {
							pen = _Element.GetPenForCodeMemberType(tagType);
							drawingContext.DrawLine(pen, new Point(level, y1), new Point(_Element.ActualWidth, y1));
							drawingContext.DrawLine(pen, new Point(level, y1), new Point(level, y2));
							drawingContext.DrawLine(pen, new Point(level, y2), new Point(_Element.ActualWidth, y2));
						}
						y2 -= y1;
						if (y2 > showMemberDeclarationThreshold && tag.Tag.Name != null) {
							if (pen == null) {
								pen = _Element.GetPenForCodeMemberType(tagType);
							}
							if (pen.Brush != null) {
								text = WpfHelper.ToFormattedText(tag.Tag.Name, labelSize, pen.Brush.Alpha(y2 / _Element.ActualHeight * 0.5 + 0.5));
								dt.Add(new DrawText(text, y2, new Point(level + 2, y1 -= text.Height / 2)));
							}
						}
					}

					if (tagType.IsType()) {
						if (memberType.IsMember()) {
							// draw range for previous grouped members
							y1 = _ScrollBar.GetYCoordinateOfBufferPosition(rangeFrom);
							y2 = _ScrollBar.GetYCoordinateOfBufferPosition(rangeTo);
							drawingContext.DrawLine(_Element.GetPenForCodeMemberType(memberType), new Point(memberLevel, y1), new Point(memberLevel, y2));
						}
						// draw type declaration line
						pen = _Element.GetPenForCodeMemberType(tagType);
						y1 = _ScrollBar.GetYCoordinateOfBufferPosition(start);
						y2 = _ScrollBar.GetYCoordinateOfBufferPosition(end);
						drawingContext.DrawRectangle(pen.Brush.Alpha(1), pen, new Rect(level - (MarkerSize / 2), y1 - (MarkerSize / 2), MarkerSize, MarkerSize));
						drawingContext.DrawLine(pen, new Point(level, y1), new Point(level, y2));
						if (Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.TypeDeclaration) && tag.Tag.Name != null) {
							// draw type name
							text = WpfHelper.ToFormattedText(tag.Tag.Name, labelSize, pen.Brush.Alpha(1))
								.SetBold();
							if (level != 1) {
								text.SetFontStyle(FontStyles.Italic);
							}
							y2 -= y1;
							dt.Add(new DrawText(text, y2, new Point(level + 1, y1 -= text.Height / 2)));
						}
						// mark the beginning of the range
						memberType = tagType;
						rangeFrom = start;
						continue;
					}
					if (Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.MethodDeclaration)) {
						if (tagType == CodeMemberType.Method) {
							if (_Element._MethodPen.Brush != null) {
								drawingContext.DrawRectangle(_Element._MethodPen.Brush.Alpha(1), _Element._MethodPen, new Rect(level - (MarkerSize / 2), _ScrollBar.GetYCoordinateOfBufferPosition(start) - (MarkerSize / 2), MarkerSize, MarkerSize));
							}
						}
						else if (tagType == CodeMemberType.Constructor) {
							if (_Element._ConstructorPen.Brush != null) {
								drawingContext.DrawRectangle(_Element._ConstructorPen.Brush.Alpha(1), _Element._ConstructorPen, new Rect(level - (MarkerSize / 2), _ScrollBar.GetYCoordinateOfBufferPosition(start) - (MarkerSize / 2), MarkerSize, MarkerSize));
							}
						}
					}
					if (tagType == memberType) {
						// expand the range to the end of the tag
						rangeTo = end;
					}
					else {
						if (memberType.IsMember()) {
							// draw range for previous grouped members
							y1 = _ScrollBar.GetYCoordinateOfBufferPosition(rangeFrom);
							y2 = _ScrollBar.GetYCoordinateOfBufferPosition(rangeTo);
							drawingContext.DrawLine(_Element.GetPenForCodeMemberType(memberType), new Point(level, y1), new Point(level, y2));
						}
						memberType = tagType;
						rangeFrom = start;
						rangeTo = end;
						memberLevel = level;
					}
				}
				if (memberType.IsMember()) {
					// draw range for previous grouped members
					y1 = _ScrollBar.GetYCoordinateOfBufferPosition(rangeFrom);
					y2 = _ScrollBar.GetYCoordinateOfBufferPosition(rangeTo);
					drawingContext.DrawLine(_Element.GetPenForCodeMemberType(memberType), new Point(memberLevel, y1), new Point(memberLevel, y2));
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

			void DrawRegions(DrawingContext drawingContext, int labelSize, ITextSnapshot snapshot, List<DirectiveTriviaSyntax> regions) {
				foreach (var region in regions.OfType<RegionDirectiveTriviaSyntax>()) {
					var s = region.GetDeclarationSignature();
					if (s != null) {
						var text = WpfHelper.ToFormattedText(s, labelSize, _Element._RegionForeground);
						SnapshotPoint rp;
						try {
							rp = new SnapshotPoint(snapshot, region.SpanStart);
						}
						catch (ArgumentOutOfRangeException) {
							break;
						}
						var p = new Point(5, _ScrollBar.GetYCoordinateOfBufferPosition(rp) - text.Height / 2);
						if (_Element._RegionBackground != null) {
							drawingContext.DrawRectangle(_Element._RegionBackground, null, new Rect(p, new Size(text.Width, text.Height)));
						}
						drawingContext.DrawText(text, p);
					}
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
			readonly Pen _DefinitionMarkerPen = new Pen(ThemeHelper.ToolWindowTextBrush, MarkerMargin);
			IWpfTextView _View;
			IVerticalScrollBar _ScrollBar;
			CSharpMembersMargin _Margin;
			ReferenceContext _Context;
			ISymbol _Symbol;

			public SymbolReferenceMarker(IWpfTextView textView, IVerticalScrollBar verticalScrollbar, CSharpMembersMargin margin) {
				_View = textView;
				_ScrollBar = verticalScrollbar;
				_Margin = margin;
			}

			internal void HookEvents() {
				_View.Selection.SelectionChanged -= UpdateReferences;
				_View.Selection.SelectionChanged += UpdateReferences;
			}
			internal void UnhookEvents() {
				_View.Selection.SelectionChanged -= UpdateReferences;
			}
			internal void Dispose() {
				if (_View != null) {
					UnhookEvents();
					_ScrollBar = null;
					_Margin = null;
					_View = null;
					_Context = null;
					_Symbol = null;
				}
			}

			internal void Render(DrawingContext drawingContext) {
				var ctx = _Context;
				if (ctx == null) {
					return;
				}
				var snapshot = _View.TextSnapshot;
				var snapshotLength = snapshot.Length;
				var cur = ctx.SyntaxTree.GetCompilationUnitRoot(_Margin._Cancellation.GetToken());
				var config = Config.Instance.SymbolReferenceMarkerSettings;
				foreach (var item in ctx.ReferencePoints) {
					if (_Margin._Cancellation?.IsCancellationRequested != false) {
						break;
					}
					var pu = CodeAnalysisHelper.GetPotentialUsageKinds(item.Definition);
					foreach (var loc in item.Locations) {
						var start = loc.Location.SourceSpan.Start;
						if (start >= snapshotLength) {
							continue;
						}
						var n = cur.FindNode(loc.Location.SourceSpan);
						SolidColorBrush b;
						Pen p = null;
						switch (CodeAnalysisHelper.GetUsageKind(pu, n)) {
							case SymbolUsageKind.Write:
								b = config.WriteMarkerBrush;
								break;
							case SymbolUsageKind.Write | SymbolUsageKind.SetNull:
								b = null;
								p = config.SetNullPen;
								break;
							default:
								b = config.ReferenceMarkerBrush;
								break;
						}
						var y = _ScrollBar.GetYCoordinateOfBufferPosition(new SnapshotPoint(snapshot, start));
						drawingContext.DrawRectangle(b, p, new Rect(MarkerMargin, y - (MarkerSize / 2), MarkerSize, MarkerSize));
					}
					if (item.Definition.ContainingAssembly.GetSourceType() == AssemblySource.Metadata) {
						continue;
					}
					// draws the definition marker
					foreach (var loc in item.Definition.DeclaringSyntaxReferences) {
						if (loc.SyntaxTree == ctx.SyntaxTree) {
							var start = loc.Span.Start;
							if (start >= snapshotLength) {
								continue;
							}
							var y = _ScrollBar.GetYCoordinateOfBufferPosition(new SnapshotPoint(snapshot, start));
							drawingContext.DrawRectangle(config.ReferenceMarkerBrush, _DefinitionMarkerPen, new Rect(0, y - (MarkerSize / 2), MarkerSize + MarkerMargin, MarkerSize + MarkerMargin));
						}
					}
				}
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void UpdateReferences(object sender, EventArgs e) {
				try {
					SyncHelper.CancelAndDispose(ref _Margin._Cancellation, true);
					await UpdateReferencesAsync().ConfigureAwait(false);
				}
				catch (ObjectDisposedException) {
					// ignore exception
				}
				catch (OperationCanceledException) {
					// ignore canceled
				}
			}

			async Task UpdateReferencesAsync() {
				var cancellation = _Margin._Cancellation.GetToken();
				var ctx = _Margin._SemanticContext;
				if (await ctx.UpdateAsync(_View.Selection.Start.Position, cancellation).ConfigureAwait(false) == false) {
					_Symbol = null;
					goto REFRESH;
				}
				var symbol = await ctx.GetSymbolAsync(_View.Selection.Start.Position, cancellation).ConfigureAwait(false);
				if (symbol == null) {
					if (Interlocked.Exchange(ref _Context, null) != null) {
						_Symbol = null;
						goto REFRESH;
					}
					return;
				}

				if (ReferenceEquals(Interlocked.Exchange(ref _Symbol, symbol), symbol)) {
					return;
				}
				var doc = ctx.Document;
				// todo show marked symbols on scrollbar margin
				try {
					_Context = new ReferenceContext(
						await SymbolFinder.FindReferencesAsync(symbol.GetAliasTarget(), doc.Project.Solution, ImmutableSortedSet.Create(doc), cancellation).ConfigureAwait(false),
						ctx.Compilation.SyntaxTree
						);
				}
				catch (ArgumentException) {
					// hack: multiple async updates could occur and invalidated ctx.Document, which might cause ArgumentException
					// ignore this at this moment
					return;
				}
				REFRESH:
				await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellation);
				_Margin.InvalidateVisual();
			}

			sealed class ReferenceContext
			{
				public readonly IEnumerable<ReferencedSymbol> ReferencePoints;
				public readonly SyntaxTree SyntaxTree;

				public ReferenceContext(IEnumerable<ReferencedSymbol> referencePoints, SyntaxTree syntaxTree) {
					ReferencePoints = referencePoints;
					SyntaxTree = syntaxTree;
				}
			}
		}
	}
}
