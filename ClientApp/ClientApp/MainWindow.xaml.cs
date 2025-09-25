using System;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ChatClient
{
    public partial class MainWindow : Window
    {
        TcpClient? client;
        NetworkStream? stream;
        string username = "";

        //Theme
        private bool isDark = false;

        public MainWindow()
        {
            InitializeComponent();
            lstUsers.MouseDoubleClick += LstUsers_MouseDoubleClick;
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

                    // kirim pesan join
                    var joinMsg = new ChatMessage
                    {
                        type = "join",
                        from = username,
                        text = "",
                        ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    };
                    string joinJson = JsonSerializer.Serialize(joinMsg);
                    byte[] joinData = Encoding.UTF8.GetBytes(joinJson);
                    await stream.WriteAsync(joinData);

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
                // Graceful shutdown
                stream?.Close();
                client?.Close();
                stream = null;
                client = null;
                btnConnect.Content = "Connect";
                lstChat.Items.Add("Disconnected");
                lstUsers.Items.Clear();
            }
        }

        private async void btnSend_Click(object sender, RoutedEventArgs e)
        {
            await SendMessage();
        }

        private async Task SendMessage()
        {
            if (stream == null) return;
            if (string.IsNullOrWhiteSpace(txtMessage.Text)) return;

            string messageText = txtMessage.Text;
            string? targetUser = null;

            // Parsing command /w target pesan
            if (messageText.StartsWith("/w "))
            {
                var parts = messageText.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    targetUser = parts[1];
                    messageText = parts[2];
                }
            }

            var msg = new ChatMessage
            {
                type = "msg",
                from = username,
                to = targetUser ?? "",
                text = messageText,
                ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            string json = JsonSerializer.Serialize(msg);
            byte[] data = Encoding.UTF8.GetBytes(json);
            await stream.WriteAsync(data);

            txtMessage.Clear();
        }

        private void btnDarkMode_Click(object sender, RoutedEventArgs e)
        {
            var dict = new ResourceDictionary();
            if (!isDark)
            {
                dict.Source = new Uri("Themes/DarkTheme.xaml", UriKind.Relative);
                btnDarkMode.Content = "☀ Light Mode";
            }
            else
            {
                dict.Source = new Uri("Themes/LightTheme.xaml", UriKind.Relative);
                btnDarkMode.Content = "🌙 Dark Mode";
            }

            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(dict);

            isDark = !isDark;
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
                    if (msg == null) continue;

                    Dispatcher.Invoke(() =>
                    {
                        if (msg.type == "sys")
                        {
                            lstChat.Items.Add($"[SYSTEM] {msg.text}");
                        }
                        else if (msg.type == "userlist")
                        {
                            lstUsers.Items.Clear();
                            var users = msg.text.Split(',', StringSplitOptions.RemoveEmptyEntries);
                            foreach (var u in users)
                                lstUsers.Items.Add(u);
                        }
                        else if (msg.type == "msg")
                        {
                            // Bedakan whisper dan normal chat
                            if (!string.IsNullOrEmpty(msg.to))
                            {
                                if (msg.from.StartsWith("You ->"))
                                    lstChat.Items.Add($"{msg.from}: {msg.text}");
                                else
                                    lstChat.Items.Add($"[Whisper] {msg.from} -> {msg.to}: {msg.text}");
                            }
                            else
                            {
                                lstChat.Items.Add($"{msg.from}: {msg.text}");
                            }
                        }
                    });
                }
                catch
                {
                    // ignore parsing error
                }
            }

            Dispatcher.Invoke(() =>
            {
                lstChat.Items.Add("Disconnected from server");
                lstUsers.Items.Clear();

                btnConnect.Content = "Connect";
            });

            stream?.Close();
            client?.Close();
            stream = null;
            client = null;
        }

        // klik user -> auto isi "/w username "
        private void LstUsers_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (lstUsers.SelectedItem != null)
            {
                string targetUser = lstUsers.SelectedItem.ToString()!;
                txtMessage.Text = $"/w {targetUser} ";
                txtMessage.Focus();
                txtMessage.CaretIndex = txtMessage.Text.Length;
            }
        }
    }
}
