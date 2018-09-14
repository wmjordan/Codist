using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;
using AppHelpers;

namespace Codist.QuickInfo
{
	/// <summary>
	/// Provides quick info for named colors or #hex colors
	/// </summary>
	[Export(typeof(IQuickInfoSourceProvider))]
	[Name("Color Quick Info Controller")]
	[Order(After = "Default Quick Info Presenter")]
	[ContentType(Constants.CodeTypes.Text)]
	sealed class ColorQuickInfoControllerProvider : IQuickInfoSourceProvider
	{
		[Import]
		internal ITextStructureNavigatorSelectorService _NavigatorService = null;

		public IQuickInfoSource TryCreateQuickInfoSource(ITextBuffer textBuffer) {
			return new ColorQuickInfoController(_NavigatorService, textBuffer.ContentType.IsOfType(Constants.CodeTypes.CSharp));
		}

		sealed class ColorQuickInfoController : IQuickInfoSource
		{
			readonly ITextStructureNavigatorSelectorService _NavigatorService;
			readonly bool _IsCSharp;

			public ColorQuickInfoController(ITextStructureNavigatorSelectorService navigatorService, bool isCSharp) {
				_NavigatorService = navigatorService;
				_IsCSharp = isCSharp;
			}

			public void AugmentQuickInfoSession(IQuickInfoSession session, IList<Object> qiContent, out ITrackingSpan applicableToSpan) {
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Color) == false) {
					goto EXIT;
				}
				var buffer = session.TextView.TextBuffer;
				var snapshot = session.TextView.TextSnapshot;
				var navigator = _NavigatorService.GetTextStructureNavigator(buffer);
				var extent = navigator.GetExtentOfWord(session.GetTriggerPoint(snapshot).GetValueOrDefault()).Span;
				var word = snapshot.GetText(extent);
				var brush = UIHelper.GetBrush(word, _IsCSharp);
				if (brush == null) {
					if ((extent.Length == 6 || extent.Length == 8) && extent.Span.Start > 0 && Char.IsPunctuation(snapshot.GetText(extent.Span.Start - 1, 1)[0])) {
						word = "#" + word;
					}
					brush = UIHelper.GetBrush(word, _IsCSharp);
				}
				if (brush != null) {
					qiContent.Add(GetColorInfo(brush));
					applicableToSpan = snapshot.CreateTrackingSpan(extent.Span, SpanTrackingMode.EdgeExclusive);
					return;
				}
				EXIT:
				applicableToSpan = null;
			}

			static StackPanel GetColorInfo(SolidColorBrush brush) {
				const string SAMPLE = "Hi,WM.";
				var c = brush.Color;
				var v = Microsoft.VisualStudio.Imaging.HslColor.FromColor(c);
				return new StackPanel {
					Children = {
						new ToolTipText().Append(new System.Windows.Shapes.Rectangle { Width = 16, Height = 16, Fill = brush }).Append("Color", true),
						new StackPanel().AddReadOnlyTextBox($"{c.A}, {c.R}, {c.G}, {c.B}").Add(new ToolTipText(" ARGB", true)).MakeHorizontal(),
						new StackPanel().AddReadOnlyTextBox(c.ToHexString()).Add(new ToolTipText(" HEX", true)).MakeHorizontal(),
						new StackPanel().AddReadOnlyTextBox($"{v.Hue.ToString("0.###")}, {v.Saturation.ToString("0.###")}, {v.Luminosity.ToString("0.###")}").Add(new ToolTipText(" HSL", true)).MakeHorizontal(),
						new StackPanel {
							Children = {
									new TextBox { Text = SAMPLE, BorderBrush = Brushes.Black, Foreground = brush, Background = Brushes.Black },
									new TextBox { Text = SAMPLE, BorderBrush = Brushes.Black, Background = brush, Foreground = Brushes.Black },
									new TextBox { Text = SAMPLE, BorderBrush = Brushes.Black, Foreground = brush, Background = Brushes.Gray },
									new TextBox { Text = SAMPLE, BorderBrush = Brushes.Black, Background = brush, Foreground = Brushes.White },
									new TextBox { Text = SAMPLE, BorderBrush = Brushes.Black, Foreground = brush, Background = Brushes.White },
							}
						}.MakeHorizontal(),
					}
				};
			}

			void IDisposable.Dispose() {}
		}
	}

}
