using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

class Program
{
    static List<TcpClient> clients = new List<TcpClient>();
    const int PORT = 9000;

    static async Task Main()
    {
        var listener = new TcpListener(IPAddress.Any, PORT);
        listener.Start();
        Console.WriteLine($"Server running on port {PORT}");

        while (true)
        {
            var client = await listener.AcceptTcpClientAsync();
            clients.Add(client);
            Console.WriteLine("Client connected");
            _ = HandleClient(client);
            //test
        }
    }

    static async Task HandleClient(TcpClient client)
    {
        using var stream = client.GetStream();
        var buffer = new byte[4096];

        while (client.Connected)
        {
            int read;
            try
            {
                read = await stream.ReadAsync(buffer);
            }
            catch { break; }

            if (read == 0) break;

            var json = Encoding.UTF8.GetString(buffer, 0, read);
            Console.WriteLine("Recv: " + json);

            // Broadcast ke semua client
            foreach (var c in clients.ToList())
            {
                try
                {
                    await c.GetStream().WriteAsync(Encoding.UTF8.GetBytes(json));
                }
                catch
                {
                    clients.Remove(c);
                }
            }
        }

        clients.Remove(client);
        Console.WriteLine("Client disconnected");
    }
}
