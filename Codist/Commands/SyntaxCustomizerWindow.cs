namespace Codist.Commands
{
	using System;
	using System.Runtime.InteropServices;
	using Microsoft.VisualStudio.Shell;

	/// <summary>
	/// This class implements the tool window exposed by this package and hosts a user control.
	/// </summary>
	/// <remarks>
	/// In Visual Studio tool windows are composed of a frame (implemented by the shell) and a pane,
	/// usually implemented by the package implementer.
	/// <para>
	/// This class derives from the ToolWindowPane class provided from the MPF in order to use its
	/// implementation of the IVsUIElementPane interface.
	/// </para>
	/// </remarks>
	[Guid("53ee84b1-bf9c-438a-8a4f-8c441d6ba03e")]
	public class SyntaxCustomizerWindow : ToolWindowPane
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="SyntaxCustomizerWindow"/> class.
		/// </summary>
		public SyntaxCustomizerWindow() : base(null) {
			Caption = "Codist Syntax Highlight Customizer";

			// This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
			// we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on
			// the object returned by the Content property.
			Content = new SyntaxCustomizerWindowControl();
		}

		public override void OnToolWindowCreated() {
			base.OnToolWindowCreated();
			AddInfoBar(new InfoBarModel(
				new[] {
					new InfoBarTextSpan("Click on any content in the code document window, the syntax classifcations for the clicked content will be listed here. Change the style settings for corresponding syntax definitions below and your changes will be saved automatically.")
				})
				);
		}
	}
}
