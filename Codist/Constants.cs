using System;
using System.ComponentModel;
using System.Windows.Media;

namespace Codist
{
	#region Demo area
	/////////////////////////////////////////
	////COPYRIGHT
	//The quick brown fox jumps over the lazy dog

	/*? hallo for en kommentar!? */
	/*! A long comment - will it get bold!? 
     * Should this be bold as well?
     * Another line
	*/
	/*!? Normal comment - here '*/
	#endregion

	/// <summary>
	/// Comment style constants
	/// </summary>
	static class Constants
	{
		public const string DefaultComment = "Comment";
		public const string XmlDocComment = "xml doc comment - text";
		public const string XmlDocTag = "xml doc comment - name";
		public const string ClassName = "class name";
		public const string StructName = "struct name";
		public const string EnumName = "enum name";
		public const string InterfaceName = "interface name";
		public const string Keyword = "Keyword";
		public const string PreProcessorKeyword = "preprocessor keyword";
		//! Important
		//# Notice
		public const String EmphasisComment = "Comment - Emphasis";
		//? Question
		public const String QuestionComment = "Comment - Question";
		//!? Exclaimation
		public const String ExclaimationComment = "Comment - Exclaimation";
		//x Removed
		public const String DeletionComment = "Comment - Deletion";

		//TODO: This does not need work
		public const String TodoComment = "Comment Task - ToDo";
		//NOTE: Watch-out!
		public const String NoteComment = "Comment Task - Note";
		//Hack: B-)
		public const String HackComment = "Comment Task - Hack";

		//+++ heading 1
		public const string Heading1Comment = "Comment: Heading 1";
		//++ heading 2
		public const string Heading2Comment = "Comment: Heading 2";
		//+ heading 3
		public const string Heading3Comment = "Comment: Heading 3";
		//- heading 4
		public const string Heading4Comment = "Comment: Heading 4";
		//-- heading 5
		public const string Heading5Comment = "Comment: Heading 5";
		//--- heading 6
		public const string Heading6Comment = "Comment: Heading 6";

		public const string ReturnKeyword = "Keyword: Return";
		public const string ExitKeyword = "Keyword: Exit";

		public static readonly Color CommentColor = Colors.Green;
		public static readonly Color QuestionColor = Colors.MediumPurple;
		public static readonly Color ExclaimationColor = Colors.IndianRed;
		public static readonly Color DeletionColor = Colors.Gray;
		public static readonly Color ToDoColor = Colors.DarkBlue;
		public static readonly Color NoteColor = Colors.Orange;
		public static readonly Color HackColor = Colors.Black;
		public static readonly Color ExitColor = Colors.MediumBlue;
		public static readonly Color ReturnColor = Colors.Blue;
	}

	enum CommentStyle
	{
		[Description(Constants.DefaultComment)]
		Default,
		[Description(Constants.XmlDocComment)]
		XmlDocComment,
		[Description(Constants.XmlDocTag)]
		XmlDocTag,
		[Description(Constants.EmphasisComment)]
		Emphasis,
		[Description(Constants.QuestionComment)]
		Question,
		[Description(Constants.ExclaimationComment)]
		Exclaimation,
		[Description(Constants.DeletionComment)]
		Deletion,
		[Description(Constants.TodoComment)]
		ToDo,
		[Description(Constants.NoteComment)]
		Note,
		[Description(Constants.HackComment)]
		Hack,
		[Description(Constants.Heading1Comment)]
		Heading1,
		[Description(Constants.Heading2Comment)]
		Heading2,
		[Description(Constants.Heading3Comment)]
		Heading3,
		[Description(Constants.Heading4Comment)]
		Heading4,
		[Description(Constants.Heading5Comment)]
		Heading5,
		[Description(Constants.Heading6Comment)]
		Heading6
	}

	enum CommentStyleApplication
	{
		Content,
		Tag,
		TagAndContent
	}
}
