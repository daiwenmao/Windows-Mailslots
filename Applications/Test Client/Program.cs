using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using Codeology.IPC.Mailslots;

namespace Test_Client
{

    class Program
    {

        static void Main(string[] args)
        {
            MailslotClient client = new MailslotClient("test");

            client.Connect();

            try {
                for(int i = 1; i <= 256; i++) {
                    byte[] buffer = Encoding.UTF8.GetBytes("Iteration " + i.ToString());
                    int count = client.Write(buffer);

                    Thread.Sleep(1000);
                }
            } finally {
                client.Disconnect();
            }
        }

    }

}
