﻿using System;
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
        public int puppetID { get; set; } = -1;
        public string playerName { get; set; }
        public Position position { get; set; }
        public int health { get; set; }
    }
    public class ServerPlayer
    {
        public PlayerInfo info { get; set; }
        public TcpClient tcpClient { get; set; }

    }
    public class ObjectInfo
    {
        public int puppetID { get; set; }
        public Position position { get; set; }
        public int master { get; set; } = -1;
    }

    // Updated PlayerInfoList to match the new format
    [Serializable]
    public class PuppetInfoList
    {
        public List<PlayerInfo> players = new List<PlayerInfo>();
        public List<ObjectInfo> objects = new List<ObjectInfo>();
    }

    public class Program
    {
        private const int maxPlayers = 4;
        public TcpListener server;
        public static List<ServerPlayer> serverPlayers = new List<ServerPlayer>();
        public static List<PlayerInfo> players = new List<PlayerInfo>();
        public static List<TcpClient> clients = new List<TcpClient>();
        public static List<ObjectInfo> objects = new List<ObjectInfo>();

        public static PlayerInfo master;

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
            master = new PlayerInfo();
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
        private PlayerInfo CheckMaster()
        {
            try { if (master.puppetID == -1) { PlayerInfo p = players.First(); Logger.Log($"{p.playerName} has been selected as master"); return p; } } catch { return new PlayerInfo(); }
            return master;
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
            int puppetID = AssingPuppetID();
            try
            {
                NetworkStream stream = client.GetStream();
                byte[] bytes = new byte[512];
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
                            if (ClientCode(d, client, FindPlayerByID(puppetID))) { return; }
                            PlayerInfo playerInfo = JsonConvert.DeserializeObject<PlayerInfo>(d);
                            playerInfo.puppetID = puppetID; // Assign puppetID

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
                                    lock (serverPlayers) { ServerPlayer sp = new ServerPlayer { info = playerInfo, tcpClient = client }; serverPlayers.Add(sp); }
                                    lock (master) { master = CheckMaster(); }
                                }
                            }
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

                    try { BroadcastPuppetData(); }
                    catch (Exception e) { Logger.LogError(e.Message); continue; };
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
                serverPlayers.Remove(FindServerPlayer(FindPlayerByID(puppetID)));
                players.Remove(FindPlayerByID(puppetID));
                if (master.puppetID == puppetID) { Logger.LogWarning($"Master disconnected, attempting to assign new Master"); master.puppetID = -1; master = CheckMaster(); }
            }

            client.Close();
            Logger.Log($"Client with Puppet ID {puppetID} disconnected.");
        }
        void Disconnect(TcpClient client)
        {
            lock (clients)
            {
                clients.Remove(client);
            }

            client.Close();
            Logger.Log($"Client {client.Client.RemoteEndPoint} disconnected.");
        }

        static PlayerInfo FindPlayerByID(int playerID)
        {
            foreach (PlayerInfo player in players) { if (player.puppetID == playerID) { return player; } }
            return null;
        }
        static ServerPlayer FindServerPlayer(PlayerInfo pinfo)
        {
            foreach (ServerPlayer sps in serverPlayers) { if (sps.info.puppetID == pinfo.puppetID) { return sps; } }
            return null;
        }

        static ServerPlayer FindServerPlayer(TcpClient client)
        {
            foreach (ServerPlayer sps in serverPlayers) { if (sps.tcpClient.Client.RemoteEndPoint == client.Client.RemoteEndPoint) { return sps; } }
            return null;
        }

        int AssingPuppetID()
        {
            int id;
            Random random = new Random();
            do { id = random.Next(1, 1000); } while (players.Contains(FindPlayerByID(id)));
            return id;
        }

        private void BroadcastPuppetData()
        {
            int i = 0;
            // Create PlayerInfoList from the players dictionary
            PuppetInfoList puppetInfoList = new PuppetInfoList
            {
                players = players,
                objects = objects,
            };
            // Serialize PlayerInfoList and send to each connected client
            string allPuppetData = JsonConvert.SerializeObject(puppetInfoList);
            //Logger.Log($"{allPlayersData}");
            byte[] data = Encoding.ASCII.GetBytes(allPuppetData + '\n');

            lock (clients)
            {
                foreach (TcpClient client in clients)
                {
                    NetworkStream stream = client.GetStream();
                    if (true)
                    {
                        i++;
                        stream.Write(data, 0, data.Length);
                        stream.Flush();
                    }
                }
            }

        }


        private void MaxPlayer(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            byte[] data = Encoding.ASCII.GetBytes("Server full");
            stream.Write(data, 0, data.Length);
            stream.Close();
            client.Close();
        }
    }
}
