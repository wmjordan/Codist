using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Codist.Controls
{
	/// <summary>
	/// Provides extended support for drag drop operation.
	/// see: https://weblogs.asp.net/akjoshi/Attached-behavior-for-auto-scrolling-containers-while-doing-drag-amp-drop
	/// </summary>
	public static class DragDropHelper
	{
		public static readonly DependencyProperty ScrollOnDragDropProperty =
            DependencyProperty.RegisterAttached("ScrollOnDragDrop",
                typeof(bool),
                typeof(DragDropHelper),
                new PropertyMetadata(false, HandleScrollOnDragDropChanged));
 
        public static bool GetScrollOnDragDrop(DependencyObject element) {
			if (element == null) {
				throw new ArgumentNullException("element");
			}

			return (bool)element.GetValue(ScrollOnDragDropProperty);
		}

		public static void SetScrollOnDragDrop(DependencyObject element, bool value) {
			if (element == null) {
				throw new ArgumentNullException("element");
			}

			element.SetValue(ScrollOnDragDropProperty, value);
		}

		static void HandleScrollOnDragDropChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
			var container = d as FrameworkElement;

			if (d == null) {
				//Debug.Fail("Invalid type!");
				return;
			}

			Unsubscribe(container);

			if (true.Equals(e.NewValue)) {
				Subscribe(container);
			}
		}

		static void Subscribe(FrameworkElement container) {
			container.PreviewDragOver += OnContainerPreviewDragOver;
		}

		static void OnContainerPreviewDragOver(object sender, DragEventArgs e) {
			var container = sender as FrameworkElement;
			if (container == null) {
				return;
			}
			var scrollViewer = container.GetFirstVisualChild<ScrollViewer>(v => v.Name == "PART_Scroller")
				?? container.GetFirstVisualChild<ScrollViewer>();
			if (scrollViewer == null) {
				return;
			}

			const double tolerance = 60;
			const double offset = 20;
			double verticalPos = e.GetPosition(container).Y;

			if (verticalPos < tolerance) // Top of visible list? 
			{
				scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - offset); //Scroll up. 
			}
			else if (verticalPos > container.ActualHeight - tolerance) //Bottom of visible list? 
			{
				scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + offset); //Scroll down.     
			}
		}

		static void Unsubscribe(FrameworkElement container) {
			container.PreviewDragOver -= OnContainerPreviewDragOver;
		}
	}
}
