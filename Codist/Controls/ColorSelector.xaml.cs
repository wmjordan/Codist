using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// This code file is taken from WPF Color Picker Construction Kit
//   by KenJohnson
//   https://www.codeproject.com/Articles/131708/WPF-Color-Picker-Construction-Kit
namespace Codist.Controls
{
	public enum ColorChangeSource
	{
		ColorPropertySet,
		MouseDown,
		SliderMove,
	}

	/// <summary>
	/// Interaction logic for ColorSelector.xaml
	/// </summary>
	public partial class ColorSelector : UserControl
	{
		public event EventHandler<EventArgs<ColorChangeEventArgs>> ColorChanged;

		bool ProcessSliderEvents { get; set; }

		ColorChangeSource _ColorChangeSource = ColorChangeSource.ColorPropertySet;
		readonly TranslateTransform _SelectionTransform = new TranslateTransform();

		readonly WriteableBitmap _SelectionBitmap = new WriteableBitmap(256, 256, 96, 96, PixelFormats.Bgr24, null);
		readonly WriteableBitmap _NormalBitmap = new WriteableBitmap(24, 256, 96, 96, PixelFormats.Bgr24, null);

		public static Type ClassType {
			get { return typeof(ColorSelector); }
		}

		public ColorSelector() {
			InitializeComponent();

			ColorComponent = ColorComponent.Brightness;
			ColorPlane.Source = _SelectionBitmap;
			NormalColorImage.Source = _NormalBitmap;

			ColorPlane.MouseDown += ColorPlane_MouseDown;
			selectionEllipse.RenderTransform = _SelectionTransform;
			selectionOuterEllipse.RenderTransform = _SelectionTransform;
			ProcessSliderEvents = true;
			OnColorComponentChanged(ColorComponent.Brightness);
		}

		#region Color

		public static DependencyProperty ColorProperty = DependencyProperty.Register("Color", typeof(Color), ClassType,
			new FrameworkPropertyMetadata(Colors.Transparent, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnColorChanged));
		[Category("ColorPicker")]
		public Color Color {
			get {
				return (Color)GetValue(ColorProperty);
			}
			set {
				SetValue(ColorProperty, value);
			}
		}

		static void OnColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
			var c = (Color)e.NewValue;
			if (c != (Color)e.OldValue) {
				((ColorSelector)d).OnColorChanged(c);
			}
		}

		void OnColorChanged(Color color) {
			if (_ColorChangeSource == ColorChangeSource.ColorPropertySet) {
				UpdateColorPlaneBitmap(ColorComponent.Value(color));
				SelectionPoint = ColorComponent.PointFromColor(color);
				_SelectionTransform.X = SelectionPoint.X - (_SelectionBitmap.PixelWidth / 2.0);
				_SelectionTransform.Y = SelectionPoint.Y - (_SelectionBitmap.PixelHeight / 2.0);

				NormalSlider.Value = ColorComponent.Value(color);

				if (!ColorComponent.IsNormalIndependentOfColor) {
					ColorComponent.UpdateNormalBitmap(_NormalBitmap, color);
				}
			}

			ColorChanged?.Invoke(this, new EventArgs<ColorChangeEventArgs>(new ColorChangeEventArgs(color, _ColorChangeSource)));
		}

		#endregion

		#region ColorComponent

		public static DependencyProperty ColorComponentProperty = DependencyProperty.Register("ColorComponent", typeof(ColorComponent),
			ClassType, new FrameworkPropertyMetadata(ColorComponent.Brightness, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnColorComponentChanged));
		[Category("ColorPicker")]
		public ColorComponent ColorComponent {
			get {
				return (ColorComponent)GetValue(ColorComponentProperty);
			}
			set {
				SetValue(ColorComponentProperty, value);
			}
		}

		static void OnColorComponentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
			try {
				var cc = (ColorComponent)e.NewValue;
				var cs = (ColorSelector)d;
				cs.OnColorComponentChanged(cc);
			}
			catch (Exception ex) {
				Debug.WriteLine(ex.Message);
			}
		}

		void OnColorComponentChanged(ColorComponent cc) {
			SelectionPoint = cc.PointFromColor(Color);
			_SelectionTransform.X = SelectionPoint.X - (ColorPlane.ActualWidth / 2);
			_SelectionTransform.Y = SelectionPoint.Y - (ColorPlane.ActualHeight / 2);
			ProcessSliderEvents = false;
			NormalSlider.Minimum = cc.MinValue;
			NormalSlider.Maximum = cc.MaxValue;
			NormalSlider.Value = cc.Value(Color);
			ProcessSliderEvents = true;
			cc.UpdateNormalBitmap(_NormalBitmap, Color);
			cc.UpdateColorPlaneBitmap(_SelectionBitmap, cc.Value(Color));
		}

		#endregion

		#region Event Handlers

		void ColorPlane_MouseDown(object sender, MouseButtonEventArgs e) {
			_ColorChangeSource = ColorChangeSource.MouseDown;

			ProcessMouseDown(e.GetPosition((IInputElement)sender));

			_ColorChangeSource = ColorChangeSource.ColorPropertySet;
		}

		void ProcessMouseDown(Point selectionPoint) {
			SelectionPoint = selectionPoint;
			_SelectionTransform.X = SelectionPoint.X - (ColorPlane.ActualWidth / 2);
			_SelectionTransform.Y = SelectionPoint.Y - (ColorPlane.ActualHeight / 2);
			var newColor = ColorComponent.ColorAtPoint(SelectionPoint, (int)NormalSlider.Value);
			if (!ColorComponent.IsNormalIndependentOfColor) {
				ColorComponent.UpdateNormalBitmap(_NormalBitmap, newColor);
			}
			Color = newColor;
		}

		void NormalSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
			_ColorChangeSource = ColorChangeSource.SliderMove;
			if (ProcessSliderEvents) {
				ProcessSliderEvents = false;
				Color = ColorComponent.ColorAtPoint(SelectionPoint, (int)e.NewValue);
				UpdateColorPlaneBitmap(ColorComponent.Value(Color));
				ProcessSliderEvents = true;
			}
			_ColorChangeSource = ColorChangeSource.ColorPropertySet;
		}

		void ColorPlane_MouseMove(object sender, MouseEventArgs e) {
			_ColorChangeSource = ColorChangeSource.MouseDown;

			if (Mouse.LeftButton == MouseButtonState.Pressed) {
				var point = e.GetPosition((IInputElement)sender);
				if (point.X != 256 && point.Y != 256) //Avoids problem that occurs when dragging to edge of colorPane
				{
					ProcessMouseDown(point);
				}
			}

			_ColorChangeSource = ColorChangeSource.ColorPropertySet;
		}

		void NormalColorImage_MouseDown(object sender, MouseButtonEventArgs e) {
			var yPos = (e.GetPosition((IInputElement)sender)).Y;
			var proportion = 1 - yPos / 255;
			var componentRange = ColorComponent.MaxValue - ColorComponent.MinValue;

			NormalSlider.Value = ColorComponent.MinValue + proportion * componentRange;
		}
		#endregion

		int lastColorComponentValue = -1;
		string lastComponentName = "";
		void UpdateColorPlaneBitmap(int colorComponentValue) {
			if (lastColorComponentValue != colorComponentValue || lastComponentName != ColorComponent.Name) {
				ColorComponent.UpdateColorPlaneBitmap(_SelectionBitmap, colorComponentValue);
				lastColorComponentValue = colorComponentValue;
				lastComponentName = ColorComponent.Name;
			}
		}

		Point SelectionPoint { get; set; }

		public void IncrementNormalSlider() {
			NormalSlider.Value++;
		}

		void NormalColorImage_MouseMove(object sender, MouseEventArgs e) {
			if (Mouse.LeftButton == MouseButtonState.Pressed) {
				var yPos = e.GetPosition((IInputElement)sender).Y;
				var proportion = 1 - yPos / 255;
				var componentRange = ColorComponent.MaxValue - ColorComponent.MinValue;

				NormalSlider.Value = (double)(ColorComponent.MinValue + proportion * componentRange);
			}
		}

		public class ColorChangeEventArgs : EventArgs
		{
			public ColorChangeEventArgs(Color color, ColorChangeSource source) {
				Color = color;
				Source = source;
			}

			public Color Color { get; }
			public ColorChangeSource Source { get; }
		}
	}
}
