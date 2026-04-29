using Microsoft.AspNetCore.SignalR;

namespace RetroRec_Server
{
    public class RecNetHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            Console.WriteLine($"[Hub] Client connected: {Context.ConnectionId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"[Hub] Client disconnected: {Context.ConnectionId}");
            await base.OnDisconnectedAsync(exception);
        }

        public Task SubscribeToPlayers(object data)
        {
            Console.WriteLine($"[Hub] SubscribeToPlayers called: {data}");
            return Task.CompletedTask;
        }

        public Task UnsubscribeFromPlayers(object data)
        {
            Console.WriteLine($"[Hub] UnsubscribeFromPlayers called: {data}");
            return Task.CompletedTask;
        }
    }
}
