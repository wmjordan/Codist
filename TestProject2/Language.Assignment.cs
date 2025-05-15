using System;
using System.Collections.Generic;

namespace TestProject.Language.Assignment;

static class NullConditionalAssignment
{
	public sealed class O
	{
		public string Name { get; internal set; }
	}

	public static void SetName(O item, object value) {
		item?.Name = value?.ToString();
	}
}
