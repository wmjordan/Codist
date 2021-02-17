using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable")]
	sealed class CSharpMembersMargin : FrameworkElement, IWpfTextViewMargin
	{
		public const string MarginName = nameof(CSharpMembersMargin);

		//todo user customizable opacity of markers
		const double MarkerSize = 3, Padding = 3, LineSize = 2, TypeLineSize = 1, TypeAlpha = 0.5, MemberAlpha = 0.5;

		CancellationTokenSource _Cancellation = new CancellationTokenSource();
		readonly MemberMarker _MemberMarker;
		readonly SymbolReferenceMarker _SymbolReferenceMarker;
		readonly IEditorFormatMap _FormatMap;
		readonly SemanticContext _SemanticContext;
		Pen _ClassPen, _InterfacePen, _StructPen, _EnumPen, _EventPen, _DelegatePen, _ConstructorPen, _MethodPen, _PropertyPen, _FieldPen, _RegionPen;
		Brush _RegionForeground, _RegionBackground;

		/// <summary>
		/// Constructor for the <see cref="CSharpMembersMargin"/>.
		/// </summary>
		/// <param name="textView">ITextView to which this <see cref="CSharpMembersMargin"/> will be attacheded.</param>
		/// <param name="verticalScrollbar">Vertical scrollbar of the ITextViewHost that contains <paramref name="textView"/>.</param>
		public CSharpMembersMargin(IWpfTextView textView, IVerticalScrollBar verticalScrollbar) {
			IsHitTestVisible = false;
			SnapsToDevicePixels = true;
			Width = Padding + MarkerSize;
			_MemberMarker = new MemberMarker(textView, verticalScrollbar, this);
			_SymbolReferenceMarker = new SymbolReferenceMarker(textView, verticalScrollbar, this);
			_FormatMap = ServicesHelper.Instance.EditorFormatMap.GetEditorFormatMap(textView);
			_SemanticContext = SemanticContext.GetOrCreateSingetonInstance(textView);
			IsVisibleChanged += _MemberMarker.OnIsVisibleChanged;
			textView.Closed += TextView_Closed;

			Config.Updated += Config_Updated;
			Config_Updated(null, new ConfigUpdatedEventArgs(Features.ScrollbarMarkers));
			if (Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.SymbolReference)) {
				_SymbolReferenceMarker.HookEvents();
			}
		}

		void TextView_Closed(object sender, EventArgs e) {
			Dispose();
		}

		bool ITextViewMargin.Enabled => IsVisible;
		FrameworkElement IWpfTextViewMargin.VisualElement => this;
		double ITextViewMargin.MarginSize => Width;

		public void Dispose() {
			IsVisibleChanged -= _MemberMarker.OnIsVisibleChanged;
			Config.Updated -= Config_Updated;
			_MemberMarker.Dispose();
			_SymbolReferenceMarker.Dispose();
			SyncHelper.CancelAndDispose(ref _Cancellation, false);
		}

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

		void Config_Updated(object sender, ConfigUpdatedEventArgs e) {
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
			//todo cache opaque brushes
			_ClassPen = new Pen(_FormatMap.GetProperties(Constants.CodeClassName).GetBrush().Alpha(TypeAlpha), TypeLineSize);
			_ConstructorPen = new Pen(_FormatMap.GetBrush(Constants.CSharpConstructorMethodName, Constants.CSharpMethodName, Constants.CodeMethodName).Alpha(MemberAlpha), LineSize);
			_DelegatePen = new Pen(_FormatMap.GetProperties(Constants.CodeDelegateName).GetBrush().Alpha(MemberAlpha), LineSize);
			_EnumPen = new Pen(_FormatMap.GetProperties(Constants.CodeEnumName).GetBrush().Alpha(TypeAlpha), TypeLineSize);
			_EventPen = new Pen(_FormatMap.GetBrush(Constants.CSharpEventName, Constants.CodeEventName).Alpha(MemberAlpha), LineSize);
			_FieldPen = new Pen(_FormatMap.GetBrush(Constants.CSharpFieldName, Constants.CodeFieldName).Alpha(MemberAlpha), LineSize);
			_InterfacePen = new Pen(_FormatMap.GetProperties(Constants.CodeInterfaceName).GetBrush().Alpha(TypeAlpha), TypeLineSize);
			_MethodPen = new Pen(_FormatMap.GetBrush(Constants.CSharpMethodName, Constants.CodeMethodName).Alpha(MemberAlpha), LineSize);
			_PropertyPen = new Pen(_FormatMap.GetBrush(Constants.CSharpPropertyName, Constants.CodePropertyName).Alpha(MemberAlpha), LineSize);
			_StructPen = new Pen(_FormatMap.GetProperties(Constants.CodeStructName).GetBrush().Alpha(TypeAlpha), TypeLineSize);
			_RegionForeground = _FormatMap.GetProperties(Constants.CodePreprocessorText).GetBrush();
			_RegionBackground = _FormatMap.GetProperties(Constants.CodePreprocessorText).GetBackgroundBrush().Alpha(TypeAlpha);
			_RegionPen = new Pen(_RegionBackground ?? _RegionForeground, TypeLineSize);
		}

		ITextViewMargin ITextViewMargin.GetTextViewMargin(string marginName) {
			return string.Equals(marginName, MarginName, StringComparison.OrdinalIgnoreCase) ? this : null;
		}

		sealed class MemberMarker
		{
			readonly IWpfTextView _TextView;
			readonly IVerticalScrollBar _ScrollBar;

			IEnumerable<IMappingTagSpan<ICodeMemberTag>> _Tags;
			List<DirectiveTriviaSyntax> _Regions;
			ITagAggregator<ICodeMemberTag> _CodeMemberTagger;
			readonly CSharpMembersMargin _Element;

			public MemberMarker(IWpfTextView textView, IVerticalScrollBar verticalScrollbar, CSharpMembersMargin element) {
				_TextView = textView;
				_ScrollBar = verticalScrollbar;
				_Element = element;
			}

			internal void Dispose() {
				_ScrollBar.TrackSpanChanged -= OnTagsChanged;
				if (_CodeMemberTagger != null) {
					_CodeMemberTagger.BatchedTagsChanged -= OnTagsChanged;
				}
			}

			internal void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
				if ((bool)e.NewValue) {
					//todo refresh the margin when format mapping or syntax highlight style is changed
					//Hook up to the various events we need to keep the margin current.
					_ScrollBar.TrackSpanChanged += OnTagsChanged;

					_Element.UpdateSyntaxColors();

					_CodeMemberTagger = ServicesHelper.Instance.ViewTagAggregatorFactory.CreateTagAggregator<ICodeMemberTag>(_TextView);
					_CodeMemberTagger.BatchedTagsChanged += OnTagsChanged;

					//Force the margin to be re-rendered since things might have changed while the margin was hidden.
					_Element.InvalidateVisual();
				}
				else {
					_ScrollBar.TrackSpanChanged -= OnTagsChanged;
					_CodeMemberTagger.BatchedTagsChanged -= OnTagsChanged;
					_CodeMemberTagger.Dispose();
					_CodeMemberTagger = null;
				}
			}

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
				const int showMemberDeclarationThredshold = 30, longDeclarationLines = 50, labelSize = 8;
				if (Config.Instance.MarkerOptions.HasAnyFlag(MarkerOptions.MemberDeclaration | MarkerOptions.RegionDirective) == false
					|| _TextView.IsClosed) {
					return;
				}
				var snapshot = _TextView.TextSnapshot;
				var regions = _Regions;
				FormattedText text;
				if (regions != null && Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.RegionDirective)) {
					foreach (RegionDirectiveTriviaSyntax region in regions) {
						var s = region.GetDeclarationSignature();
						if (s != null) {
							text = WpfHelper.ToFormattedText(s, labelSize, _Element._RegionForeground);
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
				var tags = _Tags;
				if (tags == null || _CodeMemberTagger == null || Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.MemberDeclaration) == false) {
					return;
				}
				var snapshotLength = snapshot.Length;
				var memberLevel = 0;
				var memberType = CodeMemberType.Root;
				var lastLabel = Double.MinValue;
				SnapshotPoint rangeFrom = default, rangeTo = default;

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
					if (Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.LongMemberDeclaration) && span.Length > 150 && tagType.IsMember()) {
						var lineCount = snapshot.GetLineNumberFromPosition(end) - snapshot.GetLineNumberFromPosition(start);
						var y1 = _ScrollBar.GetYCoordinateOfBufferPosition(start);
						Pen pen = null;
						var y2 = _ScrollBar.GetYCoordinateOfBufferPosition(end);
						if (lineCount >= longDeclarationLines) {
							pen = _Element.GetPenForCodeMemberType(tagType);
							drawingContext.DrawLine(pen, new Point(level, y1), new Point(_Element.ActualWidth, y1));
							drawingContext.DrawLine(pen, new Point(level, y1), new Point(level, y2));
							drawingContext.DrawLine(pen, new Point(level, y2), new Point(_Element.ActualWidth, y2));
						}
						if (y2 - y1 > showMemberDeclarationThredshold && y1 > lastLabel && tag.Tag.Name != null) {
							if (pen == null) {
								pen = _Element.GetPenForCodeMemberType(tagType);
							}
							if (pen.Brush != null) {
								text = WpfHelper.ToFormattedText(tag.Tag.Name, labelSize, pen.Brush.Alpha((y2 - y1) * 20 / _Element.ActualHeight));
								y1 -= text.Height / 2;
								drawingContext.DrawText(text, new Point(level + 2, y1));
							}
							lastLabel = y1 + labelSize;
						}
					}

					if (tagType.IsType()) {
						if (memberType.IsMember()) {
							// draw range for previous grouped members
							var y1 = _ScrollBar.GetYCoordinateOfBufferPosition(rangeFrom);
							var y2 = _ScrollBar.GetYCoordinateOfBufferPosition(rangeTo);
							drawingContext.DrawLine(_Element.GetPenForCodeMemberType(memberType), new Point(memberLevel, y1), new Point(memberLevel, y2));
						}
						// draw type declaration line
						var pen = _Element.GetPenForCodeMemberType(tagType);
						var yTop = _ScrollBar.GetYCoordinateOfBufferPosition(start);
						var yBottom = _ScrollBar.GetYCoordinateOfBufferPosition(end);
						drawingContext.DrawRectangle(pen.Brush.Alpha(1), pen, new Rect(level - (MarkerSize / 2), yTop - (MarkerSize / 2), MarkerSize, MarkerSize));
						drawingContext.DrawLine(pen, new Point(level, yTop), new Point(level, yBottom));
						if (yTop > lastLabel && Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.TypeDeclaration) && tag.Tag.Name != null) {
							// draw type name
							text = WpfHelper.ToFormattedText(tag.Tag.Name, labelSize, pen.Brush.Alpha(1))
								.SetBold();
							if (level != 1) {
								text.SetFontStyle(FontStyles.Italic);
							}
							yTop -= text.Height / 2;
							drawingContext.DrawText(text, new Point(level + 1, yTop));
							lastLabel = yTop + text.Height;
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
							var y1 = _ScrollBar.GetYCoordinateOfBufferPosition(rangeFrom);
							var y2 = _ScrollBar.GetYCoordinateOfBufferPosition(rangeTo);
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
					var y1 = _ScrollBar.GetYCoordinateOfBufferPosition(rangeFrom);
					var y2 = _ScrollBar.GetYCoordinateOfBufferPosition(rangeTo);
					drawingContext.DrawLine(_Element.GetPenForCodeMemberType(memberType), new Point(memberLevel, y1), new Point(memberLevel, y2));
				}
			}
		}

		sealed class SymbolReferenceMarker
		{
			const double MarkerMargin = 1;
			readonly IWpfTextView _View;
			readonly IVerticalScrollBar _ScrollBar;
			readonly CSharpMembersMargin _Margin;
			readonly Brush _MarkerBrush = Brushes.Aqua;
			readonly Pen _DefinitionMarkerPen = new Pen(ThemeHelper.ToolWindowTextBrush, MarkerMargin);
			IEnumerable<ReferencedSymbol> _ReferencePoints;
			SyntaxTree _DocSyntax;
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
				UnhookEvents();
			}

			internal void Render(DrawingContext drawingContext) {
				var refs = _ReferencePoints;
				if (refs == null) {
					return;
				}
				var snapshot = _View.TextSnapshot;
				var snapshotLength = snapshot.Length;
				foreach (var item in refs) {
					if (_Margin._Cancellation?.IsCancellationRequested != false) {
						break;
					}
					foreach (var loc in item.Locations) {
						var start = loc.Location.SourceSpan.Start;
						if (start >= snapshotLength) {
							continue;
						}
						var y = _ScrollBar.GetYCoordinateOfBufferPosition(new SnapshotPoint(snapshot, start));
						drawingContext.DrawRectangle(_MarkerBrush, null, new Rect(MarkerMargin, y - (MarkerSize / 2), MarkerSize, MarkerSize));
					}
					if (item.Definition.ContainingAssembly.GetSourceType() == AssemblySource.Metadata) {
						continue;
					}
					// draws the definition marker
					foreach (var loc in item.Definition.DeclaringSyntaxReferences) {
						if (loc.SyntaxTree == _DocSyntax) {
							var start = loc.Span.Start;
							if (start >= snapshotLength) {
								continue;
							}
							var y = _ScrollBar.GetYCoordinateOfBufferPosition(new SnapshotPoint(snapshot, start));
							drawingContext.DrawRectangle(_MarkerBrush, _DefinitionMarkerPen, new Rect(0, y - (MarkerSize / 2), MarkerSize + MarkerMargin, MarkerSize + MarkerMargin));
						}
					}
				}
			}

			async void UpdateReferences(object sender, EventArgs e) {
				try {
					SyncHelper.CancelAndDispose(ref _Margin._Cancellation, true);
					//if (_View.Selection.IsEmpty == false) {
					//	if (Interlocked.Exchange(ref _ReferencePoints, null) != null) {
					//		_Element.InvalidateVisual();
					//	}
					//	return;
					//}
					await UpdateReferencesAsync();
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
					await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellation);
					_Margin.InvalidateVisual();
					return;
				}
				var symbol = await ctx.GetSymbolAsync(_View.Selection.Start.Position, cancellation).ConfigureAwait(false);
				if (symbol == null) {
					if (Interlocked.Exchange(ref _ReferencePoints, null) != null) {
						_Symbol = null;
						await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellation);
						_Margin.InvalidateVisual();
					}
					return;
				}

				if (ReferenceEquals(Interlocked.Exchange(ref _Symbol, symbol), symbol) == false) {
					var doc = ctx.Document;
					_DocSyntax = ctx.Compilation.SyntaxTree;
					// todo show marked symbols on scrollbar margin
					try {
						_ReferencePoints = await SymbolFinder.FindReferencesAsync(symbol.GetAliasTarget(), doc.Project.Solution, System.Collections.Immutable.ImmutableSortedSet.Create(doc), cancellation).ConfigureAwait(false);
					}
					catch (ArgumentException) {
						// hack: multiple async updates could occur and invalidated ctx.Document, which might cause ArgumentException
						// ignore this at this moment
						return;
					}
					await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellation);
					_Margin.InvalidateVisual();
				}
			}
		}
	}
}
