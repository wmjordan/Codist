using System;
using System.Windows;
using System.Windows.Controls;

namespace Codist.Controls
{
	/// <summary>
	/// Contains <see cref="DependencyProperty"/> instances to extend existing WPF controls.
	/// </summary>
	static class ExtensionProperties
	{
		public static TObj SetProperty<TObj, TValue>(this TObj owner, ExtensionProperty<TObj, TValue> property, TValue value)
			where TObj : DependencyObject {
			property.Set(owner, value);
			return owner;
		}

		#region Browser Search
		static readonly ExtensionProperty<FrameworkElement, string> __SearchUrl = ExtensionProperty<FrameworkElement, string>.Register("SearchUrl");
		static readonly ExtensionProperty<FrameworkElement, string> __SearchParameter = ExtensionProperty<FrameworkElement, string>.Register("SearchParameter");

		public static void SetSearchUrlPattern(this FrameworkElement element, string url, string parameter) {
			__SearchUrl.Set(element, url);
			__SearchParameter.Set(element, parameter);
		}
		public static string GetSearchUrl(this FrameworkElement element) {
			return __SearchUrl.Get(element);
		}
		public static string GetSearchParameter(this FrameworkElement element) {
			return __SearchParameter.Get(element);
		}
		#endregion

		#region Menu click
		static readonly ExtensionProperty<ContextMenu, bool> __IsRightClicked = ExtensionProperty<ContextMenu, bool>.Register("IsRightClicked");

		public static void SetIsRightClicked(this ContextMenu menu) {
			__IsRightClicked.Set(menu, true);
		}
		public static bool GetIsRightClicked(this ContextMenu menu) {
			return __IsRightClicked.Get(menu);
		}
		#endregion
	}

	sealed class ExtensionProperty<TOwner, TProperty> where TOwner : DependencyObject
	{
		public static ExtensionProperty<TOwner, TProperty> Register(string name) {
			return new ExtensionProperty<TOwner, TProperty>(name);
		}

		readonly DependencyProperty _Property;

		ExtensionProperty(string name) {
			_Property = DependencyProperty.Register(name, typeof(TProperty), typeof(TOwner));
		}

		public TProperty Get(TOwner owner) {
			return owner.GetValue(_Property) is TProperty p ? p : default;
		}
		public void Set(TOwner owner, TProperty property) {
			owner.SetValue(_Property, property);
		}
		public void Clear(TOwner owner) {
			owner.ClearValue(_Property);
		}
	}
}
