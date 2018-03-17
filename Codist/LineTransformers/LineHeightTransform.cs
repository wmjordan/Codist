using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text.Formatting;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Differencing;

namespace Codist.LineTransformers
{
	[Export(typeof(ILineTransformSourceProvider))]
	[ContentType(Constants.CodeTypes.Text)]
	[TextViewRole(PredefinedTextViewRoles.Document)]
	sealed class LineHeightTransformProvider : ILineTransformSourceProvider
	{
		static readonly LineTransform __DefaultLineTransform = new LineTransform(1);
		static LineTransform __LineTransform = new LineTransform(1);
		static LineTransform __FirstWrappedLineTransform = new LineTransform(1);
		static LineTransform __LastWrappedLineTransform = new LineTransform(1);

		public static double TopSpace {
			get => __LineTransform.TopSpace;
			set {
				__LineTransform = new LineTransform(value < 0 ? 0 : value > 100 ? 100 : value, BottomSpace, 1);
				__FirstWrappedLineTransform = new LineTransform(value < 0 ? 0 : value > 100 ? 100 : value, 0, 1);
			}
		}
		public static double BottomSpace {
			get => __LineTransform.BottomSpace;
			set {
				__LineTransform = new LineTransform(TopSpace, value < 0 ? 0 : value > 100 ? 100 : value, 1);
				__LastWrappedLineTransform = new LineTransform(0, value < 0 ? 0 : value > 100 ? 100 : value, 1);
			}
		}

		public ILineTransformSource Create(IWpfTextView textView) {
			if (textView.Roles.Contains(DifferenceViewerRoles.LeftViewTextViewRole)
				 || textView.Roles.Contains(DifferenceViewerRoles.RightViewTextViewRole)
				 || textView.Roles.Contains("VSMERGEDEFAULT")) {
				// Ignore diff views 
				return null;
			}

			return new LineHeightTransform();
		}

		sealed class LineHeightTransform : ILineTransformSource
		{
			// todo: refresh after settings are changed
			public LineTransform GetLineTransform(ITextViewLine line, double yPosition, ViewRelativePosition placement) {
				if (Config.Instance.NoSpaceBetweenWrappedLines == false) {
					return __LineTransform;
				}

				var l = line.Start.GetContainingLine();
				return line.Length == l.Length ? __LineTransform
					: line.Start == l.Start ? __FirstWrappedLineTransform
					: line.End == l.End ? __LastWrappedLineTransform
					: __DefaultLineTransform;
			}
		}
	}
}
