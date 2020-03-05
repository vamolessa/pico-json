using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace PicoJson.Untyped
{
	public struct JsonArray : IEnumerable
	{
		internal List<JsonValue> collection;

		public void Add(JsonValue value)
		{
			if (collection == null)
				collection = new List<JsonValue>();
			collection.Add(value);
		}

		public IEnumerator GetEnumerator()
		{
			if (collection == null)
				collection = new List<JsonValue>();
			return collection.GetEnumerator();
		}
	}

	public struct JsonObject : IEnumerable
	{
		internal Dictionary<string, JsonValue> collection;

		public void Add(string key, JsonValue value)
		{
			if (collection == null)
				collection = new Dictionary<string, JsonValue>();
			collection.Add(key, value);
		}

		public IEnumerator GetEnumerator()
		{
			if (collection == null)
				collection = new Dictionary<string, JsonValue>();
			return collection.GetEnumerator();
		}
	}

	public readonly struct JsonValue
	{
		private static readonly List<JsonValue> EmptyValues = new List<JsonValue>();

		public readonly object wrapped;

		private JsonValue(object value)
		{
			wrapped = value;
		}

		public int Count
		{
			get { return wrapped is List<JsonValue> l ? l.Count : 0; }
		}

		public JsonValue this[int index]
		{
			get { return wrapped is List<JsonValue> l ? l[index] : default; }
			set { if (wrapped is List<JsonValue> l) l[index] = value; }
		}

		public void Add(JsonValue value)
		{
			if (wrapped is List<JsonValue> l)
				l.Add(value);
		}

		public JsonValue this[string key]
		{
			get { return wrapped is Dictionary<string, JsonValue> d && d.TryGetValue(key, out var v) ? v : default; }
			set { if (wrapped is Dictionary<string, JsonValue> d) d[key] = value; }
		}

		public static implicit operator JsonValue(JsonArray value)
		{
			return new JsonValue(value.collection ?? new List<JsonValue>());
		}

		public static implicit operator JsonValue(JsonObject value)
		{
			return new JsonValue(value.collection ?? new Dictionary<string, JsonValue>());
		}

		public static implicit operator JsonValue(bool value)
		{
			return new JsonValue(value);
		}

		public static implicit operator JsonValue(int value)
		{
			return new JsonValue(value);
		}

		public static implicit operator JsonValue(float value)
		{
			return new JsonValue(value);
		}

		public static implicit operator JsonValue(string value)
		{
			return new JsonValue(value);
		}

		public bool IsArray
		{
			get { return wrapped is List<JsonValue>; }
		}

		public bool IsObject
		{
			get { return wrapped is Dictionary<string, JsonValue>; }
		}

		public bool TryGet<T>(out T value)
		{
			if (wrapped is T v)
			{
				value = v;
				return true;
			}
			else
			{
				value = default;
				return false;
			}
		}

		public T GetOr<T>(T defaultValue)
		{
			return wrapped is T value ? value : defaultValue;
		}

		public List<JsonValue>.Enumerator GetEnumerator()
		{
			return wrapped is List<JsonValue> l ?
				l.GetEnumerator() :
				EmptyValues.GetEnumerator();
		}
	}

	public static class Json
	{
		private sealed class ParseException : System.Exception
		{
		}

		public static string Serialize(JsonValue value)
		{
			var sb = new StringBuilder();
			Serialize(value, sb);
			return sb.ToString();
		}

		public static void Serialize(JsonValue value, StringBuilder sb)
		{
			switch (value.wrapped)
			{
			case null:
				sb.Append("null");
				break;
			case bool b:
				sb.Append(b ? "true" : "false");
				break;
			case int i:
				sb.Append(i);
				break;
			case float f:
				sb.AppendFormat(CultureInfo.InvariantCulture, "{0}", f);
				break;
			case string s:
				sb.Append('"');
				foreach (var c in s)
				{
					switch (c)
					{
					case '\"': sb.Append("\\\""); break;
					case '\\': sb.Append("\\\\"); break;
					case '\b': sb.Append("\\b"); break;
					case '\f': sb.Append("\\f"); break;
					case '\n': sb.Append("\\n"); break;
					case '\r': sb.Append("\\r"); break;
					case '\t': sb.Append("\\t"); break;
					default:
						if (c >= 32 && c <= 126)
						{
							sb.Append(c);
						}
						else
						{
							sb.Append('\\');
							sb.Append('u');
							sb.Append(ToHexDigit(c >> 12));
							sb.Append(ToHexDigit(c >> 8));
							sb.Append(ToHexDigit(c >> 4));
							sb.Append(ToHexDigit(c));
						}
						break;
					}
				}
				sb.Append('"');
				break;
			case List<JsonValue> l:
				sb.Append('[');
				foreach (var v in l)
				{
					Serialize(v, sb);
					sb.Append(',');
				}
				if (l.Count > 0)
					sb.Remove(sb.Length - 1, 1);
				sb.Append(']');
				break;
			case Dictionary<string, JsonValue> d:
				sb.Append('{');
				foreach (var p in d)
				{
					sb.Append('"').Append(p.Key).Append('"').Append(':');
					Serialize(p.Value, sb);
					sb.Append(',');
				}
				if (d.Count > 0)
					sb.Remove(sb.Length - 1, 1);
				sb.Append('}');
				break;
			}
		}

		public static bool TryDeserialize(string source, out JsonValue value)
		{
			try
			{
				var index = 0;
				value = Parse(source, ref index, new StringBuilder());
				return true;
			}
			catch (ParseException)
			{
				value = default;
				return false;
			}
		}

		private static JsonValue Parse(string source, ref int index, StringBuilder sb)
		{
			SkipWhiteSpace(source, ref index);
			switch (Next(source, ref index))
			{
			case 'n':
				Consume(source, ref index, 'u');
				Consume(source, ref index, 'l');
				Consume(source, ref index, 'l');
				SkipWhiteSpace(source, ref index);
				return new JsonValue();
			case 'f':
				Consume(source, ref index, 'a');
				Consume(source, ref index, 'l');
				Consume(source, ref index, 's');
				Consume(source, ref index, 'e');
				SkipWhiteSpace(source, ref index);
				return false;
			case 't':
				Consume(source, ref index, 'r');
				Consume(source, ref index, 'u');
				Consume(source, ref index, 'e');
				SkipWhiteSpace(source, ref index);
				return true;
			case '"':
				return ConsumeString(source, ref index, sb);
			case '[':
				{
					SkipWhiteSpace(source, ref index);
					var array = new JsonArray();
					if (!Match(source, ref index, ']'))
					{
						do
						{
							var value = Parse(source, ref index, sb);
							array.Add(value);
						} while (Match(source, ref index, ','));
						Consume(source, ref index, ']');
					}
					SkipWhiteSpace(source, ref index);
					return array;
				}
			case '{':
				{
					SkipWhiteSpace(source, ref index);
					var obj = new JsonObject();
					if (!Match(source, ref index, '}'))
					{
						do
						{
							SkipWhiteSpace(source, ref index);
							Consume(source, ref index, '"');
							var key = ConsumeString(source, ref index, sb);
							Consume(source, ref index, ':');
							var value = Parse(source, ref index, sb);
							obj.Add(key, value);
						} while (Match(source, ref index, ','));
						Consume(source, ref index, '}');
					}
					SkipWhiteSpace(source, ref index);
					return obj;
				}
			default:
				{
					var negative = source[--index] == '-';
					if (negative)
						index++;
					if (!IsDigit(source, index))
						throw new ParseException();

					while (Match(source, ref index, '0'))
						continue;

					var integer = 0;
					while (IsDigit(source, index))
					{
						integer = 10 * integer + source[index] - '0';
						index++;
					}

					if (Match(source, ref index, '.'))
					{
						if (!IsDigit(source, index))
							throw new ParseException();

						var fractionBase = 1.0f;
						var fraction = 0.0f;

						while (IsDigit(source, index))
						{
							fractionBase *= 0.1f;
							fraction += (source[index] - '0') * fractionBase;
							index++;
						}

						fraction += integer;
						SkipWhiteSpace(source, ref index);
						return negative ? -fraction : fraction;
					}

					SkipWhiteSpace(source, ref index);
					return negative ? -integer : integer;
				}
			}
		}

		private static void SkipWhiteSpace(string source, ref int index)
		{
			while (index < source.Length && char.IsWhiteSpace(source, index))
				index++;
		}

		private static char Next(string source, ref int index)
		{
			if (index < source.Length)
				return source[index++];
			throw new ParseException();
		}

		private static bool Match(string source, ref int index, char c)
		{
			if (index < source.Length && source[index] == c)
			{
				index++;
				return true;
			}
			else
			{
				return false;
			}
		}

		private static void Consume(string source, ref int index, char c)
		{
			if (index >= source.Length || source[index++] != c)
				throw new ParseException();
		}

		private static bool IsDigit(string s, int i)
		{
			return i < s.Length && char.IsDigit(s, i);
		}

		private static int ConsumeHexDigit(string source, ref int index)
		{
			var c = Next(source, ref index);
			if (c >= 'a' && c <= 'f')
				return c - 'a' + 10;
			if (c >= 'A' && c <= 'F')
				return c - 'A' + 10;
			if (c >= '0' && c <= '9')
				return c - '0';
			throw new ParseException();
		}

		private static char ToHexDigit(int n)
		{
			n &= 0xf;
			return n <= 9 ? (char)(n + '0') : (char)(n - 10 + 'a');
		}

		private static string ConsumeString(string source, ref int index, StringBuilder sb)
		{
			sb.Clear();
			while (index < source.Length)
			{
				var c = Next(source, ref index);
				switch (c)
				{
				case '"':
					SkipWhiteSpace(source, ref index);
					return sb.ToString();
				case '\\':
					switch (Next(source, ref index))
					{
					case '"': sb.Append('"'); break;
					case '\\': sb.Append('\\'); break;
					case '/': sb.Append('/'); break;
					case 'b': sb.Append('\b'); break;
					case 'f': sb.Append('\f'); break;
					case 'n': sb.Append('\n'); break;
					case 'r': sb.Append('\r'); break;
					case 't': sb.Append('\t'); break;
					case 'u':
						{
							var h = ConsumeHexDigit(source, ref index) << 12;
							h += ConsumeHexDigit(source, ref index) << 8;
							h += ConsumeHexDigit(source, ref index) << 4;
							h += ConsumeHexDigit(source, ref index);
							sb.Append((char)h);
						}
						break;
					default:
						throw new ParseException();
					}
					break;
				default:
					sb.Append(c);
					break;
				}
			}

			throw new ParseException();
		}
	}
}