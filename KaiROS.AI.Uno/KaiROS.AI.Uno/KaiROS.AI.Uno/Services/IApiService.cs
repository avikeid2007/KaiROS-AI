namespace KaiROS.AI.Uno.Services;

public interface IApiService
{
    bool IsRunning { get; }
    int Port { get; }
    Task StartAsync(int port = 5000);
    Task StopAsync();
}
