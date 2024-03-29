﻿using System.Net;
using System.Net.Sockets;
using System.Text;
using CommunityToolkit.Maui.Storage;
using Newtonsoft.Json;

namespace CrossDrop;

public partial class MainPage
{
    private Connection _connection;
    private byte[] _currentFile;
    private string _currentFileName;
    private WebView _webView;
    private CancellationTokenSource _autoConnectCancellationTokenSource;

    public MainPage()
    {
        InitializeComponent();
        _webView = new WebView();
       
        ContentPage.Content = _webView;
        _webView.Source = "http://192.168.108.212:5173/";
        _webView.Reload();
        _webView.Navigating += async (_, args) =>
        {
            if (args.Url.Contains("action."))
            {
                args.Cancel = true;
                var command = args.Url.Replace("action.", "");
                var parts = command.Split(":");
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
                            _autoConnectCancellationTokenSource = new CancellationTokenSource();
                            var ct = _autoConnectCancellationTokenSource.Token;
                            _connection = await Connection.FindConnectionAsync(ct);
                            if (_connection == null) return;
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

                        if (identifier == "customConnect")
                        {
                            var ip = parts[3];
                            if (_autoConnectCancellationTokenSource.IsCancellationRequested)
                            {
                                _autoConnectCancellationTokenSource.Cancel();
                            }

                            _connection = await Connection.Initialize(ip, 11000);
                            if (_connection == null) return;
                            var ipAddress = _connection.GetIpAddress()!.ToString();
                            await _webView.EvaluateJavaScriptAsync(
                                $"actionHandler.resultEvent('command:searchDevice', '{JsonConvert.SerializeObject(new { ipAddress })}')");
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
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await _webView.EvaluateJavaScriptAsync($"window.setCurrentFileName('{fileName}')");
                    });
                }
                else
                {
                    await memoryStream.WriteAsync(buffer.AsMemory(0, bytesReceived));
                    var progress = (memoryStream.ToArray().Length / completeSize ) * 100;
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await _webView.EvaluateJavaScriptAsync($"window.setProgress('{progress}')");
                    });
                    if (completeSize != memoryStream.ToArray().Length) continue;
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        await _webView.EvaluateJavaScriptAsync("window.setProgress('-1')");
                        await _webView.EvaluateJavaScriptAsync("window.setCurrentFileName('')");
                    });
                    var mem = new MemoryStream(memoryStream.ToArray());
                    var endName = fileName;
                    await MainThread.InvokeOnMainThreadAsync(async () =>
                    {
                        try
                        {
                            await FileSaver.Default.SaveAsync(endName, mem, CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            await DisplayAlert("Error", $"Failed to save file, {ex.Message}", "Cancel.");
                        }
                    });
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