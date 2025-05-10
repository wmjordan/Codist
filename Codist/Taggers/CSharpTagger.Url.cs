using System;
using System.Collections.Generic;
using CLR;

namespace Codist.Taggers
{
	partial class CSharpTagger
	{
		enum UrlPart
		{
			FullUrl,
			Scheme,
			Credential,
			Host,
			Port,
			FileName,
			LastSegment = FileName,
			QueryQuestionMark,
			QueryName,
			QueryValue,
			Fragment
		}

		delegate void ProcessUrlPart(UrlPart part, int position, int length);

		enum UrlParserState
		{
			Start,
			Scheme,
			AfterScheme,
			Authority,
			UserInfo,
			Host,
			Port,
			Path,
			Query,
			Fragment,
			Interpolation,
			EndOfHostAndPort
		}

		/// <summary>
		/// Returns the position of ":" in URL, -1 if no valid scheme is detected.
		/// </summary>
		/// <param name="input">The string to parse.</param>
		/// <param name="leading">The characters to be skipped at the beginning of <paramref name="input"/>. If colon is found, this value will be changed to the beginning position of the URL.</param>
		/// <param name="length">The number of characters to be checked.</param>
		static int FindUrlScheme(string input, ref int leading, ref int length) {
		FIND_COLON:
			var i = input.IndexOf(':', leading, length); // head for the ":" in "://"
			if (i < 0) {
				return i;
			}
			char c;
			if (i > leading
				&& i + 3 < leading + length
				&& input[i + 1] == '/'
				&& input[i + 2] == '/'
				&& IsValidHostStart(input[i + 3])) {
				int p = i;
				do {
					--i;
					if (IsValidSchemeChar(c = input[i])) {
						continue;
					}
					if (c <= ' ' && IsValidSchemeStart(input[++i])) {
						length -= i - leading;
						leading = i;
						return p;
					}
					break;
				}
				while (i > leading);

				if (i == leading) {
					return p;
				}
				length -= p + 3 - leading;
				leading = p + 3;
				goto FIND_COLON;
			}
			length -= i + 1 - leading;
			leading = i + 1;
			goto FIND_COLON;
		}

		static int TryParseUrl(
			string input,
			int start,
			int length,
			ProcessUrlPart processPart,
			int interpolationLevel = 1) {
			var s = FindUrlScheme(input, ref start, ref length);
			if (s < 0) {
				return -1;
			}
			processPart(UrlPart.Scheme, start, s - start);
			length -= (s + 3) - start; // skip "://", start from AfterScheme
			var end = TryParseUrlAfterScheme(input, s + 3, length, processPart, interpolationLevel);
			if (end != -1) {
				processPart(UrlPart.FullUrl, start, end - start);
			}
			return end;
		}

		private static int TryParseUrlAfterScheme(string input, int start, int length, ProcessUrlPart processPart, int interpolationLevel) {
			int end = start + length;
			int urlStart = -1, partStart = 0, subPartStart = 0;
			var state = UrlParserState.AfterScheme;
			int interpolationDepth = 0;
			var interpolationStack = new Stack<UrlParserState>();

			for (int i = start; i < end; i++) {
				char c = input[i];

				// Handle interpolation
				if (state == UrlParserState.Interpolation) {
					if (c == '}') {
						interpolationDepth--;
						if (interpolationDepth == 0) {
							// Return to previous state
							state = interpolationStack.Pop();
						}
					}
					else if (c == '{') {
						interpolationDepth++;
					}
					continue;
				}

				// Check for interpolation start
				if (c == '{' && IsInterpolationStart(input, i, end, interpolationLevel)) {
					interpolationStack.Push(state);
					state = UrlParserState.Interpolation;
					interpolationDepth = interpolationLevel;
					i += interpolationLevel - 1;
					continue;
				}

				// Handle URL end
				if (c <= 0x0020 && state != UrlParserState.Start) {
					// Finalize current part if any
					if (state > UrlParserState.AfterScheme) {
						end = i;
						goto FINAL;
					}
					state = UrlParserState.Start;
					continue;
				}

				switch (state) {
					case UrlParserState.Start:
						if (IsValidSchemeStart(c)) {
							urlStart = i;
							partStart = i;
							state = UrlParserState.Scheme;
						}
						break;

					case UrlParserState.Scheme:
						if (c == ':') {
							processPart(UrlPart.Scheme, partStart, i - partStart);
							state = UrlParserState.AfterScheme;
							i += 2; // Skip the "//"
							partStart = i + 1;
						}
						else if (IsValidSchemeChar(c) == false) {
							urlStart = -1;
							partStart = 0;
							state = UrlParserState.Start;
						}
						break;

					case UrlParserState.AfterScheme:
						state = UrlParserState.Authority;
						urlStart = i;
						partStart = i;
						goto case UrlParserState.Authority;

					case UrlParserState.Authority:
						if (c == '@') {
							// We have user:password
							processPart(UrlPart.Credential, partStart, i - partStart);
							partStart = i + 1;
							state = UrlParserState.Host;
						}
						else if (c == ':') {
							// Could be port or password
							state = UrlParserState.UserInfo;
							subPartStart = i;
						}
						else if (c == '/' || c == '?' || c == '#') {
							// No credentials, this is the host
							processPart(UrlPart.Host, partStart, i - partStart);
							partStart = i;
							subPartStart = i + 1;
							goto case UrlParserState.EndOfHostAndPort;
						}
						break;

					case UrlParserState.UserInfo:
						if (c == '@') {
							// It was user:password
							processPart(UrlPart.Credential, partStart, i - partStart);
							partStart = i + 1;
							state = UrlParserState.Host;
						}
						else if (c == '/' || c == '?' || c == '#') {
							// It was actually a port
							processPart(UrlPart.Host, partStart, subPartStart - partStart);
							if (IsValidPort(input, subPartStart + 1, i - subPartStart - 1) == false) {
								return -1;
							}
							processPart(UrlPart.Port, ++subPartStart, i - subPartStart);
							partStart = i;
							subPartStart = i + 1;
							goto case UrlParserState.EndOfHostAndPort;
						}
						break;

					case UrlParserState.Host:
						if (c == ':') {
							processPart(UrlPart.Host, partStart, i - partStart);
							state = UrlParserState.Port;
							partStart = i + 1;
						}
						else if (c == '/' || c == '?' || c == '#') {
							processPart(UrlPart.Host, partStart, i - partStart);
							partStart = i;
							subPartStart = i + 1;
							goto case UrlParserState.EndOfHostAndPort;
						}
						break;

					case UrlParserState.Port:
						if (c == '/' || c == '?' || c == '#') {
							processPart(UrlPart.Port, partStart, i - partStart);
							partStart = i;
							subPartStart = i + 1;
							goto case UrlParserState.EndOfHostAndPort;
						}
						else if (!char.IsDigit(c)) {
							// Invalid port, reset
							state = UrlParserState.Start;
							urlStart = -1;
						}
						break;

					case UrlParserState.Path:
						if (c == '/') {
							subPartStart = i + 1;
						}
						else if (c == '?' || c == '#') {
							// Report last segment if any
							if (subPartStart != 0) {
								processPart(UrlPart.FileName, subPartStart, i - subPartStart);
							}
							partStart = i;
							goto case UrlParserState.EndOfHostAndPort;
						}
						break;

					case UrlParserState.Query:
						if (c == '#') {
							ParseQueryString(input, partStart, i - partStart, processPart);
							partStart = i;
							state = UrlParserState.Fragment;
						}
						break;

					case UrlParserState.Fragment:
						// Nothing to do, just consume until end
						break;

					case UrlParserState.EndOfHostAndPort:
						switch (c) {
							case '/':
								state = UrlParserState.Path;
								break;
							case '?':
								state = UrlParserState.Query;
								processPart(UrlPart.QueryQuestionMark, partStart, 1);
								partStart++;
								break;
							case '#':
								state = UrlParserState.Fragment;
								break;
						}
						break;
				}
			}

			// Finalize at end of input
			if (urlStart == -1) {
				return -1;
			}
			if (state == UrlParserState.Interpolation) {
				while (interpolationStack.Count != 0) {
					state = interpolationStack.Pop();
				}
			}
		FINAL:
			if (state > UrlParserState.AfterScheme) {
				switch (state) {
					case UrlParserState.Authority:
					case UrlParserState.Host:
						processPart(UrlPart.Host, partStart, end - partStart);
						break; ;
					case UrlParserState.UserInfo:
						if (subPartStart != 0) {
							processPart(UrlPart.Host, partStart, subPartStart - partStart);
							partStart = subPartStart + 1;
							if (IsValidPort(input, partStart, end - partStart)) {
								goto case UrlParserState.Port;
							}
							return subPartStart;
						}
						break;
					case UrlParserState.Port:
						processPart(UrlPart.Port, partStart, end - partStart);
						break;
					case UrlParserState.Path:
						ReportLastSegment(input, partStart, end, processPart);
						break;
					case UrlParserState.Query:
						ParseQueryString(input, partStart, end - partStart, processPart);
						break;
					case UrlParserState.Fragment:
						processPart(UrlPart.Fragment, partStart, end - partStart);
						break;
					case UrlParserState.Start:
					case UrlParserState.Scheme:
					case UrlParserState.AfterScheme:
					case UrlParserState.Interpolation:
					case UrlParserState.EndOfHostAndPort:
						// should not be here
						return -1;
				}
			}
			return end;
		}

		private static void ReportLastSegment(string input, int pathStart, int pathEnd, ProcessUrlPart processPart) {
			int lastSlash = input.LastIndexOf('/', pathEnd - 1, pathEnd - pathStart);
			if (lastSlash != -1 && lastSlash + 1 < pathEnd) {
				processPart(UrlPart.LastSegment, lastSlash + 1, pathEnd - lastSlash - 1);
			}
		}

		private static bool IsInterpolationStart(string input, int pos, int end, int level) {
			for (int i = 1; i < level; i++) {
				if (pos + i >= end || input[pos + i] != '{')
					return false;
			}
			return true;
		}

		static bool IsValidSchemeStart(char c) {
			return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
		}

		static bool IsValidHostStart(char c) {
			return c.IsBetween('a', 'z') || c.IsBetween('0', '9') || c.IsBetween('A', 'Z');
		}

		static bool IsValidSchemeChar(char c) {
			return IsValidSchemeStart(c) || char.IsDigit(c) || c == '+' || c == '-' || c == '.';
		}

		static bool IsValidPort(string input, int start, int length) {
			int p = 0, d;
			for (int i = 0; i < length; i++) {
				if ((d = input[start + i] - '0').IsBetween(0, 9) == false) {
					return false;
				}
				p = p * 10 + d;
				if (p > UInt16.MaxValue) {
					return false;
				}
			}
			return true;
		}

		static void ParseQueryString(string input, int start, int length, ProcessUrlPart processPart) {
			int end = start + length;
			int pos = start;

			while (pos < end) {
				int equalPos = input.IndexOf('=', pos, end - pos);
				if (equalPos == -1) {
					processPart(UrlPart.QueryName, pos, end - pos);
					break;
				}

				processPart(UrlPart.QueryName, pos, equalPos - pos);

				int valueStart = equalPos + 1;
				int nextAmp = input.IndexOf('&', valueStart, end - valueStart);
				int valueEnd = nextAmp == -1 ? end : nextAmp;

				if (valueEnd > valueStart) {
					processPart(UrlPart.QueryValue, valueStart, valueEnd - valueStart);
				}

				pos = valueEnd + 1;
			}
		}
	}
}
