using System;
using System.Reflection;
using System.Reflection.Emit;
using AppHelpers;

namespace Codist
{
	static class ReflectionHelper
	{
		public static Func<TInstance, TField> CreateGetFieldMethod<TInstance, TField>(string name) where TInstance : class where TField : class {
			var type = typeof(TInstance);
			var fieldInfo = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if (fieldInfo != null) {
				var m = new DynamicMethod("Get" + name, typeof(TField), new[] { type }, true);
				var il = m.GetILGenerator();
				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Castclass, type);
				il.Emit(OpCodes.Ldfld, fieldInfo);
				il.Emit(OpCodes.Ret);
				return m.CreateDelegate<Func<TInstance, TField>>();
			}
			return (s) => null;
		}
		public static Func<TInstance, TProperty> CreateGetPropertyMethod<TInstance, TProperty>(string name, Type castType = null) where TInstance : class
			{
			var type = castType ?? typeof(TInstance);
			var propInfo = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if (propInfo != null) {
				var m = new DynamicMethod("Get" + name, typeof(TProperty), new[] { typeof(TInstance) }, true);
				var il = m.GetILGenerator();
				il.Emit(OpCodes.Ldarg_0);
				var notInst = il.DefineLabel();
				if (castType != null) {
					il.Emit(OpCodes.Isinst, type);
					il.Emit(OpCodes.Dup);
					il.Emit(OpCodes.Brfalse_S, notInst);
				}
				il.Emit(OpCodes.Callvirt, propInfo.GetGetMethod(true));
				il.Emit(OpCodes.Ret);
				il.MarkLabel(notInst);
				il.Emit(OpCodes.Pop);
				il.LoadDefault(typeof(TProperty));
				il.Emit(OpCodes.Ret);
				return m.CreateDelegate<Func<TInstance, TProperty>>();
			}
			return (s) => default(TProperty);
		}
		public static Action<TInstance, TProperty> CreateSetPropertyMethod<TInstance, TProperty>(string name) where TInstance : class {
			var type = typeof(TInstance);
			var propInfo = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if (propInfo != null) {
				var m = new DynamicMethod("Set" + name, null, new[] { type, typeof(TProperty) }, true);
				var il = m.GetILGenerator();
				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Castclass, type);
				il.Emit(OpCodes.Ldarg_1);
				il.Emit(OpCodes.Callvirt, propInfo.GetSetMethod(true));
				il.Emit(OpCodes.Ret);
				return m.CreateDelegate<Action<TInstance, TProperty>>();
			}
			return null;
		}
		static void LoadDefault(this ILGenerator il, Type type) {
			var t = type;
			switch (Type.GetTypeCode(t)) {
				case TypeCode.Boolean:
				case TypeCode.Byte:
				case TypeCode.Char:
				case TypeCode.Int16:
				case TypeCode.Int32:
				case TypeCode.SByte:
				case TypeCode.UInt16:
				case TypeCode.UInt32:
					il.Emit(OpCodes.Ldc_I4_0);
					return;
				case TypeCode.Int64:
				case TypeCode.UInt64:
					il.Emit(OpCodes.Ldc_I8, 0L);
					return;
				case TypeCode.Single:
					il.Emit(OpCodes.Ldc_R4, 0f);
					return;
				case TypeCode.Double:
					il.Emit(OpCodes.Ldc_R8, 0d);
					return;
				case TypeCode.DBNull:
				default:
					if (t.IsValueType) {
						var i = il.DeclareLocal(t);
						il.Emit(OpCodes.Ldloca_S, i);
						il.Emit(OpCodes.Initobj, t);
						il.Emit(OpCodes.Ldloc_S, i);
						return;
					}
					il.Emit(OpCodes.Ldnull);
					return;
			}
		}
	}
}
