using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Codist.Controls
{
	internal static class SquiggleBrushCache
	{
		static readonly ConditionalWeakTable<Brush, VisualBrush> __BrushCache = new ConditionalWeakTable<Brush, VisualBrush>();
		static readonly Geometry __SquiggleGeometry = Geometry.Parse("M0,0 L1.5,-1.5 4.5,1.5 6,0");
		static readonly Rect __ViewportRect = new Rect(0.0, -0.6, 6.0, 4.3);

		public static VisualBrush GetOrCreate(Brush colorBrush) {
			return __BrushCache.GetValue(colorBrush, CreateBrush);
		}

		static VisualBrush CreateBrush(Brush colorBrush) {
			var pen = new Pen {
				Brush = colorBrush,
				EndLineCap = PenLineCap.Square,
				LineJoin = PenLineJoin.Round,
				MiterLimit = 10.0,
				StartLineCap = PenLineCap.Square,
				Thickness = 0.8
			};
			if (pen.CanFreeze) {
				pen.Freeze();
			}
			var geometryDrawing = new GeometryDrawing {
				Geometry = __SquiggleGeometry,
				Pen = pen
			};
			if (geometryDrawing.CanFreeze) {
				geometryDrawing.Freeze();
			}
			var drawingBrush = new DrawingBrush {
				Stretch = Stretch.None,
				TileMode = TileMode.Tile,
				Viewport = __ViewportRect,
				ViewportUnits = BrushMappingMode.Absolute,
				Drawing = geometryDrawing
			};
			if (drawingBrush.CanFreeze) {
				drawingBrush.Freeze();
			}
			return new VisualBrush(new Rectangle {
				Width = 200,
				Height = 3,
				Fill = drawingBrush,
				VerticalAlignment = VerticalAlignment.Bottom,
				HorizontalAlignment = HorizontalAlignment.Left
			}) { Stretch = Stretch.UniformToFill };
		}
	}
}
