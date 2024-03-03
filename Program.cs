using System.Net;
using System.Net.Sockets;
using System.Text;

Console.WriteLine("Welcome to Hardik's Firewall Network");

const int proxyPort = 8989;

TcpListener listener = new(IPAddress.Any, proxyPort);
listener.Start();

Console.WriteLine("Proxy server start on Port: {0}", proxyPort);

while (true)
{
    TcpClient client = await listener.AcceptTcpClientAsync();
    _ = HandleClientAsync(client);
}

static async Task HandleClientAsync(TcpClient client)
{
    using (client)
    await using (NetworkStream clientStream = client.GetStream())
    {
        byte[] buffer = new byte[4096];
        int bytesRead = await clientStream.ReadAsync(buffer, 0, buffer.Length);
        string request = Encoding.UTF8.GetString(buffer, 0, bytesRead);
        foreach (var line in request.Split("\r\n"))
        {
            Console.WriteLine(line);
        }

        string targetHost = GetHostFromRequest(request);
        
        
        using (TcpClient targetClient = new TcpClient())
        {
            await targetClient.ConnectAsync(targetHost, 80);
            await using (NetworkStream targetStream = targetClient.GetStream())
            {
                await targetStream.WriteAsync(buffer, 0, bytesRead);
                await targetStream.FlushAsync();

                bytesRead = await targetStream.ReadAsync(buffer, 0, buffer.Length);

                await clientStream.WriteAsync(buffer, 0, bytesRead);
                await clientStream.FlushAsync();
            }
        }

    }

}

static string GetHostFromRequest(string request)
{
    string[] lines = request.Split('\n');
    foreach (var line in lines)
    {
        if (line.StartsWith("Host:"))
        {
            return line[6..].Trim();
        }
    }

    throw new ArgumentException("Host not found in request");
}