using LksBrothers.Core.Models;
using LksBrothers.Core.Primitives;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace LksBrothers.Networking.P2P;

/// <summary>
/// Manages P2P networking, peer discovery, and message propagation
/// </summary>
public class NetworkManager : INetworkManager
{
    private readonly ILogger<NetworkManager> _logger;
    private readonly NetworkOptions _options;
    private readonly IPeerDiscovery _peerDiscovery;
    private readonly IMessageHandler _messageHandler;
    private readonly IDoSProtection _dosProtection;
    
    private readonly ConcurrentDictionary<string, Peer> _connectedPeers = new();
    private readonly Channel<NetworkMessage> _incomingMessages;
    private readonly Channel<NetworkMessage> _outgoingMessages;
    
    private TcpListener? _listener;
    private bool _isRunning;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    
    public event EventHandler<PeerConnectedEventArgs>? PeerConnected;
    public event EventHandler<PeerDisconnectedEventArgs>? PeerDisconnected;
    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
    
    public NetworkManager(
        ILogger<NetworkManager> logger,
        IOptions<NetworkOptions> options,
        IPeerDiscovery peerDiscovery,
        IMessageHandler messageHandler,
        IDoSProtection dosProtection)
    {
        _logger = logger;
        _options = options.Value;
        _peerDiscovery = peerDiscovery;
        _messageHandler = messageHandler;
        _dosProtection = dosProtection;
        
        _incomingMessages = Channel.CreateUnbounded<NetworkMessage>();
        _outgoingMessages = Channel.CreateUnbounded<NetworkMessage>();
    }
    
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting network manager on port {Port}", _options.ListenPort);
        
        _isRunning = true;
        
        // Start TCP listener
        _listener = new TcpListener(IPAddress.Any, _options.ListenPort);
        _listener.Start();
        
        // Start background tasks
        var tasks = new[]
        {
            AcceptConnectionsAsync(_cancellationTokenSource.Token),
            ProcessIncomingMessagesAsync(_cancellationTokenSource.Token),
            ProcessOutgoingMessagesAsync(_cancellationTokenSource.Token),
            PeerMaintenanceAsync(_cancellationTokenSource.Token),
            StartPeerDiscoveryAsync(_cancellationTokenSource.Token)
        };
        
        _logger.LogInformation("Network manager started successfully");
        
        await Task.WhenAll(tasks);
    }
    
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping network manager");
        
        _isRunning = false;
        _cancellationTokenSource.Cancel();
        
        // Close all peer connections
        var disconnectTasks = _connectedPeers.Values.Select(peer => DisconnectPeerAsync(peer.Id));
        await Task.WhenAll(disconnectTasks);
        
        // Stop listener
        _listener?.Stop();
        
        _logger.LogInformation("Network manager stopped");
    }
    
    public async Task<bool> ConnectToPeerAsync(string address, int port)
    {
        try
        {
            var peerId = $"{address}:{port}";
            
            if (_connectedPeers.ContainsKey(peerId))
            {
                _logger.LogDebug("Already connected to peer {PeerId}", peerId);
                return true;
            }
            
            _logger.LogInformation("Connecting to peer {Address}:{Port}", address, port);
            
            var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(address, port);
            
            var peer = new Peer
            {
                Id = peerId,
                Address = address,
                Port = port,
                Client = tcpClient,
                Stream = tcpClient.GetStream(),
                ConnectedAt = DateTime.UtcNow,
                IsOutbound = true
            };
            
            _connectedPeers[peerId] = peer;
            
            // Start handling this peer
            _ = Task.Run(() => HandlePeerAsync(peer, _cancellationTokenSource.Token));
            
            PeerConnected?.Invoke(this, new PeerConnectedEventArgs(peer));
            
            _logger.LogInformation("Successfully connected to peer {PeerId}", peerId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to peer {Address}:{Port}", address, port);
            return false;
        }
    }
    
    public async Task<bool> DisconnectPeerAsync(string peerId)
    {
        if (!_connectedPeers.TryRemove(peerId, out var peer))
            return false;
        
        try
        {
            peer.Client?.Close();
            peer.Stream?.Close();
            
            PeerDisconnected?.Invoke(this, new PeerDisconnectedEventArgs(peer));
            
            _logger.LogInformation("Disconnected from peer {PeerId}", peerId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disconnecting from peer {PeerId}", peerId);
            return false;
        }
    }
    
    public async Task BroadcastMessageAsync(NetworkMessage message)
    {
        var tasks = _connectedPeers.Values
            .Where(peer => peer.IsConnected)
            .Select(peer => SendMessageToPeerAsync(peer, message));
        
        await Task.WhenAll(tasks);
        
        _logger.LogDebug("Broadcasted message type {MessageType} to {PeerCount} peers",
            message.Type, _connectedPeers.Count);
    }
    
    public async Task SendMessageToPeerAsync(string peerId, NetworkMessage message)
    {
        if (_connectedPeers.TryGetValue(peerId, out var peer))
        {
            await SendMessageToPeerAsync(peer, message);
        }
    }
    
    public List<PeerInfo> GetConnectedPeers()
    {
        return _connectedPeers.Values
            .Select(peer => new PeerInfo
            {
                Id = peer.Id,
                Address = peer.Address,
                Port = peer.Port,
                ConnectedAt = peer.ConnectedAt,
                IsOutbound = peer.IsOutbound,
                BytesSent = peer.BytesSent,
                BytesReceived = peer.BytesReceived,
                LastSeen = peer.LastSeen
            })
            .ToList();
    }
    
    private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
    {
        while (_isRunning && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                var tcpClient = await _listener!.AcceptTcpClientAsync();
                var endpoint = tcpClient.Client.RemoteEndPoint as IPEndPoint;
                
                if (endpoint == null)
                {
                    tcpClient.Close();
                    continue;
                }
                
                // Check DoS protection
                if (!await _dosProtection.AllowConnectionAsync(endpoint.Address.ToString()))
                {
                    _logger.LogWarning("Connection rejected from {Address} due to DoS protection", endpoint.Address);
                    tcpClient.Close();
                    continue;
                }
                
                var peerId = $"{endpoint.Address}:{endpoint.Port}";
                
                var peer = new Peer
                {
                    Id = peerId,
                    Address = endpoint.Address.ToString(),
                    Port = endpoint.Port,
                    Client = tcpClient,
                    Stream = tcpClient.GetStream(),
                    ConnectedAt = DateTime.UtcNow,
                    IsOutbound = false
                };
                
                _connectedPeers[peerId] = peer;
                
                // Start handling this peer
                _ = Task.Run(() => HandlePeerAsync(peer, cancellationToken));
                
                PeerConnected?.Invoke(this, new PeerConnectedEventArgs(peer));
                
                _logger.LogInformation("Accepted connection from peer {PeerId}", peerId);
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error accepting connection");
                await Task.Delay(1000, cancellationToken);
            }
        }
    }
    
    private async Task HandlePeerAsync(Peer peer, CancellationToken cancellationToken)
    {
        try
        {
            // Send handshake
            await SendHandshakeAsync(peer);
            
            // Read messages from peer
            var buffer = new byte[4096];
            
            while (peer.IsConnected && !cancellationToken.IsCancellationRequested)
            {
                var bytesRead = await peer.Stream!.ReadAsync(buffer, cancellationToken);
                
                if (bytesRead == 0)
                {
                    // Peer disconnected
                    break;
                }
                
                peer.BytesReceived += (ulong)bytesRead;
                peer.LastSeen = DateTime.UtcNow;
                
                // Parse and handle messages
                await ProcessReceivedDataAsync(peer, buffer.AsSpan(0, bytesRead));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling peer {PeerId}", peer.Id);
        }
        finally
        {
            await DisconnectPeerAsync(peer.Id);
        }
    }
    
    private async Task SendHandshakeAsync(Peer peer)
    {
        var handshake = new HandshakeMessage
        {
            Version = _options.ProtocolVersion,
            NodeId = _options.NodeId,
            ChainId = _options.ChainId,
            Timestamp = DateTime.UtcNow,
            ListenPort = _options.ListenPort
        };
        
        var message = new NetworkMessage
        {
            Type = MessageType.Handshake,
            Data = MessagePack.MessagePackSerializer.Serialize(handshake)
        };
        
        await SendMessageToPeerAsync(peer, message);
    }
    
    private async Task SendMessageToPeerAsync(Peer peer, NetworkMessage message)
    {
        try
        {
            if (!peer.IsConnected)
                return;
            
            var serialized = MessagePack.MessagePackSerializer.Serialize(message);
            var lengthBytes = BitConverter.GetBytes(serialized.Length);
            
            await peer.Stream!.WriteAsync(lengthBytes);
            await peer.Stream.WriteAsync(serialized);
            await peer.Stream.FlushAsync();
            
            peer.BytesSent += (ulong)(lengthBytes.Length + serialized.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message to peer {PeerId}", peer.Id);
            await DisconnectPeerAsync(peer.Id);
        }
    }
    
    private async Task ProcessReceivedDataAsync(Peer peer, ReadOnlySpan<byte> data)
    {
        try
        {
            // Simple message parsing - in production would need proper framing
            var message = MessagePack.MessagePackSerializer.Deserialize<NetworkMessage>(data.ToArray());
            
            // Validate message
            if (!await _messageHandler.ValidateMessageAsync(message, peer))
            {
                _logger.LogWarning("Invalid message received from peer {PeerId}", peer.Id);
                return;
            }
            
            // Process message
            await _incomingMessages.Writer.WriteAsync(message);
            
            MessageReceived?.Invoke(this, new MessageReceivedEventArgs(message, peer));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing received data from peer {PeerId}", peer.Id);
        }
    }
    
    private async Task ProcessIncomingMessagesAsync(CancellationToken cancellationToken)
    {
        await foreach (var message in _incomingMessages.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                await _messageHandler.HandleMessageAsync(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing incoming message");
            }
        }
    }
    
    private async Task ProcessOutgoingMessagesAsync(CancellationToken cancellationToken)
    {
        await foreach (var message in _outgoingMessages.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                await BroadcastMessageAsync(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outgoing message");
            }
        }
    }
    
    private async Task PeerMaintenanceAsync(CancellationToken cancellationToken)
    {
        while (_isRunning && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken);
                
                // Remove stale peers
                var stalePeers = _connectedPeers.Values
                    .Where(peer => DateTime.UtcNow - peer.LastSeen > TimeSpan.FromMinutes(5))
                    .ToList();
                
                foreach (var peer in stalePeers)
                {
                    await DisconnectPeerAsync(peer.Id);
                }
                
                // Maintain minimum peer count
                if (_connectedPeers.Count < _options.MinPeers)
                {
                    await _peerDiscovery.DiscoverPeersAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in peer maintenance");
            }
        }
    }
    
    private async Task StartPeerDiscoveryAsync(CancellationToken cancellationToken)
    {
        await _peerDiscovery.StartAsync(cancellationToken);
    }
}

// Supporting classes and interfaces
public class Peer
{
    public string Id { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int Port { get; set; }
    public TcpClient? Client { get; set; }
    public NetworkStream? Stream { get; set; }
    public DateTime ConnectedAt { get; set; }
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public bool IsOutbound { get; set; }
    public ulong BytesSent { get; set; }
    public ulong BytesReceived { get; set; }
    public bool IsConnected => Client?.Connected == true;
}

public class PeerInfo
{
    public string Id { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public int Port { get; set; }
    public DateTime ConnectedAt { get; set; }
    public DateTime LastSeen { get; set; }
    public bool IsOutbound { get; set; }
    public ulong BytesSent { get; set; }
    public ulong BytesReceived { get; set; }
}

public class NetworkMessage
{
    public MessageType Type { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string SenderId { get; set; } = string.Empty;
}

public enum MessageType
{
    Handshake,
    BlockProposal,
    BlockVote,
    Transaction,
    PeerDiscovery,
    StateSync,
    Ping,
    Pong
}

public class HandshakeMessage
{
    public int Version { get; set; }
    public string NodeId { get; set; } = string.Empty;
    public string ChainId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public int ListenPort { get; set; }
}

public class NetworkOptions
{
    public int ListenPort { get; set; } = 30303;
    public int ProtocolVersion { get; set; } = 1;
    public string NodeId { get; set; } = Guid.NewGuid().ToString();
    public string ChainId { get; set; } = "lks-mainnet";
    public int MinPeers { get; set; } = 5;
    public int MaxPeers { get; set; } = 50;
    public List<string> BootstrapNodes { get; set; } = new();
}

// Event args
public class PeerConnectedEventArgs : EventArgs
{
    public Peer Peer { get; }
    public PeerConnectedEventArgs(Peer peer) => Peer = peer;
}

public class PeerDisconnectedEventArgs : EventArgs
{
    public Peer Peer { get; }
    public PeerDisconnectedEventArgs(Peer peer) => Peer = peer;
}

public class MessageReceivedEventArgs : EventArgs
{
    public NetworkMessage Message { get; }
    public Peer Peer { get; }
    public MessageReceivedEventArgs(NetworkMessage message, Peer peer)
    {
        Message = message;
        Peer = peer;
    }
}

// Interfaces
public interface INetworkManager
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task<bool> ConnectToPeerAsync(string address, int port);
    Task<bool> DisconnectPeerAsync(string peerId);
    Task BroadcastMessageAsync(NetworkMessage message);
    Task SendMessageToPeerAsync(string peerId, NetworkMessage message);
    List<PeerInfo> GetConnectedPeers();
}

public interface IPeerDiscovery
{
    Task StartAsync(CancellationToken cancellationToken);
    Task DiscoverPeersAsync();
}

public interface IMessageHandler
{
    Task<bool> ValidateMessageAsync(NetworkMessage message, Peer peer);
    Task HandleMessageAsync(NetworkMessage message);
}

public interface IDoSProtection
{
    Task<bool> AllowConnectionAsync(string address);
    Task RecordViolationAsync(string address);
}
