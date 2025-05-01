using System.Text.Json;

public class ProgressReporter
{
    private readonly List<HttpResponse> _subscribers = new();
    private readonly object _lock = new();

    public void Subscribe(HttpResponse response)
    {
        lock (_lock)
        {
            _subscribers.Add(response);
        }
    }

    public void Unsubscribe(HttpResponse response)
    {
        lock (_lock)
        {
            _subscribers.Remove(response);
        }
    }

    public async Task ReportAsync(int progress, string message)
    {
        lock (_lock)
        {
            _subscribers.RemoveAll(r => !r.HttpContext.Response.Body.CanWrite);
        }

        foreach (var response in _subscribers)
        {
            var payload = $"data: {JsonSerializer.Serialize(new { progress, message })}\n\n";
            await response.WriteAsync(payload);
            await response.Body.FlushAsync();
        }
    }
}
