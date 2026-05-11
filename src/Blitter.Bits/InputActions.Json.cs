using System.Text;
using System.Text.Json;

namespace Blitter.Bits;

/// <summary>
/// JSON persistence for <see cref="InputActions"/>. Format (stable):
/// <code>
/// {
///   "Jump": { "kind": "digital", "bindings": [
///       { "type": "key",         "key": "Space" },
///       { "type": "physicalKey", "key": "W" },
///       { "type": "mouseButton", "button": "Left" }
///   ]},
///   "Strafe": { "kind": "direction", "bindings": [
///       { "type": "keyDir", "negative": "A", "positive": "D" }
///   ]},
///   "Move": { "kind": "direction2d", "bindings": [
///       { "type": "keyDir2D", "left": "A", "right": "D", "down": "S", "up": "W" }
///   ]}
/// }
/// </code>
/// Enum members are written by name (e.g. <c>"Space"</c>) for stability
/// and human-readability.
/// </summary>
public partial class InputActions
{

    /// <summary>
    /// Serializes the action map to JSON. Suitable for round-tripping
    /// via <see cref="LoadJson"/> to apply a saved config.
    /// </summary>
    public string ToJson()
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true }))
        {
            w.WriteStartObject();
            foreach (var kvp in _actions)
            {
                w.WritePropertyName(kvp.Key);
                w.WriteStartObject();
                w.WriteString("kind", KindToToken(kvp.Value.Kind));
                w.WritePropertyName("bindings");
                w.WriteStartArray();
                foreach (var b in kvp.Value.Bindings)
                    WriteBinding(w, b);
                w.WriteEndArray();
                w.WriteEndObject();
            }
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    /// <summary>
    /// Replaces this map's contents with the bindings parsed from
    /// <paramref name="json"/>. Existing actions are dropped.
    /// </summary>
    public void LoadJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        _actions.Clear();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new FormatException("InputActions JSON must be an object.");
        foreach (var prop in root.EnumerateObject())
        {
            var entry = new ActionEntry
            {
                Kind = KindFromToken(prop.Value.GetProperty("kind").GetString()
                                     ?? throw new FormatException("missing 'kind'")),
            };
            foreach (var b in prop.Value.GetProperty("bindings").EnumerateArray())
                entry.Bindings.Add(ReadBinding(b));
            _actions[prop.Name] = entry;
        }
    }

    /// <summary>
    /// Creates a new <see cref="InputActions"/> from a JSON config,
    /// bound to the given <see cref="FrameInput"/>.
    /// </summary>
    public static InputActions FromJson(string json, FrameInput input)
    {
        var actions = new InputActions(input);
        actions.LoadJson(json);
        return actions;
    }

    private static void WriteBinding(Utf8JsonWriter w, InputBinding b)
    {
        w.WriteStartObject();
        switch (b)
        {
            case KeyBinding k:
                w.WriteString("type", "key");
                w.WriteString("key", k.Key.ToString());
                break;
            case PhysicalKeyBinding p:
                w.WriteString("type", "physicalKey");
                w.WriteString("key", p.Key.ToString());
                break;
            case MouseButtonBinding m:
                w.WriteString("type", "mouseButton");
                w.WriteString("button", m.Button.ToString());
                break;
            case KeyDirectionBinding d:
                w.WriteString("type", "keyDir");
                w.WriteString("negative", d.Negative.ToString());
                w.WriteString("positive", d.Positive.ToString());
                break;
            case KeyDirection2DBinding d2:
                w.WriteString("type", "keyDir2D");
                w.WriteString("left", d2.Left.ToString());
                w.WriteString("right", d2.Right.ToString());
                w.WriteString("down", d2.Down.ToString());
                w.WriteString("up", d2.Up.ToString());
                break;
            default:
                throw new InvalidOperationException(
                    $"Unknown InputBinding type: {b.GetType().Name}");
        }
        w.WriteEndObject();
    }

    private static InputBinding ReadBinding(JsonElement el)
    {
        var type = el.GetProperty("type").GetString();
        return type switch
        {
            "key" => new KeyBinding(ParseEnum<Key>(el, "key")),
            "physicalKey" => new PhysicalKeyBinding(
                ParseEnum<PhysicalKey>(el, "key")),
            "mouseButton" => new MouseButtonBinding(
                ParseEnum<MouseButton>(el, "button")),
            "keyDir" => new KeyDirectionBinding(
                ParseEnum<Key>(el, "negative"),
                ParseEnum<Key>(el, "positive")),
            "keyDir2D" => new KeyDirection2DBinding(
                ParseEnum<Key>(el, "left"),
                ParseEnum<Key>(el, "right"),
                ParseEnum<Key>(el, "down"),
                ParseEnum<Key>(el, "up")),
            _ => throw new FormatException(
                $"Unknown InputBinding type token '{type}'."),
        };
    }

    private static T ParseEnum<T>(JsonElement el, string property) where T : struct, Enum
    {
        var s = el.GetProperty(property).GetString()
            ?? throw new FormatException(
                $"Missing '{property}' in InputBinding.");
        if (!Enum.TryParse<T>(s, ignoreCase: true, out var value))
            throw new FormatException(
                $"Unrecognized {typeof(T).Name} value '{s}'.");
        return value;
    }

    private static string KindToToken(InputActionKind kind) => kind switch
    {
        InputActionKind.Digital => "digital",
        InputActionKind.Direction => "direction",
        InputActionKind.Direction2D => "direction2d",
        InputActionKind.Unset => "unset",
        _ => throw new InvalidOperationException(),
    };

    private static InputActionKind KindFromToken(string token) =>
        token.ToLowerInvariant() switch
        {
            "digital" => InputActionKind.Digital,
            "direction" => InputActionKind.Direction,
            "direction2d" => InputActionKind.Direction2D,
            "unset" => InputActionKind.Unset,
            _ => throw new FormatException(
                $"Unknown InputActionKind token '{token}'."),
        };
}
