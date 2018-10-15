using System;
using System.Windows;
using System.Windows.Media;
using System.Threading;
using AppHelpers;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;

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
		Pen _ClassPen, _InterfacePen, _StructPen, _EnumPen, _EventPen, _DelegatePen, _ConstructorPen, _MethodPen, _PropertyPen, _FieldPen;

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
			IsVisibleChanged += _MemberMarker.OnIsVisibleChanged;
			textView.Closed += TextView_Closed;

			Config.Updated += Config_Updated;
			Config_Updated(null, new ConfigUpdatedEventArgs(Features.ScrollbarMarkers));
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
			_ClassPen = new Pen(_FormatMap.GetBrush(Constants.CodeClassName).Alpha(TypeAlpha), TypeLineSize);
			_ConstructorPen = new Pen(_FormatMap.GetBrush(Constants.CSharpConstructorMethodName).Alpha(MemberAlpha), LineSize);
			_DelegatePen = new Pen(_FormatMap.GetBrush(Constants.CodeDelegateName).Alpha(MemberAlpha), LineSize);
			_EnumPen = new Pen(_FormatMap.GetBrush(Constants.CodeEnumName).Alpha(TypeAlpha), TypeLineSize);
			_EventPen = new Pen(_FormatMap.GetBrush(Constants.CSharpEventName).Alpha(MemberAlpha), LineSize);
			_FieldPen = new Pen(_FormatMap.GetBrush(Constants.CSharpFieldName).Alpha(MemberAlpha), LineSize);
			_InterfacePen = new Pen(_FormatMap.GetBrush(Constants.CodeInterfaceName).Alpha(TypeAlpha), TypeLineSize);
			_MethodPen = new Pen(_FormatMap.GetBrush(Constants.CSharpMethodName).Alpha(MemberAlpha), LineSize);
			_PropertyPen = new Pen(_FormatMap.GetBrush(Constants.CSharpPropertyName).Alpha(MemberAlpha), LineSize);
			_StructPen = new Pen(_FormatMap.GetBrush(Constants.CodeStructName).Alpha(TypeAlpha), TypeLineSize);
		}

		ITextViewMargin ITextViewMargin.GetTextViewMargin(string marginName) {
			return string.Equals(marginName, MarginName, StringComparison.OrdinalIgnoreCase) ? this : null;
		}

		sealed class MemberMarker
		{
			readonly IWpfTextView _TextView;
			readonly IVerticalScrollBar _ScrollBar;

			IEnumerable<IMappingTagSpan<ICodeMemberTag>> _Tags;
			ITagAggregator<ICodeMemberTag> _CodeMemberTagger;
			readonly CSharpMembersMargin _Element;
			readonly Pen _EmptyPen = new Pen();

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
					CancellationHelper.CancelAndDispose(ref _Element._Cancellation, true);
					await Task.Run((Action)TagDocument, _Element._Cancellation.GetToken());
				}
				catch (ObjectDisposedException) {
					return;
				}
				catch (OperationCanceledException) {
					return;
				}
				_Element.InvalidateVisual();
			}

			void TagDocument() {
				var tagger = _CodeMemberTagger;
				if (_TextView.IsClosed || tagger == null || Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.MemberDeclaration) == false) {
					return;
				}
				var snapshot = _TextView.TextSnapshot;
				_Tags = new List<IMappingTagSpan<ICodeMemberTag>>(tagger.GetTags(new SnapshotSpan(snapshot, 0, snapshot.Length)));
			}

			internal void Render(DrawingContext drawingContext) {
				const int showMemberDeclarationThredshold = 30, longDeclarationLines = 50, labelSize = 8;
				var tags = _Tags;
				if (tags == null || _TextView.IsClosed || _CodeMemberTagger == null || Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.MemberDeclaration) == false) {
					return;
				}
				var snapshot = _TextView.TextSnapshot;
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
					if (Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.LongMemberDeclaration) && span.Length > 150) {
						if (tagType.IsMember()) {
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
									var text = WpfHelper.ToFormattedText(tag.Tag.Name, labelSize, pen.Brush.Alpha((y2 - y1) * 20 / _Element.ActualHeight));
									y1 -= text.Height / 2;
									drawingContext.DrawText(text, new Point(level + 2, y1));
								}
								lastLabel = y1 + labelSize;
							}
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
							var text = WpfHelper.ToFormattedText(tag.Tag.Name, labelSize, pen.Brush.Alpha(1))
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
					if (Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.MethodDeclaration)
						&& tagType == CodeMemberType.Method
						&& _Element._MethodPen.Brush != null) {
						drawingContext.DrawRectangle(_Element._MethodPen.Brush.Alpha(1), _Element._MethodPen, new Rect(level - (MarkerSize / 2), _ScrollBar.GetYCoordinateOfBufferPosition(start) - (MarkerSize / 2), MarkerSize, MarkerSize));
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
			readonly CSharpMembersMargin _Element;
			readonly Brush _MarkerBrush = Brushes.Aqua;
			readonly Pen _DefinitionMarkerPen = new Pen(ThemeHelper.ToolWindowTextBrush, MarkerMargin);
			IEnumerable<ReferencedSymbol> _ReferencePoints;
			SyntaxTree _DocSyntax;
			SyntaxNode _Node;

			public SymbolReferenceMarker(IWpfTextView textView, IVerticalScrollBar verticalScrollbar, CSharpMembersMargin element) {
				_View = textView;
				_ScrollBar = verticalScrollbar;
				_Element = element;
				HookEvents();
			}

			internal void HookEvents() {
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
					if (_Element._Cancellation?.IsCancellationRequested != false) {
						break;
					}
					foreach (var loc in item.Locations) {
						var start = loc.Location.SourceSpan.Start;
						if (start > snapshotLength) {
							continue;
						}
						var y = _ScrollBar.GetYCoordinateOfBufferPosition(new SnapshotPoint(snapshot, start));
						drawingContext.DrawRectangle(_MarkerBrush, null, new Rect(MarkerMargin, y - (MarkerSize / 2), MarkerSize, MarkerSize));
					}
					if (item.Definition.CanBeReferencedByName == false) {
						continue;
					}
					var locs = item.Definition.GetSourceLocations();
					if (locs.IsDefaultOrEmpty) {
						continue;
					}
					// draws the definition marker
					foreach (var loc in locs) {
						if (loc.SourceTree == _DocSyntax) {
							var start = loc.SourceSpan.Start;
							if (start > snapshotLength) {
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
					CancellationHelper.CancelAndDispose(ref _Element._Cancellation, true);
					if (_View.Selection.IsEmpty == false) {
						if (Interlocked.Exchange(ref _ReferencePoints, null) != null) {
							_Element.InvalidateVisual();
						}
						return;
					}
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
				var cancellation = _Element._Cancellation.GetToken();
				var doc = _View.TextSnapshot.GetOpenDocumentInCurrentContextWithChanges();
				var model = await doc.GetSemanticModelAsync(cancellation);
				_DocSyntax = model.SyntaxTree;
				var node = (await _DocSyntax.GetRootAsync(cancellation)).FindNode(new TextSpan(_View.Selection.ActivePoint.Position, 0));
				if (node.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.Argument)) {
					node = (node as Microsoft.CodeAnalysis.CSharp.Syntax.ArgumentSyntax).Expression;
				}
				if (node.HasLeadingTrivia && node.GetLeadingTrivia().FullSpan.Contains(_View.Selection, true)
					|| node.HasTrailingTrivia && node.GetTrailingTrivia().FullSpan.Contains(_View.Selection, true)) {
					if (Interlocked.Exchange(ref _ReferencePoints, null) != null) {
						_Node = null;
						_Element.InvalidateVisual();
					}
					return;
				}
				var symbol = model.GetSymbolOrFirstCandidate(node, cancellation) ?? model.GetSymbolExt(node, cancellation);
				if (symbol != null) {
					if (Interlocked.Exchange(ref _Node, node) != node) {
						// todo show marked symbols on scrollbar margin
						_ReferencePoints = await SymbolFinder.FindReferencesAsync(symbol.GetAliasTarget(), doc.Project.Solution, System.Collections.Immutable.ImmutableSortedSet.Create(doc), cancellation);
						_Element.InvalidateVisual();
					}
				}
				else {
					if (Interlocked.Exchange(ref _ReferencePoints, null) != null) {
						_Node = null;
						_Element.InvalidateVisual();
					}
				}
			}
		}
	}
}
