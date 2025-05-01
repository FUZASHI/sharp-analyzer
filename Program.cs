using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<ProjectAnalyzer>();
builder.Services.AddSingleton<ProgressReporter>();
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/analyze", async (HttpContext context, ProjectAnalyzer analyzer, ProgressReporter reporter) =>
{
    var projectPath = await new StreamReader(context.Request.Body).ReadToEndAsync();
    var result = await analyzer.AnalyzeProject(projectPath, reporter);
    return Results.Json(result);
});

app.MapPost("/api/list-projects", async (HttpContext context) =>
{
    var directoryPath = await new StreamReader(context.Request.Body).ReadToEndAsync();
    if (!Directory.Exists(directoryPath))
        return Results.NotFound("Directory not found");

    var projectFiles = Directory.GetFiles(directoryPath, "*.csproj");
    return Results.Ok(projectFiles);
});

app.MapGet("/api/progress", async (HttpContext context, ProgressReporter reporter) =>
{
    context.Response.Headers.Add("Content-Type", "text/event-stream");
    context.Response.Headers.Add("Cache-Control", "no-cache");
    context.Response.Headers.Add("Connection", "keep-alive");

    reporter.Subscribe(context.Response);
    try
    {
        // Wait indefinitely until the request ends
        while (!context.RequestAborted.IsCancellationRequested)
        {
            await Task.Delay(1000); // Keep the connection open
        }
    }
    finally
    {
        reporter.Unsubscribe(context.Response);
    }
});


app.Run();