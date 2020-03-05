using System.Globalization;
using System.Text;

namespace PicoJson.Typed
{
	public readonly struct JsonLazy
	{
		private readonly string source;
		private readonly int index;

		internal JsonLazy(string source, int index)
		{
			this.source = source;
			this.index = index;
		}
	}

	public delegate void JsonElementSerializer<S, T>(ref S serializer, ref T value) where S : IJsonSerializer;

	public interface IJsonSerializable
	{
		void Serialize<T>(ref T serializer) where T : IJsonSerializer;
	}

	public interface IJsonSerializer
	{
		void Serialize(string name, ref JsonLazy value);
		void Serialize(string name, ref bool value);
		void Serialize(string name, ref int value);
		void Serialize(string name, ref float value);
		void Serialize(string name, ref string value);
		void Serialize<T>(string name, ref T value) where T : IJsonSerializable, new();
		void Serialize<S, T>(string name, ref T[] value, JsonElementSerializer<S, T> elementSerializer) where S : IJsonSerializer;
	}

	public static class Json
	{
		private sealed class ErrorException : System.Exception
		{
		}

		public ref struct Writer : IJsonSerializer
		{
			private StringBuilder sb;

			internal Writer(StringBuilder sb)
			{
				this.sb = sb;
			}

			public void Serialize(string name, ref JsonLazy value)
			{
				throw new ErrorException();
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
				sb.Append('"');
				foreach (var c in value)
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
					default: sb.Append(c); break;
					}
				}
				sb.Append('"');
			}

			public void Serialize<T>(string name, ref T value) where T : IJsonSerializable, new()
			{
				WritePrefix(name);
				var startIndex = sb.Length;
				value.Serialize(ref this);
				if (startIndex < sb.Length)
					sb[startIndex] = '{';
				else
					sb.Append('{');
				sb.Append('}');
			}

			public void Serialize<S, T>(string name, ref T[] value, JsonElementSerializer<S, T> elementSerializer) where S : IJsonSerializer
			{
				WritePrefix(name);
				var startIndex = sb.Length;
				for (var i = 0; i < value.Length; i++)
					elementSerializer(ref this, ref value[i]);
				if (startIndex < sb.Length)
					sb[startIndex] = '[';
				else
					sb.Append(']');
			}

			private void WritePrefix(string name)
			{
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

		public struct Reader : IJsonSerializer
		{
			private string source;
			private int index;
			private StringBuilder sb;
			private string currentKey;

			internal Reader(string source, int index, StringBuilder sb)
			{
				this.source = source;
				this.index = index;
				this.sb = sb;
				this.currentKey = null;
			}

			public void Serialize(string name, ref JsonLazy value)
			{
				if (currentKey != name)
					return;

				value = new JsonLazy(source, index);
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
					throw new ErrorException();
				}
			}

			public void Serialize(string name, ref int value)
			{
				if (currentKey != name)
					return;

				var negative = Next(source, ref index) == '-';
				if (negative)
					index++;
				if (!IsDigit(source, index))
					throw new ErrorException();

				while (Match(source, ref index, '0'))
					continue;

				var integer = 0;
				while (IsDigit(source, index))
				{
					integer = 10 * integer + source[index] - '0';
					index++;
				}

				if (Match(source, ref index, '.'))
					throw new ErrorException();

				SkipWhiteSpace(source, ref index);
				value = negative ? -integer : integer;
			}

			public void Serialize(string name, ref float value)
			{
				if (currentKey != name)
					return;

				var negative = Next(source, ref index) == '-';
				if (negative)
					index++;
				if (!IsDigit(source, index))
					throw new ErrorException();

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
						throw new ErrorException();

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
					value = negative ? -fraction : fraction;
				}
				else
				{
					SkipWhiteSpace(source, ref index);
					value = negative ? -integer : integer;
				}
			}

			public void Serialize(string name, ref string value)
			{
				if (currentKey != name)
					return;

				if (Next(source, ref index) != '"')
					throw new ErrorException();

				value = ConsumeString(source, ref index, sb);
			}

			public void Serialize<T>(string name, ref T value) where T : IJsonSerializable, new()
			{
				if (currentKey != name)
					return;

				value = new T();
				SkipWhiteSpace(source, ref index);
				Consume(source, ref index, '{');
				SkipWhiteSpace(source, ref index);
				if (!Match(source, ref index, '}'))
				{
					do
					{
						SkipWhiteSpace(source, ref index);
						Consume(source, ref index, '"');
						var previousKey = currentKey;
						currentKey = ConsumeString(source, ref index, sb);
						Consume(source, ref index, ':');
						value.Serialize(ref this);
						currentKey = previousKey;
					} while (Match(source, ref index, ','));
					Consume(source, ref index, '}');
				}
				SkipWhiteSpace(source, ref index);
			}

			public void Serialize<S, T>(string name, ref T[] value, JsonElementSerializer<S, T> elementSerializer) where S : IJsonSerializer
			{
				if (currentKey != name)
					return;

				var list = new System.Collections.Generic.List<T>();
				SkipWhiteSpace(source, ref index);
				Consume(source, ref index, '[');
				SkipWhiteSpace(source, ref index);
				if (!Match(source, ref index, ']'))
				{
					do
					{
						SkipWhiteSpace(source, ref index);
						var element = default(T);
						elementSerializer(ref this, ref element);
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
			throw new ErrorException();
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
				throw new ErrorException();
		}

		private static bool IsDigit(string s, int i)
		{
			return i < s.Length && char.IsDigit(s, i);
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
					case 'u': throw new ErrorException();
					default: throw new ErrorException();
					}
					break;
				default:
					sb.Append(c);
					break;
				}
			}
			throw new ErrorException();
		}
	}
}