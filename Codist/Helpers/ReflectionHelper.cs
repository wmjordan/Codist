using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using CLR;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace Codist
{
	static class ReflectionHelper
	{
		public static TDelegate CreateDelegate<TDelegate>(this DynamicMethod method) where TDelegate : class, Delegate {
			return method.CreateDelegate(typeof(TDelegate)) as TDelegate;
		}

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
			return (_) => null;
		}
		public static Func<TInstance, TProperty> CreateGetPropertyMethod<TInstance, TProperty>(string name, Type castType = null) where TInstance : class
			{
			var type = castType ?? typeof(TInstance);
			var propInfo = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if (propInfo != null) {
				var m = new DynamicMethod("Get" + name, typeof(TProperty), new[] { typeof(TInstance) }, true);
				var il = m.GetILGenerator();
				il.Emit(OpCodes.Ldarg_0);
				Label notInst = default;
				if (castType != null) {
					notInst = il.DefineLabel();
					il.Emit(OpCodes.Isinst, type);
					il.Emit(OpCodes.Dup);
					il.Emit(OpCodes.Brfalse_S, notInst);
				}
				il.Emit(OpCodes.Callvirt, propInfo.GetGetMethod(true));
				il.Emit(OpCodes.Ret);
				if (castType != null) {
					il.MarkLabel(notInst);
					il.Emit(OpCodes.Pop);
					il.LoadDefault(typeof(TProperty));
					il.Emit(OpCodes.Ret);
				}
				return m.CreateDelegate<Func<TInstance, TProperty>>();
			}
			return (_) => default;
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
		public static Func<TIn, TOut> CallStaticFunc<TIn, TOut>(MethodInfo method) {
			if (method != null) {
				var m = new DynamicMethod("Call" + method.Name, typeof(TOut), new[] { typeof(TIn) }, true);
				var il = m.GetILGenerator();
				il.Emit(OpCodes.Ldarg_0);
				il.EmitCall(OpCodes.Call, method, null);
				il.Emit(OpCodes.Ret);
				return m.CreateDelegate<Func<TIn, TOut>>();
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

		/// <summary>
		/// Get's the underlying COM name of System.__COMObject.
		/// </summary>
		/// <seealso href="https://qiita.com/kob58im/items/4e6e773aebbf3ea4594d"/>
		public static string GetTypeNameFromComObject(object obj) {
			var disp = obj as IDispatch;
			if (disp == null) {
				return null;
			}
			int hResult = disp.GetTypeInfoCount(out int count);
			if (hResult < 0 || count < 1) { // failed or no type info
				return null;
			}

			IntPtr ptr = IntPtr.Zero;
			try {
				disp.GetTypeInfo(0, 0, out ptr);
				if (ptr == IntPtr.Zero) {
					return null;
				}

				if (Marshal.GetTypedObjectForIUnknown(ptr, typeof(ComTypes.ITypeInfo)) is ComTypes.ITypeInfo typeInfo) {
					typeInfo.GetDocumentation(-1, out string strName, out _, out _, out _);
					return strName;
				}
				return null;
			}
			finally {
				if (ptr != IntPtr.Zero) {
					Marshal.Release(ptr);
				}
			}
		}

		public static bool IsDefined<TEnum>(this TEnum value)
			where TEnum : struct, Enum {
			return EnumCache<TEnum>.IsDefined(value);
		}

		static class EnumCache<TEnum> where TEnum : struct, Enum
		{
			static readonly HashSet<TEnum> _Enums = InitEnumValues();

			static HashSet<TEnum> InitEnumValues() {
				var values = new HashSet<TEnum>();
				var enumType = typeof(TEnum);
				foreach (var item in enumType.GetFields()) {
					if (item.IsLiteral && item.FieldType == enumType) {
						values.Add(Op.Unbox<TEnum>(item.GetRawConstantValue()));
					}
				}
				return values;
			}

			public static bool IsDefined(TEnum value) {
				return _Enums.Contains(value);
			}
		}

		[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("00020400-0000-0000-c000-000000000046")]
		private interface IDispatch
		{
			[PreserveSig]
			int GetTypeInfoCount(out int count);
			[PreserveSig]
			int GetTypeInfo([In] int itinfo, [In] int lcid, out IntPtr typeinfo);
			[PreserveSig]
			int GetIDsOfNames();//dummy. don't call this method
			[PreserveSig]
			int Invoke();//dummy. don't call this method
		}
	}
}
