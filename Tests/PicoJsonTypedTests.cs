using Xunit;
using PicoJson;
using PicoJson.Typed;

public sealed class JsonTypedTests
{
	public readonly struct SerializeWrapper<T> : IJsonSerializable
	{
		public readonly T value;
		public readonly System.Action<IJsonSerializer, T> serializeFunction;

		public SerializeWrapper(T value, System.Action<IJsonSerializer, T> serializeFunction)
		{
			this.value = value;
			this.serializeFunction = serializeFunction;
		}

		public void Serialize(IJsonSerializer serializer)
		{
			serializeFunction(serializer, value);
		}
	}

	[Theory]
	[InlineData(null, "null")]
	[InlineData(true, "true")]
	[InlineData(false, "false")]
	[InlineData(0, "0")]
	[InlineData(1, "1")]
	[InlineData(-1, "-1")]
	[InlineData(99.5f, "99.5")]
	[InlineData("string", "\"string\"")]
	[InlineData("\u00e1", "\"\\u00e1\"")]
	[InlineData("\ufa09", "\"\\ufa09\"")]
	[InlineData("\"\\/\b\f\n\r\t", "\"\\\"\\\\/\\b\\f\\n\\r\\t\"")]
	public void SerializeValue(object value, string expectedJson)
	{
		SerializeWrapper<T> Wrap<T>(T v, System.Action<IJsonSerializer, T> s)
		{
			return new SerializeWrapper<T>(v, s);
		}

		var json = "";

		switch (value)
		{
		case bool b:
			json = Json.Serialize(Wrap(b, (s, v) => s.Serialize("value", ref v)));
			break;
		case int i:
			json = Json.Serialize(Wrap(i, (s, v) => s.Serialize("value", ref v)));
			break;
		case float f:
			json = Json.Serialize(Wrap(f, (s, v) => s.Serialize("value", ref v)));
			break;
		case string s:
			json = Json.Serialize(Wrap(s, (s, v) => s.Serialize("value", ref v)));
			break;
		default:
			json = Json.Serialize(Wrap<string>(null, (s, v) => s.Serialize("value", ref v)));
			break;
		}

		expectedJson = string.Concat("{\"value\":", expectedJson, "}");
		Assert.Equal(expectedJson, json);
	}

	// [Fact]
	// public void SerializeComplex()
	// {
	// 	var obj = new JsonObject {
	// 		{"array", new JsonArray {
	// 			{"string"},
	// 			{false},
	// 			{null},
	// 			{0.25f},
	// 			{new JsonObject{
	// 				{"int", 7},
	// 				{"bool", false},
	// 				{"null", null},
	// 				{"string", "some text"}
	// 			}},
	// 			{new JsonArray()}
	// 		}},
	// 		{"str", "asdad"},
	// 		{"empty", new JsonObject()}
	// 	};

	// 	var sb = new StringBuilder();
	// 	Json.Serialize(obj, sb);

	// 	Assert.Equal(
	// 		"{\"array\":[\"string\",false,null,0.25,{\"int\":7,\"bool\":false,\"null\":null,\"string\":\"some text\"},[]],\"str\":\"asdad\",\"empty\":{}}",
	// 		sb.ToString()
	// 	);
	// }

	// [Theory]
	// [InlineData("null", null)]
	// [InlineData("true", true)]
	// [InlineData("false", false)]
	// [InlineData("0", 0)]
	// [InlineData("1", 1)]
	// [InlineData("-1", -1)]
	// [InlineData("99.5", 99.5f)]
	// [InlineData("99.25", 99.25f)]
	// [InlineData("99.125", 99.125f)]
	// [InlineData("\"string\"", "string")]
	// [InlineData("\"\\u00e1\"", "\u00e1")]
	// [InlineData("\"\\ufa09\"", "\ufa09")]
	// [InlineData("\"\\\"\\\\\\/\\b\\f\\n\\r\\t\"", "\"\\/\b\f\n\r\t")]
	// public void DeserializeValue(string json, object expectedValue)
	// {
	// 	var success = Json.TryDeserialize(json, out var value);
	// 	Assert.True(success);
	// 	Assert.Equal(expectedValue, value.wrapped);
	// }

	// [Fact]
	// public void DeserializeComplex()
	// {
	// 	Assert.True(Json.TryDeserialize(
	// 		" { \"array\"  : [\"string\",  false,null,0.25,\n{\"int\":  7,  \"bool\":false,\"null\":null, \t\n   \"string\":\"some text\"},[]],   \n\"str\":\"asdad\", \"empty\":{}}",
	// 		out var value
	// 	));

	// 	Assert.True(value.IsObject);

	// 	Assert.True(value["array"].IsArray);
	// 	Assert.Equal(6, value["array"].Count);
	// 	Assert.Equal("string", value["array"][0].wrapped);
	// 	Assert.Equal(false, value["array"][1].wrapped);
	// 	Assert.Null(value["array"][2].wrapped);
	// 	Assert.Equal(0.25f, value["array"][3].wrapped);

	// 	Assert.True(value["array"][4].IsObject);
	// 	Assert.Equal(7, value["array"][4]["int"].wrapped);
	// 	Assert.Equal(false, value["array"][4]["bool"].wrapped);
	// 	Assert.Null(value["array"][4]["null"].wrapped);
	// 	Assert.Equal("some text", value["array"][4]["string"].wrapped);

	// 	Assert.True(value["array"][5].IsArray);
	// 	Assert.Equal(0, value["array"][5].Count);

	// 	Assert.Equal("asdad", value["str"].wrapped);
	// 	Assert.True(value["empty"].IsObject);
	// }

	// [Theory]
	// [InlineData("[]")]
	// [InlineData("[1]", 1)]
	// [InlineData("[1, 2, 3]", 1, 2, 3)]
	// [InlineData("[true, 4, null, \"string\"]", true, 4, null, "string")]
	// [InlineData("4")]
	// [InlineData("4.5")]
	// [InlineData("true")]
	// [InlineData("false")]
	// [InlineData("\"string\"")]
	// [InlineData("{}")]
	// [InlineData("{\"key\": [false]}")]
	// public void Enumerate(string json, params object[] expectedValues)
	// {
	// 	var success = Json.TryDeserialize(json, out var value);
	// 	Assert.True(success);

	// 	var elements = new List<object>();
	// 	foreach (var e in value)
	// 		elements.Add(e.wrapped);
	// 	var elementsArray = elements.ToArray();

	// 	Assert.Equal(expectedValues, elementsArray);
	// }
}