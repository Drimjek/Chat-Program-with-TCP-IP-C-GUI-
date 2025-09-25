using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

class Program
{
    static Dictionary<string, TcpClient> users = new(); // username -> client
    const int PORT = 9000;
    static readonly object logLock = new();

    static async Task Main()
    {
        var listener = new TcpListener(IPAddress.Any, PORT);
        listener.Start();
        Log($"Server running on port {PORT}");

        while (true)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync();
                Log("Client connected.");
                _ = HandleClient(client);
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Accept client failed: {ex.Message}");
            }
        }
    }

    static async Task HandleClient(TcpClient client)
    {
        NetworkStream? stream = null;
        string? username = null;

        try
        {
            stream = client.GetStream();
            var buffer = new byte[4096];

            while (client.Connected)
            {
                int read = await stream.ReadAsync(buffer);
                if (read == 0) break;

                var json = Encoding.UTF8.GetString(buffer, 0, read);
                var msg = JsonSerializer.Deserialize<ChatMessage>(json);
                if (msg == null) continue;

                // JOIN
                if (msg.type == "join" && username == null)
                {
                    if (users.ContainsKey(msg.from))
                    {
                        var err = new ChatMessage
                        {
                            type = "sys",
                            from = "server",
                            text = "Username already taken!",
                            ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                            
                        };
                        await SendAsync(client, err);

                        stream.Close();
                        client.Close();
                        Log($"Username taken :, {msg.from}, connection closed ");
                        return;
                    }

                    username = msg.from;
                    users[username] = client;
                    Log($"{username} joined.");

                    Broadcast(new ChatMessage
                    {
                        type = "sys",
                        from = "server",
                        text = $"{username} has joined.",
                        ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    });

                    SendUserList();
                }
                // MESSAGE
                else if (msg.type == "msg")
                {
                    if (!string.IsNullOrEmpty(msg.to))
                    {
                        await HandlePrivateMessage(client, msg);
                    }
                    else
                    {
                        Broadcast(msg);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log($"[ERROR] {ex.Message}");
        }
        finally //Gracefull shutdown
        {
            if (username != null && users.ContainsKey(username))
            {
                users.Remove(username);
                Log($"{username} left.");

                Broadcast(new ChatMessage
                {
                    type = "sys",
                    from = "server",
                    text = $"{username} has left.",
                    ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                });

                SendUserList();
            }

            try { stream?.Dispose(); } catch { }
            try { client.Close(); } catch { }
        }
    }

    static async Task HandlePrivateMessage(TcpClient sender, ChatMessage msg)
    {
        if (users.TryGetValue(msg.to, out var targetClient))
        {
            // ke target
            var whisperToTarget = new ChatMessage
            {
                type = "msg",
                from = msg.from,
                to = msg.to,
                text = $"(whisper) {msg.text}",
                ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            await SendAsync(targetClient, whisperToTarget);

            // konfirmasi ke pengirim
            var whisperToSender = new ChatMessage
            {
                type = "msg",
                from = $"You -> {msg.to}",
                to = msg.to,
                text = msg.text,
                ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            await SendAsync(sender, whisperToSender);

            Log($"[PM] {msg.from} -> {msg.to}: {msg.text}");
        }
        else
        {
            var err = new ChatMessage
            {
                type = "sys",
                from = "server",
                text = $"User {msg.to} not found.",
                ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };
            await SendAsync(sender, err);
        }
    }

    static async Task SendAsync(TcpClient client, ChatMessage msg)
    {
        try
        {
            if (!client.Connected) return;
            var json = JsonSerializer.Serialize(msg) + "\n";
            var data = Encoding.UTF8.GetBytes(json);
            await client.GetStream().WriteAsync(data, 0, data.Length);
            Log($" {msg.from}: {msg.text}");

        }
        catch (Exception ex)
        {
            Log($"[ERROR] Send failed: {ex.Message}");
        }
    }

    static void Broadcast(ChatMessage msg)
    {
        var json = JsonSerializer.Serialize(msg) + "\n";
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
                Log($"[WARN] Removed {kvp.Key} (disconnected).");
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

    static void Log(string text)
    {
        string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {text}";
        Console.WriteLine(line);
        lock (logLock)
        {
            File.AppendAllText("server.log", line + Environment.NewLine);
        }
    }
}
