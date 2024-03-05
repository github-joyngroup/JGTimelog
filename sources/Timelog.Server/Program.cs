//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace Timelog.Server
//{
//    internal class Program
//    {
//        private static void Main(string[] args)
//        {
//            var configuration = new Configuration
//            {
//                AuthorizedAppKeys = new List<Guid>
//                {
//                    Guid.Parse("8ce94d5e-b2a3-4685-9e6c-ab21410b595f"),
//                },
//            };

//            Console.WriteLine("Type 'exit' to exit Timelog.Server");

//            string input = "";
//            var CancellationTokenSource = new System.Threading.CancellationTokenSource();
//            while (input != "exit")
//            {
//                Listener.Startup(configuration, null, CancellationTokenSource.Token);

//                input = Console.ReadLine();
                
//                if (input == "exit")
//                {
//                    CancellationTokenSource.Cancel();
//                }
                                
//            };
            
//        }
//    }
//}
