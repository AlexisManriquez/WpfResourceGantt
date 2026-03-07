using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace WpfResourceGantt
{
    public class CustomDateConverter : JsonConverter<DateTime>
    {
        private const string Format = "yyyy-MM-dd"; // Example: 2025-01-31

        // Writes the date to JSON (Serialization)
        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(Format));
        }

        // Reads the date from JSON (Deserialization)
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var dateString = reader.GetString();

            // Try parsing the simple format
            if (DateTime.TryParseExact(dateString, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
            {
                return date;
            }

            // Fallback: If the JSON contains the old ISO format (2025-01-01T00:00:00), try generic parsing
            if (DateTime.TryParse(dateString, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime genericDate))
            {
                return genericDate;
            }

            return DateTime.MinValue;
        }
    }
}
