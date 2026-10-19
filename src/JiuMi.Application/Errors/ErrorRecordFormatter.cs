using System.Globalization;
using System.Text;

namespace JiuMi.Application.Errors;

/// <summary>
/// Turns an error record into the text copied by the details action.
/// </summary>
public static class ErrorRecordFormatter
{
    /// <summary>
    /// Formats <paramref name="record"/> with the localized text as the first line.
    /// </summary>
    /// <param name="record">The record to format.</param>
    /// <param name="text">The localized message for the record's event code.</param>
    /// <returns>The localized line followed by one <c>key: value</c> line per present field.</returns>
    public static string Format(ErrorRecord record, string text)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(text);

        var builder = new StringBuilder();
        builder.AppendLine(ErrorRedactor.Redact(text));
        AppendField(builder, "timestamp", record.Timestamp.ToString("O", CultureInfo.InvariantCulture));
        AppendField(builder, "level", record.Level.ToString());
        AppendField(builder, "code", record.Code);
        AppendField(builder, "operation", record.Operation);
        AppendField(builder, "exceptionType", record.ExceptionType);
        AppendField(builder, "exceptionMessage", record.ExceptionMessage);
        AppendField(builder, "stackTrace", record.StackTrace);
        AppendField(builder, "appVersion", record.AppVersion);
        AppendField(builder, "osDescription", record.OsDescription);
        AppendField(builder, "configPath", record.ConfigPath);
        AppendField(builder, "count", record.Count.ToString(CultureInfo.InvariantCulture));
        return builder.ToString();
    }

    private static void AppendField(StringBuilder builder, string key, string? value)
    {
        if (value is null)
        {
            return;
        }

        builder.Append(key).Append(": ").AppendLine(ErrorRedactor.Redact(value));
    }
}
