namespace Maf.Agent.Api.Chat.Domain;

public sealed class ChatFile
{
    public const int MaxPerMessage = 4;

    public const int MaxFileNameLength = 255;

    public const int MaxMediaTypeLength = 127;

    public const int MaxBytes = 4 * 1024 * 1024;

    public ChatFile(string? fileName, string? mediaType, string? contentBase64)
    {
        FileName = NormalizeFileName(fileName);
        MediaType = NormalizeMediaType(mediaType);
        ContentBase64 = NormalizeBase64(contentBase64);
    }

    public string FileName { get; }

    public string MediaType { get; }

    public string ContentBase64 { get; }

    private static string NormalizeFileName(string? fileName)
    {
        string name = fileName?.Trim() ?? "";
        int slash = Math.Max(name.LastIndexOf('/'), name.LastIndexOf('\\'));
        if (slash >= 0)
        {
            name = name[(slash + 1)..];
        }

        name = name.Trim();
        if (name is "" or "." or "..")
        {
            return "file";
        }

        return name.Length <= MaxFileNameLength ? name : name[..MaxFileNameLength];
    }

    private static string NormalizeMediaType(string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            return "application/octet-stream";
        }

        string type = mediaType.Trim();
        int separator = type.IndexOf(';');
        if (separator >= 0)
        {
            type = type[..separator].Trim();
        }

        type = type.ToLowerInvariant();
        if (type.Length is 0 or > MaxMediaTypeLength || !IsMediaType(type))
        {
            return "application/octet-stream";
        }

        return type;
    }

    private static bool IsMediaType(string type)
    {
        int slash = type.IndexOf('/');
        if (slash <= 0 || slash != type.LastIndexOf('/') || slash == type.Length - 1)
        {
            return false;
        }

        foreach (char character in type)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('/' or '+' or '-' or '.'))
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeBase64(string? contentBase64)
    {
        if (string.IsNullOrWhiteSpace(contentBase64))
        {
            throw new ArgumentException("فایل خالی است.");
        }

        string compact = contentBase64.Trim();
        int maxChars = ((MaxBytes + 2) / 3) * 4;
        if (compact.Length > maxChars)
        {
            throw new ArgumentException("حجم فایل بیش از ۴ مگابایت است.");
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(compact);
        }
        catch (FormatException)
        {
            throw new ArgumentException("محتوای فایل معتبر نیست.");
        }

        if (bytes.Length == 0)
        {
            throw new ArgumentException("فایل خالی است.");
        }

        if (bytes.Length > MaxBytes)
        {
            throw new ArgumentException("حجم فایل بیش از ۴ مگابایت است.");
        }

        return Convert.ToBase64String(bytes);
    }
}
