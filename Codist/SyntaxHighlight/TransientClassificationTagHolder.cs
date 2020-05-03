using System;
using System.Reflection;
using Codist.Taggers;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;

namespace Codist.SyntaxHighlight
{
	sealed class TransientMemberTagHolder
	{
		[ClassificationType(ClassificationTypeNames = Constants.CSharpNamespaceName)]
		public ClassificationTag Namespace;
		[ClassificationType(ClassificationTypeNames = Constants.CodeClassName)]
		public ClassificationTag Class;
		[ClassificationType(ClassificationTypeNames = Constants.CodeStructName)]
		public ClassificationTag Struct;
		[ClassificationType(ClassificationTypeNames = Constants.CodeInterfaceName)]
		public ClassificationTag Interface;
		[ClassificationType(ClassificationTypeNames = Constants.CodeEnumName)]
		public ClassificationTag Enum;
		[ClassificationType(ClassificationTypeNames = Constants.CodeDelegateName)]
		public ClassificationTag Delegate;
		[ClassificationType(ClassificationTypeNames = Constants.CodeEventName)]
		public ClassificationTag Event;
		[ClassificationType(ClassificationTypeNames = Constants.CSharpFieldName)]
		public ClassificationTag Field;
		[ClassificationType(ClassificationTypeNames = Constants.CSharpPropertyName)]
		public ClassificationTag Property;
		[ClassificationType(ClassificationTypeNames = Constants.CSharpMethodName)]
		public ClassificationTag Method;
		[ClassificationType(ClassificationTypeNames = Constants.CSharpConstructorMethodName)]
		public ClassificationTag Constructor;

		public static readonly TransientMemberTagHolder Default = ClassificationTagHelper.InitFields<TransientMemberTagHolder>((s, t) => new ClassificationTag(t));

		public static readonly TransientMemberTagHolder DeclarationBraces = ClassificationTagHelper.InitFields<TransientMemberTagHolder>((s, t) => new ClassificationTag(s.CreateTransientClassificationType(t, ClassificationTagHelper.DeclarationBrace)));

		public static readonly TransientMemberTagHolder StrongDeclarationBraces = ClassificationTagHelper.InitFields<TransientMemberTagHolder>((s, t) => new ClassificationTag(s.CreateTransientClassificationType(t, ClassificationTagHelper.StrongBrace, ClassificationTagHelper.DeclarationBrace)));

		public static readonly TransientMemberTagHolder StrongBraces = ClassificationTagHelper.InitFields<TransientMemberTagHolder>((s, t) => new ClassificationTag(s.CreateTransientClassificationType(t, ClassificationTagHelper.StrongBrace)));

		public TransientMemberTagHolder Clone() {
			return (TransientMemberTagHolder)MemberwiseClone();
		}
	}

	sealed class TransientKeywordTagHolder
	{
		[ClassificationType(ClassificationTypeNames = Constants.CSharpResourceKeyword)]
		public ClassificationTag Resource;
		[ClassificationType(ClassificationTypeNames = Constants.CodeLoopKeyword)]
		public ClassificationTag Loop;
		[ClassificationType(ClassificationTypeNames = Constants.CodeBranchingKeyword)]
		public ClassificationTag Branching;
		[ClassificationType(ClassificationTypeNames = Constants.CodeTypeCastKeyword)]
		public ClassificationTag TypeCast;

		public static readonly TransientKeywordTagHolder Default = ClassificationTagHelper.InitFields<TransientKeywordTagHolder>((s, t) => new ClassificationTag(t));

		public static readonly TransientKeywordTagHolder StrongBraces = ClassificationTagHelper.InitFields<TransientKeywordTagHolder>((s, t) => new ClassificationTag(s.CreateTransientClassificationType(t, ClassificationTagHelper.StrongBrace)));

		public TransientKeywordTagHolder Clone() {
			return (TransientKeywordTagHolder)MemberwiseClone();
		}
	}

	static class ClassificationTagHelper
	{
		public static readonly IClassificationType DeclarationBrace = ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationType(Constants.CSharpDeclarationBrace);
		public static readonly IClassificationType StrongBrace = ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationType(Constants.CodeSpecialPunctuation);
		public static readonly IClassificationType StrongDeclarationBrace = ServicesHelper.Instance.ClassificationTypeRegistry.CreateTransientClassificationType(DeclarationBrace, StrongBrace);

		public static readonly ClassificationTag DeclarationBraceTag = new ClassificationTag(DeclarationBrace);
		public static readonly ClassificationTag StrongBraceTag = new ClassificationTag(StrongBrace);
		public static readonly ClassificationTag StrongDeclarationBraceTag = new ClassificationTag(StrongDeclarationBrace);

		public static T InitFields<T>(Func<IClassificationTypeRegistryService, IClassificationType, ClassificationTag> tagBuilder)
			where T : class, new() {
			var c = ServicesHelper.Instance.ClassificationTypeRegistry;
			var h = new T();
			var ct = typeof(ClassificationTag);
			foreach (var item in typeof(T).GetFields()) {
				if (item.FieldType != ct) {
					continue;
				}
				var n = item.GetCustomAttribute<ClassificationTypeAttribute>()?.ClassificationTypeNames;
				if (n != null) {
					var t = c.GetClassificationType(n);
					if (t != null) {
						item.SetValue(h, tagBuilder(c, t));
					}
				}
			}
			return h;
		}
	}
}
