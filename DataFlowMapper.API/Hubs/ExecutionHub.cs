using Microsoft.AspNetCore.SignalR;

namespace DataFlowMapper.API.Hubs;

public class ExecutionHub : Hub
{
    public async Task JoinExecution(string executionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, executionId);
    }

    public async Task LeaveExecution(string executionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, executionId);
    }

    public async Task SendLog(string executionId, object log)
    {
        await Clients.Group(executionId).SendAsync("ReceiveLog", log);
    }

    public async Task SendProgress(string executionId, object stats)
    {
        await Clients.Group(executionId).SendAsync("ReceiveProgress", stats);
    }
}
