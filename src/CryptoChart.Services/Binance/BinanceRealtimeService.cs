using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CryptoChart.Core.Enums;
using CryptoChart.Core.Interfaces;
using CryptoChart.Core.Models;

namespace CryptoChart.Services.Binance;

/// <summary>
/// Binance WebSocket client for real-time candle updates.
/// </summary>
public class BinanceRealtimeService : IRealtimeMarketService
{
    private const string WebSocketBaseUrl = "wss://stream.binance.com:9443/ws";
    
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private readonly HashSet<string> _subscriptions = new();
    private readonly object _lock = new();
    private bool _disposed;

    public event EventHandler<CandleUpdateEventArgs>? CandleUpdated;
    public event EventHandler<ConnectionStatusEventArgs>? ConnectionStatusChanged;

    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    public async Task SubscribeAsync(string symbol, TimeFrame timeFrame, CancellationToken cancellationToken = default)
    {
        var streamName = $"{symbol.ToLowerInvariant()}@kline_{timeFrame.ToBinanceInterval()}";

        lock (_lock)
        {
            if (_subscriptions.Contains(streamName))
                return;
            _subscriptions.Add(streamName);
        }

        await EnsureConnectedAsync(cancellationToken);
        await SendSubscribeMessageAsync(streamName, true, cancellationToken);
    }

    public async Task UnsubscribeAsync(string symbol, TimeFrame timeFrame, CancellationToken cancellationToken = default)
    {
        var streamName = $"{symbol.ToLowerInvariant()}@kline_{timeFrame.ToBinanceInterval()}";

        lock (_lock)
        {
            if (!_subscriptions.Contains(streamName))
                return;
            _subscriptions.Remove(streamName);
        }

        if (IsConnected)
        {
            await SendSubscribeMessageAsync(streamName, false, cancellationToken);
        }
    }

    public async Task UnsubscribeAllAsync(CancellationToken cancellationToken = default)
    {
        string[] streams;
        lock (_lock)
        {
            streams = _subscriptions.ToArray();
            _subscriptions.Clear();
        }

        if (IsConnected)
        {
            foreach (var stream in streams)
            {
                await SendSubscribeMessageAsync(stream, false, cancellationToken);
            }
        }
    }

    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        if (IsConnected)
            return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        _webSocket?.Dispose();
        _webSocket = new ClientWebSocket();

        try
        {
            await _webSocket.ConnectAsync(new Uri(WebSocketBaseUrl), cancellationToken);
            
            ConnectionStatusChanged?.Invoke(this, new ConnectionStatusEventArgs
            {
                IsConnected = true,
                Message = "Connected to Binance WebSocket"
            });

            // Start receive loop
            _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);
        }
        catch (Exception ex)
        {
            ConnectionStatusChanged?.Invoke(this, new ConnectionStatusEventArgs
            {
                IsConnected = false,
                Message = "Failed to connect",
                Error = ex
            });
            throw;
        }
    }

    private async Task SendSubscribeMessageAsync(string streamName, bool subscribe, CancellationToken cancellationToken)
    {
        if (_webSocket?.State != WebSocketState.Open)
            return;

        var message = new
        {
            method = subscribe ? "SUBSCRIBE" : "UNSUBSCRIBE",
            @params = new[] { streamName },
            id = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        var json = JsonSerializer.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        
        await _webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            cancellationToken);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        var messageBuffer = new StringBuilder();

        while (!cancellationToken.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
        {
            try
            {
                var result = await _webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await HandleDisconnectAsync();
                    break;
                }

                messageBuffer.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                if (result.EndOfMessage)
                {
                    var message = messageBuffer.ToString();
                    messageBuffer.Clear();
                    ProcessMessage(message);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                ConnectionStatusChanged?.Invoke(this, new ConnectionStatusEventArgs
                {
                    IsConnected = false,
                    Message = "WebSocket error",
                    Error = ex
                });
                
                await HandleDisconnectAsync();
                break;
            }
        }
    }

    private void ProcessMessage(string message)
    {
        try
        {
            // Check if it's a kline event (not a subscription response)
            if (!message.Contains("\"e\":\"kline\""))
                return;

            var klineEvent = JsonSerializer.Deserialize<BinanceKlineStreamEvent>(message);
            if (klineEvent == null)
                return;

            var candle = MapToCandle(klineEvent.Kline);
            var timeFrame = ParseTimeFrame(klineEvent.Kline.Interval);

            CandleUpdated?.Invoke(this, new CandleUpdateEventArgs
            {
                Symbol = klineEvent.Symbol,
                TimeFrame = timeFrame,
                Candle = candle,
                IsClosed = klineEvent.Kline.IsClosed
            });
        }
        catch (JsonException)
        {
            // Ignore malformed messages
        }
    }

    private static Candle MapToCandle(BinanceKlineData kline)
    {
        return new Candle
        {
            OpenTime = DateTimeOffset.FromUnixTimeMilliseconds(kline.OpenTime).UtcDateTime,
            CloseTime = DateTimeOffset.FromUnixTimeMilliseconds(kline.CloseTime).UtcDateTime,
            Open = decimal.Parse(kline.Open),
            High = decimal.Parse(kline.High),
            Low = decimal.Parse(kline.Low),
            Close = decimal.Parse(kline.Close),
            Volume = decimal.Parse(kline.Volume),
            QuoteVolume = decimal.Parse(kline.QuoteVolume),
            TradeCount = kline.TradeCount,
            TimeFrame = ParseTimeFrame(kline.Interval)
        };
    }

    private static TimeFrame ParseTimeFrame(string interval) => interval switch
    {
        "1h" => TimeFrame.Hourly,
        "1d" => TimeFrame.Daily,
        _ => throw new ArgumentException($"Unknown interval: {interval}")
    };

    private async Task HandleDisconnectAsync()
    {
        ConnectionStatusChanged?.Invoke(this, new ConnectionStatusEventArgs
        {
            IsConnected = false,
            Message = "Disconnected from Binance WebSocket"
        });

        // Attempt reconnection after delay
        await Task.Delay(5000);
        
        if (!_disposed && _subscriptions.Count > 0)
        {
            try
            {
                await EnsureConnectedAsync(CancellationToken.None);
                
                // Resubscribe to all streams
                foreach (var stream in _subscriptions.ToArray())
                {
                    await SendSubscribeMessageAsync(stream, true, CancellationToken.None);
                }
            }
            catch
            {
                // Will retry on next subscription attempt
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        _cts?.Cancel();

        if (_webSocket?.State == WebSocketState.Open)
        {
            try
            {
                await _webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Disposing",
                    CancellationToken.None);
            }
            catch
            {
                // Ignore close errors during disposal
            }
        }

        _webSocket?.Dispose();
        _cts?.Dispose();
        
        if (_receiveTask != null)
        {
            try
            {
                await _receiveTask;
            }
            catch
            {
                // Ignore task errors during disposal
            }
        }
    }
}
