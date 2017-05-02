using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text.Editor;

namespace Codist.Options
{
	[Guid("8ECD56D1-87C1-47E2-9FB0-742B0FF35FEF")]
	class SyntaxStyle : DialogPage
	{
		SyntaxStyleOptionPage _control;

		protected override IWin32Window Window {
			get {
				return _control ?? (_control = new SyntaxStyleOptionPage(this));
			}
		}
		protected override void OnApply(PageApplyEventArgs e) {
			base.OnApply(e);
			Config.Instance.SaveConfig();
		}
		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);
			_control.Dispose();
		}

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


	}

	[Guid("1EB954DF-37FE-4849-B63A-58EC43088856")]
	class CommentTagger : DialogPage
	{
		CommentTaggerOptionControl _control;
		protected override IWin32Window Window {
			get {
				return _control ?? (_control = new CommentTaggerOptionControl());
			}
		}
		protected override void OnApply(PageApplyEventArgs e) {
			base.OnApply(e);
		}
		protected override void Dispose(bool disposing) {
			base.Dispose(disposing);
			_control.Dispose();
		}
	}
}
