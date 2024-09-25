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
    public class Rotation
    {
        public float x { get; set;}
        public float y { get; set;}
        public float z { get; set;}
        public float w { get; set;}
    }

    public class PlayerInfo
    {
        public int puppetID { get; set; } = -1;
        public string playerName { get; set; }
        public Position position { get; set; }
        public Rotation rotation { get; set; }
        public int health { get; set; }
    }
    public class ServerPlayer
    {
        public PlayerInfo info { get; set; }
        public TcpClient tcpClient { get; set; }
        public Thread thread { get; set; }

    }
    public class ObjectInfo
    {
        public int puppetID { get; set; } = -1;
        public Position position { get; set; }
        public Rotation rotation { get; set; }
        public int master { get; set; } = -1;
    }
    [Serializable]
    public class ServerInfo
    {
        public string type = "ServerInfo";
        public PlayerInfo master { get; set; }
    }
    [Serializable]
    public class PlayerInfoList
    {
        public string type = "PlayerInfo";
        public List<PlayerInfo> players = new List<PlayerInfo>();
    }
    [Serializable]
    public class ObjectInfoList
    {
        public string type = "ObjectInfo";
        public List<ObjectInfo> objects = new List<ObjectInfo>();
    }

    public class TypeCheck { public string type; }

    public class Program
    {
        private const int maxPlayers = 4;
        public TcpListener server;
        public static List<ServerPlayer> serverPlayers = new List<ServerPlayer>();
        public static List<PlayerInfo> players = new List<PlayerInfo>();
        public static List<TcpClient> clients = new List<TcpClient>();
        public static List<ObjectInfo> objects = new List<ObjectInfo>();
        public static int expectedKB = 4;

        //public static PlayerInfo master;
        public static ServerInfo serverInfo = new ServerInfo();

        public static void Main(string[] args)
        {
            Program program = new();
            ConsoleHandler ch = new() { program = program };
            Thread serverThread = new Thread(program.StartServer) { IsBackground = true, Priority = ThreadPriority.Highest };
            //serverThread.Start();

            Thread consoleThread = new Thread(ch.StartConsole) { IsBackground = true };
            consoleThread.Start();

            //Thread broadcast = new Thread(program.BroadcastPuppetData) { IsBackground = true };
            //broadcast.Start();
            //            while (serverThread.IsAlive) { Thread.Sleep(100); }
            serverInfo.master = new PlayerInfo();
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
        private static PlayerInfo CheckMaster()
        {
            try { if (serverInfo.master.puppetID == -1) { PlayerInfo p = players.First(); Logger.Log($"{p.playerName} has been selected as master"); return p; } } catch { return new PlayerInfo(); }
            return serverInfo.master;
        }
        private bool ClientCode(string message, TcpClient client, PlayerInfo player)
        {
            switch (message)
            {
                case "Disconnect":
                    Disconnect(client, player.puppetID);
                    return true;
                default:
                    return false;
            }
        }
        private void HandleClient(TcpClient client)
        {
            int puppetID = AssignPuppetID();
            try
            {
                NetworkStream stream = client.GetStream();
                byte[] bytes = new byte[1024 * expectedKB];
                string data = null;

                byte[] puppetIDData = Encoding.UTF8.GetBytes($"{puppetID}\n");
                stream.Write(puppetIDData, 0, puppetIDData.Length);
                Console.WriteLine($"Assigned Puppet ID: {puppetID}");
                Thread.Sleep(100);

                while (client.Connected)
                {
                    stream.ReadTimeout = 5000;
                    int bytesRead = stream.Read(bytes, 0, bytes.Length);
                    //Logger.Log(bytesRead.ToString());
                    if (bytesRead == 0) break;

                    data = Encoding.UTF8.GetString(bytes, 0, bytesRead);

                    string[] datas = data.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    try
                    {
                        foreach (string d in datas)
                        {
                            Logger.LogWarning(d);
                            TypeCheck tp = JsonConvert.DeserializeObject<TypeCheck>(d);
                            if (ClientCode(d, client, FindPlayerByID(puppetID))) { return; }
                            //ReceiveCommand(d,client);
                            //ReceivePlayerData(d, client,puppetID);
                            try { ReceivePlayerData(d, client, puppetID); continue; } catch (IncompatiblePacketException e) { } catch (Exception e) { Logger.LogError(e.ToString()); }
                            try { ReceiveCommand(d, client); continue; } catch (IncompatiblePacketException e) { } catch (Exception e) { Logger.LogError(e.ToString()); }
                            try { ReceiveObjectData(d, client); continue; } catch (IncompatiblePacketException e) { } catch (Exception e) { Logger.LogError(e.ToString()); }
                            //try { Thread commandThread = new Thread(() => ReceiveCommand(d, client)) { IsBackground = true }; commandThread.Start(); continue; } catch { }
                        }
                    }
                    catch (JsonSerializationException e)
                    {
                        Logger.LogError($"Packet error: {e.Message}");
                        continue;
                    }
                    catch (JsonReaderException e)
                    {
                        Logger.LogError($"Packet error: {e.Message}");
                        continue;
                    }
                    catch (Exception e)
                    {
                        Logger.LogError(e.Message);
                    }

                    try { BroadcastPlayerData(); BroadcastObjectData(); }
                    catch (Exception e) { Logger.LogError(e.Message); continue; };
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Client connection error: {e}");
                if (e.Message.Contains("A connection attempt failed because the connected party did not properly respond after a period of time"))
                {
                    Logger.LogError("Read failure");
                    byte[] data = Encoding.UTF8.GetBytes("Read failure");
                    NetworkStream stream = client.GetStream();
                    stream.WriteTimeout = 500;
                    stream.Write(data, 0, data.Length);
                }
                Disconnect(client, puppetID);
            }
        }

        void ReceiveCommand(string data, TcpClient client)
        {
            Command command = JsonConvert.DeserializeObject<Command>(data);
            if (command.command == null) throw new IncompatiblePacketException("Incompatible packet for commands");
            switch (command.command)
            {
                case "RequestID":
                    CommandHandler.AssignObjectID(command,client);
                    break;
                default:
                    throw new IncompatiblePacketException("No proper command found");
            }
        }

        void ReceivePlayerData(string data, TcpClient client, int puppetID)
        {
            PlayerInfo playerInfo = JsonConvert.DeserializeObject<PlayerInfo>(data);
            playerInfo.puppetID = puppetID; // Assign puppetID
            if (playerInfo.playerName == null) throw new IncompatiblePacketException("Incompatible packet for Player Data");
            lock (players)
            {
                playerInfo.playerName = NameCheck(playerInfo);
                IPEndPoint ip = client.Client.RemoteEndPoint as IPEndPoint;
                //playerInfo.IPv4 = ip.Address.ToString();
                // Find existing player with the same puppetID and update their info, or add new player
                PlayerInfo existingPlayer = FindPlayerByID(puppetID);
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
                    lock (serverPlayers) { ServerPlayer sp = new ServerPlayer { info = playerInfo, tcpClient = client, thread = Thread.CurrentThread }; serverPlayers.Add(sp); }
                    lock (serverInfo.master) { serverInfo.master = CheckMaster(); }
                    CommandHandler.BroadcastMaster(serverInfo.master);
                }
            }
        }

        void ReceiveObjectData(string data, TcpClient client)
        {
            ObjectInfo objectInfo = JsonConvert.DeserializeObject<ObjectInfo>(data);
            if (objectInfo.master == null) throw new IncompatiblePacketException("Incompatible packet for ObjectData");

            ObjectInfo existingObj = FindObjectByID(objectInfo.puppetID);
            if (existingObj != null)
            {
                existingObj.position = objectInfo.position;
            }
            else
            {
                //objectInfo.puppetID = AssingPuppetID();
                objects.Add(objectInfo);
            }
        }

        string NameCheck(PlayerInfo playerInfo)
        {
            if (playerInfo.playerName == "") { return $"Puppet{playerInfo.puppetID}"; }
            return playerInfo.playerName;
        }

        public static void Disconnect(TcpClient client, int puppetID)
        {
            lock (clients)
            {
                clients.Remove(client);
            }
            ServerPlayer sp = FindServerPlayer(FindPlayerByID(puppetID));

            lock (players)
            {
                serverPlayers.Remove(sp);
                players.Remove(FindPlayerByID(puppetID));
                if (serverInfo.master.puppetID == puppetID) { Logger.LogWarning($"Master disconnected, attempting to assign new Master"); serverInfo.master = new(); serverInfo.master = CheckMaster(); }
            }

            client.Close();
            Logger.Log($"Client with Puppet ID {puppetID} disconnected.");
            Thread.CurrentThread.Join();
        }

        public static void Disconnect(TcpClient client)
        {
            lock (clients)
            {
                clients.Remove(client);
            }

            Logger.Log($"Client {client.Client.RemoteEndPoint} disconnected.");
            client.Close();
        }

        public static PlayerInfo FindPlayerByID(int playerID)
        {
            foreach (PlayerInfo player in players) { if (player.puppetID == playerID) { return player; } }
            return null;
        }
        public static ObjectInfo FindObjectByID(int objectID)
        {
            if (objectID == -1) { return null; }
            foreach (ObjectInfo obj in objects) { if (obj.puppetID == objectID) { return obj; } }
            return null;
        }
        public static ServerPlayer FindServerPlayer(PlayerInfo pinfo)
        {
            if (pinfo == null) { return null; }
            foreach (ServerPlayer sps in serverPlayers) { if (sps.info.puppetID == pinfo.puppetID) { return sps; } }
            return null;
        }

        public static ServerPlayer FindServerPlayer(TcpClient client)
        {
            foreach (ServerPlayer sps in serverPlayers) { if (sps.tcpClient.Client.RemoteEndPoint == client.Client.RemoteEndPoint) { return sps; } }
            return null;
        }

        public static int AssignPuppetID()
        {
            int id;
            Random random = new Random();
            do { id = random.Next(1, 99); } while (players.Contains(FindPlayerByID(id))||objects.Contains(FindObjectByID(id)));
            return id;
        }

        public void BroadcastServerInfo()
        {
            string s = JsonConvert.SerializeObject(serverInfo);
            Logger.LogWarning(s);
            byte[] data = Encoding.UTF8.GetBytes(s + '\n');
            BroadcastData(data);
        }

        private void BroadcastPlayerData()
        {
            // Create PlayerInfoList from the players dictionary
            PlayerInfoList puppetInfoList = new PlayerInfoList
            {
                players = players
            };
            // Serialize PlayerInfoList and send to each connected client
            string allPuppetData = JsonConvert.SerializeObject(puppetInfoList);
            byte[] data = Encoding.UTF8.GetBytes(allPuppetData + '\n');

            BroadcastData(data);
        }

        private void BroadcastObjectData()
        {
            ObjectInfoList objectInfoList = new ObjectInfoList
            {
                objects = objects
            };
            string allPuppetData = JsonConvert.SerializeObject(objectInfoList);
            byte[] data = Encoding.UTF8.GetBytes(allPuppetData + '\n');

            BroadcastData(data);
        }

        public static void BroadcastData(byte[] data)
        {
            lock (clients)
            {
                foreach (TcpClient client in clients)
                {
                    try
                    {
                        NetworkStream stream = client.GetStream();
                        if (true)
                        {
                            stream.Write(data, 0, data.Length);
                            stream.Flush();
                        }
                    }
                    catch (InvalidOperationException e) 
                    {
                        Logger.LogError($"{e}");
                        Disconnect(client);
                        continue;
                    }
                    catch (Exception e) { Logger.LogError($"{e}");  continue; }
                }
            }
        }


        private void MaxPlayer(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            byte[] data = Encoding.UTF8.GetBytes("Server full");
            stream.Write(data, 0, data.Length);
            stream.Close();
            client.Close();
        }
    }
}
