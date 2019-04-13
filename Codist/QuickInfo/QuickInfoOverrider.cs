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
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Language.Intellisense;

namespace Codist.QuickInfo
{
	interface IQuickInfoOverrider
	{
		void SetDiagnostics(IList<Diagnostic> diagnostics);
		void ApplyClickAndGo(ISymbol symbol);
		void LimitQuickInfoItemSize(IList<object> qiContent);
		void OverrideDocumentation(UIElement docElement);
		void OverrideException(UIElement exceptionDoc);
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
		/// <remarks>
		/// <para>The original implementation of QuickInfo locates at: Common7\IDE\CommonExtensions\Microsoft\Editor\Microsoft.VisualStudio.Platform.VSEditor.dll</para>
		/// <para>class: Microsoft.VisualStudio.Text.AdornmentLibrary.ToolTip.Implementation.WpfToolTipControl</para>
		/// </remarks>
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
			public void OverrideException(UIElement exceptionDoc) {
				_Overrider.ExceptionDoc = exceptionDoc;
			}

			public void SetDiagnostics(IList<Diagnostic> diagnostics) {
				_Overrider.Diagnostics = diagnostics;
			}

			sealed class Overrider : UIElement
			{
				static readonly Thickness __DocPanelBorderMargin = new Thickness(0, 0, -9, 3);
				static readonly Thickness __DocPanelBorderPadding = new Thickness(0, 0, 9, 0);
				static readonly Thickness __TitlePanelMargin = new Thickness(0, 0, 30, 6);

				public ISymbol ClickAndGoSymbol;
				public bool LimitItemSize;
				public UIElement DocElement;
				public UIElement ExceptionDoc;
				public IList<Diagnostic> Diagnostics;

				protected override void OnVisualParentChanged(DependencyObject oldParent) {
					base.OnVisualParentChanged(oldParent);
					var p = this.GetParent<StackPanel>();
					if (p == null) {
						goto EXIT;
					}
					if ((Config.Instance.DisplayOptimizations & DisplayOptimizations.CodeWindow) != 0) {
						WpfHelper.SetUITextRenderOptions(p, true);
					}
					if (p.Children.Count > 1) {
						OverrideDiagnosticInfo(p);
						p.SetValue(TextBlock.FontFamilyProperty, ThemeHelper.ToolTipFont);
						p.SetValue(TextBlock.FontSizeProperty, ThemeHelper.ToolTipFontSize);
					}
					if (DocElement != null || ExceptionDoc != null || ClickAndGoSymbol != null || LimitItemSize) {
						FixQuickInfo(p);
					}
					if (LimitItemSize) {
						ApplySizeLimit(this.GetParent<StackPanel>());
					}
					EXIT:
					// hides the parent container from taking excessive space in the quick info window
					this.GetParent<Border>().Collapse();
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
						TextEditorWrapper.CreateFor(cp);
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
					var titlePanel = infoPanel.GetFirstVisualChild<WrapPanel>();
					if (titlePanel == null) {
						return;
					}
					var doc = titlePanel.GetParent<StackPanel>();
					if (doc == null) {
						return;
					}
					var v16_1orLater = infoPanel.GetFirstVisualChild<StackPanel>() != doc;

					titlePanel.HorizontalAlignment = HorizontalAlignment.Stretch;
					doc.HorizontalAlignment = HorizontalAlignment.Stretch;

					var icon = infoPanel.GetFirstVisualChild<CrispImage>();
					var signature = infoPanel.GetFirstVisualChild<TextBlock>();

					// beautify the title panel
					if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.AlternativeStyle)) {
						var bgIcon = CreateEnlargedIcon(icon);
						infoPanel.GetParent<Border>().Background = new VisualBrush(bgIcon) {
							Opacity = 0.3,
							AlignmentX = AlignmentX.Right,
							AlignmentY = AlignmentY.Bottom,
							TileMode = TileMode.None,
							Stretch = Stretch.None
						};
						icon.Visibility = Visibility.Collapsed;
						var c = infoPanel.GetParent<Border>();
						c.Margin = __DocPanelBorderMargin;
						c.Padding = __DocPanelBorderPadding;
						c.MinHeight = 50;
						titlePanel.Margin = __TitlePanelMargin;
					}

					// replace the default XML doc
					System.Collections.IList items = doc.Children;
					if (DocElement != null) {
						try {
							// sequence of items in default XML Doc panel:
							// 1. summary
							// 2. type parameter
							// 3. usage
							// 4. exception
							if (items.Count > 1 && items[1] is TextBlock) {
								items.RemoveAt(1);
								items.Insert(1, DocElement);
							}
							else {
								items.Add(DocElement);
							}
						}
						catch (InvalidOperationException) {
							// ignore exception: doc.Children was changed by another thread
						}
					}
					if (ExceptionDoc != null) {
						if (v16_1orLater) {
							items = doc.GetParent<ItemsControl>().Items;
						}
						try {
							if (items.Count > 1
								&& (v16_1orLater && items[items.Count - 1] is StackPanel || items[items.Count - 1] is TextBlock)) {
								items.RemoveAt(items.Count - 1);
							}
							items.Add(ExceptionDoc);
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
							signature.MaxWidth = Config.Instance.QuickInfoMaxWidth - icon.Width - 30;
						}
					}
				}

				private static CrispImage CreateEnlargedIcon(CrispImage icon) {
					var bgIcon = new CrispImage { Moniker = icon.Moniker, Width = 48, Height = 48 };
					bgIcon.SetBackgroundForCrispImage(ThemeHelper.TitleBackgroundColor);
					return bgIcon;
				}

				static void ApplySizeLimit(StackPanel quickInfoPanel) {
					if (quickInfoPanel == null) {
						return;
					}
					var docPanel = quickInfoPanel.Children[0].GetFirstVisualChild<WrapPanel>().GetParent<StackPanel>();
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
							continue;
						}
						(c as ThemedTipDocument)?.ApplySizeLimit();
						if (c is ScrollViewer
							|| c is Microsoft.VisualStudio.Text.Editor.IWpfTextView // skip snippet tooltip
							) {
							continue;
						}
						o = c as DependencyObject;
						if (o == null) {
							var s = c as string;
							if (s != null) {
								cp.Content = new ThemedTipText {
									Text = s
								}.Scrollable();
							}
							continue;
						}
						cp.Content = null;
						cp.Content = o.Scrollable();
					}
					if (docPanel != null) {
						foreach (var r in docPanel.Children) {
							(r as ThemedTipDocument)?.ApplySizeLimit();
						}
						var cp = docPanel.GetParent<ContentPresenter>();
						if (cp != null && cp.Content == docPanel) {
							cp.Content = null;
							cp.Content = docPanel.Scrollable();
						}
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
			public void OverrideException(UIElement exceptionDoc) { }

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
					var p = _QuickInfoPanel.GetParent() as ContentPresenter;
					if (p != null) {
						p.Content = null;
						p.Content = _QuickInfoPanel.Scrollable().LimitSize();
					}
					// hides the parent container from taking excessive space in the quick info window
					this.GetParent<Border>().Collapse();
				}
			}

		}

	}
}
