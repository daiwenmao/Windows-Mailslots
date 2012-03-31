using System;
using System.Collections.Generic;
using System.Text;

using Codeology.IPC.Mailslots;

namespace Test_Server
{

    class Program
    {

        static void Main(string[] args)
        {
            MailslotServer server = new MailslotServer("test");

            server.Open();

            try {
                // Start waiting for message
                server.BeginWait(new AsyncCallback(HandleMessage),server);

                // Wait for key press
                Console.ReadKey();
            } finally {
                server.Close();
            }
        }

        static void HandleMessage(IAsyncResult asyncResult)
        {
            // Get mailslot server
            MailslotServer server = (MailslotServer)asyncResult.AsyncState;

            // Get message size
            int size = server.EndWait(asyncResult);

            // Allocate buffer
            byte[] buffer = new byte[size];

            // Read in message
            server.Read(buffer,0,buffer.Length);

            // Convert buffer to string
            string s = Encoding.UTF8.GetString(buffer);

            // Write out string
            Console.WriteLine(s);

            // Start waiting for message
            server.BeginWait(new AsyncCallback(HandleMessage),server);
        }

    }

}
