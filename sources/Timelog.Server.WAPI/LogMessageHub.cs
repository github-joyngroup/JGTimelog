using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

public class LogMessageHub : Hub
{
    public async Task SendLogMessage(string message)
    {
        await Clients.All.SendAsync("ReceiveLogMessage", message);
    }
}
