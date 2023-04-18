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
		[ClassificationType(ClassificationTypeNames = Constants.CSharpEventName)]
		public ClassificationTag Event;
		[ClassificationType(ClassificationTypeNames = Constants.CSharpFieldName)]
		public ClassificationTag Field;
		[ClassificationType(ClassificationTypeNames = Constants.CSharpPropertyName)]
		public ClassificationTag Property;
		[ClassificationType(ClassificationTypeNames = Constants.CSharpMethodName)]
		public ClassificationTag Method;
		[ClassificationType(ClassificationTypeNames = Constants.CSharpConstructorMethodName)]
		public ClassificationTag Constructor;

		public static readonly TransientMemberTagHolder Default = ClassificationTagHelper.InitFields<TransientMemberTagHolder>(t => new ClassificationTag(t));

		public static readonly TransientMemberTagHolder DeclarationBraces = ClassificationTagHelper.InitFields<TransientMemberTagHolder>(t => ClassificationTagHelper.CreateTransientClassificationTag(t, ClassificationTagHelper.DeclarationBrace));

		public static readonly TransientMemberTagHolder BoldDeclarationBraces = ClassificationTagHelper.InitFields<TransientMemberTagHolder>(t => ClassificationTagHelper.CreateTransientClassificationTag(t, ClassificationTagHelper.Bold, ClassificationTagHelper.DeclarationBrace));

		public static readonly TransientMemberTagHolder BoldBraces = ClassificationTagHelper.InitFields<TransientMemberTagHolder>(t => ClassificationTagHelper.CreateTransientClassificationTag(t, ClassificationTagHelper.Bold));

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

		public static readonly TransientKeywordTagHolder Default = ClassificationTagHelper.InitFields<TransientKeywordTagHolder>(t => new ClassificationTag(t));

		public static readonly TransientKeywordTagHolder Bold = ClassificationTagHelper.InitFields<TransientKeywordTagHolder>(t => ClassificationTagHelper.CreateTransientClassificationTag(t, ClassificationTagHelper.Bold));

		public TransientKeywordTagHolder Clone() {
			return (TransientKeywordTagHolder)MemberwiseClone();
		}
	}

	static class ClassificationTagHelper
	{
		public static readonly IClassificationType Declaration = ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationType(Constants.CSharpDeclarationName);
		public static readonly IClassificationType DeclarationBrace = ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationType(Constants.CSharpDeclarationBrace);
		public static readonly IClassificationType Bold = ServicesHelper.Instance.ClassificationTypeRegistry.GetClassificationType(Constants.CodeBold);
		public static readonly IClassificationType BoldDeclarationBrace = ServicesHelper.Instance.ClassificationTypeRegistry.CreateTransientClassificationType(DeclarationBrace, Bold);

		public static readonly ClassificationTag DeclarationBraceTag = new ClassificationTag(DeclarationBrace);
		public static readonly ClassificationTag BoldTag = new ClassificationTag(Bold);
		public static readonly ClassificationTag BoldDeclarationBraceTag = new ClassificationTag(BoldDeclarationBrace);

		public static ClassificationTag CreateTransientClassificationTag(params IClassificationType[] tags) {
			return new ClassificationTag(ServicesHelper.Instance.ClassificationTypeRegistry.CreateTransientClassificationType(tags));
		}
		public static ClassificationTag CreateTransientClassificationTag(params ClassificationTag[] tags) {
			return new ClassificationTag(ServicesHelper.Instance.ClassificationTypeRegistry.CreateTransientClassificationType(Array.ConvertAll(tags, i => i.ClassificationType)));
		}

		public static T InitFields<T>(Func<IClassificationType, ClassificationTag> tagBuilder)
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
						item.SetValue(h, tagBuilder(t));
					}
				}
			}
			return h;
		}
	}
}
