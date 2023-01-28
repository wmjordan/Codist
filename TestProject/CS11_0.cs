using System;
using System.Linq.Expressions;

namespace TestProject.CS11_0;

class Strings
{
	const string Raw = """This\is\all "content"!""",
		MultiLine = """
    <element attr="content">
      <body>
        This line is indented by 4 spaces.
      </body>
    </element>
    """;

	void F() {
		var u8 = "This is a UTF-8 string!"u8;
	}
}
