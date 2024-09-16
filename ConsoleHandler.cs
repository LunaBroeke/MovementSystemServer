using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MovementSystemServer
{
    public class ConsoleHandler
    {
        public Program program;
        public void StartConsole()
        {
            while (true)
            {
                string input = Console.ReadLine();
                if (input.ToLower() == "shutdown")
                {
                    Console.WriteLine("Shutting down server...");
                    program.server.Stop();  // Stop the server
                    lock (Program.clients)
                    {
                        foreach (var client in Program.clients)
                        {
                            client.Close();
                        }
                    }
                    Environment.Exit(0);  // Terminate the application
                }
                else if (input.ToLower() == "status")
                {
                    Logger.Log($"Connected clients: {Program.clients.Count}\n {ListClients(Program.clients)}");
                    Logger.Log($"Active players: {Program.serverPlayers.Count}\n {ListPlayers(Program.serverPlayers)}");
                    Logger.Log($"Master: {Program.serverInfo.master.playerName}");
                    program.BroadcastServerInfo();
                }
                else
                {
                    Console.WriteLine("Unknown command.");
                }
            }
        }
        private string ListClients(List<TcpClient> tcps)
        {
            string s = string.Empty;
            int i = 0;
            foreach (TcpClient client in tcps)
            {
                i++;
                IPEndPoint ip = client.Client.RemoteEndPoint as IPEndPoint;
                s += $"{i}: {ip.Address}:{ip.Port}\n ";
            }
            return s;
        }
        public string ListPlayers(List<ServerPlayer> sps)
        {
            string s = string.Empty;
            int i = 0;
            foreach (ServerPlayer p in sps)
            {
                PlayerInfo pi = p.info;
                TcpClient tcp = p.tcpClient;
                IPEndPoint ipe = tcp.Client.RemoteEndPoint as IPEndPoint;
                i++;
                s += $"IPv4: {ipe.Address}:{ipe.Port}\n Name: {pi.playerName}\n PuppetID:{pi.puppetID}\n ";
            }
            return s;
        }
    }
}
