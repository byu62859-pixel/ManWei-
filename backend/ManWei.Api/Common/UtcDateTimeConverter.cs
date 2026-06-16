using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManWei.Api.Common;

/// <summary>
/// 自定义 DateTime JSON 转换器，强制所有时间为 UTC 时区
/// </summary>
public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrEmpty(value))
            return default;

        // 尝试解析为 UTC
        if (DateTime.TryParse(value, out var result))
        {
            return DateTime.SpecifyKind(result, DateTimeKind.Utc);
        }
        return default;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        // 强制转换为 UTC
        var utcValue = value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();

        // 格式: yyyy-MM-ddTHH:mm:ssZ
        writer.WriteStringValue(utcValue.ToString("yyyy-MM-ddTHH:mm:ssZ"));
    }
}
