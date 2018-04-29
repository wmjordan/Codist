using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TestProject
{
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
	/// <para>The "para" elements no longer take an empty line.</para>
	/// </summary>
	class Comments
	{
	}
}
