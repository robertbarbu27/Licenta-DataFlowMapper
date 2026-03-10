using DataFlowMapper.Core.Interfaces;
using DataFlowMapper.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace DataFlowMapper.API.Controllers;

[ApiController]
[Route("api/connectors")]
public class ConnectorsController : ControllerBase
{
    private readonly IConnectorFactory _factory;

    public ConnectorsController(IConnectorFactory factory)
    {
        _factory = factory;
    }

    [HttpPost("test")]
    public async Task<IActionResult> Test([FromBody] SourceConfig config)
    {
        try
        {
            var connector = _factory.Create(config);
            var ok = await connector.TestConnectionAsync();
            if (!ok) return BadRequest(new { error = "Connection failed." });
            var tables = await connector.GetTablesAsync();
            return Ok(new { success = true, tables });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id}/schema/{table}")]
    public async Task<IActionResult> GetSchema(string id, string table, [FromQuery] string connectionString, [FromQuery] string type)
    {
        try
        {
            var config = new SourceConfig { Id = id, ConnectionString = connectionString, Type = type, Table = table };
            var connector = _factory.Create(config);
            var fields = await connector.GetSchemaAsync(table);
            return Ok(fields);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
