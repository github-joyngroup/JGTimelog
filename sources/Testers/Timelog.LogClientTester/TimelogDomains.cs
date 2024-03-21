using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Timelog.LogClientTester
{
    internal class TimelogDomains
    {
        internal static uint AssetGateway { get; }                           = 0x01000000; // 16 777 216
        internal static uint AssetGatewayDokRouter { get; }                  = 0x01010000; // 16 842 752
        internal static uint AssetGatewayDokRouterInitPipeline { get; }      = 0x01010100; // 16 843 008
        internal static uint AssetGatewayDokRouterExecutePipeline { get; }   = 0x01010200; // 16 843 264
        internal static uint AssetGatewayDokRouterTickPipeline { get; }      = 0x01010300; // 16 843 520
    }
}
