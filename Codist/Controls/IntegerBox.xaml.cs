using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
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

		public readonly static Style VsRepeatButtonStyle = new Style {
			TargetType = typeof(RepeatButton),
			Setters = {
				new Setter {
					Property = ForegroundProperty,
					Value = new Binding {
						Source = new DynamicResourceExtension {
							ResourceKey = VsBrushes.ButtonTextKey
						}
					}
				},
				new Setter {
					Property = BackgroundProperty,
					Value = new Binding {
						Source = new DynamicResourceExtension {
							ResourceKey = VsBrushes.ButtonFaceKey
						}
					}
				}
			},
			Triggers = {
				new Trigger {
					Property = ButtonBase.IsPressedProperty,
					Value = true,
					Setters = {
						new Setter {
							Property = BackgroundProperty,
							Value = new Binding {
								Source = new DynamicResourceExtension {
									ResourceKey = VsBrushes.ButtonHighlightKey
								}
							}
						}
					}
				},
				new Trigger {
					Property = IsMouseOverProperty,
					Value = true,
					Setters = {
						new Setter {
							Property = BackgroundProperty,
							Value = new Binding {
								Source = new DynamicResourceExtension {
									ResourceKey = VsBrushes.ButtonHighlightKey
								}
							}
						}
					}
				}
			}
		};

		public IntegerBox() {
			InitializeComponent();
		}
		public IntegerBox(int initialValue) : this() {
			Value = initialValue;
		}
		public int Maximum { get => (int)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }

		public int Minimum { get => (int)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

		public int Value { get => (int)GetValue(ValueProperty); set => SetCurrentValue(ValueProperty, value); }

		public int Step { get => (int)GetValue(StepProperty); set => SetValue(StepProperty, value); }

		public event EventHandler<DependencyPropertyChangedEventArgs> ValueChanged;
		private void RaiseValueChangedEvent(DependencyPropertyChangedEventArgs e) {
			ValueChanged?.Invoke(this, e);
		}

		public IntegerBox UseVsTheme() {
			tbmain.ReferenceStyle(VsResourceKeys.TextBoxStyleKey);
			PART_DownButton.OverridesDefaultStyle = true;
			PART_DownButton.Style = VsRepeatButtonStyle;
			return this;
		}

		public override void OnApplyTemplate() {
			base.OnApplyTemplate();
			PART_DownButton.Click -= btdown_Click;
			PART_UpButton.Click -= btup_Click;
			PART_DownButton.Click += btdown_Click;
			PART_UpButton.Click += btup_Click;
		}

		void btup_Click(object sender, RoutedEventArgs e) {
			if (Value < Maximum) {
				Value += Step;
				if (Value > Maximum)
					Value = Maximum;
			}
		}

		void btdown_Click(object sender, RoutedEventArgs e) {
			if (Value > Minimum) {
				Value -= Step;
				if (Value < Minimum)
					Value = Minimum;
			}
		}
	}
}
