using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Codist.Options
{
	public partial class MiscPage : UserControl
	{
		public MiscPage() {
			InitializeComponent();
		}
		internal MiscPage(PageBase page) : this() {

		}
		private void MiscPage_Load(object sender, EventArgs e) {
			_TopMarginBox.Value = (decimal)LineTransformers.LineHeightTransformProvider.TopSpace;
			_BottomMarginBox.Value = (decimal)LineTransformers.LineHeightTransformProvider.BottomSpace;
			_NoSpaceBetweenWrappedLinesBox.Checked = Config.Instance.NoSpaceBetweenWrappedLines;

			_CodeAbstractionsBox.Checked = Config.Instance.MarkAbstractions;
			_CodeAbstractionsBox.CheckedChanged += (s, args) => Config.Instance.MarkAbstractions = _CodeAbstractionsBox.Checked;
			_DirectivesBox.Checked = Config.Instance.MarkDirectives;
			_DirectivesBox.CheckedChanged += (s, args) => Config.Instance.MarkAbstractions = _DirectivesBox.Checked;
			_SpecialCommentsBox.Checked = Config.Instance.MarkComments;
			_SpecialCommentsBox.CheckedChanged += (s, args) => Config.Instance.MarkComments = _SpecialCommentsBox.Checked;
			_TypeDeclarationBox.Checked = Config.Instance.MarkDeclarations;
			_TypeDeclarationBox.CheckedChanged += (s, args) => Config.Instance.MarkDeclarations = _TypeDeclarationBox.Checked;

			_TopMarginBox.ValueChanged += (s, args) => LineTransformers.LineHeightTransformProvider.TopSpace = (double)_TopMarginBox.Value;
			_BottomMarginBox.ValueChanged += (s, args) => LineTransformers.LineHeightTransformProvider.BottomSpace = (double)_BottomMarginBox.Value;
			_NoSpaceBetweenWrappedLinesBox.CheckedChanged += (s, args) => Config.Instance.NoSpaceBetweenWrappedLines = _NoSpaceBetweenWrappedLinesBox.Checked;
		}
	}
}
