using System;

namespace TestProject.Language.Strings;

public class StringTokens
{
	// Use a const string to prevent 'message4' from
	// being used to store another string value.
	const string message4 = "You can't get rid of me!";

	void Variables() {
		// Initialize with a regular string literal.
		string oldPath = "c:\\Program Files\\Microsoft Visual Studio 8.0";

		// Initialize with a verbatim string literal.
		var newPath = @"c:\Program Files\Microsoft Visual Studio 9.0";
	}

	void Escaping() {
		string columns = "Column 1\tColumn 2\tColumn 3";
		string rows = "Row 1\r\nRow 2\r\nRow 3";
		string t = "\x20=\u0020=\U00000020";
		const string title = "\"The \u00C6olean Harp\", by Samuel Taylor Coleridge";
		string quote = @"Her name was ""Sara.""";
	}

	void Multiline() {
		string text = @"My pensive SARA ! thy soft cheek reclined
    Thus on mine arm, most soothing sweet it is
    To sit beside our Cot,...";
	}

	void RawString() {
		const string singleLine = """"" """"";
		const string multiLine = """
    "Hello World!" is typically the first program someone writes.
    """;
		const string embeddedXML = """
       <element attr = "content">
           <body style="normal">
               Here is the main text
           </body>
           <footer>
               Excerpts from "An amazing story"
           </footer>
       </element>
       """;
		string rawStringLiteralDelimiter = """"
            Raw string literals are delimited 
            by a string of at least three double quotes,
            like this: """
            """";
	}

	void Interpolation() {
		var jh = (firstName: "Jupiter", lastName: "Hammon", born: 1711, published: 1761);

		Console.WriteLine($"{jh.firstName} {jh.lastName} was an African American poet born in {jh.born}.");
		Console.WriteLine($"He was first published in {jh.published} at the age of {jh.published - jh.born}.");
		Console.WriteLine($"He'd be over {Math.Round((2018d - jh.born) / 100d) * 100d} years old today.");

		Console.WriteLine($@"{jh.firstName} {jh.lastName}
    was an African American poet born in {jh.born}.");

		Console.WriteLine(@$"He was first published in {jh.published}
at the age of {jh.published - jh.born}.");

		int X = 2;
		int Y = 3;

		var pointMessage = $$"""The point {{{X}}, {{Y}}} is {{Math.Sqrt(X * X + Y * Y)}} from the origin.""";
	}
}
