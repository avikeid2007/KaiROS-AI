namespace KaiROS.AI.Uno.Services;

public class ApiService : IApiService
{
    private bool _isRunning;
    private int _port = 5000;

    public bool IsRunning => _isRunning;
    public int Port => _port;

    public Task StartAsync(int port = 5000)
    {
        _port = port;
        _isRunning = true;
        // Would start HTTP listener
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        _isRunning = false;
        // Would stop HTTP listener
        return Task.CompletedTask;
    }
}
