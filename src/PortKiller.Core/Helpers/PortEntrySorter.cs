using PortKiller.Models;

namespace PortKiller.Helpers;

public enum PortSortColumn
{
    Port,
    Protocol,
    State,
    LocalAddress,
    Pid,
    Process
}

public static class PortEntrySorter
{
    public static IEnumerable<PortEntry> Sort(
        IEnumerable<PortEntry> entries,
        PortSortColumn column,
        bool ascending)
    {
        IOrderedEnumerable<PortEntry> ordered = column switch
        {
            PortSortColumn.Port => Order(entries, entry => entry.Port, ascending),
            PortSortColumn.Protocol => Order(entries, entry => entry.ProtocolDisplay, ascending),
            PortSortColumn.State => Order(entries, entry => entry.StateDisplay, ascending),
            PortSortColumn.LocalAddress => Order(entries, entry => entry.LocalAddressDisplay, ascending),
            PortSortColumn.Pid => Order(entries, entry => entry.ProcessId, ascending),
            PortSortColumn.Process => Order(entries, entry => entry.ProcessNameDisplay, ascending),
            _ => Order(entries, entry => entry.Port, ascending)
        };

        return ordered.ThenBy(entry => entry.Port).ThenBy(entry => entry.ProcessId);
    }

    private static IOrderedEnumerable<PortEntry> Order<TKey>(
        IEnumerable<PortEntry> entries,
        Func<PortEntry, TKey> keySelector,
        bool ascending)
    {
        return ascending
            ? entries.OrderBy(keySelector)
            : entries.OrderByDescending(keySelector);
    }
}
