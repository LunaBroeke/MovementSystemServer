using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MovementSystemServer
{
    public class Command
    {
        public string type { get; set; } = "Command";
        public string command { get; set; }
        public string[] arguments { get; set; }
    }
    public static class CommandHandler
    {
        public static void AssignObjectID(Command c, TcpClient client)
        {
            int tempID = int.Parse(c.arguments[1]);
            int puppetID = Program.AssingPuppetID();
            c.command = "SyncID";
            c.arguments[2] = puppetID.ToString();
            NetworkStream stream = client.GetStream();
            string s = JsonConvert.SerializeObject(c);
            byte[] data = Encoding.UTF8.GetBytes(s+'\n');
            stream.Write(data, 0, data.Length);
        }

        public static void ObjectSyncRequest(TcpClient client)
        {
            Command c = new Command
            {
                command = "ObjectSyncRequest"
            };

            NetworkStream stream = client.GetStream();
            string s = JsonConvert.SerializeObject(c);
            byte[] data = Encoding.UTF8.GetBytes(s + '\n');
            stream.Write(data,0, data.Length);
        }
    }
}
