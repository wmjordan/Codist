using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AppHelpers;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Imaging;

namespace Codist.QuickInfo
{
	interface IQuickInfoOverrider
	{
		void SetDiagnostics(IList<Diagnostic> diagnostics);
		void ApplyClickAndGo(ISymbol symbol);
		void LimitQuickInfoItemSize(IList<object> qiContent);
		void OverrideDocumentation(UIElement docElement);
	}

	static class QuickInfoOverrider
	{
		static readonly SolidColorBrush __HighlightBrush = SystemColors.HighlightBrush.Alpha(0.3);
		public static IQuickInfoOverrider CreateOverrider(IList<object> qiContent) {
			var o = new Legacy(qiContent);
			return o.Panel != null ? o : (IQuickInfoOverrider)new Default(qiContent);
		}

		static void ApplyClickAndGo(ISymbol symbol, TextBlock description) {
			var locs = symbol.GetSourceLocations();
			string path;
			description.ToolTip = String.Empty;
			if (locs.IsDefaultOrEmpty || String.IsNullOrEmpty(locs[0].SourceTree.FilePath)) {
				if (symbol.ContainingType != null) {
					// if the symbol is implicitly declared but its containing type is in source,
					// navigate to the containing type
					locs = symbol.ContainingType.GetSourceLocations();
					if (locs.Length != 0) {
						symbol = symbol.ContainingType;
						goto ClickAndGo;
					}
				}
				var asm = symbol.GetAssemblyModuleName();
				if (asm != null) {
					path = asm;
					description.ToolTipOpening += DescriptionShowToolTip;
				}
				return;
			}
			ClickAndGo:
			path = System.IO.Path.GetFileName(locs[0].SourceTree.FilePath);
			description.ToolTipOpening += DescriptionShowToolTip;
			description.Cursor = Cursors.Hand;
			description.MouseEnter += (s, args) => (s as TextBlock).Background = __HighlightBrush;
			description.MouseLeave += (s, args) => (s as TextBlock).Background = Brushes.Transparent;
			if (locs.Length == 1) {
				description.MouseLeftButtonUp += (s, args) => symbol.GoToSource();
				return;
			}
			description.MouseLeftButtonUp += (s, args) => {
				var tb = s as TextBlock;
				if (tb.ContextMenu == null) {
					tb.ContextMenu = WpfHelper.CreateContextMenuForSourceLocations(symbol.MetadataName, locs);
				}
				tb.ContextMenu.IsOpen = true;
			};
			void DescriptionShowToolTip(object sender, ToolTipEventArgs e) {
				var d = sender as TextBlock;
				d.ToolTip = ShowSymbolLocation(symbol, path);
				d.ToolTipOpening -= DescriptionShowToolTip;
			}
		}

		static StackPanel ShowSymbolLocation(ISymbol symbol, string path) {
			var tooltip = new ThemedToolTip();
			tooltip.Title.Append(symbol.Name, true);
			var t = tooltip.Content
				.Append("defined in ")
				.Append(String.IsNullOrEmpty(path) ? "?" : path, true);
			if (symbol.IsMemberOrType() && symbol.ContainingNamespace != null) {
				t.Append("\nnamespace: ").Append(symbol.ContainingNamespace.ToDisplayString());
			}
			if (symbol.Kind == SymbolKind.Method) {
				var m = (symbol as IMethodSymbol).ReducedFrom;
				if (m != null) {
					t.Append("\nclass: ").Append(m.ContainingType.Name);
				}
			}
			return tooltip;
		}

		/// <summary>
		/// The overrider for VS 15.8 and above versions.
		/// </summary>
		sealed class Default : IQuickInfoOverrider
		{
			readonly Overrider _Overrider;

			public Default(IList<object> qiContent) {
				_Overrider = new Overrider();
			}

			public void ApplyClickAndGo(ISymbol symbol) {
				_Overrider.ClickAndGoSymbol = symbol;
			}

			public void LimitQuickInfoItemSize(IList<object> qiContent) {
				if (Config.Instance.QuickInfoMaxHeight > 0 && Config.Instance.QuickInfoMaxWidth > 0 || qiContent.Count > 0) {
					_Overrider.LimitItemSize = true;
				}
				if (qiContent.Count > 0) {
					qiContent.Add(_Overrider);
				}
			}

			public void OverrideDocumentation(UIElement docElement) {
				_Overrider.DocElement = docElement;
			}

			public void SetDiagnostics(IList<Diagnostic> diagnostics) {
				_Overrider.Diagnostics = diagnostics;
			}

			sealed class Overrider : UIElement
			{
				static readonly Thickness __DocPanelBorderMargin = new Thickness(-15, 0, -17, 0);
				static readonly Thickness __TitlePanelMargin = new Thickness(-14, 0, -14, 3);
				static readonly Thickness __DocMargin = new Thickness(14, 0, 14, 0);
				static readonly Thickness __IconMargin = new Thickness(5, 0, 5, 0);
				static readonly Thickness __SignatureMargin = new Thickness(0, 0, 14, 0);

				public ISymbol ClickAndGoSymbol;
				public bool LimitItemSize;
				public UIElement DocElement;
				public IList<Diagnostic> Diagnostics;

				protected override void OnVisualParentChanged(DependencyObject oldParent) {
					base.OnVisualParentChanged(oldParent);
					var p = this.GetVisualParent<StackPanel>();
					if (p == null) {
						goto EXIT;
					}
					WpfHelper.SetUITextRenderOptions(p);
					if (p.Children.Count > 1) {
						OverrideDiagnosticInfo(p);
						p.SetValue(TextBlock.FontFamilyProperty, ThemeHelper.ToolTipFont);
						p.SetValue(TextBlock.FontSizeProperty, ThemeHelper.ToolTipFontSize);
					}
					if (DocElement != null || ClickAndGoSymbol != null || LimitItemSize) {
						FixQuickInfo(p);
					}
					if (LimitItemSize) {
						ApplySizeLimit(this.GetVisualParent<StackPanel>());
					}
					EXIT:
					// hides the parent container from taking excessive space in the quick info window
					var c = this.GetVisualParent<Border>();
					if (c != null) {
						c.Visibility = Visibility.Collapsed;
					}
				}

				void OverrideDiagnosticInfo(StackPanel panel) {
					var infoPanel = panel.Children[1].GetFirstVisualChild<ItemsControl>()?.GetFirstVisualChild<StackPanel>();
					if (infoPanel == null) {
						// try the first item (symbol title may be absent)
						infoPanel = panel.Children[0].GetFirstVisualChild<ItemsControl>()?.GetFirstVisualChild<StackPanel>();
						if (infoPanel?.GetFirstVisualChild<WrapPanel>() != null) {
							return;
						}
					}
					if (infoPanel == null) {
						return;
					}
					foreach (var item in infoPanel.Children) {
						var cp = (item as UIElement).GetFirstVisualChild<TextBlock>();
						if (cp == null) {
							continue;
						}
						if (Diagnostics != null && Diagnostics.Count > 0) {
							var t = cp.GetText();
							var d = Diagnostics.FirstOrDefault(i => i.GetMessage() == t);
							if (d != null) {
								cp.ToolTip = String.Empty;
								cp.Tag = d;
								cp.SetGlyph(ThemeHelper.GetImage(GetGlyphForSeverity(d.Severity)));
								cp.ToolTipOpening += ShowToolTipForDiagnostics;
							}
						}
						else {
							cp.SetGlyph(ThemeHelper.GetImage(KnownImageIds.StatusInformation));
						}
					}

					int GetGlyphForSeverity(DiagnosticSeverity severity) {
						switch (severity) {
							case DiagnosticSeverity.Warning: return KnownImageIds.StatusWarning;
							case DiagnosticSeverity.Error: return KnownImageIds.StatusError;
							case DiagnosticSeverity.Hidden: return KnownImageIds.StatusHidden;
							default: return KnownImageIds.StatusInformation;
						}
					}

					void ShowToolTipForDiagnostics(object sender, ToolTipEventArgs e) {
						var t = sender as TextBlock;
						var d = t.Tag as Diagnostic;
						var tip = new ThemedToolTip();
						tip.Title.Append(d.Descriptor.Category + " (" + d.Id + ")", true);
						tip.Content.Append(d.Descriptor.Title.ToString());
						if (String.IsNullOrEmpty(d.Descriptor.HelpLinkUri) == false) {
							tip.Content.AppendLine().Append("Help: " + d.Descriptor.HelpLinkUri);
						}
						if (d.IsSuppressed) {
							tip.Content.AppendLine().Append("Suppressed");
						}
						if (d.IsWarningAsError) {
							tip.Content.AppendLine().Append("Content as error");
						}
						t.ToolTip = tip;
						t.ToolTipOpening -= ShowToolTipForDiagnostics;
					}
				}

				void FixQuickInfo(StackPanel infoPanel) {
					var doc = infoPanel.GetFirstVisualChild<StackPanel>();
					if (doc == null) {
						return;
					}
					var titlePanel = infoPanel.GetFirstVisualChild<WrapPanel>();
					if (titlePanel == null) {
						return;
					}
					titlePanel.HorizontalAlignment = HorizontalAlignment.Stretch;
					doc.HorizontalAlignment = HorizontalAlignment.Stretch;

					var icon = infoPanel.GetFirstVisualChild<Microsoft.VisualStudio.Imaging.CrispImage>();
					var signature = infoPanel.GetFirstVisualChild<TextBlock>();

					// beautify the title panel
					if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.AlternativeStyle)) {
						var b = doc.GetVisualParent<Border>();
						if (b != null && icon != null && signature != null) {
							b.Margin = __DocPanelBorderMargin;
							titlePanel.Margin = __TitlePanelMargin;
							titlePanel.Background = ThemeHelper.ToolWindowBackgroundBrush.Alpha(0.5);
							doc.Margin = __DocMargin;
							icon.Margin = __IconMargin;
							signature.Margin = __SignatureMargin;
						}
					}

					// replace the default XML doc
					if (DocElement != null) {
						try {
							if (doc.Children.Count > 1 && doc.Children[1] is TextBlock) {
								doc.Children.RemoveAt(1);
								doc.Children.Insert(1, DocElement);
							}
							else {
								doc.Children.Add(DocElement);
							}
						}
						catch (InvalidOperationException) {
							// ignore exception: doc.Children was changed by another thread
						}
					}


					if (icon != null && signature != null) {
						// apply click and go feature
						if (ClickAndGoSymbol != null) {
							QuickInfoOverrider.ApplyClickAndGo(ClickAndGoSymbol, signature);
						}
						// fix the width of the signature part to prevent it from falling down to the next row
						if (Config.Instance.QuickInfoMaxWidth > 0) {
							//wrapPanel.MaxWidth = Config.Instance.QuickInfoMaxWidth;
							//signature.HorizontalAlignment = HorizontalAlignment.Left;
							signature.MaxWidth = Config.Instance.QuickInfoMaxWidth - icon.Width - 40;
						}
					}
				}

				static void ApplySizeLimit(StackPanel quickInfoPanel) {
					if (quickInfoPanel == null) {
						return;
					}
					var docPanel = quickInfoPanel.Children[0].GetFirstVisualChild<StackPanel>();
					if (docPanel?.Margin != __DocMargin) {
						docPanel = null;
					}
					foreach (var item in quickInfoPanel.Children) {
						var o = item as DependencyObject;
						if (o == null) {
							continue;
						}
						var cp = o.GetFirstVisualChild<ContentPresenter>();
						if (cp == null) {
							continue;
						}
						var c = cp.Content;
						if (c is Overrider
							|| (c is IInteractiveQuickInfoContent && c.GetType().Name == "LightBulbQuickInfoPlaceHolder")) {
							continue;
						}
						cp.LimitSize();
						if (docPanel == c) {
							cp.MaxWidth += 32;
						}
						if (c is ScrollViewer) {
							continue;
						}
						o = c as DependencyObject;
						if (o == null) {
							var s = c as string;
							if (s != null) {
								cp.Content = new ThemedText {
									Text = s
								}.Scrollable();
							}
							continue;
						}
						cp.Content = null;
						cp.Content = o.Scrollable();
					}
				}
			}
		}

		/// <summary>
		/// This class works for versions earlier than Visual Studio 15.8 only.
		/// From version 15.8 on, VS no longer creates WPF elements for the Quick Info immediately,
		/// thus, we can't hack into the qiContent for the Quick Info panel.
		/// </summary>
		sealed class Legacy : IQuickInfoOverrider
		{
			static Func<StackPanel, TextBlock> __GetMainDescription;
			static Func<StackPanel, TextBlock> __GetDocumentation;

			public Legacy(IList<object> qiContent) {
				Panel = FindDefaultQuickInfoPanel(qiContent);
				if (__GetMainDescription == null && Panel != null) {
					var t = Panel.GetType();
					__GetMainDescription = t.CreateGetPropertyMethod<StackPanel, TextBlock>("MainDescription");
					__GetDocumentation = t.CreateGetPropertyMethod<StackPanel, TextBlock>("Documentation");
				}
			}
			public StackPanel Panel { get; }
			public TextBlock MainDesciption => Panel != null ? __GetMainDescription(Panel) : null;
			public TextBlock Documentation => Panel != null ? __GetDocumentation(Panel) : null;

			/// <summary>Hack into the default QuickInfo panel and provides click and go feature for symbols.</summary>
			public void ApplyClickAndGo(ISymbol symbol) {
				if (symbol == null) {
					return;
				}
				var description = MainDesciption;
				if (description == null) {
					return;
				}
				if (symbol.IsImplicitlyDeclared) {
					symbol = symbol.ContainingType;
				}
				QuickInfoOverrider.ApplyClickAndGo(symbol, description);
			}

			/// <summary>
			/// Limits the displaying size of the quick info items.
			/// </summary>
			public void LimitQuickInfoItemSize(IList<object> qiContent) {
				if (Config.Instance.QuickInfoMaxHeight <= 0 && Config.Instance.QuickInfoMaxWidth <= 0 || qiContent.Count == 0) {
					return;
				}
				for (int i = 0; i < qiContent.Count; i++) {
					var item = qiContent[i];
					var p = item as Panel;
					// finds out the default quick info panel
					if (p != null && p == Panel || i == 0) {
						// adds a dummy control to hack into the default quick info panel
						qiContent.Add(new QuickInfoSizer(p));
						continue;
					}
					var s = item as string;
					if (s != null) {
						qiContent[i] = new TextBlock { Text = s, TextWrapping = TextWrapping.Wrap }.Scrollable().LimitSize();
						continue;
					}
					// todo: make other elements scrollable
					if ((item as FrameworkElement).LimitSize() == null) {
						continue;
					}
				}
			}

			/// <summary>overrides default doc summary</summary>
			/// <param name="newDoc">The overriding doc element.</param>
			public void OverrideDocumentation(UIElement newDoc) {
				var doc = Documentation;
				if (doc != null) {
					doc.Visibility = Visibility.Collapsed;
					Panel.Children.Insert(Panel.Children.IndexOf(doc), newDoc);
				}
			}

			public void SetDiagnostics(IList<Diagnostic> diagnostics) {
				// not implemented for versions before 15.8
			}

			static StackPanel FindDefaultQuickInfoPanel(IList<object> qiContent) {
				foreach (var item in qiContent) {
					var o = item as StackPanel;
					if (o != null && o.GetType().Name == "QuickInfoDisplayPanel") {
						return o;
					}
				}
				return null;
			}

			sealed class QuickInfoSizer : UIElement
			{
				readonly Panel _QuickInfoPanel;

				public QuickInfoSizer(Panel quickInfoPanel) {
					_QuickInfoPanel = quickInfoPanel;
				}
				protected override void OnVisualParentChanged(DependencyObject oldParent) {
					base.OnVisualParentChanged(oldParent);
					if (_QuickInfoPanel == null) {
						return;
					}
					// makes the default quick info panel scrollable and size limited
					var p = _QuickInfoPanel.GetVisualParent() as ContentPresenter;
					if (p != null) {
						p.Content = null;
						p.Content = _QuickInfoPanel.Scrollable().LimitSize();
					}
					// hides the parent container from taking excessive space in the quick info window
					var c = this.GetVisualParent<Border>();
					if (c != null) {
						c.Visibility = Visibility.Collapsed;
					}
				}
			}

		}

	}
}
