using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

namespace Timelog.Server
{
    internal class ViewerRegister
    {
        private TcpListener listener;
        private int port = 0;
        private List<Viewer> viewers = new List<Viewer>();
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        public ViewerRegister(int tcpPort)
        {
            port = tcpPort;
            listener = new TcpListener(IPAddress.Any, port);
        }

        public void Start(CancellationToken cancellationToken)
        {
            listener.Start();
            Console.WriteLine($"Timelog.Server is accepting viewers on TCP port {port}.");

            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client = listener.AcceptTcpClient();
                Console.WriteLine($"Client connected.");
                Thread clientThread = new Thread(new ParameterizedThreadStart(EnrollViewer));
                clientThread.Start(client);
            }

            listener.Stop();
        }

        private void EnrollViewer(object clientObj)
        {
            _lock.EnterWriteLock();
            try
            {
                viewers.Add(new Viewer((TcpClient)clientObj));
            }
            finally
            {
                _lock.ExitWriteLock();
            }
            
        }
    }

    internal class Viewer
    {
        public TcpClient TcpClient { get; private set; }

        public Viewer(TcpClient client)
        {
            TcpClient = client;
            
        }

        //private void HandleViewer()
        //{
        //    NetworkStream stream = TcpClient.GetStream();
        //    byte[] buffer = new byte[1024];
        //    int bytesRead = 0;
        //    while (true)
        //    {
        //        bytesRead = stream.Read(buffer, 0, buffer.Length);
        //        if (bytesRead == 0)
        //        {
        //            break;
        //        }
        //        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
        //        Console.WriteLine($"Viewer says: {message}");
        //    }
        //    TcpClient.Close();
        //}   
    }
}
