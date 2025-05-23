using System;

namespace TestProject.Feature
{
	static class UrlHighlighter
	{
		static readonly string[] Valid = [
				"http://example.com/path",
				"http://example.com/path/to/file",
				"https://user:pass@site.com:8080/path?name=value#frag",
				"https://user@site.com:8080",
				"https://user@site.com/path",
				"http://site.com/search?q={value}",
				"http://site.com:8080/search?q={value}",
				"http://site.com/search?q={{value}}",
				"http://site.com/search?q={value with space}",
				"http://site.com/search?q={value with space}&q2={://}",
				"http://example.com/search?q=id{value{more}",
				"http://example.com/search?q=id{value{more}&val",
				"http://example.com/{action}",
				"http://host/{resource}/action",
				"http://host/{date:yy-mm-dd}/{action}/id",
				"http://host",
				"http://host:",
				"http://host:?",
				"http://host:/",
				"http://host:/?",
				"http://host:?#",
				"http://host:#/",
				"http://host:80",
				"http://host?",
				"http://host/",
				"http://host#",
				"http://host?#",
				"s://a",
			];

		static readonly string[] WithinText = [
				"Some text http://example.com and more http://more.url/ here",
				"Valid host, invalid port: http://example.com:invalid_port and valid url: http://more.url/ here",
				" http://example.com",
				"http://example.com more",
				"http://example.com: more",
				"http://example.com/ more",
				"http://example.com/url more",
				"http://example.com? more",
				"http://example.com# more",
				"http://example.com:65535 more",
				"http://example.com:80/ more",
			];

		static readonly string[] Invalid = [
				"s://", // no host
				"s:",
				"s:site.com",
				"this is not a url",
				"example.com/path",
				"http://example.com:xb/url",
				"http://example.com:65536/url",
				"invalid *tp://example.com and valid ftp://h, invalid x://, and valid ftp://a",
				"http:example.com",
				"://",
				":/",
				"//site.com",
			];

		static readonly string[] Interpolated = [
				$"http://{new Random().Next()}.host/{DateTime.Today:yy-mm-dd}/{"action"}/id",
				@$"http://{new Random().Next()}.host/{DateTime.Today:yy-mm-dd}/{"action"}/id",
			];

		static readonly string[] Raw = [
				"""http://host""",
				$$"""http://host/{{DateTime.Now.Year}}""",
				"""
					URL List:
					http://url1
					ws://url/2
				"""
			];

		static void Url() {
			var jh = (firstName: "Jupiter", lastName: "Hammon", born: 1711, published: 1761);

			var u = $"http://search.engine.com/search?born={jh.born}";
			u = $"http://search.engine.com/search?firstName={jh.firstName}&lastName={jh.lastName}&born={jh.born}\nhttp://another.site/search?name={jh.firstName + " " + jh.lastName}";
		}
	}
}
