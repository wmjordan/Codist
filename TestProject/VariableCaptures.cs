using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProject
{
	class VariableCaptures
	{
		public static async void Test(Exception exception) {
			DateTime now = DateTime.Now;
			string file = $"log{now.ToShortDateString()}.txt";
			// captures exception, now, file
			await Task.Run(() => WriteLog());
			WriteLog();
			// captures exception, now, file
			await Task.Run(WriteLog);
			// captures exception, now
			await Task.Run((Action)WriteConsole);
			WriteConsole();
			// captures <nothing>
			WriteTextToConsole(null);
			// captures exception
			await Task.Run(() => {
				var ex = exception.ToString();
				System.Diagnostics.Debug.WriteLine(ex);
			});
			// captures exception
			await Task.Run(() => System.Diagnostics.Debug.WriteLine(exception));
			// captures now
			Array.FindAll(new[] { DateTime.MaxValue, DateTime.Now }, t => t > now);
			// captures exception, now, file
			void WriteLog() {
				var ex = exception.ToString();
				System.IO.File.AppendAllLines(file, new[] { ex });
				WriteConsole();
			}
			// captures exception, now
			void WriteConsole() {
				Console.Write(now);
				Console.WriteLine(exception.Message);
			}
			// captures <nothing>
			Action WriteTextToConsole(string text) {
				// captures text
				return () => {
					Console.WriteLine(text);
				};
			}
		}
	}
}
