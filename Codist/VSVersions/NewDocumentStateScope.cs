using System;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell.Interop;

// Use this for VS 2022
namespace Microsoft.VisualStudio.Shell
{
	public class NewDocumentStateScope : DisposableObject
	{
		readonly IVsNewDocumentStateContext _context;

		private NewDocumentStateScope(uint state, Guid reason) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var doc = Package.GetGlobalService(typeof(IVsUIShellOpenDocument)) as IVsUIShellOpenDocument3;
			if (doc != null) {
				_context = doc.SetNewDocumentState(state, ref reason);
			}
		}

		public NewDocumentStateScope(__VSNEWDOCUMENTSTATE state, Guid reason)
			: this((uint)state, reason) {
		}

		public NewDocumentStateScope(__VSNEWDOCUMENTSTATE2 state, Guid reason)
			: this((uint)state, reason) {
		}

		protected override void DisposeNativeResources() {
			ThreadHelper.ThrowIfNotOnUIThread();
			_context?.Restore();
			base.DisposeNativeResources();
		}
	}
}
