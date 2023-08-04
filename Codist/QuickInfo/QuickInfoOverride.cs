using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using CLR;
using Codist.Controls;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Language.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using R = Codist.Properties.Resources;

namespace Codist.QuickInfo
{
	interface IQuickInfoOverride
	{
		bool OverrideBuiltInXmlDoc { get; set; }
		UIElement CreateControl(IAsyncQuickInfoSession session);
		void ApplyClickAndGo(ISymbol symbol);
		void OverrideDocumentation(UIElement docElement);
		void OverrideException(UIElement exceptionDoc);
	}

	static class QuickInfoOverride
	{
		static readonly ExtensionProperty<FrameworkElement, bool> __CodistQuickInfo = ExtensionProperty<FrameworkElement, bool>.Register("IsCodistQuickInfoItem");
		static readonly ExtensionProperty<FrameworkElement, FrameworkElement> __QuickInfoContainer = ExtensionProperty<FrameworkElement, FrameworkElement>.Register("QuickInfoContainer");

		public static IQuickInfoOverride CreateOverride(IAsyncQuickInfoSession session) {
			return session.GetOrCreateSingletonProperty<DefaultOverride>();
		}

		public static TObj Tag<TObj>(this TObj obj)
			where TObj : FrameworkElement {
			__CodistQuickInfo.Set(obj, true);
			return obj;
		}

		public static FrameworkElement GetContainer(FrameworkElement popupRoot) {
			return __QuickInfoContainer.Get(popupRoot);
		}

		static bool IsCodistQuickInfoItem(this FrameworkElement quickInfoItem) {
			return __CodistQuickInfo.Get(quickInfoItem);
		}

		public static bool CheckCtrlSuppression() {
			return Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.CtrlSuppress) && WpfHelper.IsControlDown;
		}

		public static void HoldQuickInfo(DependencyObject quickInfoItem, bool hold) {
			FindHolder(quickInfoItem)?.Hold(hold);
		}

		public static void DismissQuickInfo(DependencyObject quickInfoItem) {
			FindHolder(quickInfoItem)?.DismissAsync();
		}

		static UIOverride FindHolder(DependencyObject quickInfoItem) {
			var items = quickInfoItem.GetParent<ItemsControl>(i => i.GetType().Name == "WpfToolTipItemsControl");
			// version 16.1 or above
			items = items.GetParent<ItemsControl>(i => i.GetType().Name == "WpfToolTipItemsControl") ?? items;
			return items != null ? items.GetFirstVisualChild<UIOverride>() : quickInfoItem.GetParent<FrameworkElement>(e => e.GetType().Name == "WpfToolTipControl").GetFirstVisualChild<UIOverride>();
		}

		static ThemedToolTip ShowSymbolLocation(ISymbol symbol) {
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
				return i >= 0
					? (fileName.Substring(0, i), fileName.Substring(i))
					: (fileName, String.Empty);
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
					description.MouseEnter += HookQuickInfoDescriptionEvents;
					return;
				}
				if (symbol.Kind == SymbolKind.Method && ((IMethodSymbol)symbol).MethodKind == MethodKind.LambdaMethod) {
					ShowLambdaExpressionParameters(symbol, description);
				}
				description.UseDummyToolTip();
				if (symbol.HasSource() == false && symbol.ContainingType?.HasSource() == true) {
					// if the symbol is implicitly declared but its containing type is in source,
					// navigate to the containing type
					symbol = symbol.ContainingType;
				}
				description.MouseEnter += HookQuickInfoDescriptionEvents;
			}

			static void ShowLambdaExpressionParameters(ISymbol symbol, TextBlock description) {
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

			public static void Apply(ISymbol symbol, ITextBuffer textBuffer, TextBlock description, IAsyncQuickInfoSession quickInfoSession) {
				quickInfoSession.StateChanged += new ClickAndGo(symbol, textBuffer, description, quickInfoSession).QuickInfoSession_StateChanged;
			}

			void HookQuickInfoDescriptionEvents(object sender, MouseEventArgs e) {
				var s = sender as FrameworkElement;
				s.MouseEnter -= HookQuickInfoDescriptionEvents;
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
				((TextBlock)sender).Background = (_symbol.HasSource() ? SystemColors.HighlightBrush : SystemColors.GrayTextBrush).Alpha(WpfHelper.DimmedOpacity);
			}
			void RemoveSymbolHighlight(object sender, MouseEventArgs e) {
				((TextBlock)sender).Background = Brushes.Transparent;
			}

			void GoToSource(object sender, MouseButtonEventArgs e) {
				try {
					_symbol.GoToDefinition();
				}
				catch (Exception ex) {
					MessageWindow.Error(ex);
				}
			}

			void ShowToolTip(object sender, ToolTipEventArgs e) {
				var s = _symbol;
				if (s != null) {
					var t = sender as TextBlock;
					t.ToolTip = ShowSymbolLocation(s);
					t.ToolTipOpening -= ShowToolTip;
				}
			}

			[SuppressMessage("Usage", Suppression.VSTHRD100, Justification = Suppression.EventHandler)]
			async void ShowContextMenu(object sender, ContextMenuEventArgs e) {
				await SyncHelper.SwitchToMainThreadAsync(default);
				var s = sender as FrameworkElement;
				if (s.ContextMenu == null) {
					var v = _quickInfoSession.TextView;
					var ctx = SemanticContext.GetOrCreateSingletonInstance(v as IWpfTextView);
					await ctx.UpdateAsync(_textBuffer, default);
					await SyncHelper.SwitchToMainThreadAsync(default);
					var m = new CSharpSymbolContextMenu(_symbol,
						v.TextBuffer.ContentType.TypeName == Constants.CodeTypes.InteractiveContent
							? null
							: ctx.GetNode(_quickInfoSession.ApplicableToSpan.GetStartPoint(v.TextSnapshot), true, true),
						ctx);
					m.AddAnalysisCommands();
					if (m.HasItems) {
						m.Items.Add(new Separator());
					}
					m.AddSymbolNodeCommands();
					m.AddTitleItem(_symbol.GetOriginalName());
					m.CommandExecuted += HideQuickInfo;
					s.ContextMenu = m;
				}
				await SyncHelper.SwitchToMainThreadAsync(default);
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
				s.MouseEnter -= HookQuickInfoDescriptionEvents;
				s.ToolTipOpening -= ShowToolTip;
				s.MouseEnter -= HighlightSymbol;
				s.MouseLeave -= RemoveSymbolHighlight;
				s.MouseLeftButtonUp -= GoToSource;
				s.ContextMenuOpening -= ShowContextMenu;
				s.ContextMenuClosing -= ReleaseQuickInfo;
				if (s.ContextMenu is CSharpSymbolContextMenu m) {
					m.CommandExecuted -= HideQuickInfo;
					m.Dispose();
				}
				s.ContextMenu = null;
				_symbol = null;
				_textBuffer = null;
				_description = null;
				_quickInfoSession = null;
			}
		}

		/// <summary>
		/// The override for VS 15.8 and above versions.
		/// </summary>
		/// <remarks>
		/// <para>The original implementation of QuickInfo locates at: Common7\IDE\CommonExtensions\Microsoft\Editor\Microsoft.VisualStudio.Platform.VSEditor.dll</para>
		/// <para>class: Microsoft.VisualStudio.Text.AdornmentLibrary.ToolTip.Implementation.WpfToolTipControl</para>
		/// </remarks>
		sealed class DefaultOverride : IQuickInfoOverride
		{
			readonly bool _LimitItemSize;
			bool _OverrideBuiltInXmlDoc, _IsCSharpDoc;
			ISymbol _ClickAndGoSymbol;
			UIElement _DocElement;
			UIElement _ExceptionDoc;
			IAsyncQuickInfoSession _Session;
			ITagAggregator<IErrorTag> _ErrorTagger;
			ErrorTags _ErrorTags;

			public DefaultOverride() {
				if (Config.Instance.QuickInfo.MaxHeight > 0 || Config.Instance.QuickInfo.MaxWidth > 0) {
					_LimitItemSize = true;
				}
				_OverrideBuiltInXmlDoc = Config.Instance.QuickInfoOptions.HasAnyFlag(QuickInfoOptions.DocumentationOverride);
			}
			public bool LimitItemSize => _LimitItemSize;
			public bool OverrideBuiltInXmlDoc { get => _OverrideBuiltInXmlDoc; set => _OverrideBuiltInXmlDoc = value; }
			public bool IsCSharpDoc => _IsCSharpDoc;
			public ISymbol ClickAndGoSymbol => _ClickAndGoSymbol;
			public IAsyncQuickInfoSession Session => _Session;
			public UIElement DocElement => _DocElement;
			public UIElement ExceptionDoc => _ExceptionDoc;
			public ITagAggregator<IErrorTag> ErrorTagger => _ErrorTagger;
			public ErrorTags ErrorTags => _ErrorTags;

			public UIElement CreateControl(IAsyncQuickInfoSession session) {
				_Session = session;
				_IsCSharpDoc = session.TextView.TextBuffer.IsContentTypeIncludingProjection(Constants.CodeTypes.CSharp);
				session.StateChanged -= ReleaseSession;
				session.StateChanged += ReleaseSession;
				return new UIOverride(this);
			}

			public void ApplyClickAndGo(ISymbol symbol) {
				_ClickAndGoSymbol = symbol;
			}

			public void OverrideDocumentation(UIElement docElement) {
				_DocElement = docElement;
			}
			public void OverrideException(UIElement exceptionDoc) {
				_ExceptionDoc = exceptionDoc;
			}

			ITagAggregator<IErrorTag> GetErrorTagger() {
				return _Session?.TextView.Properties.GetOrCreateSingletonProperty(CreateErrorTagger);
			}

			ITagAggregator<IErrorTag> CreateErrorTagger() {
				return ServicesHelper.Instance.ViewTagAggregatorFactory.CreateTagAggregator<IErrorTag>(_Session.TextView);
			}

			public CrispImage GetIconForErrorText(TextBlock textBlock) {
				var f = textBlock.Inlines.FirstInline;
				var tt = ((f as Hyperlink)?.Inlines.FirstInline as Run)?.Text;
				if (tt == null) {
					tt = (f as Run)?.Text;
					if (tt == null) {
						return null;
					}
					if (tt == "SPELL") {
						return ThemeHelper.GetImage(IconIds.Suggestion);
					}
				}
				var errorTagger = GetErrorTagger();
				return errorTagger != null
					? (_ErrorTags ?? (_ErrorTags = new ErrorTags()))
						.GetErrorIcon(tt, errorTagger, _Session.ApplicableToSpan.GetSpan(_Session.TextView.TextSnapshot))
					: null;
			}

			void ReleaseSession(object sender, QuickInfoSessionStateChangedEventArgs args) {
				if (args.NewState != QuickInfoSessionState.Dismissed) {
					return;
				}
				var s = sender as IAsyncQuickInfoSession;
				s.StateChanged -= ReleaseSession;
				_ClickAndGoSymbol = null;
				_Session = null;
				if (_ErrorTagger != null) {
					_ErrorTagger.Dispose();
					_ErrorTagger = null;
				}
			}
		}

		sealed class UIOverride : UIElement, IInteractiveQuickInfoContent
		{
			static readonly Thickness __TitlePanelMargin = new Thickness(0, 0, 30, 6);

			readonly DefaultOverride _Override;
			bool _Overridden;

			public UIOverride(DefaultOverride uiOverride) {
				_Override = uiOverride;
			}

			public bool KeepQuickInfoOpen { get; set; }
			public bool IsMouseOverAggregated { get; set; }

			public void Hold(bool hold) {
				IsMouseOverAggregated = hold;
			}
			public System.Threading.Tasks.Task DismissAsync() {
				return _Override.Session?.DismissAsync() ?? System.Threading.Tasks.Task.CompletedTask;
			}

			protected override void OnVisualParentChanged(DependencyObject oldParent) {
				base.OnVisualParentChanged(oldParent);
				if (_Overridden) {
					return;
				}
				var p = this.GetParent<StackPanel>();
				if (p == null) {
					goto EXIT;
				}
				p.SetProperty(TextBlock.FontFamilyProperty, ThemeHelper.ToolTipFont)
					.SetProperty(TextBlock.FontSizeProperty, ThemeHelper.ToolTipFontSize);
				try {
					if (Config.Instance.DisplayOptimizations.MatchFlags(DisplayOptimizations.CodeWindow)) {
						WpfHelper.SetUITextRenderOptions(p, true);
					}
					Grid altSign = Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.OverrideDefaultDocumentation) || _Override.ClickAndGoSymbol != null
						? OverrideXmlDocGetSignature(p)
						: null;
					_Override.ErrorTags?.Clear();
					MakeTextualContentSelectableWithIcon(p);
					if (_Override.Session.Options == QuickInfoSessionOptions.TrackMouse) {
						if (p.GetParent<FrameworkElement>(e => e.GetType().Name == "WpfToolTipControl") is ContentControl tip
							&& tip.Content is FrameworkElement c) {
							ThemedTipDocument locDoc = Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.SymbolLocation) && _Override.ClickAndGoSymbol != null
								? p.GetFirstVisualChild<ThemedTipDocument>(d => d.Name == "SymbolLocation")
								: null;
							tip.Content = null;
							var s = new QuickInfoControl();
							if (altSign != null) {
								s.AddTopPart(altSign);
							}
							if (locDoc?.Parent is Panel lp) {
								lp.Children.Remove(locDoc);
								s.AddFooter(locDoc);
							}
							s.Add(c.Scrollable());
							if (_Override.LimitItemSize) {
								s.LimitSize();
							}
							tip.Content = s;
							__QuickInfoContainer.Set(tip.GetParent((FrameworkElement e) => e.GetType().Name == "PopupRoot"), s);
						}
					}
					else if (altSign != null) {
						GetItems(p).Insert(0, altSign);
					}
				}
				catch (Exception ex) {
					Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, (Action<Exception>)((e) => MessageWindow.Error(e.ToString(), R.T_SuperQuickInfo)), ex);
					return;
				}
			EXIT:
				_Overridden = true;
				// hides the parent container from taking excessive space in the quick info window
				this.GetParent<Border>().Collapse();
			}

			void MakeTextualContentSelectableWithIcon(Panel p) {
				var items = GetItems(p);
				for (int i = 0; i < items.Count; i++) {
					if (items[i] is DependencyObject qi
						&& ((qi as FrameworkElement)?.IsCodistQuickInfoItem()) != true) {
						if (qi is TextBlock t) {
							OverrideTextBlock(t);
							continue;
						}
						if (qi is IWpfTextView vi) {
							items[i] = null;
							items[i] = new ThemedTipText {
								Text = vi.TextSnapshot.GetText(),
								Margin = WpfHelper.MiddleBottomMargin
							}.SetGlyph(ThemeHelper.GetImage(IconIds.Info));
							continue;
						}
						foreach (var tb in qi.GetDescendantChildren((Predicate<TextBlock>)null, WorkaroundForTypeScriptQuickInfo)) {
							OverrideTextBlock(tb);
						}
						foreach (var item in qi.GetDescendantChildren<ContentPresenter>()) {
							if (item.Content is IWpfTextView v) {
								item.Content = new ThemedTipText {
									Text = v.TextSnapshot.GetText(),
									Margin = WpfHelper.MiddleBottomMargin
								}.SetGlyph(ThemeHelper.GetImage(IconIds.Info));
							}
						}
					}
				}

				// Note See GitHub: #255, TypeScript package Quick Info places TextBlock inside Button,
				//   overriding the TextBlock will break the Button
				bool WorkaroundForTypeScriptQuickInfo(DependencyObject c) => c is Button == false;
			}

			Grid OverrideXmlDocGetSignature(StackPanel infoPanel) {
				var titlePanel = infoPanel.GetFirstVisualChild<WrapPanel>();
				if (titlePanel == null) {
					Grid altSign = null;
					if (_Override.ClickAndGoSymbol != null
						&& Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.AlternativeStyle)) {
						// no built-in documentation, but Codist has it
						altSign = ShowAlternativeSignature();
						OverrideDocumentation(infoPanel);
					}
					return altSign;
				}
				var doc = titlePanel.GetParent<StackPanel>();
				if (doc == null) {
					return null;
				}

				titlePanel.HorizontalAlignment = HorizontalAlignment.Stretch;
				doc.HorizontalAlignment = HorizontalAlignment.Stretch;

				var icon = titlePanel.GetFirstVisualChild<CrispImage>();
				var signature = infoPanel.GetFirstVisualChild<TextBlock>();

				if (_Override.DocElement != null || Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.AlternativeStyle) && _Override.ClickAndGoSymbol != null) {
					OverrideDocumentation(doc);
				}

				if (icon != null && signature != null) {
					// override signature style and apply "click and go" feature
					if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.AlternativeStyle)) {
						if (_Override.ClickAndGoSymbol != null) {
							titlePanel.Visibility = Visibility.Collapsed;
							if (titlePanel.Parent is ItemsControl items) {
								items.Items.Remove(titlePanel);
							}
							return ShowAlternativeSignature();
						}
						if (_Override.IsCSharpDoc) {
							UseAlternativeStyle(infoPanel, titlePanel, icon, signature);
						}
					}
					else if (Config.Instance.QuickInfo.MaxWidth >= 100) {
						// fix the width of the original signature part to prevent it from falling down to the next row
						signature.MaxWidth = Config.Instance.QuickInfo.MaxWidth - icon.Width - 30;
					}
				}
				else if (Config.Instance.QuickInfoOptions.MatchFlags(QuickInfoOptions.AlternativeStyle) && _Override.ClickAndGoSymbol != null) {
					return ShowAlternativeSignature();
				}
				return null;
			}

			Grid ShowAlternativeSignature() {
				var s = _Override.ClickAndGoSymbol;
				var icon = ThemeHelper.GetImage(s.GetImageId(), ThemeHelper.QuickInfoLargeIconSize)
					.AsSymbolLink(Keyboard.Modifiers == ModifierKeys.Control ? s.OriginalDefinition : s);
				icon.VerticalAlignment = VerticalAlignment.Top;
				var signature = SymbolFormatter.Instance.ShowSignature(s);
				signature.MaxWidth = (Config.Instance.QuickInfo.MaxWidth >= 100
					? Config.Instance.QuickInfo.MaxWidth
					: WpfHelper.GetActiveScreenSize().Width / 2) - (ThemeHelper.QuickInfoLargeIconSize + 30);
				return new Grid {
					ColumnDefinitions = {
						new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) },
						new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
					},
					Children = { icon, signature.SetValue(Grid.SetColumn, 1) }
				}.Tag();
			}

			static IList GetItems(Panel docPanel) {
				if (docPanel.IsItemsHost) {
					var c = docPanel.GetParent<ItemsControl>();
					return c.ItemsSource as IList ?? c.Items;
				}
				return docPanel.Children;
			}

			static void UseAlternativeStyle(StackPanel infoPanel, WrapPanel titlePanel, CrispImage icon, TextBlock signature) {
				if (icon != null) {
					var b = infoPanel.GetParent<Border>();
					b.Background = new VisualBrush(CreateEnlargedIcon(icon)) {
						Opacity = WpfHelper.DimmedOpacity,
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
				if (_Override.OverrideBuiltInXmlDoc) {
					var items = doc.IsItemsHost ? (IList)doc.GetParent<ItemsControl>().Items : doc.Children;
					var v16orLater = CodistPackage.VsVersion.Major >= 16;
					ClearDefaultDocumentationItems(doc, v16orLater, items);
					if (_Override.DocElement != null) {
						OverrideDocElement(items);
					}
					if (_Override.ExceptionDoc != null) {
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

			void OverrideDocElement(IList items) {
				try {
					var d = _Override.DocElement;
					if (items.Count > 1 && items[1] is TextBlock) {
						items.RemoveAt(1);
						items.Insert(1, d);
					}
					else {
						items.Add(d);
					}
					if (d is ThemedTipDocument myDoc) {
						myDoc.Margin = new Thickness(0, 3, 0, 0);
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
					items.Add(_Override.ExceptionDoc);
				}
				catch (InvalidOperationException) {
					// ignore exception: doc.Children was changed by another thread
				}
			}

			static CrispImage CreateEnlargedIcon(CrispImage icon) {
				var bgIcon = new CrispImage {
					Moniker = icon.Moniker,
					Width = ThemeHelper.XLargeIconSize,
					Height = ThemeHelper.XLargeIconSize
				};
				bgIcon.SetBackgroundForCrispImage(ThemeHelper.TitleBackgroundColor);
				return bgIcon;
			}

			void OverrideTextBlock(TextBlock t) {
				if (t is ThemedTipText == false
					&& TextEditorWrapper.CreateFor(t) != null
					&& t.Inlines.FirstInline is InlineUIContainer == false) {
					t.TextWrapping = TextWrapping.Wrap;
					CrispImage icon = _Override.GetIconForErrorText(t);
					if (icon != null) {
						t.SetGlyph(icon);
					}
				}
			}
		}

		class QuickInfoControl : DockPanel
		{
			public QuickInfoControl() {
				Margin = WpfHelper.MiddleVerticalMargin;
				LastChildFill = true;
			}

			public void AddTopPart(UIElement part) {
				Children.Add(part.SetValue(SetDock, Dock.Top));
			}

			public void AddFooter(UIElement part) {
				Children.Add(part.SetValue(SetDock, Dock.Bottom));
			}
		}

		sealed class ErrorTags
		{
			Dictionary<string, string> _TagHolder;

			public void Clear() {
				_TagHolder?.Clear();
			}

			public CrispImage GetErrorIcon(string code, ITagAggregator<IErrorTag> tagger, SnapshotSpan span) {
				if (GetTags(tagger, span).TryGetValue(code, out var error)) {
					if (code[0] == 'C' && code[1] == 'S' && error == PredefinedErrorTypeNames.Warning) {
						return CodeAnalysisHelper.GetWarningLevel(ToErrorCode(code, 2)) < 3
							? ThemeHelper.GetImage(IconIds.SevereWarning)
							: ThemeHelper.GetImage(IconIds.Warning);
					}
					var iconId = GetIconIdForErrorType(error);
					return iconId != 0 ? ThemeHelper.GetImage(iconId).SetValue(ToolTipService.SetToolTip, error) : null;
				}
				return null;
			}

			static int GetIconIdForErrorType(string error) {
				switch (error) {
					case PredefinedErrorTypeNames.Suggestion: return IconIds.HiddenInfo;
					case PredefinedErrorTypeNames.HintedSuggestion: return IconIds.Info;
					case PredefinedErrorTypeNames.Warning: return IconIds.Warning;
					case PredefinedErrorTypeNames.SyntaxError: return IconIds.SyntaxError;
					case PredefinedErrorTypeNames.CompilerError: return IconIds.Stop;
					case PredefinedErrorTypeNames.OtherError: return IconIds.Error;
				}
				return GetIconIdForCustomErrorTypes(error);
			}

			static int GetIconIdForCustomErrorTypes(string error) {
				if (error.Contains("suggestion")) {
					return IconIds.Suggestion;
				}
				if (error.Contains("warning")) {
					return IconIds.Warning;
				}
				if (error.Contains("error")) {
					return IconIds.Error;
				}
				return 0;
			}

			Dictionary<string, string> GetTags(ITagAggregator<IErrorTag> tagger, SnapshotSpan span) {
				if (_TagHolder == null) {
					_TagHolder = new Dictionary<string, string>();
				}
				foreach (var tag in tagger.GetTags(span)) {
					var content = tag.Tag.ToolTipContent;
					if (content is ContainerElement ce) {
						foreach (var cte in ce.Elements.Cast<ClassifiedTextElement>()) {
							var firstRun = cte.Runs.First();
							if (firstRun != null) {
								_TagHolder[firstRun.Text] = tag.Tag.ErrorType;
							}
						}
					}
					else if (content is string t) {
						_TagHolder[t] = tag.Tag.ErrorType;
					}
				}
				return _TagHolder;
			}

			static int ToErrorCode(string text, int index) {
				var l = text.Length;
				var code = 0;
				for (int i = index; i < l; i++) {
					var c = text[i];
					if (c < '0' || c > '9') {
						return 0;
					}
					code = code * 10 + c - '0';
				}
				return code;
			}
		}
	}
}
