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

    public static async Task<Connection> Initialize(string ipAddress, int port, int timeout = 60000)
    {
        var task = Ini(ipAddress, port);
        if (await Task.WhenAny(task, Task.Delay(timeout)) == task)
        {
            return task.Result;
        }

        throw new TimeoutException("Connection timeout.");
    }

    public static async Task<Connection> FindConnectionAsync()
    {
        var ip = GetLocalIp();
        if (ip == null)
        {
            throw new IOException("Network error.");
        }

        var index = ip.LastIndexOf(".", StringComparison.Ordinal);
        var router = ip[..(index + 1)];
        var pos = 49;

        while (true)
        {
            try
            {
                if (pos >= 254)
                {
                    pos = 1;
                }
                pos++;
                var search = $"{router}{pos}";
                if(search == GetLocalIp()) continue;
                return await Initialize($"{router}{pos}", 11000, 2000);
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

    private static async Task<Connection> Ini(string ipAddress, int port)
    {
        var client = new TcpClient();
        while (true)
        {
            try
            {
                await client.ConnectAsync(IPAddress.Parse(ipAddress), port);
                break;
            }
            catch
            {
                // ignored
            }
        }

        return new Connection(client, client.GetStream(), ipAddress, port);
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