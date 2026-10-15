using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Differencing;
using Microsoft.VisualStudio.Utilities;
using CLR;

namespace Codist.LineTransformers;

/// <summary>
/// Adds extra margin to lines.
/// </summary>
[Export(typeof(ILineTransformSourceProvider))]
[ContentType(Constants.CodeTypes.Text)]
[TextViewRole(PredefinedTextViewRoles.Document)]
sealed class LineHeightTransformProvider : ILineTransformSourceProvider
{
	static LineHeightTransform __Transform;

	public ILineTransformSource Create(IWpfTextView textView) {
		if (textView.Roles.ContainsAny([DifferenceViewerRoles.LeftViewTextViewRole, DifferenceViewerRoles.RightViewTextViewRole, "VSMERGEDEFAULT"])) {
			// Ignore diff views 
			return null;
		}

		return __Transform ??= new LineHeightTransform();
	}

	sealed class LineHeightTransform : ILineTransformSource
	{
		static readonly LineTransform __DefaultLineTransform = new(1);

		LineTransform _LineTransform = new(1);
		LineTransform _FirstWrappedLineTransform = new(1);
		LineTransform _LastWrappedLineTransform = new(1);

		public double TopSpace {
			get => _LineTransform.TopSpace;
			set {
				_LineTransform = new LineTransform(value < 0 ? 0 : value > 100 ? 100 : value, BottomSpace, 1);
				_FirstWrappedLineTransform = new LineTransform(value < 0 ? 0 : value > 100 ? 100 : value, 0, 1);
			}
		}
		public double BottomSpace {
			get => _LineTransform.BottomSpace;
			set {
				_LineTransform = new LineTransform(TopSpace, value < 0 ? 0 : value > 100 ? 100 : value, 1);
				_LastWrappedLineTransform = new LineTransform(0, value < 0 ? 0 : value > 100 ? 100 : value, 1);
			}
		}

		public LineHeightTransform() {
			TopSpace = Config.Instance.TopSpace;
			BottomSpace = Config.Instance.BottomSpace;

			Config.RegisterUpdateHandler((args) => {
				if (args.UpdatedFeature.MatchFlags(Features.SyntaxHighlight)) {
					TopSpace = Config.Instance.TopSpace;
					BottomSpace = Config.Instance.BottomSpace;
				}
			});
		}

		// todo: refresh after settings are changed
		public LineTransform GetLineTransform(ITextViewLine line, double yPosition, ViewRelativePosition placement) {
			if (Config.Instance.NoSpaceBetweenWrappedLines == false) {
				return _LineTransform;
			}

			var l = line.Start.GetContainingLine();
			return line.Length == l.Length ? _LineTransform
				: line.Start.Position == l.Start.Position ? _FirstWrappedLineTransform
				: line.End.Position == l.End.Position ? _LastWrappedLineTransform
				: __DefaultLineTransform;
		}
	}
}
