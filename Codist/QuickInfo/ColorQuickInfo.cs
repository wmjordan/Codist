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
using Microsoft.VisualStudio.Shell;

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
			return new ColorQuickInfoController(_NavigatorService);
		}

		sealed class ColorQuickInfoController : IQuickInfoSource
		{
			readonly ITextStructureNavigatorSelectorService _NavigatorService;

			public ColorQuickInfoController(ITextStructureNavigatorSelectorService navigatorService) {
				_NavigatorService = navigatorService;
			}

			public void AugmentQuickInfoSession(IQuickInfoSession session, IList<Object> qiContent, out ITrackingSpan applicableToSpan) {
				if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.Color) == false) {
					goto EXIT;
				}
				var buffer = session.TextView.TextBuffer;
				var navigator = _NavigatorService.GetTextStructureNavigator(buffer);
				var extent = navigator.GetExtentOfWord(session.GetTriggerPoint(session.TextView.TextSnapshot).GetValueOrDefault()).Span;
				var word = session.TextView.TextSnapshot.GetText(extent);
				var brush = NamedColorCache.TryGetBrush(word);
				if (brush != null) {
					qiContent.Add(GetColorInfo(brush));
					applicableToSpan = session.TextView.TextSnapshot.CreateTrackingSpan(extent.Span, SpanTrackingMode.EdgeExclusive);
					return;
				}
				EXIT:
				applicableToSpan = null;
			}

			static StackPanel GetColorInfo(SolidColorBrush brush) {
				const string SAMPLE = "Hi,WM.";
				var c = brush.Color;
				return new StackPanel {
					Children = {
						new StackPanel() {
							Children = {
								new ToolTipText().Append("Color", true, false, brush).Append(" (ARGB: "),
								new TextBox() { Text = $"{c.A},{c.R},{c.G},{c.B}", IsReadOnly = true }.SetStyleResourceProperty(VsResourceKeys.TextBoxStyleKey),
								new ToolTipText() { Text = ", HEX: " },
								new TextBox { Text = c.ToHexString(), IsReadOnly = true }.SetStyleResourceProperty(VsResourceKeys.TextBoxStyleKey),
								new ToolTipText { Text = ")" }
							}
						}.MakeHorizontal(),
						new StackPanel {
							Children = {
									new TextBox { Text = SAMPLE, BorderBrush = Brushes.Black, Foreground = brush, Background = Brushes.Black, IsReadOnly = true },
									new TextBox { Text = SAMPLE, BorderBrush = Brushes.Black, Background = brush, Foreground = Brushes.Black, IsReadOnly = true },
									new TextBox { Text = SAMPLE, BorderBrush = Brushes.Black, Foreground = brush, Background = Brushes.Gray, IsReadOnly = true },
									new TextBox { Text = SAMPLE, BorderBrush = Brushes.Black, Background = brush, Foreground = Brushes.White, IsReadOnly = true },
									new TextBox { Text = SAMPLE, BorderBrush = Brushes.Black, Foreground = brush, Background = Brushes.White, IsReadOnly = true },
							}
						}.MakeHorizontal()
					}
				};
			}

			void IDisposable.Dispose() {}

			static class NamedColorCache
			{
				static readonly Dictionary<string, SolidColorBrush> __Cache = GetBrushes();
				internal static SolidColorBrush TryGetBrush (string name) {
					var c = WpfHelper.ParseColor(name);
					return 
						c != Colors.Transparent ? new SolidColorBrush(c)
						: (name.Length >= 3 && name.Length <= 20) ? __Cache.TryGetValue(name, out var brush) ? brush : null
						: null;
				}
				static Dictionary<string, SolidColorBrush> GetBrushes() {
					var c = Array.FindAll(typeof(Brushes).GetProperties(), p => p.PropertyType == typeof(SolidColorBrush));
					var d = new Dictionary<string, SolidColorBrush>(c.Length, StringComparer.OrdinalIgnoreCase);
					foreach (var item in c) {
						d.Add(item.Name, item.GetValue(null) as SolidColorBrush);
					}
					return d;
				}
			}
		}
	}

}
