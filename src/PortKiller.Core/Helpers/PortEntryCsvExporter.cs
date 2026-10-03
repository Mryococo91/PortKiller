using System.Globalization;
using System.Text;
using PortKiller.Models;

namespace PortKiller.Helpers;

public static class PortEntryCsvExporter
{
    public static string Export(IEnumerable<PortEntry> entries)
    {
        var builder = new StringBuilder();
        // UTF-8 BOM helps Excel recognize encoding for localized path/state text.
        builder.Append('\uFEFF');
        builder.AppendLine("Port,Protocol,State,LocalAddress,RemoteAddress,RemotePort,PID,Process,Path");

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
            builder.Append(Escape(entry.RemoteAddressDisplay));
            builder.Append(',');
            builder.Append(Escape(
                entry.Endpoint.RemotePort?.ToString(CultureInfo.InvariantCulture) ?? string.Empty));
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
        // Neutralize Excel/LibreOffice formula injection on leading trigger chars.
        if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }

        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }

        return value;
    }
}
