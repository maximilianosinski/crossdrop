using System.Net;
using System.Net.Sockets;
using System.Text;
using Newtonsoft.Json;

namespace CrossDrop;

public partial class MainPage
{
    private Connection _connection;
    private byte[] _currentFile;
    private string _currentFileName;
    private WebView _webView;

    public MainPage()
    {
        InitializeComponent();
        _webView = new WebView();
        ContentPage.Content = _webView;
        _webView.Reload();
        _webView.Source = "http://192.168.0.51:5173/";
        _webView.Navigating += async (_, args) =>
        {
            if (args.Url.Contains("action."))
            {
                args.Cancel = true;
                var command = args.Url.Replace("action.", "");
                var parts = command.Split(":", 3);
                var action = parts[0];
                var type = parts[1];
                var identifier = parts[2];

                if (action == "event")
                {
                    if (type == "command")
                    {
                        if (identifier == "searchDevice")
                        {
                            var listener = new Thread(Listener);
                            listener.Start();
                            _connection = await Connection.FindConnectionAsync();
                            var ipAddress = _connection.GetIpAddress()!.ToString();
                            await _webView.EvaluateJavaScriptAsync(
                                $"actionHandler.resultEvent('command:searchDevice', '{JsonConvert.SerializeObject(new { ipAddress })}')");
                        }
                        
                        if (identifier == "selectFile")
                        {
                            var result = await FilePicker.Default.PickAsync();
                            if (result != null)
                            {
                                var stream = await result.OpenReadAsync();
                                var memoryStream = new MemoryStream();
                                await stream.CopyToAsync(memoryStream);
                                _currentFile = memoryStream.ToArray();
                                _currentFileName = result.FileName;
                                await _webView.EvaluateJavaScriptAsync(
                                    $"actionHandler.resultEvent('command:selectFile', '{JsonConvert.SerializeObject(new { name = result.FileName })}')");
                            }
                        }

                        if (identifier == "sendFile")
                        {
                            if (_currentFile == null || _currentFileName == null) return;
                            await _connection.SendFile(_currentFileName, _currentFile);
                            _currentFile = null;
                            _currentFileName = null;
                            await _webView.EvaluateJavaScriptAsync($"actionHandler.resultEvent('command:sendFile', '{JsonConvert.SerializeObject(new { success = true })}')");
                        }
                    }
                }
            }
        };
    }

    private async void Listener()
    {
        var server = new TcpListener(IPAddress.Any, 11000);
        server.Start();

        while (true)
        {
            var client = await server.AcceptTcpClientAsync();
            var networkStream = client.GetStream();
            var memoryStream = new MemoryStream();

            var buffer = new byte[1024 * 16];
            int bytesReceived;

            long completeSize = 0;
            var fileName = string.Empty;

            while ((bytesReceived = networkStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (IsString(buffer))
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, bytesReceived);
                    if (!message.StartsWith("FILE")) continue;
                    var messageArray = message.Split(':');
                    completeSize = Convert.ToInt64(messageArray[1]);
                    fileName = messageArray[2];
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await _webView.EvaluateJavaScriptAsync($"window.setCurrentFileName('{fileName}')");
                    });
                }
                else
                {
                    await memoryStream.WriteAsync(buffer.AsMemory(0, bytesReceived));
                    if (completeSize != memoryStream.ToArray().Length) continue;
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await _webView.EvaluateJavaScriptAsync($"window.setProgress('{-1}')");
                        await _webView.EvaluateJavaScriptAsync("window.setCurrentFileName('')");
                    });
                    var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), fileName);
                    await File.WriteAllBytesAsync(
                        path,
                        memoryStream.ToArray());
                    completeSize = 0;
                    fileName = string.Empty;
                    memoryStream.Close();
                    memoryStream = new MemoryStream();
                    Array.Clear(buffer, 0, buffer.Length);
                }
            }
        }
    }

    private static bool IsString(byte[] source)
    {
        try
        {
            new UTF8Encoding(false, true).GetCharCount(source);
            return true;
        }
        catch
        {
            return false;
        }
    }
}