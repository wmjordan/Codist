using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProject
{
	/// <summary>
	/// This XML doc will be applied to namespace <see cref="TestProject"/>.
	/// </summary>
	/// <remarks>
	/// <para>To keep the <see cref="NamespaceDoc"/> class from appearing in the help file, leave off the <see langword="public"/> keyword and mark it with a <see cref="System.Runtime.CompilerServices.CompilerGeneratedAttribute"/> attribute. This will cause the class to be automatically ignored when reflection information is generated for the assembly. The following is an example (select and right click to copy code):</para>
	/// <code><![CDATA[
	/// /// <summary>
	/// /// These are the namespace comments.
	/// /// </summary>
	/// [System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
	/// class NamespaceDoc
	/// {
	/// }
	/// ]]>
	/// </code>
	/// </remarks>
	[System.Runtime.CompilerServices.CompilerGenerated]
	struct NamespaceDoc
	{
	}

	//note: Turns on "Override default XML Doc" of Super Quick Info, and move your mouse
	// on to the following code to see the overridden quick info in effect
	/// <summary>
	/// <para>Codist can override the existing Quick Info.</para>
	/// <para>The overridden Quick Info does not show qualified names to
	/// make the text more concise.<br/>
	/// Nevertheless, you can hover your mouse onto the members to see their full names.</para>
	/// <list type="number">
	/// <item><para>Generic class: <see cref="Dictionary{TKey, TValue}"/></para></item>
	/// <item><para>Langword: <see langword="true"/>, <see langword="null"/></para></item>
	/// <item><para>Error type: <see cref="NonExistingType"/></para></item>
	/// </list>
	/// <para>You can click and go to the definition of the type, if source code
	/// is accessible, like <see cref="MyEnum"/>, <see cref="MyStruct._instanceField"/>,
	/// <see cref="ConcreteClass.Method"/>, or <see cref="ConcreteClass.Method{TGeneric}"/>.</para>
	/// <para>The "<c>para</c>" elements no longer render empty lines.</para>
	/// <para>You can style your comment with <b>bold</b>, <i>italic</i> and <u>underline</u>, or <b><i><u>combinations of them</u></i></b>.</para>
	/// <para>You can separate contents with horizontal lines with <c>&lt;hr/></c> tags and semantically segment your paragraphs with <c>&lt;h1></c> to <c>&lt;h6></c> tags.</para>
	/// <h1>Heading 1</h1>
	/// <hr/>
	/// <para>See also: <seealso cref="NamespaceDoc"/>, <seealso cref="Test"/></para>
	/// </summary>
	/// <remarks>
	/// <para>Enable <i>Show <c>&lt;remarks&gt;</c> XML Doc</i> option to read remarks for symbols.</para>
	/// <para>You may also want to limit the max width and height of a Quick Info item by setting corresponding values in the Quick Info options.</para>
	/// <code><![CDATA[
	/// Console.WriteLine("press any key to continue.");
	/// Console.ReadKey();
	///
	/// Console.WriteLine("bye!");
	/// ]]></code>
	/// </remarks>
	class Comments
	{
		/** hover on <see cref="Test"/> to see its text only XML doc, 
		 * if "Allow text only XML Doc option" is turned on */
		void Test() {
			// hover on Comments to see overridden XML Doc
			var c = new Comments();
			/* hover on the FormatDate method to see the content of "returns"
			   hover on DateTime.Now or "yyyy-MM-dd" to see the parameter XML Doc*/
			var fd = FormatDate(DateTime.Now, "yyyy-MM-dd");
			Throw();
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
		/// Parses text as hexadecimal number.
		/// </summary>
		/// <param name="text">The text to be parsed as hex.</param>
		/// <returns>The result number.</returns>
		public static async Task<int> ParseAsHexNumber(string text) {
			try {
				return Int32.Parse(text, System.Globalization.NumberStyles.HexNumber);
			}
			catch (Exception ex) {
				await Log(ex);
				throw;
			}
		}

		/// <summary>
		/// <para>This method is used to test captured variables on Quick Info.</para>
		/// <para>More contents.</para>
		/// <list type="number">
		/// <item><para>item 1</para></item>
		/// <item><para>item 2</para></item>
		/// </list>
		/// <para>Ending.</para>
		/// </summary>
		/// <summary>A redundant summary.</summary>
		/// <typeparam name="T">T is exception class.</typeparam>
		/// <exception cref="System.IO.IOException"><para>Not implemented</para><para>Period.</para></exception>
		/// <seealso cref="Print{TArg1, TArg2}(TArg1, TArg2)"/>
		public static async Task Log<T>(T exception) where T : Exception {
			await Task.Run(() => Console.WriteLine(exception.ToString()));
		}

		/// <returns>It never returns.</returns>
		/// <exception cref="NotImplementedException">Boo boo.</exception>
		[return: My(new[] { typeof(int), typeof(string), typeof(DateTime), null })]
		[Name(SharedConstants.A)]
		public static string Throw() {
			throw new NotImplementedException("boo-boo");
		}

		/// <summary>Converts value.</summary>
		/// <typeparam name="TSource">Source type.</typeparam>
		/// <typeparam name="TValue">Target type.</typeparam>
		/// <param name="source">source value.</param>
		/// <returns>Returns converted value.</returns>
		public static TValue Convert<TSource, TValue> (TSource source)
			where TSource : struct, IConvertible
			where TValue : struct, IConvertible {
			return (TValue)System.Convert.ChangeType(source, typeof(TValue));
		}

		/// <summary>Prints two variables.</summary>
		/// <typeparam name="TArg1">Type of var1.</typeparam>
		/// <typeparam name="TArg2">Type of var2.</typeparam>
		/// <param name="a">Var1.</param>
		/// <param name="b">Var2.</param>
		public static void Print<TArg1, TArg2>(TArg1 a, TArg2 b)
			where TArg1 : class {
			Console.WriteLine(a);
			Console.WriteLine(b);
		}

		static void TestConvert() {
			// Hover on methods to test the display of type arguments
			var l = Convert<int, long>(1);
			var i = Convert<long, int>(l);
			// Hover to test the display of anonymous type as type arguments
			Print(new { a = 1, b = 2, c = 3, d = new { x = 1, y = l } }, new { X = 1, Y = new Dictionary<int, int>() });
		}

		static class SharedConstants
		{
			public const string A = "A";
		}
	}

	[System.AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
	sealed class MyAttribute : Attribute
	{
		public MyAttribute(Type[] types) {
		}

		public Type[] Types { get; }

		// This is a named argument
		public int Num { get; set; }
	}

	[System.AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = true)]
	sealed class NameAttribute : Attribute
	{
		public NameAttribute(string name, Type type = null) {
			Name = name;
			Type = type;
		}

		public string Name { get; }
		public Type Type { get; }
	}
}
