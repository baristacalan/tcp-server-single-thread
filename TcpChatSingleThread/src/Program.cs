using System.Net;
using System.Threading;

namespace TcpChatSingleThread.src
{
    internal class Program
    {
        static void Main(string[] args)
        {

            TcpServer server = new(IPAddress.Any, 4040);

            server.Start();

            byte[] buffer = new byte[4096];

            while (server.IsRunning)
            {
                server.Accept();
                //server.Read(buffer);
                //server.Write(buffer);
                server.HandleClients(ref buffer);

            }

            server.Stop();
        }
    }
}
