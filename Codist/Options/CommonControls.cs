using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CLR;
using Codist.Controls;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;
using R = Codist.Properties.Resources;

namespace Codist.Options
{
	sealed class TitleBox : ContentControl
	{
		public TitleBox() {
			Margin = new Thickness(0, 3, 0, 0);
			Content = new Border {
				BorderThickness = new Thickness(0, 0, 0, 1),
				BorderBrush = SystemColors.ControlDarkDarkBrush,
				Padding = WpfHelper.SmallHorizontalMargin,
				Child = Title = new TextBlock() { FontWeight = FontWeights.Bold }
			};
		}

		public TitleBox(string text) : this() {
			Title.Text = text;
		}

		public TextBlock Title { get; }
	}

	sealed class Note : Label
	{
		public Note(string text) {
			Content = new TextBlock {
				Text = text,
				TextWrapping = TextWrapping.Wrap
			};
		}
		public Note(TextBlock text) {
			Content = text;
			text.TextWrapping = TextWrapping.Wrap;
		}
	}

	sealed class DescriptionBox : TextBlock
	{
		public DescriptionBox(string text) {
			Margin = new Thickness(23, 0, 3, 0);
			TextWrapping = TextWrapping.Wrap;
			Foreground = SystemColors.GrayTextBrush;
			Text = text;
		}
	}
	sealed class OptionBox<TOption> : CheckBox where TOption : struct, Enum
	{
		readonly TOption _Option;
		readonly Action<TOption, bool> _CheckEventHandler;

		public OptionBox() {
			Margin = WpfHelper.SmallMargin;
		}
		public OptionBox(TOption initialValue, TOption option, Action<TOption, bool> checkEventHandler) : this() {
			IsChecked = initialValue.MatchFlags(option);
			_Option = option;
			_CheckEventHandler = checkEventHandler;
		}
		public OptionBox(TOption initialValue, TOption option, Action<TOption, bool> checkEventHandler, Features updateFeature) : this(initialValue, option, checkEventHandler) {
			_CheckEventHandler += (_, __) => Config.Instance.FireConfigChangedEvent(updateFeature);
		}
		public void UpdateWithOption(TOption newValue) {
			IsChecked = newValue.MatchFlags(_Option);
		}
		protected override void OnChecked(RoutedEventArgs e) {
			base.OnChecked(e);
			_CheckEventHandler?.Invoke(_Option, true);
		}
		protected override void OnUnchecked(RoutedEventArgs e) {
			base.OnUnchecked(e);
			_CheckEventHandler?.Invoke(_Option, false);
		}
	}
	sealed class OptionBox : CheckBox
	{
		readonly Action<bool?> _CheckEventHandler;

		public OptionBox() {
			Margin = WpfHelper.SmallMargin;
		}
		public OptionBox(bool initialValue, Action<bool?> checkEventHandler) : this() {
			IsChecked = initialValue;
			_CheckEventHandler = checkEventHandler;
		}
		public OptionBox(bool? initialValue, Action<bool?> checkEventHandler) : this() {
			IsChecked = initialValue;
			_CheckEventHandler = checkEventHandler;
		}
		protected override void OnIndeterminate(RoutedEventArgs e) {
			base.OnIndeterminate(e);
			_CheckEventHandler?.Invoke(null);
		}
		protected override void OnChecked(RoutedEventArgs e) {
			base.OnChecked(e);
			_CheckEventHandler?.Invoke(true);
		}
		protected override void OnUnchecked(RoutedEventArgs e) {
			base.OnUnchecked(e);
			_CheckEventHandler?.Invoke(false);
		}
	}

	sealed class LabeledControl : StackPanel
	{
		public LabeledControl(string text, double labelWidth, FrameworkElement control) {
			Orientation = Orientation.Horizontal;
			this.Add(new TextBlock {
				Text = text,
				Width = labelWidth,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = WpfHelper.SmallMargin
			},
				control.WrapMargin(WpfHelper.SmallMargin));
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

	sealed class ColorButton : Button
	{
		readonly Border _Border;
		readonly Action<Color> _ColorChangedHandler;
		bool _IsColorChanging;

		public ColorButton(Color color, string text, Action<Color> colorChangedHandler) {
			Content = new StackPanel {
				Orientation = Orientation.Horizontal,
				Children = {
						(_Border = new Border {
							Background = new SolidColorBrush(color),
							BorderThickness = WpfHelper.TinyMargin,
							Width = 16, Height = 16,
							Margin = WpfHelper.GlyphMargin
						}),
						new TextBlock { Text = text }
					}
			};
			Width = 120;
			Margin = WpfHelper.SmallMargin;
			_ColorChangedHandler = colorChangedHandler;
		}
		public Func<Color> DefaultColor { get; set; }
		public Color Color {
			get => (_Border.Background as SolidColorBrush).Color;
			set {
				if (Color != value) {
					_Border.Background = new SolidColorBrush(value);
					if (_IsColorChanging == false) {
						_IsColorChanging = true;
						try {
							_ColorChangedHandler?.Invoke(value);
						}
						finally {
							_IsColorChanging = false;
						}
					}
				}
			}
		}
		public Brush Brush => _Border.Background;

		public void UseVsTheme() {
			_Border.ReferenceProperty(Border.BorderBrushProperty, VsBrushes.CommandBarMenuIconBackgroundKey);
			this.ReferenceStyle(VsResourceKeys.ButtonStyleKey);
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
			}
			ContextMenu.IsOpen = true;
		}

		protected override void OnContextMenuOpening(ContextMenuEventArgs e) {
			e.Handled = true;
			OnClick();
		}

		void CopyColor(object sender, RoutedEventArgs e) {
			CopyColor(Color);
		}

		void PasteColor(object sender, RoutedEventArgs e) {
			Color = GetClipboardColor();
		}

		void ResetColor(object sender, RoutedEventArgs e) {
			Color = default;
		}

		static void CopyColor(Color color) {
			try {
				Clipboard.SetDataObject(color.ToHexString());
			}
			catch (System.Runtime.InteropServices.ExternalException) {
				// ignore
			}
		}

		void PickColor(object sender, RoutedEventArgs e) {
			var c = Color;
			var picker = new Picker {
				Color = Color.A != 0 ? Color : DefaultColor?.Invoke() ?? Colors.Gray,
				CustomColors = Config.Instance.CustomColors.ToArray()
			};
			picker.OriginalColor = picker.Color;
			picker.ColorChanged += Picker_ColorChanged;
			var window = this.GetParent<Window>();
			window?.Hide();
			try {
				if (new MessageWindow(picker, R.T_PickColor, MessageBoxButton.OKCancel).ShowDialog() != true) {
					Color = c;
				}
				else if (picker.IsCustomColorsChanged) {
					Config.Instance.CustomColors.Clear();
					Config.Instance.CustomColors.AddRange(picker.CustomColors);
				}
			}
			finally {
				picker.ColorChanged -= Picker_ColorChanged;
				window?.Show();
			}
		}

		void Picker_ColorChanged(object sender, EventArgs<Color> e) {
			Color = e.Data;
		}

		static int Round(double value) {
			return (int)Math.Round(value, MidpointRounding.AwayFromZero);
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

		sealed class Picker : UserControl
		{
			static readonly int[] __HueValues = new int[] { 0, 45, 90, 135, 180, 215, 260, 305 };
			static readonly float[] __LightnessValues = new float[] { 0.85f, 0.7f, 0.5f, 0.3f, 0.15f };
			static readonly Color[] __StandardColors = GenerateStandardPalette();
			readonly PaletteGrid _StandardPalette, _VariantPalette, _CustomPalette;
			readonly ColorSelector _ColorSelector;
			readonly Border _ActiveColorBox;
			readonly IntegerBox _RBox, _GBox, _BBox, _HBox, _SBox, _LBox;
			readonly Button _RevertButton, _CopyButton, _PasteButton;
			Color _Color, _OriginalColor;
			bool _UILock;

			public Picker() {
				Content = new Grid {
					ColumnDefinitions = {
						new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) },
						new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) },
					},
					Margin = WpfHelper.GlyphMargin,
					Children = {
						new StackPanel {
							Margin = WpfHelper.SmallHorizontalMargin,
							Children = {
								new PaletteGrid(R.T_StandardColors, __LightnessValues.Length + 1, () => __StandardColors).Set(ref _StandardPalette),
								new PaletteGrid(R.T_ColorVariances, 3, GenerateVariantPalette).Set(ref _VariantPalette),
								new PaletteGrid(R.T_CustomColors, 2, null) { Editable = true }.Set(ref _CustomPalette),
								new StackPanel {
									Orientation = Orientation.Horizontal,
									Children = {
										new ThemedImageButton(IconIds.Undo, new TextBlock { Text = R.CMD_Revert }) {
											Margin = WpfHelper.SmallMargin,
											MinWidth = 30,
											Padding = WpfHelper.SmallHorizontalMargin,
											ToolTip = R.CMDT_RevertColor
										}.ReferenceStyle(VsResourceKeys.ButtonStyleKey).Set(ref _RevertButton),
										new ThemedImageButton(IconIds.Copy) {
											Margin = WpfHelper.SmallMargin,
											Padding = WpfHelper.TinyMargin,
											MinWidth = 30,
											MaxWidth = 30,
											ToolTip = R.CMDT_CopyColor
										}.ReferenceStyle(VsResourceKeys.ButtonStyleKey).Set(ref _CopyButton),
										new ThemedImageButton(IconIds.Paste) {
											Margin = WpfHelper.SmallMargin,
											Padding = WpfHelper.TinyMargin,
											MinWidth = 30,
											MaxWidth = 30,
											ToolTip = R.CMDT_PasteColor
										}.ReferenceStyle(VsResourceKeys.ButtonStyleKey).Set(ref _PasteButton)
									}
								}
							}
						},
						new StackPanel {
							Margin = WpfHelper.SmallHorizontalMargin,
							Children = {
								new ColorSelector().Set(ref _ColorSelector),
								new StackPanel {
									Orientation = Orientation.Horizontal,
									Margin = WpfHelper.MiddleHorizontalMargin,
									Children = {
										new StackPanel {
											Children = {
												new Label { Content = R.OT_Color }.ReferenceStyle(VsResourceKeys.ThemedDialogLabelStyleKey),
												new Border {
													Width = 48,
													Height = 48,
													BorderBrush = Brushes.Gray,
													BorderThickness = WpfHelper.SmallMargin
												}.UseDummyToolTip().Set(ref _ActiveColorBox)
											}
										},
										CreateTripletGrid(R.T_Hue, ref _HBox, R.T_Saturation, ref _SBox, R.T_Lightness, ref _LBox),
										CreateTripletGrid(R.T_Red, ref _RBox, R.T_Green, ref _GBox, R.T_Blue, ref _BBox)
									}
								}
							}
						}.SetValue(Grid.SetColumn, 1),
					}
				};
				_HBox.Minimum = _SBox.Minimum = _LBox.Minimum
					= _RBox.Minimum = _GBox.Minimum = _BBox.Minimum
					= 0;
				_HBox.Maximum = 359;
				_SBox.Maximum = ColorComponent.Saturation.MaxValue;
				_LBox.Maximum = ColorComponent.Brightness.MaxValue;
				_RBox.Maximum = 255;
				_GBox.Maximum = 255;
				_BBox.Maximum = 255;
				_StandardPalette.PopulateGrids();
				_ColorSelector.ColorChanged += ColorSelector_ColorChanged;
				_HBox.ValueChanged += BoxValueChanged;
				_SBox.ValueChanged += BoxValueChanged;
				_LBox.ValueChanged += BoxValueChanged;
				_RBox.ValueChanged += BoxValueChanged;
				_GBox.ValueChanged += BoxValueChanged;
				_BBox.ValueChanged += BoxValueChanged;
				_StandardPalette.ColorSelected += Palette_ColorSelected;
				_VariantPalette.ColorSelected += Palette_ColorSelected;
				_CustomPalette.ColorSelected += Palette_ColorSelected;
				_RevertButton.Click += RevertButton_Clicked;
				_CopyButton.Click += CopyColor;
				_PasteButton.Click += PasteColor;
				_ActiveColorBox.ToolTipOpening += ActiveColorBox_ToolTipOpening;
			}

			public event EventHandler<EventArgs<Color>> ColorChanged;

			public Color Color {
				get => _Color;
				set {
					value = value.Alpha(Byte.MaxValue);
					if (_Color != value) {
						UpdateColor(value, ColorSource.None);
					}
				}
			}

			public Color OriginalColor {
				get => _OriginalColor;
				set => _OriginalColor = value;
			}

			public Color[] CustomColors {
				get => _CustomPalette.Slots;
				set => _CustomPalette.SetColors(value);
			}

			public bool IsCustomColorsChanged => _CustomPalette.IsPaletteChanged;

			Color[] GenerateVariantPalette() {
				var hsl = HslColor.FromColor(_ColorSelector.Color);
				int i = 0;
				var cols = __HueValues.Length;
				double h = hsl.Hue, s = hsl.Saturation, l = hsl.Luminosity;
				var colors = new Color[cols * 3];
				if (s == 0 || l.CeqAny(0, 1)) {
					// generate gray scales
					double cl = colors.Length;
					for (i = 0; i < colors.Length; i++) {
						colors[i] = ColorHelper.FromHsl(h, s, (i + 1) / cl);
					}
				}
				else {
					double hd = 120 / cols, hmid = cols / 2, hn = h - hd * hmid; // hue variance
					double ds = 1d / cols, sn = ds; // saturation variance
					double dl = 1d / (cols + 1), ln = dl; // lightness variance
					for (int j = 0; j < cols; j++) {
						if (j == hmid) {
							hn += hd;
						}
						colors[i] = ColorHelper.FromHsl(hn, s, l); // hue row
						colors[i + cols] = ColorHelper.FromHsl(h, sn, l); // saturation row
						colors[i + cols + cols] = ColorHelper.FromHsl(h, s, 1 - ln); // lightness row
						++i;
						sn += ds;
						ln += dl;
						hn += hd;
					}
				}
				return colors;
			}

			static Color[] GenerateStandardPalette() {
				var h = __HueValues;
				var l = __LightnessValues;
				int i = 0;
				var colors = new Color[h.Length * l.Length + h.Length];
				foreach (var li in l) {
					foreach (var hi in h) {
						colors[i++] = ColorHelper.FromHsl(hi, 1, li);
					}
				}
				double v = 0, d = 1d / (h.Length - 1);
				for (; i < colors.Length; i++) {
					colors[i] = ColorHelper.FromHsl(0, 0, v);
					v += d;
				}
				return colors;
			}

			static Grid CreateTripletGrid(string label1, ref IntegerBox box1, string label2, ref IntegerBox box2, string label3, ref IntegerBox box3) {
				var g = new Grid {
					ColumnDefinitions = {
						new ColumnDefinition(),
						new ColumnDefinition(),
					},
					RowDefinitions = {
						new RowDefinition(),
						new RowDefinition(),
						new RowDefinition()
					},
					Children = {
						new Label { Content = label1, Target = box1 }
							.ReferenceStyle(VsResourceKeys.ThemedDialogLabelStyleKey),
						new IntegerBox { Margin = WpfHelper.SmallVerticalMargin }
							.Set(ref box1)
							.SetValue(Grid.SetColumn, 1),
						new Label { Content = label2, Target = box2 }
							.ReferenceStyle(VsResourceKeys.ThemedDialogLabelStyleKey)
							.SetValue(Grid.SetRow, 1),
						new IntegerBox { Margin = WpfHelper.SmallVerticalMargin }
							.Set(ref box2)
							.SetValue(Grid.SetColumn, 1)
							.SetValue(Grid.SetRow, 1),
						new Label { Content = label3, Target = box3 }
							.ReferenceStyle(VsResourceKeys.ThemedDialogLabelStyleKey)
							.SetValue(Grid.SetRow, 2),
						new IntegerBox { Margin = WpfHelper.SmallVerticalMargin }
							.Set(ref box3).SetValue(Grid.SetColumn, 1)
							.SetValue(Grid.SetRow, 2),
					}
				};
				box1.UseVsTheme();
				box2.UseVsTheme();
				box3.UseVsTheme();
				return g;
			}

			void BoxValueChanged(object sender, DependencyPropertyChangedEventArgs e) {
				if (_UILock) {
					return;
				}
				_UILock = true;
				if (sender.CeqAny(_HBox, _SBox, _LBox)) {
					UpdateColor(ColorHelper.FromHsl(_HBox.Value, _SBox.Value / (double)ColorComponent.Saturation.MaxValue, _LBox.Value / (double)ColorComponent.Brightness.MaxValue), ColorSource.HslBox);
				}
				else {
					UpdateColor(Color.FromRgb((byte)_RBox.Value, (byte)_GBox.Value, (byte)_BBox.Value), ColorSource.RgbBox);
				}
				_UILock = false;
			}

			void ColorSelector_ColorChanged(object sender, EventArgs<ColorSelector.ColorChangeEventArgs> e) {
				if (_UILock) {
					return;
				}
				_UILock = true;
				UpdateColor(e.Data.Color, ColorSource.SelectorProperty + (int)e.Data.Source);
				_UILock = false;
			}

			void Palette_ColorSelected(object sender, EventArgs<Color> e) {
				if (_UILock) {
					return;
				}
				_UILock = true;
				UpdateColor(e.Data, sender != _StandardPalette ? ColorSource.Palette : ColorSource.None);
				_UILock = false;
			}

			void SetRgbValues(Color c) {
				_RBox.Value = c.R;
				_GBox.Value = c.G;
				_BBox.Value = c.B;
			}

			void SetHslValues(HslColor gc) {
				_HBox.Value = Round(gc.Hue);
				_SBox.Value = Round(gc.Saturation * ColorComponent.Saturation.MaxValue);
				_LBox.Value = Round(gc.Luminosity * ColorComponent.Brightness.MaxValue);
			}

			void UpdateColor(Color c, ColorSource source) {
				if (_Color == c) {
					return;
				}
				if (source != ColorSource.ActiveColor) {
					_ActiveColorBox.Background = new SolidColorBrush(c);
				}
				if (source.CeqAny(ColorSource.SelectorProperty, ColorSource.SelectorSpectrum, ColorSource.SelectorSlider) == false) {
					_ColorSelector.Color = c;
				}
				if (source != ColorSource.HslBox) {
					var hsl = HslColor.FromColor(c);
					switch (source) {
						case ColorSource.SelectorSpectrum:
							_HBox.Value = Round(hsl.Hue);
							_SBox.Value = Round(hsl.Saturation * ColorComponent.Saturation.MaxValue);
							break;
						case ColorSource.SelectorSlider:
							_LBox.Value = Round(hsl.Luminosity * ColorComponent.Brightness.MaxValue);
							break;
						default:
							SetHslValues(HslColor.FromColor(c));
							break;
					}
				}
				if (source != ColorSource.RgbBox) {
					SetRgbValues(c);
				}
				_VariantPalette.PopulateGrids();
				_Color = c;
				ColorChanged?.Invoke(this, new EventArgs<Color>(c));
			}


			void RevertButton_Clicked(object sender, RoutedEventArgs e) {
				Color = _OriginalColor;
			}

			void CopyColor(object sender, RoutedEventArgs e) {
				ColorButton.CopyColor(Color);
			}

			void PasteColor(object sender, RoutedEventArgs e) {
				Color = GetClipboardColor();
			}

			void ActiveColorBox_ToolTipOpening(object sender, ToolTipEventArgs e) {
				_ActiveColorBox.ToolTip = QuickInfo.ColorQuickInfoUI.PreviewColor((SolidColorBrush)_ActiveColorBox.Background);
			}

			enum ColorSource
			{
				None,
				ActiveColor,
				SelectorProperty,
				SelectorSpectrum,
				SelectorSlider,
				Palette,
				HslBox,
				RgbBox
			}
		}

		sealed class PaletteGrid : StackPanel
		{
			const int COLUMN_COUNT = 8;
			static GridLength __GridLength = new GridLength(20, GridUnitType.Pixel);
			readonly int _GridCount;
			readonly Grid _PaletteGrid;
			readonly Func<Color[]> _PaletteGenerator;

			public PaletteGrid(string title, int rowCount, Func<Color[]> paletteGenerator) {
				Children.Add(new Label { Content = title }.ReferenceStyle(VsResourceKeys.ThemedDialogLabelStyleKey));
				Children.Add(new Grid { Margin = WpfHelper.SmallMargin }.Set(ref _PaletteGrid));
				int i;
				for (i = 0; i < COLUMN_COUNT; i++) {
					_PaletteGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = __GridLength });
				}
				for (i = 0; i < rowCount; i++) {
					_PaletteGrid.RowDefinitions.Add(new RowDefinition { Height = __GridLength });
				}
				for (int r = 0; r < rowCount; r++) {
					for (int c = 0; c < COLUMN_COUNT; c++) {
						_PaletteGrid.Children.Add(new ColorGrid(Colors.White, this).SetValue(Grid.SetColumn, c).SetValue(Grid.SetRow, r));
					}
				}
				_GridCount = rowCount * COLUMN_COUNT;
				_PaletteGenerator = paletteGenerator;
			}

			public bool Editable { get; set; }
			public bool IsPaletteChanged => _PaletteGrid.Children.Cast<ColorGrid>().Any(i => i.IsChanged);

			public Color[] Slots => _PaletteGrid.Children.Cast<ColorGrid>().Select(g => g.Color).ToArray();

			public event EventHandler<EventArgs<Color>> ColorSelected;

			public void PopulateGrids() {
				var pc = _PaletteGrid.Children;
				var colors = _PaletteGenerator();
				for (int i = 0; i < colors.Length && i < _GridCount; i++) {
					((ColorGrid)pc[i]).SetColor(colors[i]);
				}
			}

			public void SetColors(Color[] colors) {
				var pc = _PaletteGrid.Children;
				for (int i = 0; i < colors.Length && i < _GridCount; i++) {
					((ColorGrid)pc[i]).SetColor(colors[i]);
				}
			}

			void OnColorChanged(EventArgs<Color> eventArgs) {
				ColorSelected?.Invoke(this, eventArgs);
			}

			sealed class ColorGrid : Button
			{
				readonly PaletteGrid _PaletteGrid;
				bool _IsChanged;

				public ColorGrid(Color color, PaletteGrid paletteGrid) {
					Background = new SolidColorBrush(color);
					_PaletteGrid = paletteGrid;
				}

				public Color Color => ((SolidColorBrush)Background).Color;
				public bool IsChanged => _IsChanged;

				public void SetColor(Color color) {
					Background = new SolidColorBrush(color);
					_IsChanged = false;
					this.UseDummyToolTip();
				}

				protected override void OnClick() {
					base.OnClick();
					_PaletteGrid.OnColorChanged(new EventArgs<Color>(((SolidColorBrush)Background).Color));
				}

				protected override void OnToolTipOpening(ToolTipEventArgs e) {
					base.OnToolTipOpening(e);
					if (this.HasDummyToolTip() == false) {
						return;
					}
					var tip = new ThemedToolTip();
					tip.Title.Text = R.T_Color;
					var c = Color;
					var hsl = HslColor.FromColor(c);
					tip.Content.Append($"HSL: {Round(hsl.Hue).ToText()}, {Round(hsl.Saturation * ColorComponent.Saturation.MaxValue).ToText()}, {Round(hsl.Luminosity * ColorComponent.Brightness.MaxValue).ToText()}").AppendLineBreak()
						.Append($"RGB: {c.R}, {c.G}, {c.B}");
					ToolTip = tip;
				}

				protected override void OnContextMenuOpening(ContextMenuEventArgs e) {
					if (ContextMenu is null) {
						ContextMenu = new ContextMenu {
							Resources = SharedDictionaryManager.ContextMenu,
							Items = {
								new ThemedMenuItem(IconIds.Copy, R.CMD_CopyColor, CopyColor),
							},
							PlacementTarget = this
						};
						if (_PaletteGrid.Editable) {
							ContextMenu.Items.Add(new ThemedMenuItem(IconIds.Paste, R.CMD_PasteColor, PasteColor));
							ContextMenu.Items.Add(new ThemedMenuItem(IconIds.PickColor, R.CMD_UseActiveColor, PasteActiveColor));
						}
					}
					base.OnContextMenuOpening(e);
				}

				void CopyColor(object sender, RoutedEventArgs e) {
					ColorButton.CopyColor(Color);
				}

				void PasteColor(object sender, RoutedEventArgs e) {
					var c = GetClipboardColor();
					if (c.A != 0 && c != Color) {
						Background = new SolidColorBrush(c);
						_IsChanged = true;
					}
				}

				void PasteActiveColor(object sender, RoutedEventArgs e) {
					var c = this.GetParent<Picker>().Color;
					if (c.A != 0 && c != Color) {
						Background = new SolidColorBrush(c);
						_IsChanged = true;
					}
				}
			}
		}
	}
}
