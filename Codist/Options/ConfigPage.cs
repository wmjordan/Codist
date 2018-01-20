using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist.Options
{
	class ConfigPage : DialogPage
	{
		internal FontInfo GetFontSettings(Guid category) {
			var storage = (IVsFontAndColorStorage)GetService(typeof(SVsFontAndColorStorage));
			var pLOGFONT = new LOGFONTW[1];
			var pInfo = new FontInfo[1];

			ErrorHandler.ThrowOnFailure(storage.OpenCategory(category, (uint)(__FCSTORAGEFLAGS.FCSF_LOADDEFAULTS | __FCSTORAGEFLAGS.FCSF_PROPAGATECHANGES)));
			try {
				if (!ErrorHandler.Succeeded(storage.GetFont(pLOGFONT, pInfo))) {
					return default(FontInfo);
				}
				return pInfo[0];
			}
			finally {
				storage.CloseCategory();
			}
		}
		internal static FontStyle GetFontStyle(StyleBase activeStyle) {
			var f = FontStyle.Regular;
			if (activeStyle.Bold == true) {
				f |= FontStyle.Bold;
			}
			if (activeStyle.Italic == true) {
				f |= FontStyle.Italic;
			}
			if (activeStyle.Underline == true) {
				f |= FontStyle.Underline;
			}
			if (activeStyle.StrikeThrough == true) {
				f |= FontStyle.Strikeout;
			}
			return f;
		}

		internal static Brush GetPreviewBrush(BrushEffect effect, System.Windows.Media.Color color, ref SizeF previewRegion) {
			switch (effect) {
				case BrushEffect.Solid:
					return new SolidBrush(color.ToGdiColor());
				case BrushEffect.ToBottom:
					return new LinearGradientBrush(new PointF(0, 0), new PointF(0, previewRegion.Height), Color.Transparent, color.ToGdiColor());
				case BrushEffect.ToTop:
					return new LinearGradientBrush(new PointF(0, previewRegion.Height), new PointF(0, 0), Color.Transparent, color.ToGdiColor());
				case BrushEffect.ToRight:
					return new LinearGradientBrush(new PointF(0, 0), new PointF(previewRegion.Width, 0), Color.Transparent, color.ToGdiColor());
				case BrushEffect.ToLeft:
					return new LinearGradientBrush(new PointF(previewRegion.Width, 0), new PointF(0, 0), Color.Transparent, color.ToGdiColor());
				default:
					goto case BrushEffect.Solid;
			}
		}



		protected override void OnApply(PageApplyEventArgs e) {
			base.OnApply(e);
			if (e.ApplyBehavior == ApplyKind.Apply) {
				Config.Instance.SaveConfig(null);
			}
		}
	}

	[Guid("8ECD56D1-87C1-47E2-9FB0-742B0FF35FEF")]
	sealed class CodeStyle : ConfigPage
	{
		SyntaxStyleOptionPage _control;

		protected override IWin32Window Window => _control ?? (_control = new SyntaxStyleOptionPage(this, () => Config.Instance.CodeStyles, Config.GetDefaultCodeStyles));
		protected override void Dispose(bool disposing) {
			_control.Dispose();
			base.Dispose(disposing);
		}
	}

	[Guid("4C16F280-BE29-4152-A6C5-58EEC5398FD4")]
	sealed class CommentStyle : ConfigPage
	{
		SyntaxStyleOptionPage _Control;

		protected override IWin32Window Window => _Control ?? (_Control = new SyntaxStyleOptionPage(this, () => Config.Instance.CommentStyles, Config.GetDefaultCommentStyles));
		protected override void Dispose(bool disposing) {
			_Control.Dispose();
			base.Dispose(disposing);
		}
	}

	[Guid("2E07AC20-D62F-4D78-8750-2A464CC011AE")]
	sealed class XmlCodeStyle : ConfigPage
	{
		SyntaxStyleOptionPage _Control;

		protected override IWin32Window Window => _Control ?? (_Control = new SyntaxStyleOptionPage(this, () => Config.Instance.XmlCodeStyles, Config.GetDefaultXmlCodeStyles));
		protected override void Dispose(bool disposing) {
			_Control.Dispose();
			base.Dispose(disposing);
		}
	}

	[Guid("1EB954DF-37FE-4849-B63A-58EC43088856")]
	sealed class CommentTagger : ConfigPage
	{
		CommentTaggerOptionPage _Control;

		protected override IWin32Window Window => _Control ?? (_Control = new CommentTaggerOptionPage(this));
		protected override void Dispose(bool disposing) {
			_Control.Dispose();
			base.Dispose(disposing);
		}
	}

	[Guid("DFC9C0E7-73A1-4DE9-8E94-161111266D38")]
	sealed class Misc : ConfigPage
	{
		MiscPage _Control;

		protected override IWin32Window Window => _Control ?? (_Control = new MiscPage(this));
		protected override void Dispose(bool disposing) {
			_Control.Dispose();
			base.Dispose(disposing);
		}
	}
}
