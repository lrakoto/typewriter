using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Typewriter.Core;

public sealed record Note
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; init; } = "Untitled";
    public string Text { get; init; } = "";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeletedAt { get; init; }
    [JsonIgnore] public int WordCount => Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    [JsonIgnore] public string Preview => string.IsNullOrWhiteSpace(Text) ? "A blank page, a fresh start." :
        string.Join(' ', Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Truncate(110);
}

internal static class StringExtensions
{
    public static string Truncate(this string text, int count) => text.Length <= count ? text : text[..count];
}

public sealed record Preferences
{
    public Guid? SelectedNote { get; init; }
    public string Theme { get; init; } = "linen";
    public bool Spotlight { get; init; } = true;
    public bool KeySounds { get; init; }
    public double FontSize { get; init; } = 17;
    public double Volume { get; init; } = 0.25;
    public Preferences Validated() => this with {
        Theme = Theme is "linen" or "parchment" or "midnight" ? Theme : "linen",
        FontSize = double.IsFinite(FontSize) ? Math.Clamp(FontSize, 14, 24) : 17,
        Volume = double.IsFinite(Volume) ? Math.Clamp(Volume, 0, 0.7) : 0.25
    };
}

public static class NoteJson
{
    public static readonly JsonSerializerOptions Options = CreateOptions();
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new UtcDateConverter());
        return options;
    }
    // Match the Mac library's ISO-8601 representation, including no fractional seconds.
    private sealed class UtcDateConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            DateTimeOffset.Parse(reader.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
    }
}
