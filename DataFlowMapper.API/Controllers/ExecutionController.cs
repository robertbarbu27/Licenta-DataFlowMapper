using DataFlowMapper.API.Hubs;
using DataFlowMapper.API.Services;
using DataFlowMapper.Core.Results;
using DataFlowMapper.Executor;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace DataFlowMapper.API.Controllers;

[ApiController]
[Route("api/pipelines")]
public class ExecutionController : ControllerBase
{
    private static readonly Dictionary<string, CancellationTokenSource> _executions = new();

    private readonly PipelineRunner _runner;
    private readonly PipelineStore _store;
    private readonly IHubContext<ExecutionHub> _hubContext;

    public ExecutionController(PipelineRunner runner, PipelineStore store, IHubContext<ExecutionHub> hubContext)
    {
        _runner = runner;
        _store = store;
        _hubContext = hubContext;
    }

    [HttpPost("{id:guid}/execute")]
    public IActionResult Execute(Guid id)
    {
        var pipeline = _store.GetById(id);
        if (pipeline == null) return NotFound();

        var executionId = Guid.NewGuid().ToString();
        var cts = new CancellationTokenSource();
        _executions[executionId] = cts;

        _ = Task.Run(async () =>
        {
            try
            {
                await _runner.ExecuteAsync(
                    pipeline,
                    async log =>
                    {
                        await _hubContext.Clients.Group(executionId).SendAsync("ReceiveLog", log);
                    },
                    async (ExecutionStats stats) =>
                    {
                        await _hubContext.Clients.Group(executionId).SendAsync("ReceiveProgress", stats);
                    },
                    cts.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                await _hubContext.Clients.Group(executionId).SendAsync("ReceiveLog", new
                {
                    Level = "Error",
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
            finally
            {
                _executions.Remove(executionId);
            }
        });

        return Accepted(new { executionId });
    }

    [HttpPost("{id:guid}/cancel")]
    public IActionResult Cancel(Guid id, [FromQuery] string executionId)
    {
        if (_executions.TryGetValue(executionId, out var cts))
        {
            cts.Cancel();
            _executions.Remove(executionId);
            return Ok(new { cancelled = true });
        }
        return NotFound(new { error = "Execution not found." });
    }
}
