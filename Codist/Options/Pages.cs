using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist.Options
{
	class PageBase : DialogPage
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

		protected override void OnApply(PageApplyEventArgs e) {
			base.OnApply(e);
			if (e.ApplyBehavior == ApplyKind.Apply) {
				Config.Instance.SaveConfig();
			}
		}
	}

	[Guid("8ECD56D1-87C1-47E2-9FB0-742B0FF35FEF")]
	class CodeStyle : PageBase
	{
		SyntaxStyleOptionPage _control;

		protected override IWin32Window Window {
			get {
				return _control ?? (_control = new SyntaxStyleOptionPage(this, Config.Instance.CodeStyles));
			}
		}
		protected override void Dispose(bool disposing) {
			_control.Dispose();
			base.Dispose(disposing);
		}
	}

	[Guid("4C16F280-BE29-4152-A6C5-58EEC5398FD4")]
	class CommentStyle : PageBase
	{
		SyntaxStyleOptionPage _Control;

		protected override IWin32Window Window {
			get {
				return _Control ?? (_Control = new SyntaxStyleOptionPage(this, Config.Instance.Styles));
			}
		}
		protected override void Dispose(bool disposing) {
			_Control.Dispose();
			base.Dispose(disposing);
		}
	}

	[Guid("1EB954DF-37FE-4849-B63A-58EC43088856")]
	class CommentTagger : PageBase
	{
		CommentTaggerOptionPage _Control;

		protected override IWin32Window Window {
			get {
				return _Control ?? (_Control = new CommentTaggerOptionPage(this));
			}
		}
		protected override void Dispose(bool disposing) {
			_Control.Dispose();
			base.Dispose(disposing);
		}
	}

	[Guid("DFC9C0E7-73A1-4DE9-8E94-161111266D38")]
	class Misc : PageBase
	{
		MiscPage _Control;

		protected override IWin32Window Window {
			get {
				return _Control ?? (_Control = new MiscPage(this));
			}
		}
		protected override void Dispose(bool disposing) {
			_Control.Dispose();
			base.Dispose(disposing);
		}
	}
}
