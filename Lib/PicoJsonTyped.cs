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
		void Serialize<T>(string name, ref T value) where T : IJsonSerializable, new();
		void Serialize<T>(string name, ref T[] value, JsonElementSerializer<T> elementSerializer);
	}

	public static class Json
	{
		private sealed class ErrorException : System.Exception
		{
		}

		public sealed class Writer : IJsonSerializer
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
				value.Serialize(this);
				if (startIndex < sb.Length)
					sb[startIndex] = '{';
				else
					sb.Append('{');
				sb.Append('}');
			}

			public void Serialize<T>(string name, ref T[] value, JsonElementSerializer<T> elementSerializer)
			{
				WritePrefix(name);
				var startIndex = sb.Length;
				for (var i = 0; i < value.Length; i++)
					elementSerializer(this, ref value[i]);
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