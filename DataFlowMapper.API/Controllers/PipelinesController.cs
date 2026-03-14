using DataFlowMapper.API.Services;
using DataFlowMapper.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace DataFlowMapper.API.Controllers;

[ApiController]
[Route("api/pipelines")]
public class PipelinesController : ControllerBase
{
    private readonly PipelineStore _store;

    public PipelinesController(PipelineStore store)
    {
        _store = store;
    }

    [HttpGet]
    public IActionResult GetAll() => Ok(_store.GetAll().ToList());

    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id)
    {
        var pipeline = _store.GetById(id);
        if (pipeline == null) return NotFound();
        return Ok(pipeline);
    }

    [HttpPost]
    public IActionResult Create([FromBody] Pipeline pipeline)
    {
        var created = _store.Add(pipeline);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public IActionResult Update(Guid id, [FromBody] Pipeline pipeline)
    {
        if (!_store.Update(id, pipeline)) return NotFound();
        return Ok(_store.GetById(id));
    }

    [HttpDelete("{id:guid}")]
    public IActionResult Delete(Guid id)
    {
        if (!_store.Remove(id)) return NotFound();
        return NoContent();
    }
}
