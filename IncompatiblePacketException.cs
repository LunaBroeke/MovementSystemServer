using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MovementSystemServer
{
    internal class IncompatiblePacketException : Exception
    {
        public IncompatiblePacketException() { }
        public IncompatiblePacketException(string message) : base(message) { }
        public IncompatiblePacketException(string message, Exception inner) : base(message, inner) { }
    }
}
