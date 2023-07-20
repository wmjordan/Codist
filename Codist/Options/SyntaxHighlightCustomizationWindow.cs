using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
		static readonly Thickness __SubOptionMargin = new Thickness(24, 0, 0, 0);
		static readonly IClassificationType __BraceMatchingClassificationType = ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationType(Constants.CodeBraceMatching);
		const int SMALL_LABEL_WIDTH = 60, MIDDLE_LABEL_WIDTH = 120;

		readonly StackPanel _SettingsList;
		readonly ThemedTextBox _SettingsFilterBox;
		readonly ThemedToggleButton _OverriddenStyleFilterButton;
		readonly ThemedButton _ClearFilterButton;
		readonly Border _OptionPageHolder;
		readonly TextBlock _Notice;
		readonly ListBox _SyntaxSourceBox;
		readonly Grid _RightPaneTitle;
		readonly TextBlock _StyleNameHolder;
		readonly Button _ResetButton;
		readonly Border _SettingsGroup, _TagSettingsGroup;
		readonly FontButton _FontButton;
		readonly NumericUpDown _FontSizeBox;
		readonly StyleCheckBox _BoldBox;
		readonly StyleCheckBox _ItalicBox;
		readonly StyleCheckBox _UnderlineBox;
		readonly StyleCheckBox _StrikethroughBox;
		readonly ComboBox _VariantBox;
		readonly ColorButton _ForegroundButton;
		readonly OpacityButton _ForegroundOpacityButton;
		readonly ColorButton _BackgroundButton;
		readonly OpacityButton _BackgroundOpacityButton;
		readonly LabeledControl _BackgroundEffectControl;
		readonly ComboBox _BackgroundEffectBox;
		readonly ColorButton _LineColorButton;
		readonly OpacityButton _LineOpacityButton;
		readonly NumericUpDown _LineThicknessBox;
		readonly NumericUpDown _LineOffsetBox;
		readonly WrapPanel _LineStyleGroup;
		readonly ComboBox _LineStyleBox;
		readonly WrapPanel _BaseTypesList;
		readonly Button _AddTagButton, _RemoveTagButton;
		readonly TextBox _TagBox;
		readonly ComboBox _TagStyleBox;
		readonly StyleCheckBox _TagCaseSensitiveBox;
		readonly StyleCheckBox _TagHasPunctuationBox;
		readonly RadioBox _TagApplyOnTagBox, _TagApplyOnContentBox, _TagApplyOnWholeBox;
		readonly Button _ImportThemeButton, _ExportThemeButton, _ResetThemeButton;
		readonly UiLock _Lock = new UiLock();
		IWpfTextView _WpfTextView;
		string _CurrentViewCategory;
		IFormatCache _FormatCache;
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
			Resources = SharedDictionaryManager.ThemedControls;
			Content = new Border {
				Padding = WpfHelper.MiddleMargin,
				Child = new Grid {
					ColumnDefinitions = {
						new ColumnDefinition { Width = new GridLength(150) },
						new ColumnDefinition { Width = new GridLength(10, GridUnitType.Star) }
					},
					Children = {
#region Left pane
						new StackPanel {
							Margin = WpfHelper.MiddleMargin,
							Children = {
								new TextBlock {
									Text = R.T_SyntaxCategories,
									Height = 24,
									VerticalAlignment = VerticalAlignment.Bottom,
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
										new ClassificationCategoryItem(SyntaxStyleSource.Xml, "XML/HTML"),
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
								new ThemedButton(IconIds.Load, R.CMD_Import, R.CMDT_LoadTheme, ImportTheme) { HorizontalContentAlignment = HorizontalAlignment.Left }
									.Set(ref _ImportThemeButton)
									.ReferenceStyle(VsResourceKeys.ButtonStyleKey),
								new ThemedButton(IconIds.SaveAs, R.CMD_Export, R.CMDT_SaveTheme, ExportTheme) { HorizontalContentAlignment = HorizontalAlignment.Left }
									.Set(ref _ExportThemeButton)
									.ReferenceStyle(VsResourceKeys.ButtonStyleKey),
								new ThemedButton(IconIds.ResetTheme, R.CMD_Reset, R.CMDT_ResetTheme, ResetTheme) { HorizontalContentAlignment = HorizontalAlignment.Left }
									.Set(ref _ResetThemeButton)
									.ReferenceStyle(VsResourceKeys.ButtonStyleKey),

								new TextBlock {
									Text = R.T_PredefinedThemes,
									Height = 20,
									FontWeight = FontWeights.Bold,
									Margin = WpfHelper.TopItemMargin
								},
								new ThemedButton(IconIds.SyntaxTheme, R.CMD_LightTheme, R.CMDT_LightTheme, () => LoadTheme(Config.LightTheme)) { HorizontalContentAlignment = HorizontalAlignment.Left }
									.ReferenceStyle(VsResourceKeys.ButtonStyleKey),
								new ThemedButton(IconIds.SyntaxTheme, R.CMD_PaleLightTheme, R.CMDT_PaleLightTheme, () => LoadTheme(Config.PaleLightTheme)) { HorizontalContentAlignment = HorizontalAlignment.Left }
									.ReferenceStyle(VsResourceKeys.ButtonStyleKey),
								new ThemedButton(IconIds.SyntaxTheme, R.CMD_DarkTheme, R.CMDT_DarkTheme, () => LoadTheme(Config.DarkTheme)) { HorizontalContentAlignment = HorizontalAlignment.Left }
									.ReferenceStyle(VsResourceKeys.ButtonStyleKey),
								new ThemedButton(IconIds.SyntaxTheme, R.CMD_PaleDarkTheme, R.CMDT_PaleDarkTheme, () => LoadTheme(Config.PaleDarkTheme)) { HorizontalContentAlignment = HorizontalAlignment.Left }
									.ReferenceStyle(VsResourceKeys.ButtonStyleKey),
								new ThemedButton(IconIds.SyntaxTheme, R.CMD_SimpleTheme, R.CMDT_SimpleTheme, () => LoadTheme(Config.SimpleTheme)) { HorizontalContentAlignment = HorizontalAlignment.Left }
									.ReferenceStyle(VsResourceKeys.ButtonStyleKey),
							}
						},
#endregion

#region Right pane
						new Grid {
							Margin = WpfHelper.MiddleMargin,
							RowDefinitions = {
								new RowDefinition { Height = new GridLength(24, GridUnitType.Pixel) },
								new RowDefinition { Height = new GridLength(10, GridUnitType.Star) },
								new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
								new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) },
							},
							Children = {
								new Grid {
									ColumnDefinitions = {
										new ColumnDefinition { Width = new GridLength(10, GridUnitType.Star) },
										new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) },
									},
									Children = {
										new TextBlock { Text = R.T_SyntaxStyles, FontWeight = FontWeights.Bold }.SetValue(Grid.SetColumn, 0),
										new Border {
											HorizontalAlignment = HorizontalAlignment.Right,
											Child = new StackPanel {
												Orientation = Orientation.Horizontal,
												Children = {
													ThemeHelper.GetImage(IconIds.Filter).WrapMargin(WpfHelper.GlyphMargin),
													new ThemedTextBox {
														Width = 120,
														Margin = WpfHelper.SmallMargin,
														ToolTip = R.OT_FilterStyleNamesTip
													}.Set(ref _SettingsFilterBox),
													new ThemedControlGroup { Margin = WpfHelper.SmallVerticalMargin }
														.AddRange(
															new ThemedToggleButton(IconIds.FilterCustomized, R.OT_ShowCustomizedStylesTip).Set(ref _OverriddenStyleFilterButton),
															new ThemedImageButton(IconIds.Add, R.CMD_Add).SetValue(ToolTipService.SetPlacement, PlacementMode.Left).Set(ref _AddTagButton),
															new ThemedImageButton(IconIds.Remove, R.CMD_Remove).SetValue(ToolTipService.SetPlacement, PlacementMode.Left).Set(ref _RemoveTagButton),
															new ThemedButton(ThemeHelper.GetImage(IconIds.ClearFilter), R.CMD_ClearFilter).SetValue(ToolTipService.SetPlacement, PlacementMode.Left).Set(ref _ClearFilterButton)),
												}
											}
										}.SetValue(Grid.SetColumn, 1)
									}
								}.Set(ref _RightPaneTitle),
								new Border {
									Child = new StackPanel().Set(ref _SettingsList)
								}.Set(ref _OptionPageHolder)
								.Scrollable()
								.SetValue(Grid.SetRow, 1),

								new Border {
									Visibility = Visibility.Collapsed,
									Child = new StackPanel {
										Children = {
											new WrapPanel {
												Children = {
													new TextBlock { Text = R.T_StyleSettings, FontWeight = FontWeights.Bold },
													new TextBlock { FontWeight = FontWeights.Bold, TextDecorations = { TextDecorations.Underline }, Margin = WpfHelper.MiddleHorizontalMargin }.Set(ref _StyleNameHolder),
													new Button { Content = R.CMD_Reset }.ReferenceStyle(VsResourceKeys.ButtonStyleKey).Set(ref _ResetButton)
												}
											},
											new WrapPanel {
												Margin = WpfHelper.SmallMargin,
												Children = {
													new LabeledControl(R.T_Font, SMALL_LABEL_WIDTH,
														new FontButton(ApplyFont) { Width = 230 }
															.Set(ref _FontButton)
															.ReferenceStyle(VsResourceKeys.ThemedDialogButtonStyleKey)),
													new LabeledControl(R.T_Size, SMALL_LABEL_WIDTH,
														new NumericUpDown { Width = 80 }
															.Set(ref _FontSizeBox)),
													new LabeledControl(R.T_Variant, SMALL_LABEL_WIDTH,
														new ComboBox { MinWidth = 80 }
															.ReferenceStyle(VsResourceKeys.ComboBoxStyleKey)
															.Set(ref _VariantBox))
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
													new OpacityButton(OnBackOpacityChanged).Set(ref _BackgroundOpacityButton),
												}
											},
											new LabeledControl(R.T_BackgroundEffect, MIDDLE_LABEL_WIDTH,
													new ComboBox { Width = 160 }
														.ReferenceStyle(VsResourceKeys.ComboBoxStyleKey)
														.Set(ref _BackgroundEffectBox)) {
												Margin = WpfHelper.SmallMargin
											}.Set(ref _BackgroundEffectControl),
											new WrapPanel {
												Margin = WpfHelper.SmallMargin,
												Children = {
													new ColorButton(Colors.Transparent, R.T_LineColor, OnLineColorChanged).Set(ref _LineColorButton),
													new OpacityButton(OnLineOpacityChanged).Set(ref _LineOpacityButton)
												}
											},
											new WrapPanel {
												Margin = WpfHelper.SmallMargin,
												Children = {
													new LabeledControl(R.T_LineThickness, MIDDLE_LABEL_WIDTH,
														new NumericUpDown { Width = 80, Minimum = 0, Maximum = 255 }
															.Set(ref _LineThicknessBox)),
													new LabeledControl(R.T_LineOffset, MIDDLE_LABEL_WIDTH,
														new NumericUpDown { Width = 80, Minimum = 0, Maximum = 255 }
															.Set(ref _LineOffsetBox)),
													new LabeledControl(R.T_LineStyle, MIDDLE_LABEL_WIDTH,
														new ComboBox { Width = 160 }
															.ReferenceStyle(VsResourceKeys.ComboBoxStyleKey)
															.Set(ref _LineStyleBox))
												}
											}.Set(ref _LineStyleGroup),
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
													new LabeledControl(R.T_Tag, SMALL_LABEL_WIDTH,
														new TextBox() { Width = 230 }
															.ReferenceStyle(VsResourceKeys.TextBoxStyleKey)
															.Set(ref _TagBox)),
													new LabeledControl(R.T_Style, SMALL_LABEL_WIDTH,
														new ComboBox { Width = 230, IsEditable = false }
															.Set(ref _TagStyleBox)
															.ReferenceStyle(VsResourceKeys.ComboBoxStyleKey))
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
													new TextBlock { Text = R.T_ApplyTo, Width = SMALL_LABEL_WIDTH, Margin = WpfHelper.SmallMargin },
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
#endregion
					}
				}.ReferenceProperty(TextBlock.ForegroundProperty, VsBrushes.ToolWindowTextKey)
			}.ReferenceProperty(Border.BackgroundProperty, VsBrushes.ToolWindowBackgroundKey);
			SetFormatMap(wpfTextView);
			_SyntaxSourceBox.SelectedIndex = 0;
			_FontSizeBox.ValueChanged += ApplyFontSize;
			_VariantBox.Items.AddRange(new[] { R.T_NotSet, R.T_Expanded, R.T_Normal, R.T_Condensed });
			_VariantBox.SelectionChanged += OnFontVariantChanged;
			LoadSyntaxStyles(SyntaxStyleSource.Selection);
			_ForegroundButton.UseVsTheme();
			_BackgroundButton.UseVsTheme();
			_BackgroundEffectBox.Items.AddRange(new[] { R.T_Solid, R.T_BottomGradient, R.T_TopGradient, R.T_RightGradient, R.T_LeftGradient });
			_BackgroundEffectBox.SelectionChanged += OnBackgroundEffectChanged;
			_LineColorButton.UseVsTheme();
			_LineThicknessBox.ValueChanged += ApplyLineThickness;
			_LineOffsetBox.ValueChanged += ApplyLineOffset;
			_LineStyleBox.Items.AddRange(new[] { R.T_SolidLine, R.T_Dot, R.T_Dash, R.T_DashDot, R.T_Squiggle });
			_LineStyleBox.SelectionChanged += OnLineStyleChanged;
			_ResetButton.Click += ResetStyle;
			_SyntaxSourceBox.SelectionChanged += SyntaxSourceChanged;
			_AddTagButton.Click += AddTag;
			_RemoveTagButton.Click += RemoveTag;
			_TagBox.TextChanged += ApplyTag;
			_SettingsFilterBox.TextChanged += FilterSettingsList;
			_OverriddenStyleFilterButton.Checked += FilterSettingsList;
			_OverriddenStyleFilterButton.Unchecked += FilterSettingsList;
			_ClearFilterButton.Click += ClearFilters;
			TextEditorHelper.ActiveTextViewChanged += HandleViewChangedEvent;
			FormatStore.EditorBackgroundChanged += FormatStore_EditorBackgroundChanged;
			FormatStore.ClassificationFormatMapChanged += RefreshList;
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

		void FormatStore_EditorBackgroundChanged(object sender, EventArgs<Color> e) {
			if (sender != _FormatCache) {
				return;
			}
			var bg = new SolidColorBrush(e.Data);
			foreach (var item in _SettingsList.Children) {
				if (item is StyleSettingsButton btn) {
					btn.Background = bg;
				}
			}
		}

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
				SetFormatMap(_WpfTextView);
				if (_SyntaxSourceBox.SelectedIndex == 0) {
					LoadSyntaxStyles(SyntaxStyleSource.Selection);
				}
				else {
					_SettingsList.ForEachChild<StackPanel, StyleSettingsButton>(RefreshStyleButton);
				}
			}
		}

		void HandleViewSelectionChangedEvent(object sender, EventArgs args) {
			if (_SyntaxSourceBox.SelectedIndex == 0) {
				LoadSyntaxStyles(SyntaxStyleSource.Selection);
			}
		}

		void UnhookSelectionChangedEvent(object sender, EventArgs args) {
			if (sender is ITextView view) {
				view.Closed -= UnhookSelectionChangedEvent;
				view.Selection.SelectionChanged -= HandleViewSelectionChangedEvent;
			}
		}

		void SetFormatMap(ITextView view) {
			_CurrentViewCategory = view.GetViewCategory();
			_FormatCache = FormatStore.GetFormatCache(_CurrentViewCategory);
			UpdateDefaultFormat();
		}

		void UpdateDefaultFormat() {
			var p = _FormatCache.DefaultTextProperties;
			if (p != _DefaultFormat) {
				_DefaultFormat = p;
				_SettingsList.SetValue(TextBlock.SetFontFamily, p.Typeface.FontFamily)
							.SetValue(TextBlock.SetFontSize, p.FontRenderingEmSize);
			}
		}

		void SyntaxSourceChanged(object source, RoutedEventArgs args) {
			if (_SyntaxSourceBox.SelectedValue is IControlProvider c) {
				_Notice.Visibility = _RightPaneTitle.Visibility = _SettingsGroup.Visibility = _StyleNameHolder.Visibility = _TagSettingsGroup.Visibility = _AddTagButton.Visibility = _RemoveTagButton.Visibility = Visibility.Collapsed;
				_OptionPageHolder.Child = c.Control;
			}
			else {
				_SettingsList.Visibility = _RightPaneTitle.Visibility = Visibility.Visible;
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
			_Notice.Visibility = _SettingsGroup.Visibility = _OverriddenStyleFilterButton.Visibility = Visibility.Collapsed;
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
					Text = R.T_NoCommentTagDefined,
					FontSize = 20,
					TextWrapping = TextWrapping.Wrap
				});
				_RemoveTagButton.Visibility = _TagSettingsGroup.Visibility = Visibility.Collapsed;
			}
			else {
				FilterSettingsList(null, EventArgs.Empty);
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
			var t = _FormatCache.GetCachedProperty(c);
			if (t == null) {
				return null;
			}
			return new StyleSettingsButton(c, _FormatCache, t, OnSelectTag) { Text = label.Label, ToolTip = label.Label }.SetProperty(StyleSettingsButton.LabelProperty, label);

			CommentStyle FindCommentStyle(CommentLabel cl) {
				var styleId = cl.StyleID;
				return Config.Instance.CommentStyles.Find(i => i.StyleID == styleId);
			}
		}

		void LoadSyntaxStyles(SyntaxStyleSource source) {
			var l = _SettingsList.Children;
			l.Clear();
			_Notice.Toggle(_SettingsList.Visibility != Visibility.Visible);
			_OverriddenStyleFilterButton.Visibility = Visibility.Visible;
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
				case SyntaxStyleSource.PriorityOrder: classifications = _FormatCache.ClassificationFormatMap.CurrentPriorityOrder.Where(FormatStore.IsFormattableClassificationType); break;
				case SyntaxStyleSource.Selection:
				default:
					if (_WpfTextView == null) {
						_SettingsGroup.Visibility = _StyleNameHolder.Visibility = Visibility.Collapsed;
						return;
					}
					classifications = GetClassificationsForSelection();
					break;
			}
			_WpfTextView.Selection.SelectionChanged -= HandleViewSelectionChangedEvent;
			string activeClassification;
			if (source == SyntaxStyleSource.Selection) {
				_WpfTextView.Selection.SelectionChanged += HandleViewSelectionChangedEvent;
				activeClassification = GetHeuristicActiveClassification(classifications);
			}
			else {
				activeClassification = ActiveStyle?.ClassificationType;
			}
			var cts = new HashSet<IClassificationType>();
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
				var t = _FormatCache.GetCachedProperty(c);
				if (t == null) {
					continue;
				}
				var button = new StyleSettingsButton(c, _FormatCache, t, OnSelectStyle) { ToolTip = c.Classification };
				if (activeClassification != null && c.Classification == activeClassification) {
					OnSelectStyle(button, null);
					_SettingsGroup.Visibility = _StyleNameHolder.Visibility = Visibility.Visible;
					activeClassification = null;
				}
				l.Add(button);
			}
			if (_SelectedStyleButton == null) {
				_SettingsGroup.Visibility = _StyleNameHolder.Visibility = Visibility.Collapsed;
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
				_SettingsGroup.Visibility = _StyleNameHolder.Visibility = _RightPaneTitle.Visibility = Visibility.Collapsed;
			}
			else {
				FilterSettingsList(null, EventArgs.Empty);
				_RightPaneTitle.Visibility = Visibility.Visible;
			}
		}

		static string GetHeuristicActiveClassification(IEnumerable<IClassificationType> classifications) {
			IClassificationType t = null;
			int level = 0;
			foreach (var item in classifications) {
				switch (item.Classification) {
					case Constants.CodeIdentifier:
					case Constants.CodeFormalLanguage:
					case Constants.CodePunctuation:
					case Constants.CodeOperator:
						if (t == null) {
							t = item;
							level = 1;
						}
						break;
					case Constants.CSharpUserSymbol:
					case Constants.CSharpMetadataSymbol:
					case Constants.CSharpStaticMemberName:
					case Constants.CSharpSealedMemberName:
					case Constants.CSharpVirtualMemberName:
					case Constants.CSharpOverrideMemberName:
					case Constants.CSharpDeclarationName:
					case Constants.CSharpMemberDeclarationName:
					case Constants.CSharpPrivateMemberName:
					case Constants.CSharpAbstractMemberName:
						continue;
					default:
						if (t == null || level < 2 || item.IsOfType(t.Classification)) {
							t = item;
							level = 2;
						}
						continue;
				}
			}
			return (t ?? classifications.FirstOrDefault())?.Classification;
		}

		static List<IClassificationType> ToClassificationTypes<TStyle>(List<TStyle> styles)
			where TStyle : StyleBase {
			var r = new List<IClassificationType>(styles.Count + 4);
			string category = null;
			var ctr = ServicesHelper.Instance.ClassificationTypeRegistry;
			foreach (var item in styles) {
				if (item.Category.Length == 0) {
					continue;
				}
				var id = item.Id;
				var style = styles.Find(i => i.Id == id) ?? item;
				if (item.Category != category) {
					r.Add(TextEditorHelper.CreateClassificationCategory(category = item.Category));
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
				.Where(s => s.Span.GetSpans(span.Snapshot.TextBuffer)[0].Intersection(span).GetValueOrDefault().Length > 0)
				.Select(t => t.Tag.ClassificationType)
				.ToList(); // cache the results for iterations below
			return classifications
				.Union(classifications.SelectMany(i => i.GetBaseTypes()), TextEditorHelper.GetClassificationTypeComparer())
				.Where(t => t.IsFormattableClassificationType() && t.IsOfType("(TRANSIENT)") == false) // remove transient classification types
				.ToList();
		}

		void FilterSettingsList(object sender, EventArgs args) {
			if (_Lock.IsLocked) {
				return;
			}
			var t = _SettingsFilterBox.Text;
			var filterByText = String.IsNullOrWhiteSpace(t) == false;
			var overriddenOnly = _OverriddenStyleFilterButton.IsChecked == true;
			foreach (var item in _SettingsList.Children) {
				if (item is StyleSettingsButton b) {
					b.ToggleVisibility((filterByText == false || b.Text.IndexOf(t, StringComparison.OrdinalIgnoreCase) != -1)
						&& (overriddenOnly == false || b.StyleSettings.IsSet));
				}
			}
			if (_SelectedStyleButton?.Visibility == Visibility.Collapsed) {
				_SelectedStyleButton.IsChecked = false;
				_SelectedStyleButton = null;
				_SettingsGroup.Visibility = _StyleNameHolder.Visibility = Visibility.Collapsed;
			}
			if (_SelectedCommentTag != null) {
				_SelectedCommentTag = null;
				_TagSettingsGroup.Visibility = Visibility.Collapsed;
			}
		}

		void ClearFilters(object sender, EventArgs args) {
			_Lock.Lock();
			_SettingsFilterBox.Text = String.Empty;
			_OverriddenStyleFilterButton.IsChecked = false;
			_Lock.Unlock();
			FilterSettingsList(sender, args);
			_SettingsFilterBox.Focus();
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
			var t = _SelectedCommentTag = StyleSettingsButton.LabelProperty.Get(b);
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
				_SettingsGroup.Visibility = _StyleNameHolder.Visibility = Visibility.Visible;
				_Notice.Visibility = Visibility.Collapsed;
			}
			_SelectedStyleButton = b;
			try {
				_Lock.Lock();
				var s = b.StyleSettings;
				_StyleNameHolder.Text = s.ClassificationType;
				_FontButton.Value = s.Font;
				_FontSizeBox.Value = (int)(s.FontSize > 100 ? 100 : s.FontSize < -10 ? -10 : s.FontSize);
				ListVariantsForFont(s.Font);
				if (_VariantBox.Items.Count > 0) {
					if (String.IsNullOrWhiteSpace(s.FontVariant)) {
						_VariantBox.SelectedIndex = 0;
					}
					else {
						_VariantBox.SelectedValue = s.FontVariant;
					}
				}
				_BoldBox.IsChecked = s.Bold;
				_ItalicBox.IsChecked = s.Italic;
				_UnderlineBox.IsChecked = s.Underline;
				_StrikethroughBox.IsChecked = s.Strikethrough;
				_ForegroundButton.Color = s.ForeColor;
				_BackgroundButton.Color = s.BackColor;
				_LineColorButton.Color = s.LineColor;
				_ForegroundButton.DefaultColor = () => ((_FormatCache.GetCachedProperty(b.Classification)?.ForegroundBrush as SolidColorBrush)?.Color).GetValueOrDefault();
				_BackgroundButton.DefaultColor = () => ((_FormatCache.GetCachedProperty(b.Classification)?.BackgroundBrush as SolidColorBrush)?.Color).GetValueOrDefault();
				_LineColorButton.DefaultColor = () => {
					var c = _FormatCache.GetCachedProperty(b.Classification);
					return c != null
						? ((c.TextDecorations.FirstOrDefault(i => i.Location == TextDecorationLocation.Underline)?.Pen?.Brush as SolidColorBrush ?? (c.ForegroundBrush as SolidColorBrush))?.Color).GetValueOrDefault()
						: default;
				};
				_ForegroundOpacityButton.Value = s.ForegroundOpacity;
				_BackgroundOpacityButton.Value = s.BackgroundOpacity;
				_BackgroundEffectBox.SelectedIndex = (int)s.BackgroundEffect;
				_BackgroundEffectControl.Toggle(s.BackColor.A > 0);
				_LineOpacityButton.Value = s.LineOpacity;
				_LineThicknessBox.Value = s.LineThickness;
				_LineOffsetBox.Value = s.LineOffset;
				_LineStyleBox.SelectedIndex = (int)s.LineStyle;
				_LineColorButton.ToggleVisibility(s.HasLine);
				_LineOpacityButton.Visibility = _LineStyleGroup.Visibility = s.HasLineColor ? Visibility.Visible : Visibility.Collapsed;
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

		void ListVariantsForFont(string font) {
			_VariantBox.Items.Clear();
			if (String.IsNullOrWhiteSpace(font) == false) {
				var ff = new FontFamily(font);
				var typefaces = new List<Typeface>();
				var names = new List<string>();
				foreach (var typeface in ff.GetTypefaces()) {
					if ((typeface.Weight == FontWeights.Regular || typeface.Weight == FontWeights.Bold)
							&& typeface.Stretch == FontStretches.Normal) {
						continue;
					}
					typefaces.Add(typeface);
				}
				typefaces.Sort(TypefaceComparer.Instance);
				var dedup = new HashSet<Typeface>(TypefaceComparer.Instance);
				foreach (var item in typefaces) {
					if (dedup.Add(item)) {
						names.Add(item.GetTypefaceName());
					}
				}
				if (names.Count > 0) {
					_VariantBox.Items.Add(R.T_Default);
					_VariantBox.Items.AddRange(names);
				}
			}
			_VariantBox.IsEnabled = _VariantBox.Items.Count > 0;
		}

		void RefreshList(object sender, EventArgs<IEnumerable<IClassificationType>> e) {
			if (sender == _FormatCache) {
				_SettingsList.ForEachChild<StackPanel, StyleSettingsButton>(RefreshStyleButton);
				UpdateDefaultFormat();
			}
		}

		void RefreshStyleButton(StyleSettingsButton button) {
			button.Refresh(_FormatCache);
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
			ListVariantsForFont(font);
			Update(() => {
				if (ActiveStyle.Font != font) {
					ActiveStyle.Font = font;
					if (_VariantBox.Items.Count > 0) {
						_VariantBox.SelectedIndex = 0;
					}
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

		void ApplyLineThickness(object sender, DependencyPropertyChangedEventArgs e) {
			Update(() => {
				byte s = (byte)_LineThicknessBox.Value;
				if (ActiveStyle.LineThickness != s) {
					ActiveStyle.LineThickness = s;
					return true;
				}
				return false;
			});
		}

		void ApplyLineOffset(object sender, DependencyPropertyChangedEventArgs e) {
			Update(() => {
				byte s = (byte)_LineOffsetBox.Value;
				if (ActiveStyle.LineOffset != s) {
					ActiveStyle.LineOffset = s;
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
				ToggleLineStyleControls();
				return true;
			});
		}
		void OnStrikeThroughChanged(bool? state) {
			Update(() => {
				ActiveStyle.Strikethrough = state;
				ToggleLineStyleControls();
				return true;
			});
		}

		void ToggleLineStyleControls() {
			var show = ActiveStyle.HasLine;
			_LineColorButton.ToggleVisibility(show);
			show &= ActiveStyle.HasLineColor;
			_LineOpacityButton.Toggle(show);
			_LineStyleGroup.Toggle(show);
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
				_BackgroundEffectControl.Toggle(color.A > 0);
				return true;
			});
		}

		void OnLineColorChanged(Color color) {
			Update(() => {
				ActiveStyle.LineColor = color;
				_LineOpacityButton.Toggle(color.A > 0);
				_LineStyleGroup.Toggle(color.A > 0);
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

		void OnLineOpacityChanged(byte value) {
			Update(() => {
				ActiveStyle.LineOpacity = value;
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

		void OnLineStyleChanged(object sender, EventArgs e) {
			Update(() => {
				var s = (LineStyle)_LineStyleBox.SelectedIndex;
				if (ActiveStyle.LineStyle != s) {
					ActiveStyle.LineStyle = s;
					_LineThicknessBox.IsEnabled = s != LineStyle.Squiggle;
					return true;
				}
				return false;
			});
		}

		void OnFontVariantChanged(object sender, EventArgs e) {
			Update(() => {
				if (_VariantBox.SelectedIndex == 0
					&& String.IsNullOrWhiteSpace(ActiveStyle.FontVariant) == false) {
					ActiveStyle.FontVariant = null;
					return true;
				}
				var v = _VariantBox.SelectedValue as string;
				if (ActiveStyle.FontVariant != v) {
					ActiveStyle.FontVariant = v;
					return true;
				}
				return false;
			});
		}

		void AddTag(object sender, EventArgs e) {
			_SettingsFilterBox.Text = String.Empty;
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
				if (_SelectedCommentTag?.IgnoreCase != value == false) {
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
		void ImportTheme() {
			if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control) {
				FormatStore.Refresh();
				return;
			}
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
					MessageWindow.Error(ex.Message, R.T_FailedToLoadStyleFile);
				}
			}
		}
		void ExportTheme() {
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
			if (MessageWindow.AskYesNo(R.T_ConfirmResetSyntaxHighlight) == true) {
				Config.ResetStyles();
			}
		}
		static void LoadTheme(string path) {
			FormatStore.Reset();
			Config.LoadConfig(path, StyleFilters.All);
		}
		void ResetStyle(object sender, EventArgs e) {
			Update(() => {
				if (_SelectedStyleButton.StyleSettings.IsSet == false) {
					return false;
				}
				_SelectedStyleButton.StyleSettings.Reset();
				FormatStore.Reset(_SelectedStyleButton.StyleSettings.ClassificationType);
				var b = _SelectedStyleButton;
				_SelectedStyleButton = null;
				OnSelectStyle(b, new RoutedEventArgs());
				RefreshStyleButton(b);
				return true;
			});
		}
		#endregion

		protected override void OnClosed(EventArgs e) {
			Owner.Activate();
			UnhookSelectionChangedEvent(_WpfTextView, EventArgs.Empty);
			TextEditorHelper.ActiveTextViewChanged -= HandleViewChangedEvent;
			FormatStore.EditorBackgroundChanged -= FormatStore_EditorBackgroundChanged;
			FormatStore.ClassificationFormatMapChanged -= RefreshList;
			_WpfTextView = null;
			_FormatCache = null;
			if (IsClosing == false) {
				Config.Instance.EndUpdate(false);
			}
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
			public static readonly ExtensionProperty<StyleSettingsButton, CommentLabel> LabelProperty = ExtensionProperty<StyleSettingsButton, CommentLabel>.Register("CommentLabel");

			readonly Border _StyleSetIndicator;
			readonly RadioButton _Selector;
			readonly TextBlock _Preview;
			readonly IClassificationType _Classification;
			StyleBase _Style;

			public StyleSettingsButton(IClassificationType ct, IFormatCache cache, TextFormattingRunProperties t, RoutedEventHandler clickHandler) {
				_Classification = ct;
				HorizontalContentAlignment = HorizontalAlignment.Stretch;
				Content = new StackPanel {
					Orientation = Orientation.Horizontal,
					Children = {
						(_Selector = new RadioButton {
								VerticalAlignment = VerticalAlignment.Center,
								Margin = WpfHelper.GlyphMargin,
								IsEnabled = false
							}).ReferenceStyle(VsResourceKeys.ThemedDialogRadioButtonStyleKey),
						(_StyleSetIndicator = new Border { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center }.ReferenceCrispImageBackground(CommonDocumentColors.PageColorKey)),
						(_Preview = new TextBlock {
							Text = ct.Classification,
							Margin = WpfHelper.SmallMargin,
							VerticalAlignment = VerticalAlignment.Center,
							TextWrapping = TextWrapping.NoWrap
						})
					},
				};
				Background = new SolidColorBrush(cache.ViewBackground);
				SetStyle(FormatStore.GetOrCreateStyle(ct, cache.ClassificationFormatMap));
				PreviewLabelStyle(_Preview, t);
				this.ReferenceStyle(VsResourceKeys.ButtonStyleKey);
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
			public void Refresh(IFormatCache formatCache) {
				_Preview.ClearValues(TextBlock.ForegroundProperty, TextBlock.BackgroundProperty,
					TextBlock.FontFamilyProperty, TextBlock.FontSizeProperty,
					TextBlock.FontStyleProperty, TextBlock.FontWeightProperty,
					TextBlock.TextDecorationsProperty);
				PreviewLabelStyle(_Preview, formatCache.GetCachedProperty(_Classification) ?? formatCache.DefaultTextProperties);
				SetStyle(FormatStore.GetOrCreateStyle(_Classification, formatCache.ClassificationFormatMap));
			}

			void SetStyle(StyleBase style) {
				_Style = style;
				_StyleSetIndicator.Child = ThemeHelper.GetImage(style.IsSet ? IconIds.CustomizeStyle : IconIds.None);
			}

			static TextBlock PreviewLabelStyle(TextBlock preview, TextFormattingRunProperties format) {
				if (format.ForegroundBrushEmpty == false) {
					preview.Foreground = format.ForegroundBrush;
				}
				if (format.BackgroundBrushEmpty == false) {
					preview.Background = format.BackgroundBrush;
				}
				if (format.ItalicEmpty == false) {
					preview.FontStyle = format.Italic ? FontStyles.Italic : FontStyles.Normal;
				}
				if (format.BoldEmpty == false) {
					preview.FontWeight = format.Bold ? FontWeights.Bold : FontWeights.Normal;
				}
				if (format.TypefaceEmpty == false) {
					preview.FontFamily = format.Typeface.FontFamily;
					preview.FontStretch = format.Typeface.Stretch;
					if (preview.FontStyle == FontStyles.Normal) {
						preview.FontStyle = format.Typeface.Style;
					}
					if (preview.FontWeight == FontWeights.Normal) {
						preview.FontWeight = format.Typeface.Weight;
					}
				}
				if (format.FontRenderingEmSizeEmpty == false) {
					preview.FontSize = format.FontRenderingEmSize;
				}
				if (format.TextDecorationsEmpty == false) {
					preview.TextDecorations = format.TextDecorations;
				}
				return preview;
			}
		}

		sealed class StyleCheckBox : CheckBox
		{
			readonly Action<bool?> _CheckHandler;

			public StyleCheckBox(string text, Action<bool?> checkHandler) {
				Content = text;
				IsThreeState = true;
				Margin = WpfHelper.SmallMargin;
				MinWidth = MIDDLE_LABEL_WIDTH;
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

		sealed class FontButton : Button
		{
			static readonly ExtensionProperty<ThemedMenuItem, string> __InstalledFontNameProperty = ExtensionProperty<ThemedMenuItem, string>.Register("InstalledFontName");
			readonly Action<string> _FontChangedHandler;
			string _Font;

			public FontButton(Action<string> fontChanged) {
				_FontChangedHandler = fontChanged;
				Content = R.T_NotSet;
			}

			public string Value {
				get => _Font;
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
						Placement = PlacementMode.Bottom,
						PlacementTarget = this,
						MinWidth = ActualWidth,
						ItemsSource = new[] { new ThemedMenuItem(-1, R.T_NotSet, SetFont).SetProperty(__InstalledFontNameProperty, null) }
							.Concat(WpfHelper.GetInstalledFonts().Select(f => new ThemedMenuItem(-1, f.Name, SetFont).SetProperty(__InstalledFontNameProperty, f.Name)))
					};
				}
				ContextMenu.IsOpen = true;
			}

			protected override void OnContextMenuOpening(ContextMenuEventArgs e) {
				e.Handled = true;
				OnClick();
			}

			void SetFont(object sender, RoutedEventArgs e) {
				var m = e.Source as ThemedMenuItem;
				var prev = ContextMenu.Items.GetFirst<ThemedMenuItem>(i => __InstalledFontNameProperty.Get(i) == Value);
				if (prev != m) {
					prev?.Highlight(false);
					m.Highlight(true);
					Value = __InstalledFontNameProperty.Get(m);
				}
			}
		}

		sealed class TypefaceComparer : IEqualityComparer<Typeface>, IComparer<Typeface>
		{
			public static readonly TypefaceComparer Instance = new TypefaceComparer();
			private TypefaceComparer() {}

			public int Compare(Typeface x, Typeface y) {
				int c;
				return (c = FontStretch.Compare(x.Stretch, y.Stretch)) != 0
					|| (c = FontWeight.Compare(x.Weight, y.Weight)) != 0
					? c
					: MapFontStyle(x.Style) - MapFontStyle(y.Style);
			}

			public bool Equals(Typeface x, Typeface y) {
				return Triple(x).Equals(Triple(y));
			}

			public int GetHashCode(Typeface obj) {
				return Triple(obj).GetHashCode();
			}

			static int MapFontStyle(FontStyle style) {
				return style == FontStyles.Normal ? 0
					: style == FontStyles.Italic ? 1
					: 2;
			}

			static (FontWeight, FontStyle, FontStretch) Triple(Typeface typeface) {
				return (typeface.Weight, typeface.Style == FontStyles.Oblique ? FontStyles.Italic : typeface.Style, typeface.Stretch);
			}
		}

		sealed class OpacityButton : Button
		{
			static readonly ExtensionProperty<ThemedMenuItem, int> __OpacityValueProperty = ExtensionProperty<ThemedMenuItem, int>.Register("OpacityValue");
			readonly Action<byte> _OpacityChangedHandler;
			byte _Opacity;

			public OpacityButton(Action<byte> opacityChangedHandler) {
				Content = R.T_Opacity;
				Width = MIDDLE_LABEL_WIDTH;
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
							SetOpacityValue(new ThemedMenuItem(IconIds.Opacity, R.T_Default, SelectOpacity), 0),
						},
						MaxHeight = 300,
						Placement = PlacementMode.Bottom,
						PlacementTarget = this,
					};
					var items = new ThemedMenuItem[16];
					for (int i = 16; i > 0; i--) {
						items[16 - i] = SetOpacityValue(new ThemedMenuItem(IconIds.None, i.ToString(), SelectOpacity), i * 16 - 1);
					}
					ContextMenu.Items.AddRange(items);
				}
				CheckMenuItem(Value, true);
				ContextMenu.IsOpen = true;
			}

			protected override void OnContextMenuOpening(ContextMenuEventArgs e) {
				e.Handled = true;
				OnClick();
			}

			static ThemedMenuItem SetOpacityValue(ThemedMenuItem item, int value) {
				__OpacityValueProperty.Set(item, value);
				return item;
			}

			void SelectOpacity(object sender, RoutedEventArgs e) {
				var v = (byte)__OpacityValueProperty.Get(e.Source as ThemedMenuItem);
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
				_HighlightLocalFunctionDeclarationBox, _HighlightNonPrivateFieldDeclarationBox, _HighlightConstructorAsTypeBox, _HighlightCapturingLambdaBox;

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
						(_HighlightCapturingLambdaBox = o.CreateOptionBox(SpecialHighlightOptions.CapturingLambdaExpression, UpdateConfig, R.OT_CapturingLambda)),
					});
				foreach (var item in new[] { _HighlightDeclarationBracesBox, _HighlightParameterBracesBox, _HighlightCastParenthesesBox, _HighlightBranchBracesBox, _HighlightLoopBracesBox, _HighlightResourceBracesBox }) {
					item.WrapMargin(__SubOptionMargin);
					item.ReferenceStyle(VsResourceKeys.CheckBoxStyleKey);
				}
				_MarkSpecialPunctuationBox.ReferenceStyle(VsResourceKeys.CheckBoxStyleKey);
				_HighlightLocalFunctionDeclarationBox.ReferenceStyle(VsResourceKeys.CheckBoxStyleKey);
				_HighlightNonPrivateFieldDeclarationBox.ReferenceStyle(VsResourceKeys.CheckBoxStyleKey);
				_HighlightConstructorAsTypeBox.ReferenceStyle(VsResourceKeys.CheckBoxStyleKey);
				_HighlightCapturingLambdaBox.ReferenceStyle(VsResourceKeys.CheckBoxStyleKey);
			}

			void UpdateConfig(SpecialHighlightOptions options, bool set) {
				Config.Instance.Set(options, set);
				Config.Instance.FireConfigChangedEvent(Features.SyntaxHighlight);
			}
		}
	}
}
