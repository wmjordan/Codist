using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Codist.Controls;
using Codist.SyntaxHighlight;
using Codist.Taggers;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.Win32;
using R = Codist.Properties.Resources;

namespace Codist.Options
{
	sealed class SyntaxHighlightCustomizationWindow : Window
	{
		static readonly Thickness SubOptionMargin = new Thickness(24, 0, 0, 0);
		static readonly IClassificationType __BraceMatchingClassificationType = ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationType(Constants.CodeBraceMatching);
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
		readonly ComboBox _StretchBox;
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
			Title = R.T_SyntaxHighlightConfigurations;
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
									Text = R.T_SyntaxCategories,
									Height = 20,
									FontWeight = FontWeights.Bold
								},
								new ListBox {
									Items = {
										new ClassificationCategoryItem(SyntaxStyleSource.Selection, R.T_SelectedCode),
										new ClassificationCategoryItem(SyntaxStyleSource.Common, R.T_Common),
										new ClassificationCategoryItem(SyntaxStyleSource.CSharp, "C#"),
										new ClassificationCategoryItem(SyntaxStyleSource.CSharpSymbolMarker, "   "+R.T_SymbolMarkers),
										new ConfigPageItem<CSharpAdditionalHighlightConfigPage>("   "+R.T_Options),
										new ClassificationCategoryItem(SyntaxStyleSource.CPlusPlus, "C++"),
										new ClassificationCategoryItem(SyntaxStyleSource.Markdown, "Markdown"),
										new ClassificationCategoryItem(SyntaxStyleSource.Xml, "XML"),
										new ClassificationCategoryItem(SyntaxStyleSource.CommentTagger, R.T_TaggedComments),
										new ClassificationCategoryItem(SyntaxStyleSource.CommentLabels, "   "+R.T_Tags),
										new ClassificationCategoryItem(SyntaxStyleSource.PriorityOrder, R.T_AllLanguages)
									}
								}.Set(ref _SyntaxSourceBox).ReferenceStyle(VsResourceKeys.ThemedDialogListBoxStyleKey),
								new TextBlock {
									Text = R.T_Themes,
									Height = 20,
									FontWeight = FontWeights.Bold,
									Margin = WpfHelper.TopItemMargin
								},
								new ThemedButton(IconIds.Load, R.CMD_Load, R.CMDT_LoadTheme, LoadTheme) { HorizontalContentAlignment = HorizontalAlignment.Left }.Set(ref _LoadThemeButton).ReferenceStyle(VsResourceKeys.ButtonStyleKey),
								new ThemedButton(IconIds.SaveAs, R.CMD_Save, R.CMDT_SaveTheme, SaveTheme) { HorizontalContentAlignment = HorizontalAlignment.Left }.Set(ref _SaveThemeButton).ReferenceStyle(VsResourceKeys.ButtonStyleKey),
								new ThemedButton(IconIds.ResetTheme, R.CMD_Reset, R.CMDT_ResetTheme, ResetTheme) { HorizontalContentAlignment = HorizontalAlignment.Left }.Set(ref _ResetThemeButton).ReferenceStyle(VsResourceKeys.ButtonStyleKey),
								new TextBlock {
									Text = R.T_PredefinedThemes,
									Height = 20,
									FontWeight = FontWeights.Bold,
									Margin = WpfHelper.TopItemMargin
								},
								new ThemedButton(IconIds.SyntaxTheme, R.CMD_LightTheme, R.CMDT_LightTheme, () => LoadTheme(Config.LightTheme)) { HorizontalContentAlignment = HorizontalAlignment.Left }.ReferenceStyle(VsResourceKeys.ButtonStyleKey),
								new ThemedButton(IconIds.SyntaxTheme, R.CMD_PaleLightTheme, R.CMDT_PaleLightTheme, () => LoadTheme(Config.PaleLightTheme)) { HorizontalContentAlignment = HorizontalAlignment.Left }.ReferenceStyle(VsResourceKeys.ButtonStyleKey),
								new ThemedButton(IconIds.SyntaxTheme, R.CMD_DarkTheme, R.CMDT_DarkTheme, () => LoadTheme(Config.DarkTheme)) { HorizontalContentAlignment = HorizontalAlignment.Left }.ReferenceStyle(VsResourceKeys.ButtonStyleKey),
								new ThemedButton(IconIds.SyntaxTheme, R.CMD_PaleDarkTheme, R.CMDT_PaleDarkTheme, () => LoadTheme(Config.PaleDarkTheme)) { HorizontalContentAlignment = HorizontalAlignment.Left }.ReferenceStyle(VsResourceKeys.ButtonStyleKey),
								new ThemedButton(IconIds.SyntaxTheme, R.CMD_SimpleTheme, R.CMDT_SimpleTheme, () => LoadTheme(Config.SimpleTheme)) { HorizontalContentAlignment = HorizontalAlignment.Left }.ReferenceStyle(VsResourceKeys.ButtonStyleKey),
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
										new TextBlock { Text = R.T_SyntaxStyles, FontWeight = FontWeights.Bold }.SetValue(Grid.SetColumn, 0),
										new Button { Content = R.CMD_Add }.ReferenceStyle(VsResourceKeys.ButtonStyleKey)
											.Set(ref _AddTagButton).SetValue(Grid.SetColumn, 1),
										new Button { Content = R.CMD_Remove }.ReferenceStyle(VsResourceKeys.ButtonStyleKey)
											.Set(ref _RemoveTagButton).SetValue(Grid.SetColumn, 2)
									}
								},
								new Border {
									BorderThickness = WpfHelper.TinyMargin,
									Child = new StackPanel().Set(ref _SettingsList)
								}.Set(ref _OptionPageHolder)
								.Scrollable()
								.SetValue(Grid.SetRow, 1)
								.ReferenceProperty(Border.BorderBrushProperty, VsBrushes.ActiveBorderKey),

								new Border {
									Visibility = Visibility.Collapsed,
									Child = new StackPanel {
										Children = {
											new TextBlock { Text = R.T_StyleSettings, FontWeight = FontWeights.Bold },
											new WrapPanel {
												Margin = WpfHelper.SmallMargin,
												Children = {
													new TextBlock { Text = R.T_Font, Width = 60, Margin = WpfHelper.SmallMargin },
													new FontButton(ApplyFont) { Width = 230, Margin = WpfHelper.SmallMargin }
														.Set(ref _FontButton)
														.ReferenceStyle(VsResourceKeys.ThemedDialogButtonStyleKey),
													new TextBlock { Text = R.T_Size, Width = 60, Margin = WpfHelper.SmallMargin },
													new NumericUpDown { Width = 80, Margin = WpfHelper.SmallMargin }
														.Set(ref _FontSizeBox),
													new TextBlock { Text = R.T_Stretch, Width = 40, Margin = WpfHelper.SmallMargin },
													new ComboBox { Width = 80, Margin = WpfHelper.SmallMargin }.ReferenceStyle(VsResourceKeys.ComboBoxStyleKey).Set(ref _StretchBox)
												}
											},
											new WrapPanel {
												Margin = WpfHelper.SmallMargin,
												Children = {
													new StyleCheckBox(R.CMD_Bold, OnBoldChanged).Set(ref _BoldBox),
													new StyleCheckBox(R.CMD_Italic, OnItalicChanged).Set(ref _ItalicBox),
													new StyleCheckBox(R.CMD_Underline, OnUnderlineChanged).Set(ref _UnderlineBox),
													new StyleCheckBox(R.CMD_Strikethrough, OnStrikeThroughChanged).Set(ref _StrikethroughBox),
												}
											},
											new WrapPanel {
												Margin = WpfHelper.SmallMargin,
												Children = {
													new ColorButton(Colors.Transparent, R.T_Foreground, OnForeColorChanged).Set(ref _ForegroundButton),
													new OpacityButton(OnForeOpacityChanged).Set(ref _ForegroundOpacityButton),
													new ColorButton(Colors.Transparent, R.T_Background, OnBackColorChanged).Set(ref _BackgroundButton),
													new OpacityButton(OnBackOpacityChanged).Set(ref _BackgroundOpacityButton)
												}
											},
											new WrapPanel {
												Margin = WpfHelper.SmallMargin,
												Children = {
													new TextBlock { Text = R.T_BackgroundEffect, Width = 130 },
													new ComboBox { Width = 160 }.ReferenceStyle(VsResourceKeys.ComboBoxStyleKey).Set(ref _BackgroundEffectBox)
												}
											},
											new WrapPanel {
												Margin = WpfHelper.SmallMargin,
												Children = {
													new TextBlock { Text = R.T_BaseSyntax }
												}
											}.Set(ref _BaseTypesList)
										}
									},
									Padding = new Thickness(0, 6, 0, 0)
								}.Set(ref _SettingsGroup)
									.ReferenceProperty(Border.BorderBrushProperty, VsBrushes.PanelBorderKey)
									.SetValue(Grid.SetRow, 2),

								new Border {
									Visibility = Visibility.Collapsed,
									Child = new StackPanel {
										Children = {
											new TextBlock { Text = R.T_CommentTagSettings, FontWeight = FontWeights.Bold },
											new WrapPanel {
												Margin = WpfHelper.SmallMargin,
												Children = {
													new StackPanel {
														Orientation = Orientation.Horizontal,
														Children = {
															new TextBlock { Text = R.T_Tag, Width = 60, Margin = WpfHelper.SmallMargin },
															new TextBox() { Width = 230, Margin = WpfHelper.SmallMargin }.ReferenceStyle(VsResourceKeys.TextBoxStyleKey).Set(ref _TagBox)
														}
													},
													new StackPanel {
														Orientation = Orientation.Horizontal,
														Children = {
															new TextBlock { Text = R.T_Style, Width = 60, Margin = WpfHelper.SmallMargin },
															new ComboBox { Width = 230, Margin = WpfHelper.SmallMargin, IsEditable = false }
																.Set(ref _TagStyleBox).ReferenceStyle(VsResourceKeys.ComboBoxStyleKey)
														}
													}
												}
											},
											new WrapPanel {
												Margin = WpfHelper.SmallMargin,
												Children = {
													new StyleCheckBox(R.T_CaseSensitive, OnTagCaseSensitiveChanged) { IsThreeState = false }.Set(ref _TagCaseSensitiveBox),
													new StyleCheckBox(R.T_EndWithPunctuation, OnTagHasPunctuationChanged) { IsThreeState = false }.Set(ref _TagHasPunctuationBox),
												}
											},
											new WrapPanel {
												Margin = WpfHelper.SmallMargin,
												Children = {
													new TextBlock { Text = R.T_ApplyOn, Width = 60, Margin = WpfHelper.SmallMargin },
													new RadioBox(R.OT_Tag, "TagApplication", OnTagApplicationChanged).Set(ref _TagApplyOnTagBox),
													new RadioBox(R.OT_Content, "TagApplication", OnTagApplicationChanged).Set(ref _TagApplyOnContentBox),
													new RadioBox(R.OT_TagContent, "TagApplication", OnTagApplicationChanged).Set(ref _TagApplyOnWholeBox),
												}
											},
										}
									},
									Padding = new Thickness(0, 6, 0, 0)
								}.Set(ref _TagSettingsGroup).SetValue(Grid.SetRow, 2),

								new TextBlock {
									Text = R.T_SyntaxHighlightConfigNotice,
									TextWrapping = TextWrapping.Wrap
								}.Set(ref _Notice).SetValue(Grid.SetRow, 2),

								new WrapPanel {
									HorizontalAlignment = HorizontalAlignment.Right,
									Children = {
										new ThemedButton(R.CMD_SaveHighlightChanges, R.CMDT_SaveChanges, Ok) { IsDefault = true, Width = 80, Margin = new Thickness(10) }.ReferenceStyle(VsResourceKeys.ButtonStyleKey),
										new ThemedButton(R.CMD_Cancel, R.CMDT_UndoChanges, Cancel) { IsCancel = true, Width = 80, Margin = new Thickness(10) }.ReferenceStyle(VsResourceKeys.ButtonStyleKey)
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
			_StretchBox.Items.AddRange(new[] { R.T_NotSet, R.T_Expanded, R.T_Normal, R.T_Condensed });
			_StretchBox.SelectionChanged += OnStretchChanged;
			LoadSyntaxStyles(SyntaxStyleSource.Selection);
			_BackgroundEffectBox.Items.AddRange(new[] { R.T_Solid, R.T_BottomGradient, R.T_TopGradient, R.T_RightGradient, R.T_LeftGradient });
			_BackgroundEffectBox.SelectionChanged += OnBackgroundEffectChanged;
			_SyntaxSourceBox.SelectionChanged += SyntaxSourceChanged;
			_AddTagButton.Click += AddTag;
			_RemoveTagButton.Click += RemoveTag;
			_TagBox.TextChanged += ApplyTag;
			Config.Instance.BeginUpdate();
		}

		public bool IsClosing { get; private set; }
		public bool IsReloading { get; private set; }
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

		void HandleViewChangedEvent(object sender, TextViewCreatedEventArgs args) {
			if (args.TextView == _WpfTextView) {
				return;
			}
			if (_WpfTextView != null) {
				UnhookSelectionChangedEvent(_WpfTextView, EventArgs.Empty);
			}
			_WpfTextView = args.TextView as IWpfTextView;
			if (_WpfTextView != null) {
				_WpfTextView.Selection.SelectionChanged += HandleViewSelectionChangedEvent;
				_WpfTextView.Closed += UnhookSelectionChangedEvent;
			}
		}

		void HandleViewSelectionChangedEvent(object sender, EventArgs args) {
			if (_SyntaxSourceBox.SelectedIndex == 0) {
				LoadSyntaxStyles(SyntaxStyleSource.Selection);
			}
		}

		void UnhookSelectionChangedEvent(object sender, EventArgs args) {
			var view = sender as ITextView;
			if (view != null) {
				view.Closed -= UnhookSelectionChangedEvent;
				view.Selection.SelectionChanged -= HandleViewSelectionChangedEvent;
			}
		}

		void SetFormatMap(ITextView view) {
			if (_FormatMap != null) {
				_FormatMap.ClassificationFormatMappingChanged -= RefreshList;
			}
			_FormatMap = view != null
				? ServicesHelper.Instance.ClassificationFormatMap.GetClassificationFormatMap(view)
				: ServicesHelper.Instance.ClassificationFormatMap.GetClassificationFormatMap(Constants.CodeText);
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
				case SyntaxStyleSource.PriorityOrder: classifications = _FormatMap.CurrentPriorityOrder.Where(i => i != __BraceMatchingClassificationType); break;
				case SyntaxStyleSource.Selection:
				default:
					if (_WpfTextView == null) {
						_SettingsGroup.Visibility = Visibility.Collapsed;
						return;
					}
					classifications = GetClassificationsForSelection();
					break;
			}
			if (source == SyntaxStyleSource.Selection) {
				TextEditorHelper.ActiveTextViewChanged += HandleViewChangedEvent;
				_WpfTextView.Selection.SelectionChanged -= HandleViewSelectionChangedEvent;
				_WpfTextView.Selection.SelectionChanged += HandleViewSelectionChangedEvent;
			}
			else {
				TextEditorHelper.ActiveTextViewChanged -= HandleViewChangedEvent;
			}
			var cts = new HashSet<IClassificationType>();
			var style = ActiveStyle;
			_SelectedStyleButton = null;
			foreach (var c in classifications) {
				if (c is TextEditorHelper.ClassificationCategory) {
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
						? R.T_NoSyntaxHighlightSelected
						: source == SyntaxStyleSource.CommentLabels
						? R.T_NoCommentTagDefined
						: R.T_NoSyntaxHighlightDefined,
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
					r.Add(TextEditorHelper.CreateClassificationType(category = item.Category));
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
				_StretchBox.SelectedIndex = GetStretchIndex(s.Stretch);
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

			int GetStretchIndex(int? stretch) {
				if (stretch.HasValue == false) {
					return 0;
				}
				var v = stretch.Value;
				if (v == FontStretches.Expanded.ToOpenTypeStretch()) {
					return 1;
				}
				if (v == FontStretches.Normal.ToOpenTypeStretch()) {
					return 2;
				}
				if (v == FontStretches.Condensed.ToOpenTypeStretch()) {
					return 3;
				}
				return 0;
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

		void OnStretchChanged(object sender, EventArgs e) {
			Update(() => {
				int? s;
				switch (_StretchBox.SelectedIndex) {
					case 1: s = FontStretches.Expanded.ToOpenTypeStretch(); break;
					case 2: s = FontStretches.Normal.ToOpenTypeStretch(); break;
					case 3: s = FontStretches.Condensed.ToOpenTypeStretch(); break;
					default: s = null; break;
				}
				if (ActiveStyle.Stretch != s) {
					ActiveStyle.Stretch = s;
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
				Title = R.T_LoadSyntaxHighlightFile,
				FileName = "Codist.styles",
				DefaultExt = "styles",
				Filter = R.T_HighlightSettingFileFilter
			};
			if (d.ShowDialog() == true) {
				try {
					LoadTheme(d.FileName);
				}
				catch (Exception ex) {
					MessageBox.Show(R.T_FailedToLoadStyleFile + Environment.NewLine + ex.Message, nameof(Codist));
				}
			}
		}
		void SaveTheme() {
			var d = new SaveFileDialog {
				Title = R.T_SaveSyntaxHighlightFile,
				FileName = "Codist.styles",
				DefaultExt = "styles",
				Filter = R.T_HighlightSettingFileFilter
			};
			if (d.ShowDialog() == true) {
				Config.Instance.SaveConfig(d.FileName, true);
			}
		}
		void ResetTheme() {
			if (MessageBox.Show(R.T_ConfirmResetSyntaxHighlight, nameof(Codist), MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
				Config.ResetStyles();
			}
		}
		static void LoadTheme(string path) {
			Config.LoadConfig(path, StyleFilters.All);
		}
		#endregion

		protected override void OnClosed(EventArgs e) {
			Owner.Activate();
			UnhookSelectionChangedEvent(_WpfTextView, EventArgs.Empty);
			TextEditorHelper.ActiveTextViewChanged -= HandleViewChangedEvent;
			_FormatMap.ClassificationFormatMappingChanged -= RefreshList;
			base.OnClosed(e);
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
					.ReferenceProperty(BackgroundProperty, EnvironmentColors.EnvironmentBackgroundBrushKey);
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
					label.FontStretch = format.Typeface.Stretch;
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
				Content = R.T_NotSet;
			}
			public string Value {
				get => _Font as string;
				set {
					if (_Font != value) {
						_Font = value;
						Content = String.IsNullOrWhiteSpace(value) ? R.T_NotSet : value;
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
						ItemsSource = new[] { new ThemedMenuItem(-1, R.T_NotSet, SetFont) { Tag = null } }
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
							new ThemedMenuItem(IconIds.PickColor, R.CMD_PickColor, PickColor),
							new ThemedMenuItem(IconIds.Reset, R.CMD_ResetColor, ResetColor),
							new ThemedMenuItem(IconIds.Copy, R.CMD_CopyColor, CopyColor),
							new ThemedMenuItem(IconIds.Paste, R.CMD_PasteColor, PasteColor),
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
					Content = value == 0 ? R.T_OpacityNotSet : R.T_Opacity + ((value + 1) / 16).ToString();
				}
			}
			protected override void OnClick() {
				base.OnClick();
				if (ContextMenu == null) {
					ContextMenu = new ContextMenu {
						Resources = SharedDictionaryManager.ContextMenu,
						Items = {
							new ThemedMenuItem(IconIds.Opacity, R.T_Default, SelectOpacity) { Tag = 0 },
						},
						MaxHeight = 300,
						Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
						PlacementTarget = this,
					};
					ContextMenu.SetBackgroundForCrispImage(ThemeHelper.TitleBackgroundColor);
					var items = new ThemedMenuItem[16];
					for (int i = 16; i > 0; i--) {
						items[16 - i] = new ThemedMenuItem(IconIds.None, i.ToString(), SelectOpacity) { Tag = i * 16 - 1 };
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
			readonly OptionBox<SpecialHighlightOptions> _MarkSpecialPunctuationBox, _HighlightDeclarationBracesBox, _HighlightParameterBracesBox, _HighlightCastParenthesesBox, _HighlightBranchBracesBox, _HighlightLoopBracesBox, _HighlightResourceBracesBox,
				_HighlightLocalFunctionDeclarationBox, _HighlightNonPrivateFieldDeclarationBox, _HighlightConstructorAsTypeBox;
			//readonly OptionBox<SpecialHighlightOptions>[] _Options;

			public CSharpAdditionalHighlightConfigPage() {
				var o = Config.Instance.SpecialHighlightOptions;
				Content = new StackPanel { Margin = WpfHelper.SmallMargin }.Add(i => new Border { Padding = WpfHelper.SmallMargin, Child = i }, new UIElement[] {
						new TitleBox(R.OT_BracesAndParentheses),
						(_MarkSpecialPunctuationBox = o.CreateOptionBox(SpecialHighlightOptions.SpecialPunctuation, UpdateConfig, R.OT_BoldBraces)),
						new DescriptionBox(R.OT_BoldBracesNote),
						(_HighlightDeclarationBracesBox = o.CreateOptionBox(SpecialHighlightOptions.DeclarationBrace, UpdateConfig, R.OT_DeclarationBraces)),
						(_HighlightParameterBracesBox = o.CreateOptionBox(SpecialHighlightOptions.ParameterBrace, UpdateConfig, R.OT_MethodParameterBraces)),
						(_HighlightCastParenthesesBox = o.CreateOptionBox(SpecialHighlightOptions.CastBrace, UpdateConfig, R.OT_TypeCastBraces)),
						(_HighlightBranchBracesBox = o.CreateOptionBox(SpecialHighlightOptions.BranchBrace, UpdateConfig, R.OT_BranceBraces)),
						(_HighlightLoopBracesBox = o.CreateOptionBox(SpecialHighlightOptions.LoopBrace, UpdateConfig, R.OT_LoopBraces)),
						(_HighlightResourceBracesBox = o.CreateOptionBox(SpecialHighlightOptions.ResourceBrace, UpdateConfig, R.OT_ResourceBraces)),

						new TitleBox(R.OT_MemberStyles),
						(_HighlightLocalFunctionDeclarationBox = o.CreateOptionBox(SpecialHighlightOptions.LocalFunctionDeclaration, UpdateConfig, R.OT_ApplyToLocalFunction)),
						(_HighlightNonPrivateFieldDeclarationBox = o.CreateOptionBox(SpecialHighlightOptions.NonPrivateField, UpdateConfig, R.OT_ApplyToNonPrivateField)),
						(_HighlightConstructorAsTypeBox = o.CreateOptionBox(SpecialHighlightOptions.UseTypeStyleOnConstructor, UpdateConfig, R.OT_StyleConstructorAsType)),

					});
				foreach (var item in new[] { _HighlightDeclarationBracesBox, _HighlightParameterBracesBox, _HighlightCastParenthesesBox, _HighlightBranchBracesBox, _HighlightLoopBracesBox, _HighlightResourceBracesBox }) {
					item.WrapMargin(SubOptionMargin);
					item.ReferenceStyle(VsResourceKeys.CheckBoxStyleKey);
				}
				_MarkSpecialPunctuationBox.ReferenceStyle(VsResourceKeys.CheckBoxStyleKey);
				_HighlightLocalFunctionDeclarationBox.ReferenceStyle(VsResourceKeys.CheckBoxStyleKey);
				_HighlightNonPrivateFieldDeclarationBox.ReferenceStyle(VsResourceKeys.CheckBoxStyleKey);
				_HighlightConstructorAsTypeBox.ReferenceStyle(VsResourceKeys.CheckBoxStyleKey);
				//_Options = new[] { _MarkSpecialPunctuationBox, _HighlightDeclarationBracesBox, _HighlightParameterBracesBox, _HighlightCastParenthesesBox, _HighlightBranchBracesBox, _HighlightLoopBracesBox, _HighlightResourceBracesBox, _HighlightLocalFunctionDeclarationBox, _HighlightNonPrivateFieldDeclarationBox };
			}

			void UpdateConfig(SpecialHighlightOptions options, bool set) {
				Config.Instance.Set(options, set);
				Config.Instance.FireConfigChangedEvent(Features.SyntaxHighlight);
			}
		}
	}
}
