using System;
using System.Reflection;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Tagging;

namespace Codist.Taggers
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

		public static readonly TransientMemberTagHolder BoldDeclarationBraces = ClassificationTagHelper.InitFields<TransientMemberTagHolder>((s, t) => new ClassificationTag(s.CreateTransientClassificationType(t, ClassificationTagHelper.BoldBrace, ClassificationTagHelper.DeclarationBrace)));

		public static readonly TransientMemberTagHolder BoldBraces = ClassificationTagHelper.InitFields<TransientMemberTagHolder>((s, t) => new ClassificationTag(s.CreateTransientClassificationType(t, ClassificationTagHelper.BoldBrace)));

		public TransientMemberTagHolder Clone() {
			return (TransientMemberTagHolder)MemberwiseClone();
		}
	}

	sealed class TransientKeywordTagHolder
	{
		[ClassificationType(ClassificationTypeNames = Constants.CSharpResourceKeyword)]
		public ClassificationTag Resource;
		[ClassificationType(ClassificationTypeNames = Constants.CSharpLoopKeyword)]
		public ClassificationTag Loop;
		[ClassificationType(ClassificationTypeNames = Constants.CSharpBranchingKeyword)]
		public ClassificationTag Branching;
		[ClassificationType(ClassificationTypeNames = Constants.CSharpTypeCastKeyword)]
		public ClassificationTag TypeCast;

		public static readonly TransientKeywordTagHolder Default = ClassificationTagHelper.InitFields<TransientKeywordTagHolder>((s, t) => new ClassificationTag(t));

		public static readonly TransientKeywordTagHolder BoldBraces = ClassificationTagHelper.InitFields<TransientKeywordTagHolder>((s, t) => new ClassificationTag(s.CreateTransientClassificationType(t, ClassificationTagHelper.BoldBrace)));

		public TransientKeywordTagHolder Clone() {
			return (TransientKeywordTagHolder)MemberwiseClone();
		}
	}

	static class ClassificationTagHelper
	{
		public static readonly IClassificationType DeclarationBrace = ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationType(Constants.CSharpDeclarationBrace);
		public static readonly IClassificationType BoldBrace = ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationType(Constants.CodeBoldBrace);
		public static readonly IClassificationType BoldDeclarationBrace = ServicesHelper.Instance.ClassificationTypeRegistry.CreateTransientClassificationType(DeclarationBrace, BoldBrace);

		public static readonly ClassificationTag DeclarationBraceTag = new ClassificationTag(DeclarationBrace);
		public static readonly ClassificationTag BoldBraceTag = new ClassificationTag(BoldBrace);
		public static readonly ClassificationTag BoldDeclarationBraceTag = new ClassificationTag(BoldDeclarationBrace);

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
