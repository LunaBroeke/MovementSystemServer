using System.Net;
using System.Net.Sockets;

namespace MovementSystemServer
{
    public class Program
    {
        TcpListener server;
        public static void Main(string[] args) => new Program().StartServer();

        public void StartServer()
        {
            try
            {
                IPAddress address = IPAddress.Any;
                int port = 37484;

                server = new TcpListener(address, port);

                server.Start();

                Byte[] bytes = new byte[256];
                string data = null;

                while (true)
                {
                    Logger.Log("Waiting for connection..");

                    using TcpClient client = server.AcceptTcpClient();
                    Logger.Log("Connected!");

                    data = null;

                    NetworkStream stream = client.GetStream();

                    int i;

                    while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                    {
                        data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                        Logger.Log($"Received: {data}");

                        data = data.ToUpper();

                        byte[] msg = System.Text.Encoding.ASCII.GetBytes(data);

                        stream.Write(msg, 0, msg.Length);
                        Logger.Log($"sent {data}");
                    }
                }
            }
            catch(Exception e) { Logger.LogError(e.ToString()); }
            finally { server.Stop(); }
            Logger.Log("Hit any key to continue");
            Console.Read();
        }
    }
}