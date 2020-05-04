using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Codist.Controls;
using Codist.SyntaxHighlight;
using Codist.Taggers;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.Win32;

namespace Codist.Options
{
	sealed class SyntaxHighlightCustomizationWindow : Window
	{
		static readonly Thickness SubOptionMargin = new Thickness(24, 0, 0, 0);
		readonly StackPanel _SettingsList;
		readonly Border _OptionPageHolder;
		readonly TextBlock _Notice;
		readonly ListBox _SyntaxSourceBox;
		readonly Border _SettingsGroup, _TagSettingsGroup;
		readonly FontButton _FontButton;
		readonly NumericUpDown _FontSizeBox;
		readonly StyleCheckBox _BoldBox;
		readonly StyleCheckBox _ItalicBox;
		readonly StyleCheckBox _UnderlineBox;
		readonly StyleCheckBox _StrikethroughBox;
		readonly ColorButton _ForegroundButton;
		readonly OpacityButton _ForegroundOpacityButton;
		readonly ColorButton _BackgroundButton;
		readonly OpacityButton _BackgroundOpacityButton;
		readonly ComboBox _BackgroundEffectBox;
		readonly WrapPanel _BaseTypesList;
		readonly Button _AddTagButton, _RemoveTagButton;
		readonly TextBox _TagBox;
		readonly ComboBox _TagStyleBox;
		readonly StyleCheckBox _TagCaseSensitiveBox;
		readonly StyleCheckBox _TagHasPunctuationBox;
		readonly RadioBox _TagApplyOnTagBox, _TagApplyOnContentBox, _TagApplyOnWholeBox;
		readonly Button _LoadThemeButton, _SaveThemeButton, _ResetThemeButton;
		readonly UiLock _Lock = new UiLock();
		IWpfTextView _WpfTextView;
		IClassificationFormatMap _FormatMap;
		TextFormattingRunProperties _DefaultFormat;
		StyleSettingsButton _SelectedStyleButton;
		CommentLabel _SelectedCommentTag;

		public SyntaxHighlightCustomizationWindow(IWpfTextView wpfTextView) {
			_WpfTextView = wpfTextView;
			Title = "Syntax Highlight Configurations";
			ShowInTaskbar = false;
			MinHeight = 300;
			MinWidth = 480;
			SnapsToDevicePixels = true;
			Content = new Border {
				Padding = WpfHelper.MiddleMargin,
				Child = new Grid {
					ColumnDefinitions = {
						new ColumnDefinition { Width = new GridLength(150) },
						new ColumnDefinition { Width = new GridLength(10, GridUnitType.Star) }
					},
					Children = {
						new StackPanel {
							Margin = WpfHelper.MiddleMargin,
							Children = {
								new TextBlock {
									Text = "Syntax categories:",
									Height = 20,
									FontWeight = FontWeights.Bold
								},
								(_SyntaxSourceBox = new ListBox {
									Items = {
										new ClassificationCategoryItem(SyntaxStyleSource.Selection, "Selected code"),
										new ClassificationCategoryItem(SyntaxStyleSource.Common, "Common"),
										new ClassificationCategoryItem(SyntaxStyleSource.CSharp, "C#"),
										new ClassificationCategoryItem(SyntaxStyleSource.CSharpSymbolMarker, "   symbol markers"),
										new ConfigPageItem<CSharpAdditionalHighlightConfigPage>("   options"),
										new ClassificationCategoryItem(SyntaxStyleSource.CPlusPlus, "C++"),
										new ClassificationCategoryItem(SyntaxStyleSource.Markdown, "Markdown"),
										new ClassificationCategoryItem(SyntaxStyleSource.Xml, "XML"),
										new ClassificationCategoryItem(SyntaxStyleSource.CommentTagger, "Tagged comments"),
										new ClassificationCategoryItem(SyntaxStyleSource.CommentLabels, "   tags"),
										new ClassificationCategoryItem(SyntaxStyleSource.PriorityOrder, "All languages")
									}
								}.ReferenceStyle(VsResourceKeys.ThemedDialogListBoxStyleKey)),
								new TextBlock {
									Text = "Themes:",
									Height = 20,
									FontWeight = FontWeights.Bold,
									Margin = WpfHelper.TopItemMargin
								},
								(_LoadThemeButton = new ThemedButton(KnownImageIds.FolderOpened, "Load...", "Load syntax highlight theme...", LoadTheme) { HorizontalContentAlignment = HorizontalAlignment.Left }).ReferenceStyle(VsResourceKeys.ButtonStyleKey),
								(_SaveThemeButton = new ThemedButton(KnownImageIds.SaveAs, "Save...", "Save syntax highlight theme...", SaveTheme) { HorizontalContentAlignment = HorizontalAlignment.Left }).ReferenceStyle(VsResourceKeys.ButtonStyleKey),
								(_ResetThemeButton = new ThemedButton(KnownImageIds.CleanData, "Reset...", "Reset syntax highlight theme to default...", ResetTheme) { HorizontalContentAlignment = HorizontalAlignment.Left }.ReferenceStyle(VsResourceKeys.ButtonStyleKey)),
								new TextBlock {
									Text = "Predefined themes:",
									Height = 20,
									FontWeight = FontWeights.Bold,
									Margin = WpfHelper.TopItemMargin
								},
								new ThemedButton(KnownImageIds.ColorPalette, "Light theme", "Load predefined theme for light editing environment", () => LoadTheme(Config.LightTheme)) { HorizontalContentAlignment = HorizontalAlignment.Left }.ReferenceStyle(VsResourceKeys.ButtonStyleKey),
								new ThemedButton(KnownImageIds.ColorPalette, "Dark theme", "Load predefined theme for dark editing environment", () => LoadTheme(Config.DarkTheme)) { HorizontalContentAlignment = HorizontalAlignment.Left }.ReferenceStyle(VsResourceKeys.ButtonStyleKey),
								new ThemedButton(KnownImageIds.ColorPalette, "Simple theme", "Load a predefined simple theme", () => LoadTheme(Config.SimpleTheme)) { HorizontalContentAlignment = HorizontalAlignment.Left }.ReferenceStyle(VsResourceKeys.ButtonStyleKey),
							}
						},

						new Grid {
							Margin = WpfHelper.MiddleMargin,
							RowDefinitions = {
								new RowDefinition { Height = new GridLength(20, GridUnitType.Pixel) },
								new RowDefinition { Height = new GridLength(10, GridUnitType.Star) },
								new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
								new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
							},
							Children = {
								new Grid {
									ColumnDefinitions = {
										new ColumnDefinition { Width = new GridLength(10, GridUnitType.Star) },
										new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) },
										new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) },
									},
									Children = {
										new TextBlock { Text = "Syntax styles:", FontWeight = FontWeights.Bold }.SetValue(Grid.SetColumn, 0),
										(_AddTagButton = new Button { Content = "Add" }.ReferenceStyle(VsResourceKeys.ButtonStyleKey)).SetValue(Grid.SetColumn, 1),
										(_RemoveTagButton = new Button { Content = "Remove" }.ReferenceStyle(VsResourceKeys.ButtonStyleKey)).SetValue(Grid.SetColumn, 2)
									}
								},
								(_OptionPageHolder = new Border {
									BorderThickness = WpfHelper.TinyMargin,
									Child = _SettingsList = new StackPanel()
								})
								.Scrollable()
								.SetValue(Grid.SetRow, 1)
								.ReferenceProperty(Border.BorderBrushProperty, VsBrushes.ActiveBorderKey),

								(_SettingsGroup = new Border {
									Visibility = Visibility.Collapsed,
									Child = new StackPanel {
										Children = {
											new TextBlock { Text = "Style settings:", FontWeight = FontWeights.Bold },
											new WrapPanel {
												Margin = WpfHelper.SmallMargin,
												Children = {
													new TextBlock { Text = "Font: ", Width = 60, Margin = WpfHelper.SmallMargin },
													(_FontButton = new FontButton(ApplyFont) { Width = 230, Margin = WpfHelper.SmallMargin }.ReferenceStyle(VsResourceKeys.ThemedDialogButtonStyleKey)),
													new TextBlock { Text = "Size: ", Width = 60, Margin = WpfHelper.SmallMargin },
													(_FontSizeBox = new NumericUpDown { Width = 80, Margin = WpfHelper.SmallMargin }),
												}
											},
											new WrapPanel {
												Margin = WpfHelper.SmallMargin,
												Children = {
													(_BoldBox = new StyleCheckBox("Bold", OnBoldChanged)),
													(_ItalicBox = new StyleCheckBox("Italic", OnItalicChanged)),
													(_UnderlineBox = new StyleCheckBox("Underline", OnUnderlineChanged)),
													(_StrikethroughBox = new StyleCheckBox("Strikethrough", OnStrikeThroughChanged)),
												}
											},
											new WrapPanel {
												Margin = WpfHelper.SmallMargin,
												Children = {
													(_ForegroundButton = new ColorButton(Colors.Transparent, "Foreground", OnForeColorChanged)),
													(_ForegroundOpacityButton = new OpacityButton(OnForeOpacityChanged)),
													(_BackgroundButton = new ColorButton(Colors.Transparent, "Background", OnBackColorChanged)),
													(_BackgroundOpacityButton = new OpacityButton(OnBackOpacityChanged))
												}
											},
											new WrapPanel {
												Margin = WpfHelper.SmallMargin,
												Children = {
													new TextBlock { Text = "Background effect: ", Width = 130 },
													(_BackgroundEffectBox = new ComboBox { Width = 160 }.ReferenceStyle(VsResourceKeys.ComboBoxStyleKey))
												}
											},
											(_BaseTypesList = new WrapPanel {
												Margin = WpfHelper.SmallMargin,
												Children = {
													new TextBlock { Text = "Base syntax: " }
												}
											})
										}
									},
									Padding = new Thickness(0, 6, 0, 0)
								}).ReferenceProperty(Border.BorderBrushProperty, VsBrushes.PanelBorderKey)
								.SetValue(Grid.SetRow, 2),

								(_TagSettingsGroup = new Border {
									Visibility = Visibility.Collapsed,
									Child = new StackPanel {
										Children = {
											new TextBlock { Text = "Comment tag settings:", FontWeight = FontWeights.Bold },
											new WrapPanel {
												Margin = WpfHelper.SmallMargin,
												Children = {
													new StackPanel {
														Orientation = Orientation.Horizontal,
														Children = {
															new TextBlock { Text = "Tag: ", Width = 60, Margin = WpfHelper.SmallMargin },
															(_TagBox = new TextBox() { Width = 230, Margin = WpfHelper.SmallMargin }.ReferenceStyle(VsResourceKeys.TextBoxStyleKey))
														}
													},
													new StackPanel {
														Orientation = Orientation.Horizontal,
														Children = {
															new TextBlock { Text = "Style: ", Width = 60, Margin = WpfHelper.SmallMargin },
															(_TagStyleBox = new ComboBox { Width = 230, Margin = WpfHelper.SmallMargin, IsEditable = false }.ReferenceStyle(VsResourceKeys.ComboBoxStyleKey))
														}
													}
												}
											},
											new WrapPanel {
												Margin = WpfHelper.SmallMargin,
												Children = {
													(_TagCaseSensitiveBox = new StyleCheckBox("Case sensitive", OnTagCaseSensitiveChanged) { IsThreeState = false }),
													(_TagHasPunctuationBox = new StyleCheckBox("May end with punctuation", OnTagHasPunctuationChanged) { IsThreeState = false }),
												}
											},
											new WrapPanel {
												Margin = WpfHelper.SmallMargin,
												Children = {
													new TextBlock { Text = "Apply on:", Width = 60, Margin = WpfHelper.SmallMargin },
													(_TagApplyOnTagBox = new RadioBox("Tag", "TagApplication", OnTagApplicationChanged)),
													(_TagApplyOnContentBox = new RadioBox("Content", "TagApplication", OnTagApplicationChanged)),
													(_TagApplyOnWholeBox = new RadioBox("Tag and content", "TagApplication", OnTagApplicationChanged)),
												}
											},
										}
									},
									Padding = new Thickness(0, 6, 0, 0)
								}).SetValue(Grid.SetRow, 2),

								(_Notice = new TextBlock {
									Text = "To configure other syntax styles, click on the corresponding place in the code document window.",
									TextWrapping = TextWrapping.Wrap
								}).SetValue(Grid.SetRow, 2),

								new WrapPanel {
									HorizontalAlignment = HorizontalAlignment.Right,
									Children = {
										new ThemedButton("Save", "Confirm changes", Ok) { IsDefault = true, Width = 80, Margin = new Thickness(10) }.ReferenceStyle(VsResourceKeys.ButtonStyleKey),
										new ThemedButton("Cancel", "Undo changes", Cancel) { Width = 80, Margin = new Thickness(10) }.ReferenceStyle(VsResourceKeys.ButtonStyleKey)
									}
								}.SetValue(Grid.SetRow, 3)
							}
						}
						.SetValue(Grid.SetColumn, 1)
					}
				}.ReferenceProperty(TextBlock.ForegroundProperty, VsBrushes.ToolWindowTextKey)
			}.ReferenceProperty(Border.BackgroundProperty, VsBrushes.ToolWindowBackgroundKey);
			SetFormatMap(wpfTextView);
			_SyntaxSourceBox.SelectedIndex = 0;
			_FontSizeBox.ValueChanged += ApplyFontSize;
			LoadSyntaxStyles(SyntaxStyleSource.Selection);
			_BackgroundEffectBox.Items.AddRange(new[] { "Solid", "Bottom gradient", "Top gradient", "Right gradient", "Left gradient" });
			_BackgroundEffectBox.SelectionChanged += OnBackgroundEffectChanged;
			_SyntaxSourceBox.SelectionChanged += SyntaxSourceChanged;
			_AddTagButton.Click += AddTag;
			_RemoveTagButton.Click += RemoveTag;
			_TagBox.TextChanged += ApplyTag;
			Config.Instance.BeginUpdate();
		}

		public bool IsClosing { get; private set; }
		internal StyleBase ActiveStyle => _SelectedStyleButton?.StyleSettings;

		#region List initializers
		void InitTagStyleBox() {
			var c = _TagStyleBox.Items;
			var t = typeof(CommentStyleTypes);
			foreach (var item in Enum.GetNames(t)) {
				var d = t.GetField(item).GetCustomAttribute<ClassificationTypeAttribute>()?.ClassificationTypeNames;
				if (d == null || d.StartsWith(Constants.CodistPrefix, StringComparison.Ordinal) == false) {
					continue;
				}
				c.Add(item);
			}
			_TagStyleBox.SelectionChanged += ApplyTagStyle;
		}
		#endregion

		#region Syntax settings loader
		// todo suppress unnecessary refresh when menus for style setting buttons are closed
		protected override void OnActivated(EventArgs e) {
			base.OnActivated(e);
			var view = TextEditorHelper.GetActiveWpfDocumentView();
			if (_WpfTextView != view) {
				_WpfTextView = view;
				SetFormatMap(view);
			}
			_SettingsList.IsEnabled = _SettingsGroup.IsEnabled = true;
			System.Diagnostics.Debug.WriteLine("Refresh style config box");
			if (_SyntaxSourceBox.SelectedIndex == 0) {
				LoadSyntaxStyles(SyntaxStyleSource.Selection);
			}
		}
		protected override void OnDeactivated(EventArgs e) {
			base.OnDeactivated(e);
			_SettingsList.IsEnabled = _SettingsGroup.IsEnabled = false;
		}

		void SetFormatMap(ITextView view) {
			if (_FormatMap != null) {
				_FormatMap.ClassificationFormatMappingChanged -= RefreshList;
			}
			_FormatMap = view != null
				? ServicesHelper.Instance.ClassificationFormatMap.GetClassificationFormatMap(view)
				: ServicesHelper.Instance.ClassificationFormatMap.GetClassificationFormatMap("text");
			_FormatMap.ClassificationFormatMappingChanged += RefreshList;
			UpdateDefaultFormat();
		}

		void UpdateDefaultFormat() {
			var p = _FormatMap.DefaultTextProperties;
			if (p != _DefaultFormat) {
				_DefaultFormat = p;
				_SettingsList.SetValue(TextBlock.SetFontFamily, p.Typeface.FontFamily)
							.SetValue(TextBlock.SetFontSize, p.FontRenderingEmSize);
			}
		}

		void SyntaxSourceChanged(object source, RoutedEventArgs args) {
			if (_SyntaxSourceBox.SelectedValue is IControlProvider c) {
				_Notice.Visibility = _SettingsGroup.Visibility = _TagSettingsGroup.Visibility = _AddTagButton.Visibility = _RemoveTagButton.Visibility = Visibility.Collapsed;
				_OptionPageHolder.Child = c.Control;
			}
			else {
				_SettingsList.Visibility = Visibility.Visible;
				_OptionPageHolder.Child = _SettingsList;
				var style = ((ClassificationCategoryItem)_SyntaxSourceBox.SelectedValue).Style;
				if (style == SyntaxStyleSource.CommentLabels) {
					LoadCommentLabels();
				}
				else {
					LoadSyntaxStyles(style);
				}
			}
		}

		void LoadCommentLabels() {
			var l = _SettingsList.Children;
			l.Clear();
			_AddTagButton.Visibility = Visibility.Visible;
			_Notice.Visibility = _SettingsGroup.Visibility = Visibility.Collapsed;
			_SelectedStyleButton = null;
			var tag = _SelectedCommentTag;
			foreach (var label in Config.Instance.Labels) {
				var button = CreateButtonForLabel(label);
				if (button == null) {
					continue;
				}
				if (tag != null && label == tag) {
					button.IsChecked = true;
					_SelectedStyleButton = button;
					_RemoveTagButton.Visibility = _TagSettingsGroup.Visibility = Visibility.Visible;
					tag = null;
				}
				l.Add(button);
			}

			if (_SelectedStyleButton == null) {
				_RemoveTagButton.Visibility = _TagSettingsGroup.Visibility = Visibility.Collapsed;
			}

			if (l.Count == 0) {
				l.Add(new TextBlock {
					Text = "No comment tag is defined. Use the Add button to add new comment tag definitions.",
					FontSize = 20,
					TextWrapping = TextWrapping.Wrap
				});
				_RemoveTagButton.Visibility = _TagSettingsGroup.Visibility = Visibility.Collapsed;
			}
		}

		StyleSettingsButton CreateButtonForLabel(CommentLabel label) {
			var s = FindCommentStyle(label);
			if (s == null) {
				return null;
			}
			var c = ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationType(s.ClassificationType);
			if (c == null) {
				return null;
			}
			var t = _FormatMap.GetTextProperties(c);
			if (t == null) {
				return null;
			}
			return new StyleSettingsButton(c, t, OnSelectTag) { Text = label.Label, Tag = label };

			Codist.SyntaxHighlight.CommentStyle FindCommentStyle(CommentLabel cl) {
				var styleId = cl.StyleID;
				return Config.Instance.CommentStyles.Find(i => i.StyleID == styleId);
			}
		}

		void LoadSyntaxStyles(SyntaxStyleSource source) {
			var l = _SettingsList.Children;
			l.Clear();
			_Notice.Visibility = _SettingsList.Visibility != Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;
			_AddTagButton.Visibility = _RemoveTagButton.Visibility = _TagSettingsGroup.Visibility = Visibility.Collapsed;
			IEnumerable<IClassificationType> classifications;
			switch (source) {
				case SyntaxStyleSource.Common: classifications = ToClassificationTypes(Config.Instance.GeneralStyles); break;
				case SyntaxStyleSource.CSharp: classifications = ToClassificationTypes(Config.Instance.CodeStyles); break;
				case SyntaxStyleSource.CSharpSymbolMarker: classifications = ToClassificationTypes(Config.Instance.SymbolMarkerStyles); break;
				case SyntaxStyleSource.CPlusPlus: classifications = ToClassificationTypes(Config.Instance.CppStyles); break;
				case SyntaxStyleSource.Markdown: classifications = ToClassificationTypes(Config.Instance.MarkdownStyles); break;
				case SyntaxStyleSource.Xml: classifications = ToClassificationTypes(Config.Instance.XmlCodeStyles); break;
				case SyntaxStyleSource.CommentTagger: classifications = ToClassificationTypes(Config.Instance.CommentStyles); break;
				case SyntaxStyleSource.PriorityOrder: classifications = _FormatMap.CurrentPriorityOrder; break;
				case SyntaxStyleSource.Selection:
				default:
					if (_WpfTextView == null) {
						_SettingsGroup.Visibility = Visibility.Collapsed;
						return;
					}
					classifications = GetClassificationsForSelection();
					break;
			}
			var cts = new HashSet<IClassificationType>();
			var style = ActiveStyle;
			_SelectedStyleButton = null;
			foreach (var c in classifications) {
				if (c is ClassificationCategory) {
					l.Add(new Label {
						Content = c.Classification,
						Padding = WpfHelper.SmallMargin,
						Margin = WpfHelper.TopItemMargin,
						FontWeight = FontWeights.Bold,
					}.ReferenceProperty(ForegroundProperty, VsBrushes.EditorExpansionTextKey)
					.ReferenceProperty(BackgroundProperty, CommonDocumentColors.PageBrushKey));
					continue;
				}
				if (c == null || cts.Add(c) == false) {
					continue;
				}
				var t = _FormatMap.GetTextProperties(c);
				if (t == null) {
					continue;
				}
				var button = new StyleSettingsButton(c, t, OnSelectStyle);
				if (style != null && c.Classification == style.ClassificationType) {
					button.IsChecked = true;
					_SelectedStyleButton = button;
					_SettingsGroup.Visibility = Visibility.Visible;
					style = null;
				}
				l.Add(button);
			}
			if (_SelectedStyleButton == null) {
				_SettingsGroup.Visibility = Visibility.Collapsed;
			}
			if (l.Count == 0) {
				l.Add(new TextBlock {
					Text = source == SyntaxStyleSource.Selection
						? "No syntax highlight is applied to active position in code window."
						: source == SyntaxStyleSource.CommentLabels
						? "No comment tag is defined. Use the Add button to add new comment tag definitions."
						: "No syntax highlight is defined for selected syntax type. You need to install corresponding feature with the Visual Studio installer.",
					FontSize = 20,
					TextWrapping = TextWrapping.Wrap
				});
				_SettingsGroup.Visibility = Visibility.Collapsed;
			}
		}

		static IEnumerable<IClassificationType> ToClassificationTypes<TStyle>(List<TStyle> styles)
			where TStyle : StyleBase {
			var r = new List<IClassificationType>(styles.Count + 4);
			string category = null;
			var ctr = ServicesHelper.Instance.ClassificationTypeRegistry;
			foreach (var item in styles) {
				if (item.Category.Length == 0) {
					continue;
				}
				var style = styles.FirstOrDefault(i => i.Id == item.Id) ?? item;
				if (item.Category != category) {
					r.Add(new ClassificationCategory(category = item.Category));
				}
				r.Add(ctr.GetClassificationType(item.ClassificationType));
			}
			return r;
		}
		IEnumerable<IClassificationType> GetClassificationsForSelection() {
			if (_WpfTextView == null) {
				return Array.Empty<IClassificationType>();
			}
			if (_WpfTextView.TryGetFirstSelectionSpan(out var span) == false) {
				span = ServicesHelper.Instance.TextStructureNavigator
					.CreateTextStructureNavigator(_WpfTextView.TextBuffer, _WpfTextView.TextBuffer.ContentType)
					.GetExtentOfWord(_WpfTextView.Caret.Position.BufferPosition)
					.Span;
			}
			var classifications = ServicesHelper.Instance.ViewTagAggregatorFactory
				.CreateTagAggregator<Microsoft.VisualStudio.Text.Tagging.IClassificationTag>(_WpfTextView)
				.GetTags(span)
				.Select(t => t.Tag.ClassificationType)
				.ToList();
			return classifications
				.Union(classifications.SelectMany(i => i.BaseTypes))
				.Where(t => t.IsOfType("(TRANSIENT)") == false) // remove transient classification types
				.ToList();
		}

		void OnSelectTag(object sender, RoutedEventArgs e) {
			var b = sender as StyleSettingsButton;
			if (b == _SelectedStyleButton) {
				return;
			}
			b.IsChecked = true;
			if (_SelectedStyleButton != null) {
				_SelectedStyleButton.IsChecked = false;
			}
			else {
				_RemoveTagButton.Visibility = _TagSettingsGroup.Visibility = Visibility.Visible;
				if (_TagStyleBox.Items.Count == 0) {
					InitTagStyleBox();
				}
				_Notice.Visibility = Visibility.Collapsed;
			}
			_SelectedStyleButton = b;
			var t = _SelectedCommentTag = b.Tag as CommentLabel;
			try {
				_Lock.Lock();
				_TagBox.Text = t.Label;
				_TagCaseSensitiveBox.IsChecked = t.IgnoreCase == false;
				_TagHasPunctuationBox.IsChecked = t.AllowPunctuationDelimiter;
				var s = t.StyleID.ToString();
				for (int n = 0; n < _TagStyleBox.Items.Count; n++) {
					if ((string)_TagStyleBox.Items[n] == s) {
						_TagStyleBox.SelectedIndex = n;
						break;
					}
				}
				switch (t.StyleApplication) {
					case CommentStyleApplication.Content: _TagApplyOnContentBox.IsChecked = true; break;
					case CommentStyleApplication.TagAndContent: _TagApplyOnWholeBox.IsChecked = true; break;
					default: _TagApplyOnTagBox.IsChecked = true; break;
				}
			}
			finally {
				_Lock.Unlock();
			}
		}
		void OnSelectStyle(object sender, RoutedEventArgs e) {
			var b = sender as StyleSettingsButton;
			if (b == _SelectedStyleButton) {
				return;
			}
			b.IsChecked = true;
			if (_SelectedStyleButton != null) {
				_SelectedStyleButton.IsChecked = false;
			}
			else {
				_SettingsGroup.Visibility = Visibility.Visible;
				_Notice.Visibility = Visibility.Collapsed;
			}
			_SelectedStyleButton = b;
			try {
				_Lock.Lock();
				var s = b.StyleSettings;
				_FontButton.Value = s.Font;
				_FontSizeBox.Value = (int)(s.FontSize > 100 ? 100 : s.FontSize < -10 ? -10 : s.FontSize);
				_BoldBox.IsChecked = s.Bold;
				_ItalicBox.IsChecked = s.Italic;
				_UnderlineBox.IsChecked = s.Underline;
				_StrikethroughBox.IsChecked = s.Strikethrough;
				_ForegroundButton.Color = s.ForeColor;
				_BackgroundButton.Color = s.BackColor;
				_ForegroundButton.DefaultColor = () => ((_FormatMap.GetTextProperties(b.Classification)?.ForegroundBrush as SolidColorBrush)?.Color).GetValueOrDefault();
				_BackgroundButton.DefaultColor = () => ((_FormatMap.GetTextProperties(b.Classification)?.BackgroundBrush as SolidColorBrush)?.Color).GetValueOrDefault();
				_ForegroundOpacityButton.Value = s.ForegroundOpacity;
				_BackgroundOpacityButton.Value = s.BackgroundOpacity;
				_BackgroundEffectBox.SelectedIndex = (int)s.BackgroundEffect;
				_BaseTypesList.Children.RemoveRange(1, _BaseTypesList.Children.Count - 1);
				if (s.ClassificationType != null) {
					var t = ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationType(s.ClassificationType);
					if (t != null) {
						bool m = false;
						foreach (var item in t.GetBaseTypes()) {
							if (m == false) {
								m = true;
							}
							else {
								_BaseTypesList.Add(new TextBlock { Text = ", " });
							}
							_BaseTypesList.Add(new TextBlock { Text = item.Classification }.ReferenceProperty(ForegroundProperty, VsBrushes.ControlLinkTextKey));
						}
					}
				}
			}
			finally {
				_Lock.Unlock();
			}
		}

		void RefreshList(object sender, EventArgs e) {
			_SettingsList.ForEachChild((StyleSettingsButton c) => c.Refresh(_FormatMap));
			UpdateDefaultFormat();
		}
		#endregion

		#region Update handlers
		void Update(Func<bool> updateAction) {
			_Lock.DoWithLock(() => {
				if (updateAction()) {
					NotifySyntaxChange();
				}
			});
		}
		void NotifySyntaxChange() {
			Config.Instance.FireConfigChangedEvent(Features.SyntaxHighlight, ActiveStyle.ClassificationType);
		}
		void ApplyFont(string font) {
			Update(() => {
				if (ActiveStyle.Font != font) {
					ActiveStyle.Font = font;
					return true;
				}
				return false;
			});
		}
		void ApplyFontSize(object sender, DependencyPropertyChangedEventArgs e) {
			Update(() => {
				double s = _FontSizeBox.Value;
				if (ActiveStyle.FontSize != s) {
					ActiveStyle.FontSize = s;
					return true;
				}
				return false;
			});
		}

		void OnBoldChanged(bool? state) {
			Update(() => {
				ActiveStyle.Bold = state;
				return true;
			});
		}
		void OnItalicChanged(bool? state) {
			Update(() => {
				ActiveStyle.Italic = state;
				return true;
			});
		}
		void OnUnderlineChanged(bool? state) {
			Update(() => {
				ActiveStyle.Underline = state;
				return true;
			});
		}
		void OnStrikeThroughChanged(bool? state) {
			Update(() => {
				ActiveStyle.Strikethrough = state;
				return true;
			});
		}

		void OnForeColorChanged(Color color) {
			Update(() => {
				ActiveStyle.ForeColor = color;
				return true;
			});
		}

		void OnBackColorChanged(Color color) {
			Update(() => {
				ActiveStyle.BackColor = color;
				return true;
			});
		}

		void OnForeOpacityChanged(byte value) {
			Update(() => {
				ActiveStyle.ForegroundOpacity = value;
				return true;
			});
		}

		void OnBackOpacityChanged(byte value) {
			Update(() => {
				ActiveStyle.BackgroundOpacity = value;
				return true;
			});
		}

		void OnBackgroundEffectChanged(object sender, EventArgs e) {
			Update(() => {
				var s = (BrushEffect)_BackgroundEffectBox.SelectedIndex;
				if (ActiveStyle.BackgroundEffect != s) {
					ActiveStyle.BackgroundEffect = s;
					return true;
				}
				return false;
			});
		}

		void AddTag(object sender, EventArgs e) {
			Update(() => {
				var label = new CommentLabel(
					_TagBox.Text.Length > 0 ? _TagBox.Text : "TAG",
					Enum.TryParse<CommentStyleTypes>((string)_TagStyleBox.SelectedValue, out var style) ? style : CommentStyleTypes.ToDo,
					_TagCaseSensitiveBox.IsChecked == false) {
					StyleApplication = _TagApplyOnContentBox.IsChecked == true ? CommentStyleApplication.Content : _TagApplyOnContentBox.IsChecked == true ? CommentStyleApplication.TagAndContent : CommentStyleApplication.Tag,
					AllowPunctuationDelimiter = _TagHasPunctuationBox.IsChecked == true
				};
				Config.Instance.Labels.Insert(0, label);
				var b = CreateButtonForLabel(label);
				_SettingsList.Children.Insert(0, b);
				_SettingsList.GetParent<ScrollViewer>().ScrollToVerticalOffset(0);
				b.PerformClick();
				return true;
			});
		}

		void RemoveTag(object sender, EventArgs e) {
			Update(() => {
				if (_SelectedStyleButton != null) {
					var i = _SettingsList.Children.IndexOf(_SelectedStyleButton);
					if (i >= 0) {
						_SettingsList.Children.RemoveAt(i);
						Config.Instance.Labels.RemoveAt(i);
					}
				}
				return true;
			});
		}

		void ApplyTag(object sender, EventArgs e) {
			Update(() => {
				var s = _TagBox.Text;
				if (_SelectedCommentTag != null
					&& _SelectedCommentTag.Label != s) {
					_SelectedStyleButton.Text = _SelectedCommentTag.Label = s;
					return true;
				}
				return false;
			});
		}

		void ApplyTagStyle(object sender, EventArgs e) {
			Update(() => {
				var s = (string)_TagStyleBox.SelectedValue;
				if (_SelectedCommentTag != null
					&& Enum.TryParse<CommentStyleTypes>(s, out var style)
					&& _SelectedCommentTag.StyleID != style) {
					_SelectedCommentTag.StyleID = style;
					var i = _SettingsList.Children.IndexOf(_SelectedStyleButton);
					if (i >= 0) {
						_SettingsList.Children.RemoveAt(i);
						_SettingsList.Children.Insert(i, _SelectedStyleButton = CreateButtonForLabel(_SelectedCommentTag));
						_SelectedStyleButton.IsChecked = true;
					}
					return true;
				}
				return false;
			});
		}

		void OnTagCaseSensitiveChanged(bool? value) {
			Update(() => {
				if (_SelectedCommentTag != null
					&& _SelectedCommentTag.IgnoreCase != value == false) {
					_SelectedCommentTag.IgnoreCase = value == false;
					return true;
				}
				return false;
			});
		}

		void OnTagHasPunctuationChanged(bool? value) {
			Update(() => {
				if (_SelectedCommentTag != null
					&& _SelectedCommentTag.AllowPunctuationDelimiter != value) {
					_SelectedCommentTag.AllowPunctuationDelimiter = value.Value;
					return true;
				}
				return false;
			});
		}
		void OnTagApplicationChanged(RadioBox sender) {
			Update(() => {
				if (_SelectedCommentTag != null) {
					var a = sender == _TagApplyOnTagBox ? CommentStyleApplication.Tag
					: sender == _TagApplyOnContentBox ? CommentStyleApplication.Content
					: CommentStyleApplication.TagAndContent;
					if (_SelectedCommentTag.StyleApplication != a) {
						_SelectedCommentTag.StyleApplication = a;
						return true;
					}
				}
				return false;
			});
		}
		#endregion

		#region Theme management
		void LoadTheme() {
			var d = new OpenFileDialog {
				Title = "Load Codist syntax highlight setting file...",
				FileName = "Codist.styles",
				DefaultExt = "styles",
				Filter = "Codist syntax highlight setting file|*.styles|All files|*.*"
			};
			if (d.ShowDialog() == true) {
				try {
					LoadTheme(d.FileName);
				}
				catch (Exception ex) {
					MessageBox.Show("Error occured while loading style file: " + ex.Message, nameof(Codist));
				}
			}
		}
		void SaveTheme() {
			var d = new SaveFileDialog {
				Title = "Save Codist syntax highlight setting file...",
				FileName = "Codist.styles",
				DefaultExt = "styles",
				Filter = "Codist syntax highlight setting file|*.styles|All files|*.*"
			};
			if (d.ShowDialog() == true) {
				Config.Instance.SaveConfig(d.FileName, true);
			}
		}
		void ResetTheme() {
			if (MessageBox.Show("Do you want to reset the syntax highlight settings to default?", nameof(Codist), MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
				Config.ResetStyles();
			}
		}
		static void LoadTheme(string path) {
			Config.LoadConfig(path, StyleFilters.All);
		}
		#endregion

		protected override void OnClosed(EventArgs e) {
			Owner.Activate();
			_FormatMap.ClassificationFormatMappingChanged -= RefreshList;
			base.OnClosed(e);
		}
		protected override void OnClosing(CancelEventArgs e) {
			if (IsClosing == false && Config.Instance.IsChanged) {
				IsClosing = true;
				var r = MessageBox.Show("The configuration has been changed, do you want to save it?", nameof(Codist), MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
				switch (r) {
					case MessageBoxResult.Yes:
						Config.Instance.EndUpdate(true); break;
					case MessageBoxResult.No:
						Config.Instance.EndUpdate(false); break;
					default:
						IsClosing = false;
						e.Cancel = true; break;
				}
			}
			base.OnClosing(e);
		}
		void Ok() {
			IsClosing = true;
			Config.Instance.EndUpdate(true);
			Close();
		}
		void Cancel() {
			IsClosing = true;
			Close();
			Config.Instance.EndUpdate(false);
		}

		sealed class StyleSettingsButton : Button
		{
			readonly CheckBox _Selector;
			readonly TextBlock _Preview;
			readonly IClassificationType _Classification;
			readonly StyleBase _Style;

			public StyleSettingsButton(IClassificationType ct, TextFormattingRunProperties t, RoutedEventHandler clickHandler) {
				_Classification = ct;
				_Style = FormatStore.GetOrCreateStyle(_Classification);
				HorizontalContentAlignment = HorizontalAlignment.Stretch;
				Content = new StackPanel {
					Orientation = Orientation.Horizontal,
					Children = {
						(_Selector = new CheckBox {
							VerticalAlignment = VerticalAlignment.Center,
							Margin = WpfHelper.GlyphMargin,
							IsEnabled = false
						}).ReferenceStyle(VsResourceKeys.CheckBoxStyleKey),
						(_Preview = new TextBlock {
							Text = _Classification.Classification,
							Margin = WpfHelper.SmallMargin,
							VerticalAlignment = VerticalAlignment.Center,
							TextWrapping = TextWrapping.NoWrap
						})
					},
				};
				PreviewLabelStyle(_Preview, t);
				this.ReferenceStyle(VsResourceKeys.ButtonStyleKey)
					.ReferenceProperty(BackgroundProperty, CommonDocumentColors.PageBrushKey);
				Click += clickHandler;
			}

			public bool IsChecked {
				get => _Selector.IsChecked == true;
				set => _Selector.IsChecked = value;
			}
			public string Text {
				get => _Preview.Text;
				set => _Preview.Text = value;
			}
			public IClassificationType Classification => _Classification;
			public StyleBase StyleSettings => _Style;

			public void PerformClick() {
				OnClick();
			}
			public void Refresh(IClassificationFormatMap formatMap) {
				_Preview.ClearValues(TextBlock.ForegroundProperty, TextBlock.BackgroundProperty,
					TextBlock.FontFamilyProperty, TextBlock.FontSizeProperty,
					TextBlock.FontStyleProperty, TextBlock.FontWeightProperty,
					TextBlock.TextDecorationsProperty);
				PreviewLabelStyle(_Preview, formatMap.GetTextProperties(_Classification));
			}

			static TextBlock PreviewLabelStyle(TextBlock label, TextFormattingRunProperties format) {
				if (format.ForegroundBrushEmpty == false) {
					label.Foreground = format.ForegroundBrush;
				}
				if (format.BackgroundBrushEmpty == false) {
					label.Background = format.BackgroundBrush;
				}
				if (format.TypefaceEmpty == false) {
					label.FontFamily = format.Typeface.FontFamily;
				}
				if (format.FontRenderingEmSizeEmpty == false) {
					label.FontSize = format.FontRenderingEmSize;
				}
				if (format.ItalicEmpty == false) {
					label.FontStyle = format.Italic ? FontStyles.Italic : FontStyles.Normal;
				}
				if (format.BoldEmpty == false) {
					label.FontWeight = format.Bold ? FontWeights.Bold : FontWeights.Normal;
				}
				if (format.TextDecorationsEmpty == false) {
					label.TextDecorations = format.TextDecorations;
				}
				return label;
			}
		}

		sealed class StyleCheckBox : CheckBox
		{
			readonly Action<bool?> _CheckHandler;

			public StyleCheckBox(string text, Action<bool?> checkHandler) {
				Content = text;
				IsThreeState = true;
				Margin = WpfHelper.SmallMargin;
				MinWidth = 120;
				this.ReferenceStyle(VsResourceKeys.CheckBoxStyleKey);
				Checked += CheckHandler;
				Unchecked += CheckHandler;
				Indeterminate += CheckHandler;
				_CheckHandler = checkHandler;
			}

			void CheckHandler(object sender, EventArgs args) {
				_CheckHandler(IsChecked);
			}
		}

		sealed class RadioBox : RadioButton
		{
			readonly Action<RadioBox> _CheckHandler;

			public RadioBox(string text, string group, Action<RadioBox> checkHandler) {
				Content = text;
				GroupName = group;
				Margin = WpfHelper.SmallMargin;
				MinWidth = 60;
				this.ReferenceStyle(VsResourceKeys.ThemedDialogRadioButtonStyleKey);
				Checked += CheckHandler;
				_CheckHandler = checkHandler;
			}

			void CheckHandler(object sender, EventArgs args) {
				_CheckHandler(this);
			}
		}

		sealed class FontButton : Button
		{
			readonly Action<string> _FontChangedHandler;
			string _Font;

			public FontButton(Action<string> fontChanged) {
				_FontChangedHandler = fontChanged;
				Content = "Not set";
			}
			public string Value {
				get => _Font as string;
				set {
					if (_Font != value) {
						_Font = value;
						Content = String.IsNullOrWhiteSpace(value) ? "Not set" : value;
						_FontChangedHandler(value);
					}
				}
			}
			protected override void OnClick() {
				base.OnClick();
				if (ContextMenu == null) {
					ContextMenu = new ContextMenu {
						Resources = SharedDictionaryManager.ContextMenu,
						MaxHeight = 300,
						Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
						PlacementTarget = this,
						ItemsSource = new[] { new ThemedMenuItem(-1, "Not set", SetFont) { Tag = null } }
							.Concat(WpfHelper.GetInstalledFonts().Select(f => new ThemedMenuItem(-1, f.Name, SetFont) { Tag = f.Name }))
							//.Concat(WpfHelper.GetInstalledFonts()
							//	.SelectMany(f => new[] { new ThemedMenuItem(-1, f.Name, SetFont) { Tag = f.Name } }
							//		.Concat(f.ExtraTypefaces.Where(t => t.Style == FontStyles.Normal).Select(t => {
							//			var n = t.GetTypefaceAdjustedName();
							//			return new ThemedMenuItem(-1, f.Name + " " + n, SetFont) { Tag = f.Name + " " + n };
							//		}))
							//	)
							//)
					};
					ContextMenu.SetBackgroundForCrispImage(ThemeHelper.TitleBackgroundColor);
				}
				ContextMenu.IsOpen = true;
			}

			void SetFont(object sender, RoutedEventArgs e) {
				var m = e.Source as ThemedMenuItem;
				var prev = ContextMenu.Items.GetFirst<ThemedMenuItem>(i => i.Tag as string == Value);
				if (prev != m) {
					prev.Highlight(false);
					m.Highlight(true);
					Value = m.Tag as string;
				}
			}
		}
		sealed class ColorButton : Button
		{
			readonly Border _Border;
			readonly Action<Color> _ColorChangedHandler;

			public ColorButton(Color color, string text, Action<Color> colorChangedHandler) {
				Content = new StackPanel {
					Orientation = Orientation.Horizontal,
					Children = {
						(_Border = new Border {
							Background = new SolidColorBrush(color),
							BorderThickness = WpfHelper.TinyMargin,
							Width = 16, Height = 16,
							Margin = WpfHelper.GlyphMargin
						}.ReferenceProperty(Border.BorderBrushProperty, VsBrushes.CommandBarMenuIconBackgroundKey)),
						new TextBlock { Text = text }
					}
				};
				Width = 120;
				Margin = WpfHelper.SmallMargin;
				this.ReferenceStyle(VsResourceKeys.ButtonStyleKey);
				_ColorChangedHandler = colorChangedHandler;
			}
			public Func<Color> DefaultColor { get; set; }
			public Color Color {
				get => (_Border.Background as SolidColorBrush).Color;
				set {
					if (Color != value) {
						_Border.Background = new SolidColorBrush(value);
						_ColorChangedHandler(value);
					}
				}
			}

			protected override void OnClick() {
				base.OnClick();
				if (ContextMenu == null) {
					ContextMenu = new ContextMenu {
						Resources = SharedDictionaryManager.ContextMenu,
						Items = {
							new ThemedMenuItem(KnownImageIds.ColorDialog, "Pick color...", PickColor),
							new ThemedMenuItem(KnownImageIds.EmptyBucket, "Reset color", ResetColor),
							new ThemedMenuItem(KnownImageIds.Copy, "Copy color", CopyColor),
							new ThemedMenuItem(KnownImageIds.Paste, "Paste color", PasteColor),
						},
						Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
						PlacementTarget = this
					};
					ContextMenu.SetBackgroundForCrispImage(ThemeHelper.TitleBackgroundColor);
				}
				ContextMenu.IsOpen = true;
			}

			void CopyColor(object sender, RoutedEventArgs e) {
				try {
					Clipboard.SetDataObject(Color.ToHexString());
				}
				catch (System.Runtime.InteropServices.ExternalException) {
					// ignore
				}
			}

			void PasteColor(object sender, RoutedEventArgs e) {
				Color = GetClipboardColor();
			}

			void ResetColor(object sender, RoutedEventArgs e) {
				Color = default;
			}

			void PickColor(object sender, RoutedEventArgs e) {
				using (var c = new System.Windows.Forms.ColorDialog() {
					FullOpen = true,
					Color = (Color.A == 0 ? DefaultColor() : Color).ToGdiColor()
				}) {
					if (c.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
						Color = c.Color.ToWpfColor();
					}
				}
			}

			static Color GetClipboardColor() {
				string c;
				try {
					c = Clipboard.GetText();
				}
				catch (System.Runtime.InteropServices.ExternalException) {
					return default;
				}
				UIHelper.ParseColor(c, out var color, out _);
				return color;
			}
		}

		sealed class OpacityButton : Button
		{
			readonly Action<byte> _OpacityChangedHandler;
			byte _Opacity;

			public OpacityButton(Action<byte> opacityChangedHandler) {
				Content = "Opacity";
				Width = 120;
				Margin = WpfHelper.SmallMargin;
				this.ReferenceStyle(VsResourceKeys.ButtonStyleKey);
				_OpacityChangedHandler = opacityChangedHandler;
			}
			public byte Value {
				get => _Opacity;
				set {
					_Opacity = value;
					Content = value == 0 ? "Opacity not set" : "Opacity: " + ((value + 1) / 16).ToString();
				}
			}
			protected override void OnClick() {
				base.OnClick();
				if (ContextMenu == null) {
					ContextMenu = new ContextMenu {
						Resources = SharedDictionaryManager.ContextMenu,
						Items = {
							new ThemedMenuItem(KnownImageIds.FillOpacity, "Default", SelectOpacity) { Tag = 0 },
						},
						MaxHeight = 300,
						Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
						PlacementTarget = this,
					};
					ContextMenu.SetBackgroundForCrispImage(ThemeHelper.TitleBackgroundColor);
					var items = new ThemedMenuItem[16];
					for (int i = 16; i > 0; i--) {
						items[16 - i] = new ThemedMenuItem(KnownImageIds.Blank, i.ToString(), SelectOpacity) { Tag = i * 16 - 1 };
					}
					ContextMenu.Items.AddRange(items);
				}
				CheckMenuItem(Value, true);
				ContextMenu.IsOpen = true;
			}

			void SelectOpacity(object sender, RoutedEventArgs e) {
				var v = (byte)(int)(e.Source as FrameworkElement).Tag;
				if (Value != v) {
					CheckMenuItem(Value, false);
					CheckMenuItem(v, true);
					_OpacityChangedHandler(Value = v);
				}
			}

			MenuItem GetMenuItem(byte v) {
				return ContextMenu.Items[v == 0 ? 0 : 1 + 16 - (v + 1) / 16] as MenuItem;
			}
			void CheckMenuItem(byte v, bool check) {
				var m = GetMenuItem(v);
				if (m != null) {
					m.IsChecked = check;
				}
			}
		}

		enum SyntaxStyleSource
		{
			Selection,
			Common,
			CSharp,
			CSharpSymbolMarker,
			CPlusPlus,
			Markdown,
			Xml,
			CommentTagger,
			CommentLabels,
			PriorityOrder
		}

		/// <summary>
		/// A dummy classification type simply to serve the purpose of grouping classification types in the configuration list
		/// </summary>
		sealed class ClassificationCategory : IClassificationType
		{
			public ClassificationCategory(string classification) {
				Classification = classification;
			}

			public string Classification { get; }
			public IEnumerable<IClassificationType> BaseTypes => null;

			public bool IsOfType(string type) { throw new NotImplementedException(); }
		}

		sealed class TagStyle
		{
			public readonly CommentStyleTypes Style;
			public readonly string Description;
			public TagStyle(CommentStyleTypes style, string description) {
				Style = style;
				Description = description;
			}
			public override string ToString() {
				return Description;
			}
		}

		sealed class ClassificationCategoryItem
		{
			public readonly SyntaxStyleSource Style;
			public readonly string Description;

			public ClassificationCategoryItem(SyntaxStyleSource style, string description) {
				Style = style;
				Description = description;
			}
			public override string ToString() {
				return Description;
			}
		}

		interface IControlProvider
		{
			Control Control { get; }
		}
		sealed class ConfigPageItem<TControl> : IControlProvider
			where TControl : Control, new()
		{
			TControl _ConfigControl;
			public Control Control => _ConfigControl ?? (_ConfigControl = new TControl());
			public readonly string Description;

			public ConfigPageItem(string description) {
				Description = description;
			}
			public override string ToString() {
				return Description;
			}
		}

		sealed class CSharpAdditionalHighlightConfigPage : ContentControl
		{
			readonly OptionBox<SpecialHighlightOptions> _MarkSpecialPunctuationBox, _HighlightDeclarationBracesBox, _HighlightParameterBracesBox, _HighlightCastParenthesesBox, _HighlightBranchBracesBox, _HighlightLoopBracesBox, _HighlightResourceBracesBox, _HighlightLocalFunctionDeclarationBox, _HighlightNonPrivateFieldDeclarationBox;
			//readonly OptionBox<SpecialHighlightOptions>[] _Options;

			public CSharpAdditionalHighlightConfigPage() {
				var o = Config.Instance.SpecialHighlightOptions;
				Content = new StackPanel { Margin = WpfHelper.SmallMargin }.Add(i => new Border { Padding = WpfHelper.SmallMargin, Child = i }, new UIElement[] {
						new TitleBox("Braces and Parentheses"),
						(_MarkSpecialPunctuationBox = o.CreateOptionBox(SpecialHighlightOptions.SpecialPunctuation, UpdateConfig, "Apply bold style to following braces")),
						new DescriptionBox("When the following checkboxes are checked, braces and parentheses will be highlighted by their semantic syntax styles"),
						(_HighlightDeclarationBracesBox = o.CreateOptionBox(SpecialHighlightOptions.DeclarationBrace, UpdateConfig, "Type and member declaration braces")),
						(_HighlightParameterBracesBox = o.CreateOptionBox(SpecialHighlightOptions.ParameterBrace, UpdateConfig, "Method parameter parentheses")),
						(_HighlightCastParenthesesBox = o.CreateOptionBox(SpecialHighlightOptions.CastBrace, UpdateConfig, "Type cast parentheses")),
						(_HighlightBranchBracesBox = o.CreateOptionBox(SpecialHighlightOptions.BranchBrace, UpdateConfig, "Branch braces and parentheses")),
						(_HighlightLoopBracesBox = o.CreateOptionBox(SpecialHighlightOptions.LoopBrace, UpdateConfig, "Loop braces and parentheses")),
						(_HighlightResourceBracesBox = o.CreateOptionBox(SpecialHighlightOptions.ResourceBrace, UpdateConfig, "Resource and exception braces and parentheses")),

						new TitleBox("Member Declaration Styles"),
						(_HighlightLocalFunctionDeclarationBox = o.CreateOptionBox(SpecialHighlightOptions.LocalFunctionDeclaration, UpdateConfig, "Apply to local functions")),
						(_HighlightNonPrivateFieldDeclarationBox = o.CreateOptionBox(SpecialHighlightOptions.NonPrivateField, UpdateConfig, "Apply to non-private fields")),

					});
				foreach (var item in new[] { _HighlightDeclarationBracesBox, _HighlightParameterBracesBox, _HighlightCastParenthesesBox, _HighlightBranchBracesBox, _HighlightLoopBracesBox, _HighlightResourceBracesBox }) {
					item.WrapMargin(SubOptionMargin);
					item.ReferenceStyle(VsResourceKeys.CheckBoxStyleKey);
				}
				_MarkSpecialPunctuationBox.ReferenceStyle(VsResourceKeys.CheckBoxStyleKey);
				_HighlightLocalFunctionDeclarationBox.ReferenceStyle(VsResourceKeys.CheckBoxStyleKey);
				_HighlightNonPrivateFieldDeclarationBox.ReferenceStyle(VsResourceKeys.CheckBoxStyleKey);
				//_Options = new[] { _MarkSpecialPunctuationBox, _HighlightDeclarationBracesBox, _HighlightParameterBracesBox, _HighlightCastParenthesesBox, _HighlightBranchBracesBox, _HighlightLoopBracesBox, _HighlightResourceBracesBox, _HighlightLocalFunctionDeclarationBox, _HighlightNonPrivateFieldDeclarationBox };
			}

			void UpdateConfig(SpecialHighlightOptions options, bool set) {
				Config.Instance.Set(options, set);
				Config.Instance.FireConfigChangedEvent(Features.SyntaxHighlight);
			}
		}
	}
}
