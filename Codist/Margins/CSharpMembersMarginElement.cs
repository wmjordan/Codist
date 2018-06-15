using System;
using System.Windows;
using System.Windows.Media;
using AppHelpers;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace Codist.Margins
{
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable")]
	/// <summary>
	/// Helper class to handle the rendering of the members margin.
	/// </summary>
	sealed class CSharpMembersMarginElement : FrameworkElement
	{
		//todo user customizable opacity of markers
		const double MarkerSize = 3, LineSize = 2, TypeLineSize = 1, Padding = 3.0, TypeAlpha = 0.5, MemberAlpha = 0.5;

		readonly IWpfTextView _textView;
		readonly IVerticalScrollBar _scrollBar;

		ITagAggregator<ICodeMemberTag> _tagger;
		IEditorFormatMap _formatMap;
		CSharpMembersMarginFactory _factory;
		Pen _ClassPen, _InterfacePen, _StructPen, _EnumPen, _EventPen, _DelegatePen, _ConstructorPen, _MethodPen, _PropertyPen, _FieldPen;
		readonly Pen _EmptyPen = new Pen();

		/// <summary>
		/// Constructor for the <see cref="CSharpMembersMarginElement"/>.
		/// </summary>
		/// <param name="textView">ITextView to which this <see cref="CSharpMembersMarginElement"/> will be attacheded.</param>
		/// <param name="verticalScrollbar">Vertical scrollbar of the ITextViewHost that contains <paramref name="textView"/>.</param>
		/// <param name="factory">MEF tag factory.</param>
		public CSharpMembersMarginElement(IWpfTextView textView, IVerticalScrollBar verticalScrollbar, CSharpMembersMarginFactory factory) {
			_textView = textView;
			_scrollBar = verticalScrollbar;
			_factory = factory;

			_formatMap = factory.EditorFormatMapService.GetEditorFormatMap(textView);

			IsHitTestVisible = false;
			SnapsToDevicePixels = true;
			Width = Padding + MarkerSize;

			Config.Updated += Config_Updated;
			IsVisibleChanged += OnIsVisibleChanged;

			Config_Updated(null, new ConfigUpdatedEventArgs(Features.ScrollbarMarkers));
		}

		public bool Enabled => IsVisible;

		public void Dispose() {
			IsVisibleChanged -= OnIsVisibleChanged;
			Config.Updated -= Config_Updated;
			_scrollBar.TrackSpanChanged -= OnTagsChanged;
			if (_tagger != null) {
				_tagger.BatchedTagsChanged -= OnTagsChanged;
			}
		}

		void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) {
			if ((bool)e.NewValue) {
				//todo refresh the margin when format mapping or syntax highlight style is changed
				//Hook up to the various events we need to keep the margin current.
				_scrollBar.TrackSpanChanged += OnTagsChanged;

				UpdateSyntaxColors();

				_tagger = _factory.TagAggregatorFactoryService.CreateTagAggregator<ICodeMemberTag>(_textView);
				_tagger.BatchedTagsChanged += OnTagsChanged;

				//Force the margin to be re-rendered since things might have changed while the margin was hidden.
				InvalidateVisual();
			}
			else {
				_scrollBar.TrackSpanChanged -= OnTagsChanged;
				_tagger.BatchedTagsChanged -= OnTagsChanged;
				_tagger.Dispose();
				_tagger = null;
			}
		}

		void UpdateSyntaxColors() {
			//todo cache opaque brushes
			_ClassPen = new Pen(_formatMap.GetBrush(Constants.CodeClassName).Alpha(TypeAlpha), TypeLineSize);
			_ConstructorPen = new Pen(_formatMap.GetBrush(Constants.CSharpConstructorMethodName).Alpha(MemberAlpha), LineSize);
			_DelegatePen = new Pen(_formatMap.GetBrush(Constants.CodeDelegateName).Alpha(MemberAlpha), LineSize);
			_EnumPen = new Pen(_formatMap.GetBrush(Constants.CodeEnumName).Alpha(TypeAlpha), TypeLineSize);
			_EventPen = new Pen(_formatMap.GetBrush(Constants.CSharpEventName).Alpha(MemberAlpha), LineSize);
			_FieldPen = new Pen(_formatMap.GetBrush(Constants.CSharpFieldName).Alpha(MemberAlpha), LineSize);
			_InterfacePen = new Pen(_formatMap.GetBrush(Constants.CodeInterfaceName).Alpha(TypeAlpha), TypeLineSize);
			_MethodPen = new Pen(_formatMap.GetBrush(Constants.CSharpMethodName).Alpha(MemberAlpha), LineSize);
			_PropertyPen = new Pen(_formatMap.GetBrush(Constants.CSharpPropertyName).Alpha(MemberAlpha), LineSize);
			_StructPen = new Pen(_formatMap.GetBrush(Constants.CodeStructName).Alpha(TypeAlpha), TypeLineSize);
		}

		void Config_Updated(object sender, ConfigUpdatedEventArgs e) {
			if (e.UpdatedFeature.HasAnyFlag(Features.SyntaxHighlight | Features.ScrollbarMarkers) == false) {
				return;
			}
			var setVisible = Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.MemberDeclaration);
			var visible = Visibility == Visibility.Visible;
			if (setVisible == false && visible) {
				Visibility = Visibility.Collapsed;
			}
			else if (setVisible && visible == false) {
				Visibility = Visibility.Visible;
			}
			if (Visibility == Visibility.Visible) {
				if (e.UpdatedFeature.MatchFlags(Features.SyntaxHighlight)) {
					UpdateSyntaxColors();
				}
				InvalidateVisual();
			}
		}

		void OnTagsChanged(object sender, EventArgs e) {
			InvalidateVisual();
		}

		/// <summary>
		/// Override for the FrameworkElement's OnRender. When called, redraw
		/// all of the markers 
		/// </summary>
		protected override void OnRender(DrawingContext drawingContext) {
			const int showMemberDeclarationThredshold = 30, longDeclarationLines = 50, labelSize = 8;

			base.OnRender(drawingContext);

			if (_textView.IsClosed || _tagger == null || ActualHeight == 0 || Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.MemberDeclaration) == false) {
				return;
			}
			var snapshot = _textView.TextSnapshot;
			var snapshotLength = snapshot.Length;
			//todo cache previous results and update modified regions behind changed position only
			var tags = _tagger.GetTags(new SnapshotSpan(snapshot, 0, snapshotLength));
			var memberLevel = 0;
			var memberType = CodeMemberType.Root;
			var lastLabel = Double.MinValue;
			SnapshotPoint rangeFrom = default(SnapshotPoint), rangeTo = default(SnapshotPoint);

			foreach (var tag in tags) {
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
					var lineCount = snapshot.GetLineNumberFromPosition(end) - snapshot.GetLineNumberFromPosition(start);
					var y1 = _scrollBar.GetYCoordinateOfBufferPosition(start);
					Pen pen = null;
					if (tagType.IsMember()) {
						var y2 = _scrollBar.GetYCoordinateOfBufferPosition(end);
						if (lineCount >= longDeclarationLines) {
							pen = GetPenForCodeMemberType(tagType);
							drawingContext.DrawLine(pen, new Point(level, y1), new Point(ActualWidth, y1));
							drawingContext.DrawLine(pen, new Point(level, y1), new Point(level, y2));
							drawingContext.DrawLine(pen, new Point(level, y2), new Point(ActualWidth, y2));
						}
						if (y2 - y1 > showMemberDeclarationThredshold && y1 > lastLabel && tag.Tag.Name != null) {
							if (pen == null) {
								pen = GetPenForCodeMemberType(tagType);
							}
							if (pen.Brush != null) {
								var text = WpfHelper.ToFormattedText(tag.Tag.Name, labelSize, pen.Brush.Clone().Alpha((y2 - y1) * 20 / ActualHeight));
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
						var y1 = _scrollBar.GetYCoordinateOfBufferPosition(rangeFrom);
						var y2 = _scrollBar.GetYCoordinateOfBufferPosition(rangeTo);
						drawingContext.DrawLine(GetPenForCodeMemberType(memberType), new Point(memberLevel, y1), new Point(memberLevel, y2));
					}
					// draw type declaration line
					var pen = GetPenForCodeMemberType(tagType);
					var yTop = _scrollBar.GetYCoordinateOfBufferPosition(start);
					var yBottom = _scrollBar.GetYCoordinateOfBufferPosition(end);
					drawingContext.DrawRectangle(pen.Brush.Clone().Alpha(1), pen, new Rect(level - (MarkerSize / 2), yTop - (MarkerSize / 2), MarkerSize, MarkerSize));
					drawingContext.DrawLine(pen, new Point(level, yTop), new Point(level, yBottom));
					if (yTop > lastLabel && Config.Instance.MarkerOptions.MatchFlags(MarkerOptions.TypeDeclaration) && tag.Tag.Name != null) {
						// draw type name
						var text = WpfHelper.ToFormattedText(tag.Tag.Name, labelSize, pen.Brush.Clone().Alpha(1))
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
					&& tagType == CodeMemberType.Method) {
					drawingContext.DrawRectangle(_MethodPen.Brush.Clone().Alpha(1), _MethodPen, new Rect(level - (MarkerSize / 2), _scrollBar.GetYCoordinateOfBufferPosition(start) - (MarkerSize / 2), MarkerSize, MarkerSize));
				}
				if (tagType == memberType) {
					// expand the range to the end of the tag
					rangeTo = end;
				}
				else {
					if (memberType.IsMember()) {
						// draw range for previous grouped members
						var y1 = _scrollBar.GetYCoordinateOfBufferPosition(rangeFrom);
						var y2 = _scrollBar.GetYCoordinateOfBufferPosition(rangeTo);
						drawingContext.DrawLine(GetPenForCodeMemberType(memberType), new Point(level, y1), new Point(level, y2));
					}
					memberType = tagType;
					rangeFrom = start;
					rangeTo = end;
					memberLevel = level;
				}
			}
			if (memberType.IsMember()) {
				// draw range for previous grouped members
				var y1 = _scrollBar.GetYCoordinateOfBufferPosition(rangeFrom);
				var y2 = _scrollBar.GetYCoordinateOfBufferPosition(rangeTo);
				drawingContext.DrawLine(GetPenForCodeMemberType(memberType), new Point(memberLevel, y1), new Point(memberLevel, y2));
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
	}
}
