using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TupleConverter;

public class TupleConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsAssignableTo(typeof(ITuple));

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(TupleConverter<>).MakeGenericType(typeToConvert))!;

    private class TupleConverter<T> : JsonConverter<T> where T : ITuple
    {
        private static readonly Type[] TupleTypes =
        [
            typeof(Tuple<>), typeof(Tuple<,>), typeof(Tuple<,,>), typeof(Tuple<,,,>), typeof(Tuple<,,,,>),
            typeof(Tuple<,,,,,>), typeof(Tuple<,,,,,,>), typeof(Tuple<,,,,,,,>)
        ];

        private static readonly Dictionary<int, Type> ValueTupleTypes = new()
        {
            { 0, typeof(ValueTuple) },
            { 1, typeof(ValueTuple<>) },
            { 2, typeof(ValueTuple<,>) },
            { 3, typeof(ValueTuple<,,>) },
            { 4, typeof(ValueTuple<,,,>) },
            { 5, typeof(ValueTuple<,,,,>) },
            { 6, typeof(ValueTuple<,,,,,>) },
            { 7, typeof(ValueTuple<,,,,,,>) },
            { 8, typeof(ValueTuple<,,,,,,,>) }
        };

        private readonly bool _supportsRead;

        public TupleConverter()
        {
            _supportsRead = typeof(T) ==typeof(ITuple) || typeof(T) != typeof(ValueTuple);
            if (!_supportsRead && typeof(T).IsGenericType)
            {
                var genericTypeDefinition = typeof(T).GetGenericTypeDefinition();
                _supportsRead = !ValueTupleTypes.ContainsValue(genericTypeDefinition) &&
                                !TupleTypes.Contains(genericTypeDefinition);
            }
        }

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (!_supportsRead)
                throw new JsonException("Type not supported for read");

            if (reader.TokenType is not JsonTokenType.StartArray || !reader.Read())
                throw new JsonException();

            return (T)(typeToConvert == typeof(ITuple)
                ? (T)ReadTuple(ref reader, options)
                : ReadTuple(ref reader, typeToConvert, options));
        }

        private static ITuple ReadTuple(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            var typeArguments = new List<Type>(8);
            var arguments = new List<object?>(8);
            
            while (reader.TokenType != JsonTokenType.EndArray)
            {
                object? argument;
                if (arguments.Count == 7)
                {
                    argument = ReadTuple(ref reader, options);
                    arguments.Add(argument);
                    typeArguments.Add(argument.GetType());
                    break;
                }

                argument = JsonSerializer.Deserialize<object?>(ref reader, options);
                arguments.Add(argument);
                typeArguments.Add(argument?.GetType() ?? typeof(object));
                if (!reader.Read())
                    throw new JsonException();
            }

            if (arguments.Count == 0)
                return new ValueTuple();

            var type = ValueTupleTypes[arguments.Count].MakeGenericType(typeArguments.ToArray());
            return (ITuple)Activator.CreateInstance(type, arguments.ToArray())!;
        }

        private static ITuple ReadTuple(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var genericArguments = typeToConvert.IsGenericType ? typeToConvert.GetGenericArguments() : [];
            var arguments = new object?[genericArguments.Length];

            var read = true;
            for (var index = 0; index < Math.Min(7, genericArguments.Length); index++)
            {
                if (!read || reader.TokenType == JsonTokenType.EndArray)
                    throw new JsonException();
                arguments[index] = JsonSerializer.Deserialize(ref reader, genericArguments[index], options);
                read = reader.Read();
            }

            if (!read)
                throw new JsonException();

            if (genericArguments.Length == 8)
                arguments[7] = ReadTuple(ref reader, genericArguments[7], options);

            if (reader.TokenType != JsonTokenType.EndArray)
                throw new JsonException();

            return (ITuple)Activator.CreateInstance(typeToConvert, arguments)!;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStartArray();
            for (var index = 0; index < value.Length; index++)
                JsonSerializer.Serialize(writer, value[index], options);
            writer.WriteEndArray();
        }
    }
}