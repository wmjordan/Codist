using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Media;
using AppHelpers;
using Microsoft.VisualStudio.Text.Classification;

namespace Codist.SyntaxHighlight
{
	public enum SyntaxCategory
	{
		Uncategorized,
		Common,
		[Description("Tagged comments")]
		CommentTag,
		[Description("C#")]
		CSharp,
		[Description("  symbol markers")]
		CSharpMarker,
		[Description("C/C++")]
		CPlusPlus,
		[Description("CSS, Less, SCSS")]
		CSS,
		Markdown,
		[Description("HTML")]
		Html,
		[Description("XML")]
		Xml,
		[Description("XML Doc comment")]
		XmlDocComment,
		Razor,
		Regex,
	}

}
