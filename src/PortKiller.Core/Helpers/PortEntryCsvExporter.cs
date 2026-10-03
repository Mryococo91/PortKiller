using System.Globalization;
using System.Text;
using PortKiller.Models;

namespace PortKiller.Helpers;

public static class PortEntryCsvExporter
{
    public static string Export(IEnumerable<PortEntry> entries)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Port,Protocol,State,LocalAddress,PID,Process,Path");

        foreach (PortEntry entry in entries)
        {
            builder.Append(entry.Port.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(Escape(entry.ProtocolDisplay));
            builder.Append(',');
            builder.Append(Escape(entry.StateDisplay));
            builder.Append(',');
            builder.Append(Escape(entry.LocalAddressDisplay));
            builder.Append(',');
            builder.Append(entry.ProcessId.ToString(CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(Escape(entry.ProcessNameDisplay));
            builder.Append(',');
            builder.Append(Escape(entry.ExecutablePath ?? string.Empty));
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string Escape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return value;
    }
}
