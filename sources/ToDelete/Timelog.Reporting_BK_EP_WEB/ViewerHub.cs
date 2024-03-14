using Microsoft.AspNetCore.SignalR;

namespace Timelog.Reporting
{
    public class ViewerHub : Hub
    {
        public async Task SendMessage(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }
    }
}
