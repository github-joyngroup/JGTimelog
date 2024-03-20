using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Timelog.LogClientTester
{
    internal class TimelogDomains
    {
        internal static int AssetGateway { get; }                       = 0x01000000;
        internal static int AssetGatewayDokRouter { get; }              = 0x01010000;
        internal static int AssetGatewayDokRouterInitPipeline { get; }  = 0x01010100;
        internal static int AssetGatewayDokRouterExecutePipeline { get; } = 0x01010200;
        internal static int AssetGatewayDokRouterTickPipeline { get; }  = 0x01010300;
    }
}
