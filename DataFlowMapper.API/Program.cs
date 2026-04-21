using DataFlowMapper.API.Hubs;
using DataFlowMapper.API.Services;
using DataFlowMapper.Connectors;
using DataFlowMapper.Core.Interfaces;
using DataFlowMapper.Executor;
using DataFlowMapper.Transforms;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<PipelineStore>();
builder.Services.AddSingleton<IConnectorFactory, ConnectorFactory>();
builder.Services.AddSingleton<ITransformFactory, TransformFactory>();
builder.Services.AddSingleton<PipelineRunner>();
builder.Services.AddSingleton<PipelineValidator>();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .SetIsOriginAllowed(_ => true);
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.MapControllers();
app.MapHub<ExecutionHub>("/hubs/execution");

app.Run();
