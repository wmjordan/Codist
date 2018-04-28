using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;

namespace Codist
{
	class SymbolFormatter
	{
		Brush _NamespaceBrush, _InterfaceBrush, _ClassBrush, _StructBrush, _TextBrush, _NumberBrush, _EnumBrush, _KeywordBrush, _MethodBrush, _DelegateBrush, _ParameterBrush, _TypeParameterBrush, _PropertyBrush, _FieldBrush;

		public Brush Namespace { get => _NamespaceBrush; }
		public Brush Interface { get => _InterfaceBrush; }
		public Brush Class { get => _ClassBrush; }
		public Brush Struct { get => _StructBrush; }
		public Brush Text { get => _TextBrush; }
		public Brush Number { get => _NumberBrush; }
		public Brush Enum { get => _EnumBrush; }
		public Brush Keyword { get => _KeywordBrush; }
		public Brush Method { get => _MethodBrush; }
		public Brush Delegate { get => _DelegateBrush; }
		public Brush Parameter { get => _ParameterBrush; }
		public Brush TypeParameter { get => _TypeParameterBrush; }
		public Brush Property { get => _PropertyBrush; }
		public Brush Field { get => _FieldBrush; }

		internal void UpdateSyntaxHighlights(IEditorFormatMap formatMap) {
			System.Diagnostics.Trace.Assert(formatMap != null, "format map is null");
			_InterfaceBrush = formatMap.GetBrush(Constants.CodeInterfaceName);
			_ClassBrush = formatMap.GetBrush(Constants.CodeClassName);
			_TextBrush = formatMap.GetBrush(Constants.CodeString);
			_EnumBrush = formatMap.GetBrush(Constants.CodeEnumName);
			_DelegateBrush = formatMap.GetBrush(Constants.CodeDelegateName);
			_NumberBrush = formatMap.GetBrush(Constants.CodeNumber);
			_StructBrush = formatMap.GetBrush(Constants.CodeStructName);
			_KeywordBrush = formatMap.GetBrush(Constants.CodeKeyword);
			_NamespaceBrush = formatMap.GetBrush(Constants.CSharpNamespaceName);
			_MethodBrush = formatMap.GetBrush(Constants.CSharpMethodName);
			_ParameterBrush = formatMap.GetBrush(Constants.CSharpParameterName);
			_TypeParameterBrush = formatMap.GetBrush(Constants.CSharpTypeParameterName);
			_PropertyBrush = formatMap.GetBrush(Constants.CSharpPropertyName);
			_FieldBrush = formatMap.GetBrush(Constants.CSharpFieldName);
		}
	}
}
