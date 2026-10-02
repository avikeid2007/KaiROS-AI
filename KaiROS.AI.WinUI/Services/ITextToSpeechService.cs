using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.SpeechSynthesis;

namespace KaiROS.AI.WinUI.Services;

public interface ITextToSpeechService
{
    bool IsSpeaking { get; }
    bool IsAvailable { get; }
    IReadOnlyList<VoiceInformation> GetAvailableVoices();
    Task SpeakAsync(string text, CancellationToken ct = default);
    Task StopAsync();
    Task SetVoiceAsync(string voiceId);
    Task SetRateAsync(double rate);   // 0.5 to 2.0
    Task SetVolumeAsync(double volume); // 0.0 to 1.0
}
