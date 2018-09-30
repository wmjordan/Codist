using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.VisualStudio.Text.Classification;

namespace Codist.Classifiers
{
	static class AssemblyMarkManager
	{
		internal const byte IsInMetadata = 0;
		internal const byte IsInSource = 1;
		static readonly ConcurrentDictionary<IAssemblySymbol, byte> _Assemblies = new ConcurrentDictionary<IAssemblySymbol, byte>();

		public static void Clear() {
			_Assemblies.Clear();
		}

		public static byte GetAssemblyMark(IAssemblySymbol assembly) {
			return _Assemblies.GetOrAdd(assembly, a => a.FirstSourceLocation() != null ? IsInSource : IsInMetadata);
		}
	}
}
