using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Timelog.LogClientTester
{
    internal class TimelogDomains
    {
        internal static uint AssetGateway { get; }                           = 0x01000000; // 1.0.0.0 - 16 777 216
        internal static uint AssetGatewayDokRouter { get; }                  = 0x01010000; // 1.1.0.0 - 16 842 752
        internal static uint AssetGatewayDokRouterInitPipeline { get; }      = 0x01010100; // 1.1.1.0 - 16 843 008
        internal static uint AssetGatewayDokRouterExecutePipeline { get; }   = 0x01010200; // 1.1.2.0 - 16 843 264
        internal static uint AssetGatewayDokRouterTickPipeline { get; }      = 0x01010300; // 1.1.3.0 - 16 843 520

        internal static uint JobOrchestrator { get; }                        = 0x02000000; // 2.0.0.0 - 33 554 432
    }
}
