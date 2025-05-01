using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<ProjectAnalyzer>();
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/api/analyze", async (HttpContext context, ProjectAnalyzer analyzer) =>
{
    var projectPath = await new StreamReader(context.Request.Body).ReadToEndAsync();
    var result = await analyzer.AnalyzeProject(projectPath);
    return Results.Json(result);
});

app.Run();