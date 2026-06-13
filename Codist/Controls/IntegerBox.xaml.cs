using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;

namespace Codist.Controls
{
	/// <summary>
	/// A counterpart for NumericUpDown in WinForm for WPF
	/// </summary>
	/// <remarks>see: https://stackoverflow.com/questions/841293/where-is-the-wpf-numeric-updown-control</remarks>
	public partial class IntegerBox : UserControl
	{
		public readonly static DependencyProperty MaximumProperty = DependencyProperty.Register("Maximum", typeof(int), typeof(IntegerBox), new UIPropertyMetadata(int.MaxValue));
		public readonly static DependencyProperty MinimumProperty = DependencyProperty.Register("Minimum", typeof(int), typeof(IntegerBox), new UIPropertyMetadata(int.MinValue));
		public readonly static DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(int), typeof(IntegerBox), new UIPropertyMetadata(0, (o, e) => ((IntegerBox)o).RaiseValueChangedEvent(e)));
		public readonly static DependencyProperty StepProperty = DependencyProperty.Register("Step", typeof(int), typeof(IntegerBox), new UIPropertyMetadata(1));
		public readonly static DependencyProperty UnitProperty = DependencyProperty.Register("Unit", typeof(string), typeof(IntegerBox), new UIPropertyMetadata(string.Empty));

		TextBox _textBox;
		RepeatButton _upButton;
		RepeatButton _downButton;
		TextBlock _unitTextBlock;

		public IntegerBox() {
			#region Build control tree
			// TextBox
			_textBox = new TextBox {
				BorderThickness = new Thickness(0),
			}.Bind(TextBox.TextProperty, new Binding(nameof(Value)) {
				Source = this,
				Mode = BindingMode.TwoWay,
				NotifyOnSourceUpdated = true,
				NotifyOnValidationError = true
			});

			// Up button
			_upButton = CreateRepeatButton("M 0 3 L 6 3 L 3 0 Z");

			// Down button
			_downButton = CreateRepeatButton("M 0 0 L 3 3 L 6 0 Z");

			// Unit text block
			_unitTextBlock = new TextBlock {
				VerticalAlignment = VerticalAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Left,
				Margin = new Thickness(2, 0, 2, 0)
			}.Bind(TextBlock.TextProperty, new Binding(nameof(Unit)) {
				Source = this
			});

			Content = new Grid {
				ColumnDefinitions = {
					new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
					new ColumnDefinition { Width = GridLength.Auto },
				},
				Children = {
					new Border {
						BorderThickness = new Thickness(1),
						BorderBrush = SystemColors.ControlDarkBrush,
						Child = new Grid {
							Background = Brushes.Transparent,
							RowDefinitions = {
								new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
								new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
							},
							ColumnDefinitions = {
								new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
								new ColumnDefinition { Width = GridLength.Auto },
							},
							Children = {
								_textBox.SetValue(Grid.SetRowSpan, 2),
								_upButton.SetValue(Grid.SetColumn, 1),
								_downButton.SetValue(Grid.SetColumn, 1).SetValue(Grid.SetRow, 1)
							}
						},
					},
					_unitTextBlock.SetValue(Grid.SetColumn, 1)
				}
			};
			Width = 60;
			#endregion

			AttachEvents();
			UpdateUnitVisibility();
		}

		public IntegerBox(int initialValue) : this() {
			Value = initialValue;
		}

		public int Maximum { get => (int)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

		public int Minimum { get => (int)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

		public int Value { get => (int)GetValue(ValueProperty); set => SetCurrentValue(ValueProperty, value); }

		public int Step { get => (int)GetValue(StepProperty); set => SetValue(StepProperty, value); }

		public string Unit {
			get => (string)GetValue(UnitProperty);
			set {
				if (value != (string)GetValue(UnitProperty)) {
					SetValue(UnitProperty, value);
					UpdateUnitVisibility();
				}
			}
		}

		public event EventHandler<DependencyPropertyChangedEventArgs> ValueChanged;
		private void RaiseValueChangedEvent(DependencyPropertyChangedEventArgs e) {
			ValueChanged?.Invoke(this, e);
		}

		public IntegerBox UseVsTheme() {
			_textBox.ReferenceStyle(VsResourceKeys.TextBoxStyleKey);
			SetupStyleAndEffects(_upButton);
			SetupStyleAndEffects(_downButton);
			return this;
		}

		void SetupStyleAndEffects(RepeatButton button) {
			// the default RepeatButton does not bind background brush,
			// thus we have to hack into it with event handler
			button.MouseEnter += UseVsThemeOnMouseEnter;
			button.MouseLeave += UseVsThemeOnMouseLeave;
			button.ReferenceProperty(ForegroundProperty, EnvironmentColors.ScrollBarArrowGlyphBrushKey)
				.ReferenceProperty(BackgroundProperty, EnvironmentColors.ScrollBarArrowBackgroundBrushKey);
		}

		void UseVsThemeOnMouseEnter(object sender, MouseEventArgs e) {
			((RepeatButton)sender)
				.ReferenceProperty(ForegroundProperty, EnvironmentColors.ScrollBarArrowGlyphMouseOverBrushKey)
				.GetFirstVisualChild<Border>()
				?.ReferenceProperty(BackgroundProperty, EnvironmentColors.ScrollBarArrowMouseOverBackgroundBrushKey);
		}

		void UseVsThemeOnMouseLeave(object sender, MouseEventArgs e) {
			((RepeatButton)sender)
				.ReferenceProperty(ForegroundProperty, EnvironmentColors.ScrollBarArrowGlyphBrushKey)
				.GetFirstVisualChild<Border>()
				?.ReferenceProperty(BackgroundProperty, EnvironmentColors.ScrollBarArrowBackgroundBrushKey);
		}

		static RepeatButton CreateRepeatButton(string pathData) {
			var button = new RepeatButton {
				BorderThickness = new Thickness(0),
				Width = 13
			};

			button.Content = new Path { Data = Geometry.Parse(pathData) }
				.Bind(Shape.FillProperty, new Binding(nameof(Foreground)) { Source = button });
			return button;
		}

		void AttachEvents() {
			_textBox.TextChanged += TextPartChanged;
			_downButton.Click += DownButtonClicked;
			_upButton.Click += UpButtonClicked;
			_unitTextBlock.ToggleVisibility(!String.IsNullOrEmpty(Unit));
		}

		void UpdateUnitVisibility() {
			_unitTextBlock.Visibility = string.IsNullOrEmpty(Unit) ? Visibility.Collapsed : Visibility.Visible;
		}

		void TextPartChanged(object sender, RoutedEventArgs e) {
			if (Int32.TryParse(_textBox.Text, out int v)) {
				var n = Math.Min(Maximum, Math.Max(Minimum, v));
				if (v != Value) {
					Value = v;
				}
				if (n != v) {
					_textBox.Text = n.ToText();
				}
			}
		}

		void UpButtonClicked(object sender, RoutedEventArgs e) {
			if (Value < Maximum) {
				Value += Step;
				if (Value > Maximum)
					Value = Maximum;
			}
		}

		void DownButtonClicked(object sender, RoutedEventArgs e) {
			if (Value > Minimum) {
				Value -= Step;
				if (Value < Minimum)
					Value = Minimum;
			}
		}
	}
}