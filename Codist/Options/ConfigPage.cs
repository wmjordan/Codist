using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AppHelpers;
using Codist.SyntaxHighlight;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace Codist.Options
{
	[Browsable(false)]
	abstract class ConfigPage : DialogPage
	{
		int _version, _oldVersion;
		Label _DisabledNotice;

		protected abstract Features Feature { get; }
		protected UserControl Control { get; set; }

		protected override void OnActivate(CancelEventArgs e) {
			base.OnActivate(e);
			_oldVersion = _version;
			if (Feature != Features.None) {
				if (Control.Enabled = Config.Instance.Features.MatchFlags(Feature)) {
					if (_DisabledNotice != null) {
						_DisabledNotice.Visible = false;
					}
				}
				else {
					if (_DisabledNotice == null) {
						_DisabledNotice = CreateDisabledNotice(Feature);
						Control.Controls.Add(_DisabledNotice);
						_DisabledNotice.BringToFront();
					}
				}
			}
			Config.Updated += UpdateVersion;
		}

		protected override void OnClosed(EventArgs e) {
			base.OnClosed(e);
			if (_version != _oldVersion) {
				Config.Updated -= UpdateVersion;
				Config.LoadConfig(Config.ConfigPath);
				_oldVersion = _version;
			}
		}

		protected override void OnApply(PageApplyEventArgs e) {
			base.OnApply(e);
			if (e.ApplyBehavior == ApplyKind.Apply) {
				Config.Instance.SaveConfig(null);
			}
		}

		protected override void OnDeactivate(CancelEventArgs e) {
			base.OnDeactivate(e);
		}

		protected override void Dispose(bool disposing) {
			Control?.Dispose();
			base.Dispose(disposing);
		}

		internal FontInfo GetFontSettings(Guid category) {
			ThreadHelper.ThrowIfNotOnUIThread();
			var storage = (IVsFontAndColorStorage)GetService(typeof(SVsFontAndColorStorage));
			var pLOGFONT = new LOGFONTW[1];
			var pInfo = new FontInfo[1];

			ErrorHandler.ThrowOnFailure(storage.OpenCategory(category, (uint)(__FCSTORAGEFLAGS.FCSF_LOADDEFAULTS | __FCSTORAGEFLAGS.FCSF_PROPAGATECHANGES)));
			try {
				return ErrorHandler.Succeeded(storage.GetFont(pLOGFONT, pInfo)) ? pInfo[0] : default;
			}
			finally {
				storage.CloseCategory();
			}
		}

		internal static Brush GetPreviewBrush(BrushEffect effect, Color color, ref SizeF previewRegion) {
			switch (effect) {
				case BrushEffect.Solid:
					return new SolidBrush(color);
				case BrushEffect.ToBottom:
					return new LinearGradientBrush(new PointF(0, 0), new PointF(0, previewRegion.Height), Color.Transparent, color);
				case BrushEffect.ToTop:
					return new LinearGradientBrush(new PointF(0, previewRegion.Height), new PointF(0, 0), Color.Transparent, color);
				case BrushEffect.ToRight:
					return new LinearGradientBrush(new PointF(0, 0), new PointF(previewRegion.Width, 0), Color.Transparent, color);
				case BrushEffect.ToLeft:
					return new LinearGradientBrush(new PointF(previewRegion.Width, 0), new PointF(0, 0), Color.Transparent, color);
				default:
					goto case BrushEffect.Solid;
			}
		}

		void UpdateVersion(object sender, EventArgs e) {
			_version++;
		}

		static Label CreateDisabledNotice(Features feature) {
			return new Label() {
				Dock = DockStyle.Top,
				BackColor = SystemColors.ActiveCaption,
				ForeColor = SystemColors.ActiveCaptionText,
				BorderStyle = BorderStyle.FixedSingle,
				Text = feature.ToString() + " is disabled. Enable it in the General page.",
				TextAlign = ContentAlignment.MiddleCenter
			};
		}
	}

	[Browsable(false)]
	[Guid("8BF03E86-FF38-4AB4-8D23-1AC70E74806C")]
	sealed class SyntaxHighlight : ConfigPage
	{
		protected override Features Feature => Features.SyntaxHighlight;
		protected override IWin32Window Window => Control ?? (Control = new SyntaxHighlightPage(this));
	}

	[Browsable(false)]
	[Guid("2E07AC20-D62F-4D78-8750-2A464CC011AE")]
	sealed class XmlStyle : ConfigPage
	{
		protected override Features Feature => Features.SyntaxHighlight;
		protected override IWin32Window Window => Control ?? (Control = new SyntaxStyleOptionPage(this, () => Config.Instance.XmlCodeStyles, Config.GetDefaultCodeStyles<XmlCodeStyle, XmlStyleTypes>));
	}

	[Browsable(false)]
	[Guid("1EB954DF-37FE-4849-B63A-58EC43088856")]
	sealed class CommentStyle : ConfigPage
	{
		protected override Features Feature => Features.SyntaxHighlight;
		protected override IWin32Window Window => Control ?? (Control = new CommentTaggerOptionPage(this));
	}

	[Browsable(false)]
	[Guid("31356507-E11A-4E61-B0C2-C9A6584632DB")]
	sealed class CSharpStyle : ConfigPage
	{
		protected override Features Feature => Features.SyntaxHighlight;
		protected override IWin32Window Window => Control ?? (Control = new CSharpSyntaxHighlightPage(this));
	}

	[Browsable(false)]
	[Guid("DFC9C0E7-73A1-4DE9-8E94-161111266D38")]
	sealed class General : ConfigPage
	{
		protected override Features Feature => Features.None;
		protected override IWin32Window Window => Control ?? (Control = new GeneralPage(this));
	}

	[Browsable(false)]
	[Guid("6B92F305-BEAD-49E3-9277-28E1829D7B57")]
	sealed class CSharpSuperQuickInfo : ConfigPage
	{
		protected override Features Feature => Features.SuperQuickInfo;
		protected override IWin32Window Window => Control ?? (Control = new CSharpSuperQuickInfoPage(this));
	}

	[Browsable(false)]
	[Guid("EE62CE13-5B5B-4EA0-A3FE-4D1F68FD2685")]
	sealed class SuperQuickInfo : ConfigPage
	{
		protected override Features Feature => Features.SuperQuickInfo;
		protected override IWin32Window Window => Control ?? (Control = new SuperQuickInfoPage(this));
	}

	[Browsable(false)]
	[Guid("23785D62-BD49-448B-A943-839DA27E8197")]
	sealed class ScrollbarMarker : ConfigPage
	{
		protected override Features Feature => Features.ScrollbarMarkers;
		protected override IWin32Window Window => Control ?? (Control = new ScrollbarMarkerPage(this));
	}

	[Browsable(false)]
	[Guid("E676D973-8A9A-461A-9085-DF69A54743A5")]
	sealed class CSharpScrollbarMarker : ConfigPage
	{
		protected override Features Feature => Features.ScrollbarMarkers;
		protected override IWin32Window Window => Control ?? (Control = new CSharpScrollbarMarkerPage(this));
	}
}
