using System;
using System.Collections.Generic;
using System.Windows.Controls.Primitives;
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

		enum StringKind
		{
			Default,
			Interpolated,
			Verbatim,
			InterpolatedVerbatim,
			Raw,
			InterpolatedRaw
		}

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

		delegate void ProcessUrlPart(UrlPart part, int position, int length);

		/// <summary>
		/// Returns the position of ":" in URL, -1 if no valid scheme is detected.
		/// </summary>
		/// <param name="input">The string to parse.</param>
		/// <param name="kind">Kind of the string.</param>
		/// <param name="leading">The characters to be skipped at the beginning of <paramref name="input"/>. If colon is found, this value will be changed to the beginning position of the URL.</param>
		/// <param name="length">The number of characters to be checked.</param>
		static int FindUrlScheme(string input, StringKind kind, ref int leading, ref int length) {
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
					if (c == '\\' && kind < StringKind.Verbatim && IsCurrentBackslashEscaped(input, i, leading)) {
						// handle escaped sequence
						var end = leading + length;
						ParseEscape(input, ref i, ref end, ref c);
						if (c <= ' ') {
							length -= i + 1 - leading;
							leading = i + 1;
							goto FIND_COLON;
						}
					}
					else if (c <= ' ' && IsValidSchemeStart(input[++i])) {
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
			StringKind kind,
			ProcessUrlPart processPart,
			int interpolationLevel = 1) {
			var s = FindUrlScheme(input, kind, ref start, ref length);
			if (s < 0) {
				return -1;
			}
			processPart(UrlPart.Scheme, start, s - start);
			length -= (s + 3) - start; // skip "://", start from AfterScheme
			var end = TryParseUrlAfterScheme(input, s + 3, length, kind, processPart, interpolationLevel);
			if (end != -1) {
				processPart(UrlPart.FullUrl, start, end - start);
			}
			return end;
		}

		static int TryParseUrlAfterScheme(string input, int start, int length, StringKind kind, ProcessUrlPart processPart, int interpolationLevel) {
			int end = start + length;
			int urlStart = -1, partStart = 0, subPartStart = 0;
			var state = UrlParserState.AfterScheme;
			int interpolationDepth = 0;
			var interpolationStack = new Stack<UrlParserState>();

			for (int i = start; i < end; i++) {
				char c = input[i];

				// Handle interpolation
				if (state == UrlParserState.Interpolation) {
					HandleInterpolation(ref state, ref interpolationDepth, interpolationStack, c);
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

				if (c == '\\' && kind < StringKind.Verbatim && i + 1 < end) {
					ParseEscape(input, ref i, ref end, ref c);
					if (c <= 0x0020 && state != UrlParserState.Start) {
						if (state > UrlParserState.AfterScheme) {
							goto FINAL;
						}
						state = UrlParserState.Start;
						continue;
					}
				}
				// Handle URL end
				else if (c <= 0x0020 && state != UrlParserState.Start) {
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
						break;
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

		static bool IsCurrentBackslashEscaped(string input, int index, int leading) {
			bool e = true;
			while (--index >= leading && input[index] == '\\') {
				e = !e;
			}
			return e;
		}

		static void ParseEscape(string input, ref int i, ref int end, ref char c) {
			int v;
			switch (input[++i]) {
				case 't':
				case 'n':
				case 'r':
				case 'b':
				case 'f':
				case 'a':
				case 'v':
					c = '\0'; // mark end of url
					end = i - 1;
					return;
				case 'u':
					if ((v = TryDecodeHex(input, ref i, ref end, 4, 4)) > -1) {
						c = (char)v;
					}
					break;
				case 'x':
					if ((v = TryDecodeHex(input, ref i, ref end, 1, Math.Min(4, end - i))) > -1) {
						c = (char)v;
					}
					break;
				case 'U':
					if ((v = TryDecodeHex(input, ref i, ref end, 8, 8)) > -1) {
						c = v > Char.MaxValue ? Char.MaxValue : (char)v;
					}
					break;
				default:
					c = input[i];
					return;
			}
		}

		static int TryDecodeHex(string input, ref int index, ref int end, int minLength, int maxLength) {
			if (index + minLength >= end) {
				return -1;
			}
			var v = 0;
			var i = index;
			int n;
			for (n = 0; n < maxLength; n++) {
				int c = input[++i];
				if (c.IsBetween('0', '9')) {
					c -= '0';
				}
				else if (c.IsBetween('A', 'F')) {
					c -= 'A' - 10;
				}
				else if (c.IsBetween('a', 'f')) {
					c -= 'a' - 10;
				}
				else {
					if (n < minLength) {
						return -1;
					}
					break;
				}
				v <<= 4;
				v += c;
			}
			if (v <= 0x20) {
				end = index - 1;
			}
			index = n == maxLength ? i : i - 1;
			return v;
		}

		static void HandleInterpolation(ref UrlParserState state, ref int interpolationDepth, Stack<UrlParserState> interpolationStack, char c) {
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
