using System.Net;
using System.Net.Sockets;
using System.Text;

Console.WriteLine("Welcome to Hardik's Firewall Network///");

const int proxyPort = 8989;
TcpListener listener = new(IPAddress.Any, proxyPort);
listener.Start();

Console.WriteLine("Proxy server start on Port: {0}", proxyPort);

const string forbiddenHostsFile = "forbidden-hosts.txt";

var forbiddenHosts = new HashSet<string>()
{
    "facebook.com",
    "discord.com"
};

while (true)
{
    TcpClient client = await listener.AcceptTcpClientAsync();
    _ = HandleClientAsync(client, forbiddenHosts);
}

static async Task HandleClientAsync(TcpClient client, HashSet<string> forbiddenHosts)
{
    using (client)
    await using (NetworkStream clientStream = client.GetStream())
    {
        byte[] buffer = new byte[4096];
        int bytesRead = await clientStream.ReadAsync(buffer, 0, buffer.Length);
        string request = Encoding.UTF8.GetString(buffer, 0, bytesRead);

        string targetHost = GetHostFromRequest(request);
        
        string host = GetHostFromRequest(request);

        if (forbiddenHosts.Contains(host.ToLower()))
        {
            string forbiddenResponse = "HTTP/1.1 403 Forbidden\r\nContent-Type: text/plain; charset=utf-8\r\n\r\nWebsite not allowed: " + host;
            byte[] forbiddenBytes = Encoding.UTF8.GetBytes(forbiddenResponse);
            await clientStream.WriteAsync(forbiddenBytes, 0, forbiddenBytes.Length);
            await clientStream.FlushAsync();
            return; 
        }
        
        using (TcpClient targetClient = new TcpClient())
        {
            await targetClient.ConnectAsync(targetHost, 80);
            await using (NetworkStream targetStream = targetClient.GetStream())
            {
                IPEndPoint clientEndpoint = (IPEndPoint)client.Client.RemoteEndPoint!;
                string clientIpAddress = clientEndpoint.Address.ToString();
                int clientPort = clientEndpoint.Port;
                Console.WriteLine("Request made. Target: {0}:{1} Client: {2}:{3}", targetHost, 80, clientIpAddress, clientPort);
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



static HashSet<string> LoadForbiddenHosts()
{
    HashSet<string> hosts = new HashSet<string>();

    string currentDirectory = Directory.GetCurrentDirectory();
    string filePath = Path.Combine(currentDirectory, forbiddenHostsFile);

    try
    {
        string[] forbiddenHosts = File.ReadAllLines(filePath);
        foreach (string host in forbiddenHosts)
        {
            hosts.Add(host.Trim());
        }
    }
    catch (FileNotFoundException)
    {
        Console.WriteLine($"File '{forbiddenHostsFile}' not found. No hosts will be forbidden.");
    }
    catch (IOException ex)
    {
        Console.WriteLine($"Error reading file '{forbiddenHostsFile}': {ex.Message}");
    }

    return hosts;
}