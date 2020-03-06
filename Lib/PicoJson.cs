using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

namespace PicoJson
{
	namespace Untyped
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
	}

	namespace Typed
	{
		public readonly struct JsonLazy
		{
			internal readonly string source;
			internal readonly int index;

			internal JsonLazy(string source, int index)
			{
				this.source = source;
				this.index = index;
			}
		}

		public delegate void JsonElementSerializer<T>(IJsonSerializer serializer, ref T value);

		public interface IJsonSerializable
		{
			void Serialize(IJsonSerializer serializer);
		}

		public interface IJsonSerializer
		{
			void Serialize(string name, ref JsonLazy value);
			void Serialize(string name, ref bool value);
			void Serialize(string name, ref int value);
			void Serialize(string name, ref float value);
			void Serialize(string name, ref string value);
			void Serialize<T>(string name, ref T value) where T : struct, IJsonSerializable;
			void Serialize<T>(string name, ref T[] value, JsonElementSerializer<T> elementSerializer);
		}
	}

	public static class Json
	{
		private static ThreadLocal<StringBuilder> CachedSb = new ThreadLocal<StringBuilder>(() => new StringBuilder(), false);
		private static ThreadLocal<Writer> CachedWriter = new ThreadLocal<Writer>(() => new Writer(), false);
		private static ThreadLocal<Reader> CachedReader = new ThreadLocal<Reader>(() => new Reader(), false);

		public static string Serialize(Untyped.JsonValue value)
		{
			var sb = CachedSb.Value;
			sb.Clear();
			Serialize(value, sb);
			return sb.ToString();
		}

		private static void Serialize(Untyped.JsonValue value, StringBuilder sb)
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
				WriteString(s, sb);
				break;
			case List<Untyped.JsonValue> l:
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
			case Dictionary<string, Untyped.JsonValue> d:
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

		public static bool TryDeserialize(string source, out Untyped.JsonValue value)
		{
			try
			{
				var index = 0;
				value = Parse(source, ref index, new StringBuilder());
				return true;
			}
			catch (ParseErrorException)
			{
				value = default;
				return false;
			}
		}

		private static Untyped.JsonValue Parse(string source, ref int index, StringBuilder sb)
		{
			SkipWhiteSpace(source, ref index);
			switch (Next(source, ref index))
			{
			case 'n':
				Consume(source, ref index, 'u');
				Consume(source, ref index, 'l');
				Consume(source, ref index, 'l');
				SkipWhiteSpace(source, ref index);
				return new Untyped.JsonValue();
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
					var array = new Untyped.JsonArray();
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
					var obj = new Untyped.JsonObject();
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
					var integer = ConsumeInteger(source, ref index);
					if (!Match(source, ref index, '.'))
					{
						SkipWhiteSpace(source, ref index);
						return integer;
					}

					var fraction = ConsumeFraction(source, ref index);
					SkipWhiteSpace(source, ref index);
					return integer + (integer >= 0 ? fraction : -fraction);
				}
			}
		}

		public static string Serialize<T>(T value) where T : struct, Typed.IJsonSerializable
		{
			var sb = CachedSb.Value;
			CachedWriter.Value.Reset(sb).Serialize(null, ref value);
			return sb.ToString();
		}

		public static bool TryDeserialize<T>(string source, out T value) where T : struct, Typed.IJsonSerializable
		{
			value = default;
			try
			{
				var index = 0;
				CachedReader.Value.Reset(CachedSb.Value, source, index).Serialize(null, ref value);
				return true;
			}
			catch (ParseErrorException)
			{
				return false;
			}
		}

		private sealed class ParseErrorException : System.Exception
		{
		}

		private sealed class Writer : Typed.IJsonSerializer
		{
			private StringBuilder sb;

			internal Writer Reset(StringBuilder sb)
			{
				sb.Clear();
				this.sb = sb;
				return this;
			}

			public void Serialize(string name, ref Typed.JsonLazy value)
			{
				var endIndex = value.index;
				SkipObject(value.source, ref endIndex);
				sb.Append(value.source, value.index, endIndex - value.index);
			}

			public void Serialize(string name, ref bool value)
			{
				WritePrefix(name);
				sb.Append(value ? "true" : "false");
			}

			public void Serialize(string name, ref int value)
			{
				WritePrefix(name);
				sb.Append(value);
			}

			public void Serialize(string name, ref float value)
			{
				WritePrefix(name);
				sb.AppendFormat(CultureInfo.InvariantCulture, "{0}", value);
			}

			public void Serialize(string name, ref string value)
			{
				WritePrefix(name);
				if (value != null)
					WriteString(value, sb);
				else
					sb.Append("null");
			}

			public void Serialize<T>(string name, ref T value) where T : struct, Typed.IJsonSerializable
			{
				WritePrefix(name);
				sb.Append('{');
				value.Serialize(this);
				if (sb[sb.Length - 1] == ',')
					sb[sb.Length - 1] = '}';
				else
					sb.Append('}');
			}

			public void Serialize<T>(string name, ref T[] value, Typed.JsonElementSerializer<T> elementSerializer)
			{
				WritePrefix(name);
				if (value == null)
				{
					sb.Append("null");
					return;
				}

				sb.Append('[');
				for (var i = 0; i < value.Length; i++)
					elementSerializer(this, ref value[i]);
				if (sb[sb.Length - 1] == ',')
					sb[sb.Length - 1] = ']';
				else
					sb.Append(']');
			}

			private void WritePrefix(string name)
			{
				var lastC = sb.Length > 0 ? sb[sb.Length - 1] : '{';
				if (lastC != '{' && lastC != '[')
					sb.Append(',');

				if (name != null)
				{
					sb.Append('"');
					sb.Append(name);
					sb.Append('"');
					sb.Append(':');
				}
			}
		}

		private sealed class Reader : Typed.IJsonSerializer
		{
			private StringBuilder sb;
			private string source;
			private int index;
			private string currentKey;

			internal Reader Reset(StringBuilder sb, string source, int index)
			{
				sb.Clear();
				this.sb = sb;
				this.source = source;
				this.index = index;
				this.currentKey = null;
				SkipWhiteSpace(source, ref index);
				return this;
			}

			public void Serialize(string name, ref Typed.JsonLazy value)
			{
				if (currentKey != name)
					return;

				value = new Typed.JsonLazy(source, index);
				SkipObject(source, ref index);
			}

			public void Serialize(string name, ref bool value)
			{
				if (currentKey != name)
					return;

				switch (Next(source, ref index))
				{
				case 'f':
					Consume(source, ref index, 'a');
					Consume(source, ref index, 'l');
					Consume(source, ref index, 's');
					Consume(source, ref index, 'e');
					SkipWhiteSpace(source, ref index);
					value = false;
					break;
				case 't':
					Consume(source, ref index, 'r');
					Consume(source, ref index, 'u');
					Consume(source, ref index, 'e');
					SkipWhiteSpace(source, ref index);
					value = true;
					break;
				default:
					throw new ParseErrorException();
				}
			}

			public void Serialize(string name, ref int value)
			{
				if (currentKey != name)
					return;

				Next(source, ref index);
				value = ConsumeInteger(source, ref index);
				if (Match(source, ref index, '.'))
					throw new ParseErrorException();
				SkipWhiteSpace(source, ref index);
			}

			public void Serialize(string name, ref float value)
			{
				if (currentKey != name)
					return;

				Next(source, ref index);
				value = ConsumeInteger(source, ref index);
				if (Match(source, ref index, '.'))
				{
					var fraction = ConsumeFraction(source, ref index);
					if (value >= 0.0f)
						value += fraction;
					else
						value -= fraction;
				}
				SkipWhiteSpace(source, ref index);
			}

			public void Serialize(string name, ref string value)
			{
				if (currentKey != name)
					return;

				if (ConsumeNullOr(source, ref index, '"'))
					value = null;
				else
					value = ConsumeString(source, ref index, sb);
			}

			public void Serialize<T>(string name, ref T value) where T : struct, Typed.IJsonSerializable
			{
				if (currentKey != name)
					return;

				value = default(T);
				SkipWhiteSpace(source, ref index);
				if (ConsumeNullOr(source, ref index, '{'))
					return;

				SkipWhiteSpace(source, ref index);
				if (!Match(source, ref index, '}'))
				{
					do
					{
						SkipWhiteSpace(source, ref index);
						var previousKey = currentKey;
						Consume(source, ref index, '"');
						currentKey = ConsumeString(source, ref index, sb);
						Consume(source, ref index, ':');
						value.Serialize(this);
						currentKey = previousKey;
					} while (Match(source, ref index, ','));
					Consume(source, ref index, '}');
				}
				SkipWhiteSpace(source, ref index);
			}

			public void Serialize<T>(string name, ref T[] value, Typed.JsonElementSerializer<T> elementSerializer)
			{
				if (currentKey != name)
					return;

				SkipWhiteSpace(source, ref index);
				if (ConsumeNullOr(source, ref index, '['))
				{
					value = null;
					return;
				}

				var list = new System.Collections.Generic.List<T>();
				SkipWhiteSpace(source, ref index);
				if (!Match(source, ref index, ']'))
				{
					do
					{
						SkipWhiteSpace(source, ref index);
						var element = default(T);
						elementSerializer(this, ref element);
						list.Add(element);
					} while (Match(source, ref index, ','));
					Consume(source, ref index, ']');
				}
				SkipWhiteSpace(source, ref index);
				value = list.ToArray();
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
			throw new ParseErrorException();
		}

		private static bool Match(string source, ref int index, char c)
		{
			if (index >= source.Length || source[index] != c)
				return false;

			index++;
			return true;
		}

		private static void Consume(string source, ref int index, char c)
		{
			if (index >= source.Length || source[index++] != c)
				throw new ParseErrorException();
		}

		private static bool ConsumeNullOr(string source, ref int index, char c)
		{
			var n = Next(source, ref index);
			if (n == c)
				return false;
			if (n != 'n')
				throw new ParseErrorException();

			Consume(source, ref index, 'u');
			Consume(source, ref index, 'l');
			Consume(source, ref index, 'l');
			SkipWhiteSpace(source, ref index);
			return true;
		}

		private static bool IsDigit(string s, int i)
		{
			return i < s.Length && char.IsDigit(s, i);
		}

		private static int ConsumeInteger(string source, ref int index)
		{
			var negative = source[--index] == '-';
			if (negative)
				index++;
			if (!IsDigit(source, index))
				throw new ParseErrorException();

			while (Match(source, ref index, '0'))
				continue;

			var integer = 0;
			while (IsDigit(source, index))
			{
				integer = 10 * integer + source[index] - '0';
				index++;
			}

			return negative ? -integer : integer;
		}

		private static float ConsumeFraction(string source, ref int index)
		{
			if (!IsDigit(source, index))
				throw new ParseErrorException();

			var fractionBase = 1.0f;
			var fraction = 0.0f;

			while (IsDigit(source, index))
			{
				fractionBase *= 0.1f;
				fraction += (source[index] - '0') * fractionBase;
				index++;
			}

			return fraction;
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
			throw new ParseErrorException();
		}

		private static char ToHexDigit(int n)
		{
			n &= 0xf;
			return n <= 9 ? (char)(n + '0') : (char)(n - 10 + 'a');
		}

		private static void WriteString(string s, StringBuilder sb)
		{
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
						throw new ParseErrorException();
					}
					break;
				default:
					sb.Append(c);
					break;
				}
			}

			throw new ParseErrorException();
		}

		private static void SkipObject(string source, ref int index)
		{
			Consume(source, ref index, '{');
			while (true)
			{
				var c = Next(source, ref index);
				if (c == '}')
					break;
				if (c == '"')
				{
					while (true)
					{
						var s = Next(source, ref index);
						if (s == '"')
							break;
						if (s == '\\')
							Next(source, ref index);
					}
				}
			}
		}
	}
}