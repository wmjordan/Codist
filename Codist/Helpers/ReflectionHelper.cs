using System;
using System.Reflection;
using System.Reflection.Emit;
using AppHelpers;

namespace Codist
{
	static class ReflectionHelper
	{
		public static Func<TObject, TField> CreateGetFieldMethod<TObject, TField>(this Type type, string name) where TObject : class where TField : class {
			var fieldInfo = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
			if (fieldInfo != null) {
				var m = new DynamicMethod("Get" + name, typeof(TField), new[] { typeof(TObject) }, true);
				var il = m.GetILGenerator();
				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Castclass, type);
				il.Emit(OpCodes.Ldfld, fieldInfo);
				il.Emit(OpCodes.Ret);
				return m.CreateDelegate<Func<TObject, TField>>();
			}
			return (s) => null;
		}
		public static Func<TObject, TProperty> CreateGetPropertyMethod<TObject, TProperty>(this Type type, string name) where TObject : class
			{
			var propInfo = type.GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance);
			if (propInfo != null) {
				var m = new DynamicMethod("Get" + name, typeof(TProperty), new[] { typeof(TObject) }, true);
				var il = m.GetILGenerator();
				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Castclass, type);
				il.Emit(OpCodes.Callvirt, propInfo.GetGetMethod(true));
				il.Emit(OpCodes.Ret);
				return m.CreateDelegate<Func<TObject, TProperty>>();
			}
			return (s) => default(TProperty);
		}
	}
}
