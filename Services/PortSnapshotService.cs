using PortKiller.Models;

namespace PortKiller.Services;

public sealed class PortSnapshotService
{
    private readonly TcpUdpTableReader _tableReader;
    private readonly ProcessInfoService _processInfoService;

    public PortSnapshotService(TcpUdpTableReader tableReader, ProcessInfoService processInfoService)
    {
        _tableReader = tableReader;
        _processInfoService = processInfoService;
    }

    public Task<IReadOnlyList<PortEntry>> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                IReadOnlyList<PortEndpoint> endpoints = _tableReader.ReadAll();
                _processInfoService.Reset();

                var entries = new List<PortEntry>(endpoints.Count);
                foreach (PortEndpoint endpoint in endpoints)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ProcessIdentity process = _processInfoService.Get(endpoint.ProcessId);
                    entries.Add(new PortEntry
                    {
                        Endpoint = endpoint,
                        Process = process
                    });
                }

                entries.Sort(static (left, right) =>
                {
                    int port = left.Port.CompareTo(right.Port);
                    if (port != 0)
                    {
                        return port;
                    }

                    int protocol = left.Endpoint.Protocol.CompareTo(right.Endpoint.Protocol);
                    if (protocol != 0)
                    {
                        return protocol;
                    }

                    return string.Compare(left.LocalAddressDisplay, right.LocalAddressDisplay, StringComparison.OrdinalIgnoreCase);
                });

                return (IReadOnlyList<PortEntry>)entries;
            },
            cancellationToken);
    }
}
