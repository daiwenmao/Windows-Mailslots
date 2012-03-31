using System;
using System.Collections.Generic;
using System.Text;

namespace Codeology.IPC.Mailslots
{

    public class MailslotException : Exception
    {

        public MailslotException() : base() {}
        public MailslotException(string message) : base(message) {}
        public MailslotException(string message, Exception innerException) : base(message,innerException) {}

    }

}
