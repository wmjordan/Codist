using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.PlatformUI;

namespace Codist.Controls
{
	sealed class NumericUpDown : ContentControl
	{
		public readonly static DependencyProperty MaximumProperty = DependencyProperty.Register("Maximum", typeof(int), typeof(NumericUpDown), new UIPropertyMetadata(int.MaxValue));
		public readonly static DependencyProperty MinimumProperty = DependencyProperty.Register("Minimum", typeof(int), typeof(NumericUpDown), new UIPropertyMetadata(int.MinValue));
		public readonly static DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(int), typeof(NumericUpDown), new UIPropertyMetadata(0, (o, e) => ((NumericUpDown)o).RaiseValueChangedEvent(e)));
		public readonly static DependencyProperty StepProperty = DependencyProperty.Register("Step", typeof(int), typeof(NumericUpDown), new UIPropertyMetadata(1));
		readonly ThemedTextBox _ValueBox;

		public NumericUpDown() {
			Content = new Border {
				BorderThickness = WpfHelper.TinyMargin,
				Child = new Grid {
					ColumnDefinitions = {
						new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) },
						new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
						new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) }
					},
					Children = {
						new ThemedButton(KnownImageIds.GlyphDown, null, Decrease) { BorderThickness = WpfHelper.TinyMargin }.SetValue(Grid.SetColumn, 0),
						(_ValueBox = new ThemedTextBox { BorderThickness = WpfHelper.NoMargin, TextAlignment = TextAlignment.Center }.SetValue(Grid.SetColumn, 1)),
						new ThemedButton(KnownImageIds.GlyphUp, null, Increase) { BorderThickness = WpfHelper.TinyMargin }.SetValue(Grid.SetColumn, 2),
					}
				}
			}.ReferenceProperty(BorderBrushProperty, CommonControlsColors.ButtonBorderBrushKey);
			_ValueBox.SetBinding(TextBox.TextProperty, new Binding {
				Source = this,
				Path = new PropertyPath(ValueProperty),
				Mode = BindingMode.TwoWay,
				NotifyOnSourceUpdated = true,
				NotifyOnValidationError = true
			});
			_ValueBox.TextChanged += (s, args) => {
				var t = (TextBox)s;
				if (Int32.TryParse(t.Text, out int v) == false) {
					if (t.Text.Trim().Length == 0) {
						Value = 0;
						t.Text = String.Empty;
					}
					else {
						t.Text = Value.ToString();
					}
				}
				else {
					Value = v;
				}
			};
			_ValueBox.PreviewLostKeyboardFocus += (s, args) => {
				var t = (TextBox)s;
				if (t.Text.Length == 0) {
					t.Text = "0";
					Value = 0;
				}
			};
		}

		public int Maximum { get => (int)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

		public int Minimum { get => (int)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

		public int Value { get => (int)GetValue(ValueProperty); set => SetCurrentValue(ValueProperty, value); }

		public int Step { get => (int)GetValue(StepProperty); set => SetValue(StepProperty, value); }

		public event EventHandler<DependencyPropertyChangedEventArgs> ValueChanged;
		private void RaiseValueChangedEvent(DependencyPropertyChangedEventArgs e) {
			ValueChanged?.Invoke(this, e);
		}

		void Increase() {
			if (Value < Maximum) {
				Value += Step;
				if (Value > Maximum)
					Value = Maximum;
			}
		}

		void Decrease() {
			if (Value > Minimum) {
				Value -= Step;
				if (Value < Minimum)
					Value = Minimum;
			}
		}
	}
}
