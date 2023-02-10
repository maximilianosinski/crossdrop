using System.Net;
using System.Net.Sockets;
using System.Text;

namespace CrossDrop;

public class Connection
{
    private TcpClient TcpClient { get; }
    private NetworkStream NetworkStream { get; }
    private string IpAddress { get; set; }
    private int? Port { get; set; }
    
    public Connection(TcpClient tcpClient, NetworkStream networkStream, string ipAddress, int port)
    {
        TcpClient = tcpClient;
        NetworkStream = networkStream;
        IpAddress = ipAddress;
        Port = port;
    }

    public static async Task<Connection> Initialize(string ipAddress, int port)
    {

        while (true)
        {
            var client = new TcpClient();
            try
            {
                await client.ConnectAsync(IPAddress.Parse(ipAddress), port);
            }
            catch
            {
                // ignored
            }
        }
    }

    public static async Task<Connection> FindConnectionAsync(CancellationToken cancellationToken)
    {
        var ip = GetLocalIp();
        if (ip == null)
        {
            throw new IOException("Network error.");
        }

        var index = ip.LastIndexOf(".", StringComparison.Ordinal);
        var router = ip[..(index + 1)];
        var pos = 2;

        while (true)
        {
            try
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return null;
                }
                if (pos >= 254)
                {
                    pos = 1;
                }
                pos++;
                var search = $"{router}{pos}";
                if(search == GetLocalIp()) continue;
                var client = new TcpClient();
                try
                {
                    await client.ConnectAsync(IPAddress.Parse(search), 11000);
                }
                catch
                {
                    // ignored
                }
            }
            catch
            {
                // ignored
            }
        }
    }

    private static string? GetLocalIp()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        return (from ip in host.AddressList where ip.AddressFamily == AddressFamily.InterNetwork select ip.ToString()).FirstOrDefault();
    }

    public async Task SendFile(string filename, byte[] data)
    {
        var message = $"FILE:{data.Length}:{filename}";
        var messageBytes = Encoding.UTF8.GetBytes(message);
        await NetworkStream.WriteAsync(messageBytes);
        await NetworkStream.WriteAsync(data);
    }

    public IPAddress? GetIpAddress()
    {
        return IpAddress == null ? null : IPAddress.Parse(IpAddress);
    }

    public int? GetPort()
    {
        return Port;
    }

    public void Disconnect()
    {
        TcpClient.Close();
        NetworkStream.Close();
        IpAddress = null;
        Port = null;
    }
}