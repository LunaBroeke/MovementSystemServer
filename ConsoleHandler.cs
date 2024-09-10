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
                    Logger.Log($"Active players: {Program.players.Count}\n {ListPlayers(Program.players)}");
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
        public string ListPlayers(List<PlayerInfo> pis)
        {
            string s = string.Empty;
            int i = 0;
            foreach (PlayerInfo p in pis)
            {
                i++;
                s += $"IPv4: {p.IPv4}\n Name: {p.playerName}\n PuppetID:{p.puppetID}\n ";
            }
            return s;
        }
    }
}
