using Xunit;
using PicoJson;
using PicoJson.Typed;

public sealed class JsonTypedTests
{
	public struct WrappedBool : IJsonSerializable
	{
		public bool value;

		public void Serialize(IJsonSerializer serializer)
		{
			serializer.Serialize(nameof(value), ref value);
		}
	}

	public struct WrappedInt : IJsonSerializable
	{
		public int value;

		public void Serialize(IJsonSerializer serializer)
		{
			serializer.Serialize(nameof(value), ref value);
		}
	}

	public struct WrappedFloat : IJsonSerializable
	{
		public float value;

		public void Serialize(IJsonSerializer serializer)
		{
			serializer.Serialize(nameof(value), ref value);
		}
	}

	public struct WrappedString : IJsonSerializable
	{
		public string value;

		public void Serialize(IJsonSerializer serializer)
		{
			serializer.Serialize(nameof(value), ref value);
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
		var json = "";
		switch (value)
		{
		case bool b:
			json = Json.Serialize(new WrappedBool { value = b });
			break;
		case int i:
			json = Json.Serialize(new WrappedInt { value = i });
			break;
		case float f:
			json = Json.Serialize(new WrappedFloat { value = f });
			break;
		case string s:
			json = Json.Serialize(new WrappedString { value = s });
			break;
		default:
			json = Json.Serialize(new WrappedString { value = null });
			break;
		}

		expectedJson = string.Concat("{\"value\":", expectedJson, "}");
		Assert.Equal(expectedJson, json);
	}

	public struct ComplexStruct : IJsonSerializable
	{
		public struct ArrayElement : IJsonSerializable
		{
			public int i;
			public bool b;
			public string s;

			public void Serialize(IJsonSerializer serializer)
			{
				serializer.Serialize(nameof(i), ref i);
				serializer.Serialize(nameof(b), ref b);
				serializer.Serialize(nameof(s), ref s);
			}
		}

		public struct Empty : IJsonSerializable
		{
			public void Serialize(IJsonSerializer serializer)
			{
			}
		}

		public ArrayElement[] array;
		public float[] numbers;
		public string str;
		public Empty empty;
		public bool[] nullArray;

		public void Serialize(IJsonSerializer serializer)
		{
			serializer.Serialize(nameof(array), ref array, (IJsonSerializer s, ref ArrayElement e) => s.Serialize(null, ref e));
			serializer.Serialize(nameof(numbers), ref numbers, (IJsonSerializer s, ref float e) => s.Serialize(null, ref e));
			serializer.Serialize(nameof(str), ref str);
			serializer.Serialize(nameof(empty), ref empty);
			serializer.Serialize(nameof(nullArray), ref nullArray, (IJsonSerializer s, ref bool e) => s.Serialize(null, ref e));
		}
	}

	[Fact]
	public void SerializeComplex()
	{
		var complex = new ComplexStruct
		{
			array = new ComplexStruct.ArrayElement[] {
				new ComplexStruct.ArrayElement {
					i = 7,
					b = false,
					s = null,
				},
				new ComplexStruct.ArrayElement {
					i = -2,
					b = true,
					s = "some text",
				},
			},
			numbers = new float[] {
				0.1f, 1.9f, -99.5f
			},
			str = "asdad",
			empty = new ComplexStruct.Empty()
		};

		var json = Json.Serialize(complex);
		Assert.Equal(
			"{\"array\":[{\"i\":7,\"b\":false,\"s\":null},{\"i\":-2,\"b\":true,\"s\":\"some text\"}],\"numbers\":[0.1,1.9,-99.5],\"str\":\"asdad\",\"empty\":{},\"nullArray\":null}",
			json
		);
	}

	[Theory]
	[InlineData("null", null)]
	[InlineData("true", true)]
	[InlineData("false", false)]
	[InlineData("0", 0)]
	[InlineData("1", 1)]
	[InlineData("-1", -1)]
	[InlineData("99.5", 99.5f)]
	[InlineData("99.25", 99.25f)]
	[InlineData("99.125", 99.125f)]
	[InlineData("\"string\"", "string")]
	[InlineData("\"\\u00e1\"", "\u00e1")]
	[InlineData("\"\\ufa09\"", "\ufa09")]
	[InlineData("\"\\\"\\\\\\/\\b\\f\\n\\r\\t\"", "\"\\/\b\f\n\r\t")]
	public void DeserializeValue(string json, object expectedValue)
	{
		json = string.Concat("{\"value\":", json, "}");
		switch (expectedValue)
		{
		case bool b:
			Assert.True(Json.TryDeserialize(json, out WrappedBool wb));
			Assert.Equal(expectedValue, wb.value);
			break;
		case int i:
			Assert.True(Json.TryDeserialize(json, out WrappedInt wi));
			Assert.Equal(expectedValue, wi.value);
			break;
		case float f:
			Assert.True(Json.TryDeserialize(json, out WrappedFloat wf));
			Assert.Equal(expectedValue, wf.value);
			break;
		case string s:
			Assert.True(Json.TryDeserialize(json, out WrappedString ws));
			Assert.Equal(expectedValue, ws.value);
			break;
		default:
			Assert.True(Json.TryDeserialize(json, out WrappedString wn));
			Assert.Null(wn.value);
			break;
		}
	}

	[Fact]
	public void DeserializeComplex()
	{
		var success = Json.TryDeserialize(
			" { \"array\"  : [ {\"i\" :   7 ,\"b\": false, \"s\" :null  },{ \"i\":  -2 ,\"b\" :true,  \"s\":  \"some text\"}  ], \"numbers\" :  [0.1,1.9,-99.5],  \"str\":  \"asdad\",\"empty\" :{    }   ,\"nullArray\": null}   ",
			out ComplexStruct value
		);
		Assert.True(success);

		Assert.NotNull(value.array);
		Assert.Equal(2, value.array.Length);

		Assert.Equal(7, value.array[0].i);
		Assert.False(value.array[0].b);
		Assert.Null(value.array[0].s);

		Assert.Equal(-2, value.array[1].i);
		Assert.True(value.array[1].b);
		Assert.Equal("some text", value.array[1].s);

		Assert.NotNull(value.numbers);
		Assert.Equal(3, value.numbers.Length);

		Assert.Equal(0.1f, value.numbers[0]);
		Assert.Equal(1.9f, value.numbers[1]);
		Assert.Equal(-99.5f, value.numbers[2]);

		Assert.Equal("asdad", value.str);

		Assert.Null(value.nullArray);
	}

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