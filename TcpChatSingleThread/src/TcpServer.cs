using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace TcpChatSingleThread.src
{
    internal class TcpServer
    {
        private readonly Socket? _socket;
        private readonly int _port;
        private readonly IPAddress? _address;

        private readonly Dictionary<Socket, NetworkStream> _clients = [];

        private bool isRunning;

        public bool IsRunning { get { return isRunning; } }

        public TcpServer(IPAddress? address, int port)
        {
            _port = port;
            _address = address;
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            ArgumentNullException.ThrowIfNull(address);
            ArgumentNullException.ThrowIfNull(_socket);

        }

        public void Start()
        {
            IPEndPoint enpoint = new(_address!, _port);
            try
            {
                _socket!.Bind(enpoint);
                _socket.Listen();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occured when starting server: {ex.Message}");
            }
            finally
            {
                isRunning = true;
                Console.WriteLine($"Server listenning on {_socket!.LocalEndPoint}...");
            }

        }
        public void Accept()
        {
            if (_socket!.Poll(0, SelectMode.SelectRead))
            {
                var client = _socket!.Accept();
                _clients.Add(client, new NetworkStream(client));
                Console.WriteLine($"{client.RemoteEndPoint} connected.");

            }
        }

        public void HandleClients(ref byte[] buffer)
        {
            var clientsToRemove = new List<Socket>();

            var currentClients = _clients.ToList();

            foreach (var client in currentClients)
            {
                var clientSocket = client.Key;
                var clientStream = client.Value;

                if (clientSocket.Poll(0, SelectMode.SelectRead))
                {
                    int bytesRecieved = 0;
                    try
                    {
                        bytesRecieved = clientStream.Read(buffer);
                    }
                    catch (Exception ex)
                    {
  
                        Console.WriteLine($"Error reading from {clientSocket.RemoteEndPoint}: {ex.Message}");
                        clientsToRemove.Add(clientSocket);
                        continue;
                    }

                    if (bytesRecieved == 0)
                    {
                        Console.WriteLine($"Client {clientSocket.RemoteEndPoint} disconnected.");
                        clientsToRemove.Add(clientSocket);
                        continue;
                    }

                    string response = Encoding.UTF8.GetString(buffer, 0, bytesRecieved);
                    Console.WriteLine($"Received from {clientSocket.RemoteEndPoint}: {response}");

                    var failedWriteClients = new List<Socket>();

                    if(clientSocket.Poll(0, SelectMode.SelectWrite))
                        failedWriteClients = BroadCastMessage(clientSocket, buffer, bytesRecieved);
                        clientsToRemove.AddRange(failedWriteClients);

                    RemoveDisconnectedClients(clientsToRemove);

                    Array.Clear(buffer, 0, buffer.Length);
                }
            }

        }

        public void RemoveDisconnectedClients(List<Socket> clientsToRemove)
        {
            foreach (var socketToRemove in clientsToRemove.Distinct())
            {
                if (_clients.TryGetValue(socketToRemove, out NetworkStream? streamToClose))
                {
                    Console.WriteLine($"Removing client {socketToRemove.RemoteEndPoint}...");
                    streamToClose.Close();
                    socketToRemove.Close();
                    _clients.Remove(socketToRemove);
                }
            }

        }

        public List<Socket> BroadCastMessage(Socket? exclude, byte[] buffer, int bytesToSent)
        {

            var failedClientWrites = new List<Socket>();

            foreach (var otherClient in _clients)
            {
                if (otherClient.Key != exclude)
                {
                    try
                    {
                        otherClient.Value.Write(buffer, 0, bytesToSent);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error broadcasting to {otherClient.Key.RemoteEndPoint}: {ex.Message}");
                        failedClientWrites.Add(otherClient.Key);
                    }
                }
            }
            return failedClientWrites;
        }

        public void Stop()
        {
            isRunning = false;
            foreach (var client in _clients.Values)
            {
                client.Dispose();
            }
            _clients.Clear();
            _socket?.Close();
        }
    }
}
