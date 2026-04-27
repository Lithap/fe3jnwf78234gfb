using Microsoft.AspNetCore.SignalR;

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

    // Stub methods the game calls - just do nothing for now
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
