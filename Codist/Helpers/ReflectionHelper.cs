using System;
using System.Reflection;
using System.Reflection.Emit;
using AppHelpers;

namespace Codist
{
	static class ReflectionHelper
	{
		public static Func<TObject, TField> CreateGetFieldMethod<TObject, TField>(string name) where TObject : class where TField : class {
			var type = typeof(TObject);
			var fieldInfo = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if (fieldInfo != null) {
				var m = new DynamicMethod("Get" + name, typeof(TField), new[] { type }, true);
				var il = m.GetILGenerator();
				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Castclass, type);
				il.Emit(OpCodes.Ldfld, fieldInfo);
				il.Emit(OpCodes.Ret);
				return m.CreateDelegate<Func<TObject, TField>>();
			}
			return (s) => null;
		}
		public static Func<TObject, TProperty> CreateGetPropertyMethod<TObject, TProperty>(string name) where TObject : class
			{
			var type = typeof(TObject);
			var propInfo = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if (propInfo != null) {
				var m = new DynamicMethod("Get" + name, typeof(TProperty), new[] { type }, true);
				var il = m.GetILGenerator();
				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Castclass, type);
				il.Emit(OpCodes.Callvirt, propInfo.GetGetMethod(true));
				il.Emit(OpCodes.Ret);
				return m.CreateDelegate<Func<TObject, TProperty>>();
			}
			return (s) => default(TProperty);
		}
		public static Action<TObject, TProperty> CreateSetPropertyMethod<TObject, TProperty>(string name) where TObject : class {
			var type = typeof(TObject);
			var propInfo = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if (propInfo != null) {
				var m = new DynamicMethod("Set" + name, null, new[] { type, typeof(TProperty) }, true);
				var il = m.GetILGenerator();
				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Castclass, type);
				il.Emit(OpCodes.Ldarg_1);
				il.Emit(OpCodes.Callvirt, propInfo.GetSetMethod(true));
				il.Emit(OpCodes.Ret);
				return m.CreateDelegate<Action<TObject, TProperty>>();
			}
			return null;
		}
	}
}
