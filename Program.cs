using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace MovementSystemServer
{
    public class Position
    {
        public float x { get; set; }
        public float y { get; set; }
        public float z { get; set; }
    }

    public class PlayerInfo
    {
        public string playerName { get; set; }
        public Position position { get; set; }
        public int health { get; set; }
        public int puppetID { get; set; }
        public string IPv4 { get; set; }
    }

    // Updated PlayerInfoList to match the new format
    [Serializable]
    public class PlayerInfoList
    {
        public List<PlayerInfo> players = new List<PlayerInfo>();
    }

    public class Program
    {
        private const int maxPlayers = 4;
        public TcpListener server;
        public static List<PlayerInfo> players = new List<PlayerInfo>();
        public static List<TcpClient> clients = new List<TcpClient>(); // Track connected clients
        
        public static void Main(string[] args)
        {
            Program program = new();
            ConsoleHandler ch = new() { program = program};
            Thread serverThread =new Thread(program.StartServer) { IsBackground = true };
            //serverThread.Start();

            Thread consoleThread = new Thread(ch.StartConsole) { IsBackground = true };
            consoleThread.Start();

            Thread broadcast = new Thread(program.BroadcastPlayerData) { IsBackground = true };
            broadcast.Start();
//            while (serverThread.IsAlive) { Thread.Sleep(100); }
            program.StartServer();
        }

        public void StartServer()
        {
            try
            {
                IPAddress address = IPAddress.Any;
                int port = 37484;

                server = new TcpListener(address, port);
                server.Start();
                Logger.Log($"Listening on {address}");
                Logger.Log($"Listening on {port}");
                Console.WriteLine("Server started. Waiting for connections...");

                while (true)
                {
                    TcpClient client = server.AcceptTcpClient();
                    IPEndPoint endPoint = client.Client.RemoteEndPoint as IPEndPoint;
                    Console.WriteLine($"Client {endPoint.Address}:{endPoint.Port} connected!");

                    lock (clients)
                    {
                        if (clients.Count < maxPlayers) { clients.Add(client); }
                        else { Logger.LogWarning($"Client {endPoint.Address}:{endPoint.Port} exceeded max players"); MaxPlayer(client); continue; }
                    }

                    Thread clientThread = new Thread(() => HandleClient(client))
                    {
                        IsBackground = true
                    };
                    clientThread.Start();
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Error: {e.Message}");
            }
            finally
            {
                server.Stop();
                Logger.Log("Server stopped.");
            }
        }

        private void HandleClient(TcpClient client)
        {
            int puppetID = AssingPuppetID();
            try
            {
                NetworkStream stream = client.GetStream();
                byte[] bytes = new byte[1024];
                string data = null;

                byte[] puppetIDData = Encoding.ASCII.GetBytes($"{puppetID}\n");
                stream.Write(puppetIDData, 0, puppetIDData.Length);
                Console.WriteLine($"Assigned Puppet ID: {puppetID}");

                while (client.Connected)
                {
                    stream.ReadTimeout = 5000;
                    int bytesRead = stream.Read(bytes, 0, bytes.Length);
                    //Logger.Log(bytesRead.ToString());
                    if (bytesRead == 0) break;

                    data = Encoding.ASCII.GetString(bytes, 0, bytesRead);
                    string[] datas = data.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    try
                    {
                        foreach (string d in datas)
                        {
                            PlayerInfo playerInfo = JsonConvert.DeserializeObject<PlayerInfo>(d);
                            playerInfo.puppetID = puppetID; // Assign puppetID

                            lock (players)
                            {
                                playerInfo.playerName = NameCheck(playerInfo);
                                IPEndPoint ip = client.Client.RemoteEndPoint as IPEndPoint;
                                playerInfo.IPv4 = ip.Address.ToString();
                                // Find existing player with the same puppetID and update their info, or add new player
                                PlayerInfo existingPlayer = FindPlayerByID(players, puppetID);
                                if (existingPlayer != null)
                                {
                                    // Update existing player's data
                                    existingPlayer.playerName = playerInfo.playerName;
                                    existingPlayer.position = playerInfo.position;
                                    existingPlayer.health = playerInfo.health;
                                }
                                else
                                {
                                    // Add new player if not found
                                    players.Add(playerInfo);
                                }
                            }
                        }
                    }
                    catch (JsonReaderException e)
                    {
                        Logger.LogError($"Packet error: {e.Message}");
                        continue;
                    }

                    //BroadcastPlayerData();
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Client connection error: {e}");
                if (e.Message.Contains("A connection attempt failed because the connected party did not properly respond after a period of time"))
                {
                    Logger.LogError("Read failure");
                    byte[] data = Encoding.ASCII.GetBytes("Read failure");
                    NetworkStream stream = client.GetStream();
                    stream.WriteTimeout = 500;
                    stream.Write(data, 0, data.Length);
                }
                Disconnect(client, puppetID);
            }
        }

        string NameCheck(PlayerInfo playerInfo)
        {
            if (playerInfo.playerName == "") { return $"Puppet{playerInfo.puppetID}"; }
            return playerInfo.playerName;
        }

        void Disconnect(TcpClient client, int puppetID)
        {
            lock (clients)
            {
                clients.Remove(client);
            }

            lock (players)
            {
                players.Remove(FindPlayerByID(players, puppetID));
            }

            client.Close();
            Logger.Log($"Client with Puppet ID {puppetID} disconnected.");
        }

        static PlayerInfo FindPlayerByID(List<PlayerInfo> players, int playerID)
        {
            foreach (PlayerInfo player in players) { if (player.puppetID == playerID) { return player; } } return null;
        }

        int AssingPuppetID()
        {
            int id;
            Random random = new Random();
            do { id = random.Next(1, 1000); } while ( players.Contains(FindPlayerByID(players,id)));
            return id;
        }

        private void BroadcastPlayerData()
        {
            int i = 0;
            while (true)
            {
                try
                {
                    // Create PlayerInfoList from the players dictionary
                    PlayerInfoList playerInfoList = new PlayerInfoList
                    {
                        players = players
                    };
                    // Serialize PlayerInfoList and send to each connected client
                    string allPlayersData = JsonConvert.SerializeObject(playerInfoList);
                    //Logger.Log($"{allPlayersData}");
                    byte[] data = Encoding.ASCII.GetBytes(allPlayersData + '\n');

                    lock (clients)
                    {
                        foreach (TcpClient client in clients)
                        {
                            try
                            {
                                NetworkStream stream = client.GetStream();
                                if (true)
                                {
                                    i++;
                                    stream.Write(data, 0, data.Length);
                                    stream.Flush();
                                }
                            }
                            catch (Exception e) { Logger.LogError($"Error sending data to client {client.Client.RemoteEndPoint}: {e.Message}"); continue; }
                        }
                    }
                    Thread.Sleep(30);
                }
                catch (Exception e) { Logger.LogError(e.ToString()); continue; }
            }
        }


        private void MaxPlayer(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            byte[] data = Encoding.ASCII.GetBytes("Server full");
            stream.Write(data,0, data.Length);
            stream.Close();
            client.Close();
        }
    }
}
