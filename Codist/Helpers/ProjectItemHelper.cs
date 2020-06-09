using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace Codist
{
	static class ProjectItemHelper
	{
		public static bool GetItemAttribute(this IVsBuildPropertyStorage propertyStorage, uint item, string name, bool defaultValue) {
			string value;
			bool result;
			if (ErrorHandler.Succeeded(propertyStorage.GetItemAttribute(item, name, out value)) &&
				bool.TryParse(value, out result)) {
				return result;
			}

			return defaultValue;
		}

		public static void SetItemAttribute(this IVsBuildPropertyStorage propertyStorage, uint item, string name, bool value, bool defaultValue) {
			if (value != defaultValue) {
				propertyStorage.SetItemAttribute(item, name, value.ToString().ToLowerInvariant());
			}
			else {
				propertyStorage.SetItemAttribute(item, name, null);
			}
		}

		public static string GetItemAttribute(this IVsBuildPropertyStorage propertyStorage, uint item, string name, string defaultValue) {
			string value;
			return ErrorHandler.Succeeded(propertyStorage.GetItemAttribute(item, name, out value)) ? value : defaultValue;
		}

		public static void SetItemAttribute(this IVsBuildPropertyStorage propertyStorage, uint item, string name, string value, string defaultValue) {
			if (value != defaultValue) {
				propertyStorage.SetItemAttribute(item, name, value);
			}
			else {
				propertyStorage.SetItemAttribute(item, name, null);
			}
		}

		public static bool GetGlobalProperty(this IVsBuildPropertyStorage propertyStorage, string propertyName, bool defaultValue) {
			var value = propertyStorage.GetGlobalProperty(propertyName, defaultValue.ToString(CultureInfo.InvariantCulture).ToLowerInvariant());
			bool result;
			if (!bool.TryParse(value, out result)) {
				return defaultValue;
			}

			return result;
		}

		public static void SetGlobalProperty(this IVsBuildPropertyStorage propertyStorage, string propertyName, bool value, bool defaultValue) {
			propertyStorage.SetGlobalProperty(propertyName,
				value.ToString(CultureInfo.InvariantCulture).ToLowerInvariant(),
				defaultValue.ToString(CultureInfo.InvariantCulture).ToLowerInvariant());
		}

		public static string GetGlobalProperty(this IVsBuildPropertyStorage propertyStorage, string propertyName, string defaultValue) {
			string value;

			// Get the evaluated property value with null condition.
			// Why null and not String.Empty?
			// There is performance and (functionality implications) for String.Empty condition. 
			// String.Empty will force us to reevaluate the project with Config = "" and Platform = "", read the value, 
			// and reevaluate back. This ensures that only not conditional property value is read.But the price is we reevaluating twice. 
			// "null" means we will just read the value using the current evaluation setitings. 
			// If property is defined in "gobal" secitopn this is the same.
			// If property is "overriden" in "confug" section it will in fact get this value, 
			// but apart that the contract is already broken, that would be the value 
			// that will be assigned during build for task to access anyway so it will be the "truth".
			int hr = propertyStorage.GetPropertyValue(propertyName, null, (uint)_PersistStorageType.PST_PROJECT_FILE, out value);
			if (!ErrorHandler.Succeeded(hr) || string.IsNullOrEmpty(value)) {
				return defaultValue;
			}

			return value;
		}

		public static void SetGlobalProperty(this IVsBuildPropertyStorage propertyStorage, string propertyName, string value, string defaultValue) {
			if (string.IsNullOrEmpty(value) || value == defaultValue) {
				//propertyStorage.RemoveProperty(
				//	propertyName,
				//	string.Empty,
				//	(uint)_PersistStorageType.PST_PROJECT_FILE);

				// Removing the value never works, so it's worse to leave it empty than to serialize the 
				// default value, I think.
				propertyStorage.SetPropertyValue(
					propertyName,
					string.Empty,
					(uint)_PersistStorageType.PST_PROJECT_FILE,
					defaultValue);
			}
			else {
				// Set the property with String.Empty condition to ensure it's global (not configuration scoped).
				propertyStorage.SetPropertyValue(
					propertyName,
					string.Empty,
					(uint)_PersistStorageType.PST_PROJECT_FILE,
					value);
			}
		}
	}
}
