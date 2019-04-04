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
    /// <list type="number">
    /// <item><para>Generic class: <see cref="Dictionary{TKey, TValue}"/></para></item>
    /// <item><para>Langword: <see langword="true"/>, <see langword="null"/></para></item>
    /// <item><para>Error type: <see cref="NonExistingType"/></para></item>
    /// </list>
	/// <para>You can click and go to the definition of the type, if source code
	/// is accessible, like <see cref="MyEnum"/>, <see cref="MyStruct._instanceField"/>,
	/// <see cref="ConcreteClass.Method"/>, or <see cref="ConcreteClass.Method{TGeneric}"/>.</para>
	/// <para>The "<c>para</c>" elements no longer generate empty lines.</para>
	/// <para>You can style your comment with <b>bold</b>, <i>italic</i> and <u>underline</u>, or <b><i><u>combinations of them</u></i></b>.</para>
	/// </summary>
	/// <remarks>
	/// <para>Enable <i>Show <c>&lt;remarks&gt;</c> XML Doc</i> option to read remarks for symbols.</para>
    /// <para>You may also want to limit the max width and height of a Quick Info item by setting corresponding values in the Quick Info options.</para>
	/// <code><![CDATA[
	/// Console.WriteLine("some time &amp; some life");
	/// Console.ReadKey();
	/// ]]></code>
	/// </remarks>
	class Comments
	{
		/** hover on <see cref="Test"/> to see its text only XML doc, 
		 * if "Allow text only XML Doc option" is turned on */
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
		/// <exception cref="FormatException"><paramref name="format"/> is invalid for <see cref="DateTime.ToString(string)"/>. Validate this in <see cref="Test"/>.</exception>
		public static string FormatDate(DateTime date, string format) {
			return date.ToString(format);
		}

		/// <summary>
		/// Parses text as hexdemical number.
		/// </summary>
		/// <param name="text">The text to be parsed as hex.</param>
		/// <returns>The result number.</returns>
		public static int ParseAsHexNumber(string text) {




			return Int32.Parse(text, System.Globalization.NumberStyles.HexNumber);




		}
	}
}
