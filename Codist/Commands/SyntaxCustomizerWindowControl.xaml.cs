namespace Codist.Commands
{
	using System.Diagnostics.CodeAnalysis;
	using System.Windows;
	using System.Windows.Controls;
	using Microsoft.VisualStudio.PlatformUI;
	using Microsoft.VisualStudio.Shell;

	/// <summary>
	/// Interaction logic for SyntaxCustomizerWindowControl.
	/// </summary>
	public partial class SyntaxCustomizerWindowControl : UserControl
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="SyntaxCustomizerWindowControl"/> class.
		/// </summary>
		public SyntaxCustomizerWindowControl() {
			InitializeComponent();
			SetResourceReference(BackgroundProperty, EnvironmentColors.ToolboxBackgroundBrushKey);
			SetResourceReference(ForegroundProperty, EnvironmentColors.ToolboxContentTextBrushKey);
			button1.ReferenceStyle(VsResourceKeys.ButtonStyleKey);
		}

		/// <summary>
		/// Handles click on the button by displaying a message box.
		/// </summary>
		/// <param name="sender">The event sender.</param>
		/// <param name="e">The event args.</param>
		[SuppressMessage("Microsoft.Globalization", "CA1300:SpecifyMessageBoxOptions", Justification = "Sample code")]
		[SuppressMessage("StyleCop.CSharp.NamingRules", "SA1300:ElementMustBeginWithUpperCaseLetter", Justification = "Default event handler naming pattern")]
		private void button1_Click(object sender, RoutedEventArgs e) {
			MessageBox.Show(
				string.Format(System.Globalization.CultureInfo.CurrentUICulture, "Invoked '{0}'", this.ToString()),
				"SyntaxCustomizerWindow");
		}
	}
}