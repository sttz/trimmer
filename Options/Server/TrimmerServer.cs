//
// Trimmer Framework for Unity
// https://sttz.ch/trimmer
//

using UnityEngine;
using System;
using System.Net.Sockets;
using System.Net;
using System.Collections.Generic;
using System.Collections;

namespace sttz.Trimmer.Options
{

#if TRIMMER_SERVER || TRIMMER_CLIENT || UNITY_EDITOR

public static class Common
{
    /// <summary>
    /// Decode the data received from the network.
    /// </summary>
    public static string Decode(byte[] bytes, int position = 0, int length = -1)
    {
        try {
            if (length < 0) length = bytes.Length;
            return System.Text.Encoding.UTF8.GetString(bytes, position, length);
        } catch (Exception e) {
            Debug.LogError("Failed to decode string from client: " + e);
            return null;
        }
    }

    /// <summary>
    /// Encode a string to be sent over the network.
    /// </summary>
    public static byte[] Encode(string input)
    {
        try {
            return System.Text.Encoding.UTF8.GetBytes(input);
        } catch (Exception e) {
            Debug.LogError("Failed to encode string for client: " + e);
            return null;
        }
    }
}

#endif

#if TRIMMER_SERVER || UNITY_EDITOR

/// <summary>
/// Server is a simple TCP server that allows to access Trimmer
/// configuration over the network.
/// </summary>
/// <remarks>
/// The server essentially provides a network API to the <see cref="RuntimeProfile"/>.
/// You can use <see cref="TrimmerClient"/> to connect to the server. The server
/// currently only allows a single client to be connected at a time.
/// 
/// The protocol is very simple and can also be used with a command line
/// tool like `nc` or `socat`.
/// 
/// You can use socat to discover servers on the local network (replace 
/// `TRIM` with <see cref="ClientHello"/> and `21076` with <see cref="ServerPort"/>):
/// 
/// ```sh
/// echo "TRIM" | socat -d -d - UDP-DATAGRAM:255.255.255.255:21076,broadcast
/// ```
/// 
/// Connect to a server (replacing `127.0.0.1` with the server IP and
/// again `21076` with the server port):
/// 
/// ```sh
/// socat readline TCP:127.0.0.1:21076
/// ```
/// 
/// Then you can type commands to send to the server. First send the 
/// client hello and then use the available commands:
/// * **GET** OPTION_PATH: Get the value of an Option using its path
///   in ini-format.
/// * **SET** OPTION_PATH = VALUE: Set the value of an Option using
///   its path.
/// * **PING**: Simple ping, server responds with PONG.
/// 
/// The server allows you to add custom commands using <see cref="AddCommand"/>.
/// 
/// If you use this class directly, make sure you call <see cref="Update"/>
/// regularly for the server to process commands.
/// 
/// > [!WARNING]
/// > The communication is unencrypted and doesn't use authentication.
/// > The use of this server is only intended for development.
/// </remarks>
public class TrimmerServer
{
    // ------ Configuration ------

    /// <summary>
    /// Sets wether the server replies to broadcast inquiries, which
    /// lets clients detect available servers.
    /// </summary>
    public bool IsDiscoverable {
        get {
            return _isDiscoverable;
        }
        set {
            if (IsRunning) throw new InvalidOperationException();
            _isDiscoverable = value;
        }
    }
    bool _isDiscoverable = true;

    /// <summary>
    /// The address the server will listen on.
    /// Defaults to <see cref="IPAddress.Any"/>.
    /// </summary>
    public IPAddress ServerAddress {
        get {
            return _serverAddress;
        }
        set {
            if (IsRunning) throw new InvalidOperationException();
            _serverAddress = value;
        }
    }
    IPAddress _serverAddress = IPAddress.Any;

    /// <summary>
    /// The port the server is listening on.
    /// </summary>
    public int ServerPort {
        get {
            return _serverPort;
        }
        set {
            if (IsRunning) throw new InvalidOperationException();
            _serverPort = value;
        }
    }
    int _serverPort = 21076;

    /// <summary>
    /// Expected string to receive from clients for discovery
    /// and connections.
    /// </summary>
    public string ClientHello {
        get {
            return _clientHello;
        }
        set {
            if (IsRunning) throw new InvalidOperationException();
            _clientHello = value;
        }
    }
    string _clientHello = "TRIM";

    /// <summary>
    /// Hello sent back to the client.
    /// </summary>
    /// <remarks>
    /// The format string can contain following placeholders:
    /// * {0}: Application.productName
    /// * {1}: Application.version
    /// * {2}: Application.unityVersion
    /// </remarks>
    public string ServerHelloFormat {
        get {
            return _serverHelloFormat;
        }
        set {
            if (IsRunning) throw new InvalidOperationException();
            _serverHelloFormat = value;
        }
    }
    string _serverHelloFormat = "TRAM {0} {1} {2}";

    // ------ API ------

    /// <summary>
    /// Wether the server is currently running.
    /// </summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Start the server.
    /// </summary>
    /// <remarks>
    /// Does nothing is the server is already running.
    /// </remarks>
    public void Start()
    {
        if (IsRunning) return;
        IsRunning = true;

        // Done here because callbacks are called on another thread
        serverHello = string.Format(ServerHelloFormat + "\n",
            Application.productName,
            Application.version,
            Application.unityVersion
        );

        if (IsDiscoverable) {
            BeginDiscoverable();
        }

        StartServer();
    }

    /// <summary>
    /// Stop the server.
    /// </summary>
    /// <remarks>
    /// Does nothing is the server is not running.
    /// </remarks>
    public void Stop()
    {
        if (!IsRunning) return;
        IsRunning = false;

        EndDiscoverable();
        StopServer();
    }

    /// <summary>
    /// Call this method in a regular interval to process client messages.
    /// </summary>
    public void Update()
    {
        ProcessCommands();
    }

    /// <summary>
    /// Handler for custom commands (see <see cref="AddCommand"/>).
    /// </summary>
    /// <param name="command">The name of the command received</param>
    /// <param name="input">Command arguments received</param>
    /// <returns>The reply send to the client</returns>
    public delegate string CommandHandler(string command, string input);

    /// <summary>
    /// Add a custom command to the server.
    /// </summary>
    /// <remarks>
    /// As a convention, commands return `ERROR Message here` in case
    /// an error is encountered.
    /// </remarks>
    /// <param name="name">The name of the command</param>
    /// <param name="handler">Handler to respond to an incoming command</param>
    public void AddCommand(string name, CommandHandler handler)
    {
        if (handler == null) {
            commands.Remove(name);
        } else {
            commands[name] = handler;
        }
    }

    // ------ General ------

    public TrimmerServer()
    {
        LoadCommands();
    }

    // ------ Discovery ------

    UdpClient announcer;
    string serverHello;

    void BeginDiscoverable()
    {
        EndDiscoverable();

        var endPoint = new IPEndPoint(ServerAddress, ServerPort);
        announcer = new UdpClient(endPoint);
        announcer.BeginReceive(OnDiscoverData, null);

        Debug.Log("Trimmer server is discoverable on port " + ServerPort);
    }

    void EndDiscoverable()
    {
        if (announcer != null) {
            announcer.Close();
            announcer = null;
        }
    }

    void OnDiscoverData(IAsyncResult ar)
    {
        if (announcer == null)
            return;

        try {
            IPEndPoint endPoint = null;
            var message = Common.Decode(announcer.EndReceive(ar, ref endPoint));
            // Only respond to expected hello message
            if (message.StartsWith(ClientHello)) {
                var data = Common.Encode(serverHello);
                announcer.Send(data, data.Length, endPoint);
            }
            announcer.BeginReceive(OnDiscoverData, null);
        } catch (ObjectDisposedException) {
            // Client was closed
        } catch (Exception e) {
            Debug.LogError("Error handling discovery: " + e);
        }
    }

    // ------ Server ------

    const int MaxMessageLength = 100 * 1024;
    const byte MessageEnd = (byte)'\n';

    TcpListener server;
    TcpClient client;
    NetworkStream stream;
    byte[] readBuffer = new byte[MaxMessageLength];
    int readPosition = 0;
    bool clientSaidHello;

    Queue<string> messageQueue = new Queue<string>();

    void StartServer()
    {
        var endPoint = new IPEndPoint(ServerAddress, ServerPort);
        server = new TcpListener(endPoint);
        server.Start();
        server.BeginAcceptTcpClient(OnConnection, null);

        var localEndPoint = (IPEndPoint)server.Server.LocalEndPoint;
        Debug.Log("Trimmer server listening on " + localEndPoint);
    }

    void DisconnectClient()
    {
        lock (((ICollection)messageQueue).SyncRoot) {
            messageQueue.Clear();
        }

        if (stream != null) {
            stream.Close();
            stream = null;
        }

        if (client != null) {
            client.Close();
            client = null;
        }
    }

    void StopServer()
    {
        DisconnectClient();

        if (server != null) {
            server.Stop();
            server = null;
        }
    }

    void OnConnection(IAsyncResult ar)
    {
        if (server == null)
            return;

        try {
            var newClient = server.EndAcceptTcpClient(ar);
            if (client != null) {
                // Currently only single client connection support
                newClient.Close();
            } else {
                Debug.Log("New client connected: " + newClient.Client.RemoteEndPoint);

                client = newClient;
                clientSaidHello = false;
                
                stream = client.GetStream();
                readPosition = 0;
                stream.BeginRead(readBuffer, 0, readBuffer.Length, OnConnectionData, null);
            }
            server.BeginAcceptTcpClient(OnConnection, null);
        } catch (ObjectDisposedException) {
            // Client was closed
        } catch (Exception e) {
            if (!clientSaidHello) {
                DisconnectClient();
            }
            Debug.LogError("Server error: " + e);
        }
    }

    void OnConnectionData(IAsyncResult ar)
    {
        if (server == null || client == null || stream == null)
            return;
        
        try {
            var readLength = stream.EndRead(ar);
            if (readLength == 0) {
                Debug.Log("TrimmerServer: Stream closed");
                DisconnectClient();
                return;
            }

            var messageEnd = Array.IndexOf(readBuffer, MessageEnd, readPosition, readLength);
            if (messageEnd < 0) {
                // Wait for end of message
                readPosition += readLength;
                stream.BeginRead(readBuffer, readPosition, readBuffer.Length - readPosition, OnConnectionData, null);
                return;
            }

            // Handle message
            var message = Common.Decode(readBuffer, 0, messageEnd);
            if (!clientSaidHello) {
                if (!message.StartsWith(ClientHello)) {
                    // Client sent wrong hello
                    Debug.LogWarning("Got invalid hello from client: " + message);
                    DisconnectClient();
                    return;
                }

                Debug.Log("Client hello: " + message);
                clientSaidHello = true;
                var data = Common.Encode(serverHello);
                stream.Write(data, 0, data.Length);

            } else {
                if (message.Trim().Length == 0) {
                    Debug.Log("Client disconnected");
                    DisconnectClient();
                    return;
                }

                lock (((ICollection)messageQueue).SyncRoot) {
                    messageQueue.Enqueue(message);
                }
            }
            
            // Handle possible remainder message
            var remaining = (readPosition + readLength) - (messageEnd + 1);
            if (remaining > 0) {
                Array.Copy(readBuffer, messageEnd + 1, readBuffer, 0, remaining);
                readPosition = remaining;
            } else {
                readPosition = 0;
            }

            stream.BeginRead(readBuffer, readPosition, readBuffer.Length - readPosition, OnConnectionData, null);
        } catch (ObjectDisposedException) {
            // Client was closed
        } catch (Exception e) {
            DisconnectClient();
            Debug.LogError("Server error: " + e);
        }
    }

    void ProcessCommands()
    {
        if (stream == null)
            return;

        while (true) {
            string message;
            lock (((ICollection)messageQueue).SyncRoot) {
                if (messageQueue.Count == 0)
                    return;
                message = messageQueue.Dequeue();
            }

            message = message.Trim();
            string reply;

            string commandName, commandArgument;
            var firstSpace = message.IndexOf(' ');
            if (firstSpace <= 0) {
                commandName = message;
                commandArgument = string.Empty;
            } else {
                commandName = message.Substring(0, firstSpace);
                commandArgument = message.Substring(firstSpace + 1);
            }

            CommandHandler command;
            if (!commands.TryGetValue(commandName, out command)) {
                reply = "ERROR Unknown command";
            } else {
                reply = command(commandName, commandArgument);
            }

            var data = Common.Encode(reply + "\n");
            
            var endIndex = Array.IndexOf(data, MessageEnd);
            if (endIndex != data.Length - 1) {
                Debug.LogError($"TrimmerServer: Reply cannot contain a newlines (found at index {endIndex})");
                data = Common.Encode("ERROR Invalid reply\n");
            }
            
            stream.Write(data, 0, data.Length);
        }
    }

    // ------ Commands ------

    Dictionary<string, CommandHandler> commands
        = new Dictionary<string, CommandHandler>(StringComparer.OrdinalIgnoreCase);

    void LoadCommands()
    {
        commands["PING"] = CommandPing;
        commands["GET"] = CommandGet;
        commands["SET"] = CommandSet;
    }

    string CommandPing(string name, string input)
    {
        return "PONG";
    }

    string CommandGet(string name, string input)
    {
        var path = IniAdapter.NameToPath(input);
        if (path == null) {
            return "ERROR Invalid Option path";
        }

        var option = RuntimeProfile.Main.GetOption(path);
        if (option == null) {
            return "ERROR Option not found";
        }

        return option.Save();
    }

    string CommandSet(string name, string input)
    {
        var path = IniAdapter.NameToPath(input);
        if (path == null) {
            return "ERROR Invalid Option path";
        }

        var value = IniAdapter.GetValue(input);
        if (value == null) {
            return "ERROR Invalid Option value";
        }

        var option = RuntimeProfile.Main.GetOption(path);
        if (option == null) {
            return "ERROR Option not found";
        }

        option.Load(value);
        option.ApplyFromRoot();

        return option.Save();
    }
}

#endif

}
