using System;
using System.Collections.Generic;
using System.Linq;
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
                    Console.WriteLine($"Connected clients: {Program.clients.Count}");
                    Console.WriteLine($"Active players: {Program.players.Count}");
                }
                else
                {
                    Console.WriteLine("Unknown command.");
                }
            }
        }
    }
}
