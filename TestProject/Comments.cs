using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TestProject
{
	//note: Turns on "Override default XML Doc" of Super Quick Info, and move your mouse
	// on to the following code to see the overriden quick info in effect
	/// <summary>
	/// <para>Codist can override the existing Quick Info.</para>
	/// <para>The overriden Quick Info does not show qualified names to
	/// make the text more concise.
	/// Nevertheless, you can hover your mouse onto the members to see their full names.</para>
	/// <para>Generic class: <see cref="Dictionary{TKey, TValue}"/></para>
	/// <para>Langword: <see langword="true"/>, <see langword="null"/></para>
	/// <para>Error type: <see cref="NonExistingType"/></para>
	/// <para>You can click and go to the definition of the type, if source code
	/// is accessible, like <see cref="MyEnum"/>, <see cref="MyStruct._instanceField"/>,
	/// <see cref="ConcreteClass.Method"/>, or <see cref="ConcreteClass.Method{TGeneric}"/>.</para>
	/// <para>The "para" elements no longer generate empty lines.</para>
	/// <para>You can style your comment with <b>bold</b>, <i>italic</i> and <u>underline</u>, or <b><i><u>combinations of them</u></i></b>.</para>
	/// </summary>
	class Comments
	{
		/** text only XML doc */
		void Test() {
			// hover on Comments to see overriden XML Doc
			var c = new Comments();
			// hover on the FormatDate method to see the content of "returns"
			// hover on DateTime.Now or "yyyy-MM-dd" to see the parameter XML Doc
			var fd = FormatDate(DateTime.Now, "yyyy-MM-dd");
		}
		/// <summary>
		/// Formats the <paramref name="date"/> with the given <paramref name="format"/>.
		/// </summary>
		/// <param name="date">The date to be formatted to string.</param>
		/// <param name="format">The format for the date.</param>
		/// <returns>The formatted <see cref="DateTime"/>.</returns>
		public static string FormatDate(DateTime date, string format) {
			return date.ToString(format);
		}
	}
}
