using System;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace ChatClient
{
    public partial class MainWindow : Window
    {
        TcpClient? client;
        NetworkStream? stream;
        string username = "";

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (client == null || !client.Connected)
            {
                try
                {
                    client = new TcpClient();
                    await client.ConnectAsync(txtIP.Text, int.Parse(txtPort.Text));
                    stream = client.GetStream();
                    username = txtUser.Text;

                    _ = Task.Run(ReceiveLoop);

                    btnConnect.Content = "Disconnect";
                    lstChat.Items.Add("Connected as " + username);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Connect failed: " + ex.Message);
                }
            }
            else
            {
                client.Close();
                btnConnect.Content = "Connect";
                lstChat.Items.Add("Disconnected");
            }
        }

        private async void btnSend_Click(object sender, RoutedEventArgs e)
        {
            if (stream == null) return;

            var msg = new ChatMessage
            {
                type = "msg",
                from = username,
                text = txtMessage.Text,
                ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            string json = JsonSerializer.Serialize(msg);
            byte[] data = Encoding.UTF8.GetBytes(json);
            await stream.WriteAsync(data);
            txtMessage.Clear();
        }

        private async Task ReceiveLoop()
        {
            var buffer = new byte[4096];
            while (client != null && client.Connected)
            {
                int bytesRead;
                try
                {
                    bytesRead = await stream!.ReadAsync(buffer);
                }
                catch { break; }

                if (bytesRead == 0) break;

                string json = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                try
                {
                    var msg = JsonSerializer.Deserialize<ChatMessage>(json);
                    Dispatcher.Invoke(() =>
                    {
                        lstChat.Items.Add($"{msg!.from}: {msg.text}");
                    });
                }
                catch
                {
                    // ignore parsing error
                }
            }

            Dispatcher.Invoke(() => lstChat.Items.Add("Disconnected from server."));
        }
    }
}
