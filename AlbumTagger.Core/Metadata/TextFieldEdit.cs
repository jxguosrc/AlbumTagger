namespace AlbumTagger.Core.Metadata;

public enum TextFieldEditMode
{
    Keep,
    Set,
    Clear,
}

public readonly record struct TextFieldEdit(TextFieldEditMode Mode, string? Value)
{
    public static TextFieldEdit Keep => new(TextFieldEditMode.Keep, null);

    public static TextFieldEdit Clear => new(TextFieldEditMode.Clear, null);

    public static TextFieldEdit Set(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new TextFieldEdit(TextFieldEditMode.Set, value.Trim());
    }

    public string ApplyTo(string currentValue) => Mode switch
    {
        TextFieldEditMode.Keep => currentValue,
        TextFieldEditMode.Set => Value!,
        TextFieldEditMode.Clear => string.Empty,
        _ => throw new InvalidOperationException($"未知的文本字段操作：{Mode}。"),
    };
}
