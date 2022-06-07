using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using AppHelpers;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using R = Codist.Properties.Resources;
using TH = Microsoft.VisualStudio.Shell.ThreadHelper;

namespace Codist.QuickInfo
{
	interface IQuickInfoOverrider
	{
		UIElement Control { get; }
		bool OverrideBuiltInXmlDoc { get; set; }
		void SetDiagnostics(IList<Diagnostic> diagnostics);
		void ApplyClickAndGo(ISymbol symbol, ITextBuffer textBuffer, IAsyncQuickInfoSession quickInfoSession);
		void OverrideDocumentation(UIElement docElement);
		void OverrideException(UIElement exceptionDoc);
		void OverrideAnonymousTypeInfo(UIElement anonymousTypeInfo);
	}

	static class QuickInfoOverrider
	{
		public static IQuickInfoOverrider CreateOverrider(IAsyncQuickInfoSession session) {
			return session.Properties.GetOrCreateSingletonProperty<DefaultOverrider>(() => new DefaultOverrider());
		}

		public static void HoldQuickInfo(DependencyObject quickInfoItem, bool hold) {
			FindHolder(quickInfoItem)?.Hold(hold);
		}

		public static void DismissQuickInfo(DependencyObject quickInfoItem) {
			FindHolder(quickInfoItem)?.DismissAsync();
		}

		static IQuickInfoHolder FindHolder(DependencyObject quickInfoItem) {
			var items = quickInfoItem.GetParent<ItemsControl>(i => i.GetType().Name == "WpfToolTipItemsControl");
			// version 16.1 or above
			items = items.GetParent<ItemsControl>(i => i.GetType().Name == "WpfToolTipItemsControl") ?? items;
			return items.GetFirstVisualChild<StackPanel>(o => o is IQuickInfoHolder) as IQuickInfoHolder;
		}

		static StackPanel ShowSymbolLocation(ISymbol symbol, string path) {
			var tooltip = new ThemedToolTip();
			tooltip.Title.Append(symbol.GetOriginalName(), true);
			var t = tooltip.Content;
			if (symbol.Kind != SymbolKind.Namespace) {
				var sr = symbol.GetSourceReferences();
				if (sr.Length > 0) {
					t.Append(R.T_DefinedIn);
					var s = false;
					foreach (var (name, ext) in sr.Select(r => DeconstructFileName(System.IO.Path.GetFileName(r.SyntaxTree.FilePath))).Distinct().OrderBy(i => i.name)) {
						if (s) {
							t.AppendLine();
						}
						t.Append(name, true);
						if (ext.Length > 0) {
							t.Append(ext);
						}
						s = true;
					}
				}
			}
			t.AppendLineBreak().Append(R.T_Assembly);
			if (symbol.Kind == SymbolKind.Namespace) {
				var s = false;
				foreach (var (name, ext) in ((INamespaceSymbol)symbol).ConstituentNamespaces.Select(n => n.ContainingModule?.Name).Where(m => m != null).Select(DeconstructFileName).Distinct().OrderBy(i => i.name)) {
					if (s) {
						t.AppendLine();
					}
					t.Append(name, true);
					if (ext.Length > 0) {
						t.Append(ext);
					}
					s = true;
				}
			}
			else {
				t.Append(symbol.ContainingModule?.Name);
			}
			if (symbol.IsMemberOrType() && symbol.ContainingNamespace != null) {
				t.AppendLineBreak().Append(R.T_Namespace).Append(symbol.ContainingNamespace.ToDisplayString());
			}
			if (symbol.Kind == SymbolKind.Method) {
				var m = ((IMethodSymbol)symbol).ReducedFrom;
				if (m != null) {
					t.AppendLineBreak().Append(R.T_Class).Append(m.ContainingType.Name);
				}
			}
			return tooltip;

			(string name, string ext) DeconstructFileName(string fileName) {
				int i = fileName.LastIndexOf('.');
				if (i >= 0) {
					return (fileName.Substring(0, i), fileName.Substring(i));
				}
				return (fileName, String.Empty);
			}
		}

		sealed class ClickAndGo
		{
			ISymbol _symbol;
			ITextBuffer _textBuffer;
			TextBlock _description;
			IAsyncQuickInfoSession _quickInfoSession;

			ClickAndGo(ISymbol symbol, ITextBuffer textBuffer, TextBlock description, IAsyncQuickInfoSession quickInfoSession) {
				_symbol = symbol;
				_textBuffer = textBuffer;
				_description = description;
				_quickInfoSession = quickInfoSession;

				if (symbol.Kind == SymbolKind.Namespace) {
					description.MouseEnter += HookEvents;
					return;
				}
				if (symbol.Kind == SymbolKind.Method) {
					if (((IMethodSymbol)symbol).MethodKind == MethodKind.LambdaMethod) {
						using (var sbr = Microsoft.VisualStudio.Utilities.ReusableStringBuilder.AcquireDefault(30)) {
							var sb = sbr.Resource;
							sb.Append('(');
							foreach (var item in ((IMethodSymbol)symbol).Parameters) {
								if (item.Ordinal > 0) {
									sb.Append(", ");
								}
								sb.Append(item.Type.ToDisplayString(CodeAnalysisHelper.QuickInfoSymbolDisplayFormat))
									.Append(item.Type.GetParameterString())
									.Append(' ')
									.Append(item.Name);
							}
							sb.Append(')');
							description.Append(sb.ToString(), ThemeHelper.DocumentTextBrush);
						}
					}
				}
				description.UseDummyToolTip();
				if (symbol.HasSource() == false && symbol.ContainingType?.HasSource() == true) {
					// if the symbol is implicitly declared but its containing type is in source,
					// navigate to the containing type
					symbol = symbol.ContainingType;
				}
				description.MouseEnter += HookEvents;
			}

			public static void Apply(ISymbol symbol, ITextBuffer textBuffer, TextBlock description, IAsyncQuickInfoSession quickInfoSession) {
				quickInfoSession.StateChanged += new ClickAndGo(symbol, textBuffer, description, quickInfoSession).QuickInfoSession_StateChanged;
			}

			void HookEvents(object sender, MouseEventArgs e) {
				var s = sender as FrameworkElement;
				s.MouseEnter -= HookEvents;
				if (CodistPackage.VsVersion.Major == 15 || Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.AlternativeStyle)) {
					HighlightSymbol(sender, e);
					s.Cursor = Cursors.Hand;
					s.ToolTipOpening += ShowToolTip;
					s.UseDummyToolTip();
					s.MouseEnter += HighlightSymbol;
					s.MouseLeave += RemoveSymbolHighlight;
					s.MouseLeftButtonUp += GoToSource;
				}
				s.ContextMenuOpening += ShowContextMenu;
				s.ContextMenuClosing += ReleaseQuickInfo;
			}

			void HighlightSymbol(object sender, EventArgs e) {
				((TextBlock)sender).Background = (_symbol.HasSource() ? SystemColors.HighlightBrush : SystemColors.GrayTextBrush).Alpha(0.3);
			}
			void RemoveSymbolHighlight(object sender, MouseEventArgs e) {
				((TextBlock)sender).Background = Brushes.Transparent;
			}

			async void GoToSource(object sender, MouseButtonEventArgs e) {
				var s = _symbol;
				await _quickInfoSession.DismissAsync();
				s.GoToDefinition();
			}
			void ShowToolTip(object sender, ToolTipEventArgs e) {
				var t = sender as TextBlock;
				t.ToolTip = ShowSymbolLocation(_symbol, _symbol.HasSource() ? System.IO.Path.GetFileName(_symbol.Locations[0].SourceTree.FilePath) : _symbol.GetAssemblyModuleName());
				t.ToolTipOpening -= ShowToolTip;
			}
			async void ShowContextMenu(object sender, ContextMenuEventArgs e) {
				await TH.JoinableTaskFactory.SwitchToMainThreadAsync(default);
				var s = sender as FrameworkElement;
				if (s.ContextMenu == null) {
					var v = _quickInfoSession.TextView;
					var ctx = SemanticContext.GetOrCreateSingetonInstance(v as IWpfTextView);
					await ctx.UpdateAsync(_textBuffer, default);
					await TH.JoinableTaskFactory.SwitchToMainThreadAsync(default);
					var m = new CSharpSymbolContextMenu(_symbol, v.TextBuffer.ContentType.TypeName == Constants.CodeTypes.InteractiveContent ? null : ctx.GetNode(_quickInfoSession.ApplicableToSpan.GetStartPoint(v.TextSnapshot).Position, true, true), ctx);
					m.AddAnalysisCommands();
					if (m.HasItems) {
						m.Items.Add(new Separator());
					}
					m.AddSymbolNodeCommands();
					m.AddTitleItem(_symbol.GetOriginalName());
					m.Closed += HideQuickInfo;
					m.DisposeOnClose();
					s.ContextMenu = m;
				}
				await TH.JoinableTaskFactory.SwitchToMainThreadAsync(default);
				HoldQuickInfo(s, true);
				s.ContextMenu.IsOpen = true;
			}

			void ReleaseQuickInfo(object sender, RoutedEventArgs e) {
				HoldQuickInfo(sender as DependencyObject, false);
			}
			void HideQuickInfo(object sender, RoutedEventArgs e) {
				_quickInfoSession?.DismissAsync();
			}

			void QuickInfoSession_StateChanged(object sender, QuickInfoSessionStateChangedEventArgs e) {
				if (e.NewState != QuickInfoSessionState.Dismissed) {
					return;
				}
				_quickInfoSession.StateChanged -= QuickInfoSession_StateChanged;
				var s = _description;
				s.MouseEnter -= HookEvents;
				s.ToolTipOpening -= ShowToolTip;
				s.MouseEnter -= HighlightSymbol;
				s.MouseLeave -= RemoveSymbolHighlight;
				s.MouseLeftButtonUp -= GoToSource;
				s.ContextMenuOpening -= ShowContextMenu;
				s.ContextMenuClosing -= ReleaseQuickInfo;
				if (s.ContextMenu is CSharpSymbolContextMenu m) {
					m.Closed -= HideQuickInfo;
				}
				s.ContextMenu = null;
				_symbol = null;
				_textBuffer = null;
				_description = null;
				_quickInfoSession = null;
			}
		}

		interface IQuickInfoHolder
		{
			void Hold(bool hold);
			System.Threading.Tasks.Task DismissAsync();
		}

		/// <summary>
		/// The overrider for VS 15.8 and above versions.
		/// </summary>
		/// <remarks>
		/// <para>The original implementation of QuickInfo locates at: Common7\IDE\CommonExtensions\Microsoft\Editor\Microsoft.VisualStudio.Platform.VSEditor.dll</para>
		/// <para>class: Microsoft.VisualStudio.Text.AdornmentLibrary.ToolTip.Implementation.WpfToolTipControl</para>
		/// </remarks>
		sealed class DefaultOverrider : IQuickInfoOverrider
		{
			readonly OverriderControl _Overrider;

			public DefaultOverrider() {
				_Overrider = new OverriderControl();
				if (Config.Instance.QuickInfoMaxHeight > 0 && Config.Instance.QuickInfoMaxWidth > 0) {
					_Overrider.LimitItemSize = true;
				}
			}

			public UIElement Control => _Overrider;
			public bool OverrideBuiltInXmlDoc {
				get => _Overrider.OverrideBuiltInXmlDoc;
				set => _Overrider.OverrideBuiltInXmlDoc = value;
			}

			public void ApplyClickAndGo(ISymbol symbol, ITextBuffer textBuffer, IAsyncQuickInfoSession quickInfoSession) {
				_Overrider.ClickAndGoSymbol = symbol;
				_Overrider.QuickInfoSession = quickInfoSession;
				_Overrider.TextBuffer = textBuffer;
				quickInfoSession.StateChanged -= _Overrider.ReleaseSession;
				quickInfoSession.StateChanged += _Overrider.ReleaseSession;
			}

			public void OverrideDocumentation(UIElement docElement) {
				_Overrider.DocElement = docElement;
			}
			public void OverrideException(UIElement exceptionDoc) {
				_Overrider.ExceptionDoc = exceptionDoc;
			}
			public void OverrideAnonymousTypeInfo(UIElement anonymousTypeInfo) {
				_Overrider.AnonymousTypeInfo = anonymousTypeInfo;
			}
			public void SetDiagnostics(IList<Diagnostic> diagnostics) {
				_Overrider.Diagnostics = diagnostics;
			}

			sealed class OverriderControl : StackPanel, IInteractiveQuickInfoContent, IQuickInfoHolder
			{
				static readonly Thickness __TitlePanelMargin = new Thickness(0, 0, 30, 6);

				public ISymbol ClickAndGoSymbol;
				public bool LimitItemSize, OverrideBuiltInXmlDoc;
				public UIElement DocElement;
				public UIElement ExceptionDoc;
				public UIElement AnonymousTypeInfo;
				public IList<Diagnostic> Diagnostics;
				public IAsyncQuickInfoSession QuickInfoSession;
				public ITextBuffer TextBuffer;

				public bool KeepQuickInfoOpen { get; set; }
				public bool IsMouseOverAggregated { get; set; }

				public void Hold(bool hold) {
					IsMouseOverAggregated = hold;
				}
				public System.Threading.Tasks.Task DismissAsync() {
					return QuickInfoSession?.DismissAsync();
				}

				protected override void OnVisualParentChanged(DependencyObject oldParent) {
					base.OnVisualParentChanged(oldParent);
					var p = this.GetParent<StackPanel>();
					if (p == null) {
						goto EXIT;
					}
					if (Config.Instance.DisplayOptimizations.MatchFlags(DisplayOptimizations.CodeWindow)) {
						WpfHelper.SetUITextRenderOptions(p, true);
					}
					if (p.Children.Count > 1) {
						OverrideDiagnosticInfo(p);
						p.SetValue(TextBlock.FontFamilyProperty, ThemeHelper.ToolTipFont);
						p.SetValue(TextBlock.FontSizeProperty, ThemeHelper.ToolTipFontSize);
					}
					if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.OverrideDefaultDocumentation) || ClickAndGoSymbol != null || LimitItemSize) {
						FixQuickInfo(p);
					}
					if (LimitItemSize) {
						ApplySizeLimit(this.GetParent<StackPanel>());
					}
					EXIT:
					// hides the parent container from taking excessive space in the quick info window
					this.GetParent<Border>().Collapse();
					ClickAndGoSymbol = null;
					Diagnostics = null;
					TextBuffer = null;
				}

				public void ReleaseSession(object sender, QuickInfoSessionStateChangedEventArgs args) {
					if (args.NewState != QuickInfoSessionState.Dismissed) {
						return;
					}
					var s = sender as IAsyncQuickInfoSession;
					s.StateChanged -= ReleaseSession;
					ClickAndGoSymbol = null;
					Diagnostics = null;
					TextBuffer = null;
					QuickInfoSession = null;
				}

				void OverrideDiagnosticInfo(StackPanel panel) {
					var infoPanel = panel.Children[1].GetFirstVisualChild<ItemsControl>()?.GetFirstVisualChild<StackPanel>();
					if (infoPanel == null) {
						// try the first item (symbol title may be absent)
						infoPanel = panel.Children[0].GetFirstVisualChild<ItemsControl>()?.GetFirstVisualChild<StackPanel>();
						if (infoPanel?.GetFirstVisualChild<WrapPanel>()?.GetFirstVisualChild<Image>() != null) {
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
						cp.LimitSize();
						if (Diagnostics != null && Diagnostics.Count > 0) {
							var t = cp.GetText();
							var d = Diagnostics.FirstOrDefault(i => i.GetMessage() == t);
							if (d != null) {
								cp.UseDummyToolTip();
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

					titlePanel.HorizontalAlignment = HorizontalAlignment.Stretch;
					doc.HorizontalAlignment = HorizontalAlignment.Stretch;

					var icon = infoPanel.GetFirstVisualChild<CrispImage>();
					var signature = infoPanel.GetFirstVisualChild<TextBlock>();

					if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.AlternativeStyle)) {
						UseAlternativeStyle(infoPanel, titlePanel, icon, signature);
					}

					OverrideDocumentation(doc);

					if (icon != null && signature != null) {
						// apply click and go feature
						if (ClickAndGoSymbol != null) {
							ClickAndGo.Apply(ClickAndGoSymbol, TextBuffer, signature, QuickInfoSession);
						}
						// fix the width of the signature part to prevent it from falling down to the next row
						if (Config.Instance.QuickInfoMaxWidth >= 100) {
							signature.MaxWidth = Config.Instance.QuickInfoMaxWidth - icon.Width - 30;
						}
					}
				}

				static void UseAlternativeStyle(StackPanel infoPanel, WrapPanel titlePanel, CrispImage icon, TextBlock signature) {
					if (icon != null) {
						var b = infoPanel.GetParent<Border>();
						b.Background = new VisualBrush(CreateEnlargedIcon(icon)) {
							Opacity = 0.3,
							AlignmentX = AlignmentX.Right,
							AlignmentY = AlignmentY.Bottom,
							TileMode = TileMode.None,
							Stretch = Stretch.None,
							Transform = new TranslateTransform(0, -6)
						};
						b.MinHeight = 60;
						icon.Visibility = Visibility.Collapsed;
					}
					if (signature != null) {
						var list = (IList)signature.Inlines;
						for (var i = 0; i < list.Count; i++) {
							if (list[i] is Hyperlink link
								&& link.Inlines.FirstInline is Run r) {
								list[i] = new Run { Text = r.Text, Foreground = r.Foreground, Background = r.Background };
							}
						}
					}
					titlePanel.Margin = __TitlePanelMargin;
				}

				void OverrideDocumentation(StackPanel doc) {
					// https://github.com/dotnet/roslyn/blob/version-3.0.0/src/Features/Core/Portable/QuickInfo/CommonSemanticQuickInfoProvider.cs
					// replace the default XML doc
					// sequence of items in default XML Doc panel:
					// 1. summary
					// 2. generic type parameter
					// 3. anonymous types
					// 4. availability warning
					// 5. usage
					// 6. exception
					// 7. captured variables
					if (OverrideBuiltInXmlDoc && (DocElement != null || ExceptionDoc != null)) {
						var items = doc.IsItemsHost ? (IList)doc.GetParent<ItemsControl>().Items : doc.Children;
						var v16orLater = CodistPackage.VsVersion.Major >= 16;
						ClearDefaultDocumentationItems(doc, v16orLater, items);
						if (DocElement != null) {
							OverrideDocElement(doc, v16orLater, items);
						}
						if (ExceptionDoc != null) {
							OverrideExceptionDocElement(doc, v16orLater, items);
						}
					}
				}

				static void ClearDefaultDocumentationItems(StackPanel doc, bool v16orLater, IList items) {
					if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.OverrideDefaultDocumentation)) {
						if (v16orLater) {
							items = doc.GetParent<ItemsControl>().Items;
						}
						try {
							for (int i = items.Count - 1; i > 0; i--) {
								var item = items[i];
								if (v16orLater && item is Panel && item is ThemedTipDocument == false || item is TextBlock) {
									items.RemoveAt(i);
								}
							}
						}
						catch (InvalidOperationException) {
							// ignore exception: doc.Children was changed by another thread
						}
					}
				}

				void OverrideDocElement(StackPanel doc, bool v16orLater, IList items) {
					try {
						if (items.Count > 1 && items[1] is TextBlock) {
							items.RemoveAt(1);
							items.Insert(1, DocElement);
						}
						else {
							items.Add(DocElement);
						}
						if (DocElement is ThemedTipDocument myDoc) {
							myDoc.ApplySizeLimit();
						}
					}
					catch (ArgumentException) {
						// ignore exception: Specified Visual is already a child of another Visual or the root of a CompositionTarget
					}
					catch (InvalidOperationException) {
						// ignore exception: doc.Children was changed by another thread
					}
				}

				void OverrideExceptionDocElement(StackPanel doc, bool v16orLater, IList items) {
					if (v16orLater) {
						items = doc.GetParent<ItemsControl>().Items;
					}
					try {
						items.Add(ExceptionDoc);
						//todo move this to ApplySizeLimit
						(ExceptionDoc as ThemedTipDocument)?.ApplySizeLimit();
					}
					catch (InvalidOperationException) {
						// ignore exception: doc.Children was changed by another thread
					}
				}

				static CrispImage CreateEnlargedIcon(CrispImage icon) {
					var bgIcon = new CrispImage { Moniker = icon.Moniker, Width = 48, Height = 48 };
					bgIcon.SetBackgroundForCrispImage(ThemeHelper.TitleBackgroundColor);
					return bgIcon;
				}

				static void ApplySizeLimit(StackPanel quickInfoPanel) {
					if (quickInfoPanel == null) {
						return;
					}
					var docPanel = quickInfoPanel.Children[0].GetFirstVisualChild<WrapPanel>().GetParent<StackPanel>();
					var docPanelHandled = docPanel == null; // don't process docPanel if it is not found
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
						if (c is OverriderControl || c is IInteractiveQuickInfoContent /* don't hack interactive content */) {
							continue;
						}
						if (docPanel == c || docPanelHandled == false && cp.GetFirstVisualChild<StackPanel>(i => i == docPanel) != null) {
							cp.LimitSize();
							if (Config.Instance.QuickInfoXmlDocExtraHeight > 0 && Config.Instance.QuickInfoMaxHeight > 0) {
								cp.MaxHeight += Config.Instance.QuickInfoXmlDocExtraHeight;
							}
							foreach (var r in docPanel.Children) {
								(r as ThemedTipDocument)?.ApplySizeLimit();
							}
							c = cp.Content;
							cp.Content = null;
							cp.Content = ((DependencyObject)c).Scrollable();
							docPanelHandled = true;
							continue;
						}
						else if (c is StackPanel s) {
							MakeChildrenScrollable(s);
							continue;
						}
						(c as ThemedTipDocument)?.ApplySizeLimit();
						if (c is ScrollViewer) {
							continue;
						}
						var v = c as IWpfTextView; // snippet tooltip, some other default tooltip
						if (v != null) {
							// use the custom control to enable selection
							cp.Content = new ThemedTipText {
								Text = v.TextSnapshot.GetText()
							}.Scrollable().LimitSize();
							//v.VisualElement.LimitSize();
							//v.Options.SetOptionValue(DefaultTextViewHostOptions.VerticalScrollBarName, true);
							//v.Options.SetOptionValue(DefaultTextViewOptions.WordWrapStyleName, WordWrapStyles.WordWrap);
							//v.Options.SetOptionValue(DefaultTextViewOptions.AutoScrollName, true);
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
						if (cp.Content is ItemsControl items && items.GetType().Name == "WpfToolTipItemsControl") {
							try {
								MakeChildrenScrollable(items);
								continue;
							}
							catch (InvalidOperationException) {
								// ignore
#if DEBUG
								throw;
#endif
							}
						}
						cp.Content = null;
						cp.Content = o.Scrollable();
					}
				}

				static void MakeChildrenScrollable(StackPanel s) {
					var children = new DependencyObject[s.Children.Count];
					var i = -1;
					foreach (DependencyObject n in s.Children) {
						children[++i] = n;
					}
					s.Children.Clear();
					foreach (var c in children) {
						var d = c as ThemedTipDocument;
						if (d != null) {
							foreach (var item in d.Paragraphs) {
								//(item as FrameworkElement)?.LimitSize();
								item.TextWrapping = TextWrapping.Wrap;
							}
							d.ApplySizeLimit();
							d.WrapMargin(WpfHelper.SmallVerticalMargin);
						}
						s.Add(c.Scrollable().LimitSize());
					}
				}
				static void MakeChildrenScrollable(ItemsControl s) {
					var children = new object[s.Items.Count];
					var i = -1;
					foreach (object n in s.Items) {
						children[++i] = n;
					}
					s.Items.Clear();
					foreach (var c in children) {
						var t = c as TextBlock;
						if (t != null) {
							t.TextWrapping = TextWrapping.Wrap;
							s.Items.Add(t.Scrollable().LimitSize());
						}
						else {
							s.Items.Add(c);
						}
					}
				}
			}
		}
	}
}
