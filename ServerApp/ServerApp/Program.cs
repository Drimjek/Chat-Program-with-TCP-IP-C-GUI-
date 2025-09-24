using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

class Program
{
    static Dictionary<string, TcpClient> users = new(); // username -> client
    const int PORT = 9000;

    static async Task Main()
    {
        var listener = new TcpListener(IPAddress.Any, PORT);
        listener.Start();
        Console.WriteLine($"Server running on port {PORT}");

        while (true)
        {
            var client = await listener.AcceptTcpClientAsync();
            Console.WriteLine("Client connected");
            _ = HandleClient(client);
        }
    }

    static async Task HandleClient(TcpClient client)
    {
        using var stream = client.GetStream();
        var buffer = new byte[4096];
        string? username = null;

        try
        {
            while (client.Connected)
            {
                int read = await stream.ReadAsync(buffer);
                if (read == 0) break;

                var json = Encoding.UTF8.GetString(buffer, 0, read);
                var msg = JsonSerializer.Deserialize<ChatMessage>(json);

                if (msg == null) continue;

                // Jika pesan pertama adalah join
                if (msg.type == "join" && username == null)
                {
                    if (users.ContainsKey(msg.from))
                    {
                        // Username sudah ada
                        var err = new ChatMessage
                        {
                            type = "sys",
                            from = "server",
                            text = "Username already taken!",
                            ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                        };
                        await SendAsync(client, err);
                        break; // putuskan koneksi
                    }

                    username = msg.from;
                    users[username] = client;
                    Console.WriteLine($"{username} joined.");

                    // Broadcast system join
                    Broadcast(new ChatMessage
                    {
                        type = "sys",
                        from = "server",
                        text = $"{username} has joined.",
                        ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    });

                    // Kirim daftar user ke semua client
                    SendUserList();
                }
                else if (msg.type == "msg")
                {
                    // Broadcast normal chat
                    Broadcast(msg);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            if (username != null && users.ContainsKey(username))
            {
                users.Remove(username);
                Console.WriteLine($"{username} left.");

                // Broadcast leave
                Broadcast(new ChatMessage
                {
                    type = "sys",
                    from = "server",
                    text = $"{username} has left.",
                    ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                });

                SendUserList();
            }

            client.Close();
        }
    }

    static async Task SendAsync(TcpClient client, ChatMessage msg)
    {
        try
        {
            var json = JsonSerializer.Serialize(msg);
            var data = Encoding.UTF8.GetBytes(json);
            await client.GetStream().WriteAsync(data);
        }
        catch
        {
            // ignore send error
        }
    }

    static void Broadcast(ChatMessage msg)
    {
        var json = JsonSerializer.Serialize(msg);
        var data = Encoding.UTF8.GetBytes(json);

        foreach (var kvp in users.ToList())
        {
            try
            {
                kvp.Value.GetStream().Write(data, 0, data.Length);
            }
            catch
            {
                users.Remove(kvp.Key);
            }
        }
    }

    static void SendUserList()
    {
        var list = string.Join(",", users.Keys);
        var msg = new ChatMessage
        {
            type = "userlist",
            from = "server",
            text = list,
            ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        Broadcast(msg);
    }
}
