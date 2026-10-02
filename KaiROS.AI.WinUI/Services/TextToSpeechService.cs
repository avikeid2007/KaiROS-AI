using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;

namespace KaiROS.AI.WinUI.Services;

public class TextToSpeechService : ITextToSpeechService
{
    private readonly SpeechSynthesizer _synthesizer = new();
    private readonly MediaPlayer _mediaPlayer = new();
    private double _rate = 1.0;
    private double _volume = 1.0;

    public bool IsSpeaking { get; private set; }
    public bool IsAvailable => true; // Built-in Windows SpeechSynthesizer is always available on Windows 10/11

    public TextToSpeechService()
    {
        _mediaPlayer.Volume = _volume;
    }

    public IReadOnlyList<VoiceInformation> GetAvailableVoices()
    {
        return SpeechSynthesizer.AllVoices.ToList();
    }

    public Task SetVoiceAsync(string voiceId)
    {
        var voice = SpeechSynthesizer.AllVoices.FirstOrDefault(v => v.Id == voiceId);
        if (voice != null)
        {
            _synthesizer.Voice = voice;
        }
        return Task.CompletedTask;
    }

    public Task SetRateAsync(double rate)
    {
        _rate = Math.Clamp(rate, 0.5, 3.0);
        _synthesizer.Options.SpeakingRate = _rate;
        return Task.CompletedTask;
    }

    public Task SetVolumeAsync(double volume)
    {
        _volume = Math.Clamp(volume, 0.0, 1.0);
        _mediaPlayer.Volume = _volume;
        return Task.CompletedTask;
    }

    public async Task SpeakAsync(string text, CancellationToken ct = default)
    {
        await StopAsync();

        var plainText = StripMarkdown(text);
        if (string.IsNullOrWhiteSpace(plainText)) return;

        try
        {
            var stream = await _synthesizer.SynthesizeTextToStreamAsync(plainText);
            
            var tcs = new TaskCompletionSource<bool>();
            
            _mediaPlayer.Source = MediaSource.CreateFromStream(stream, stream.ContentType);

            void OnMediaEnded(MediaPlayer sender, object args) => tcs.TrySetResult(true);
            void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args) => tcs.TrySetException(new Exception($"TTS Playback failed: {args.Error}"));

            _mediaPlayer.MediaEnded += OnMediaEnded;
            _mediaPlayer.MediaFailed += OnMediaFailed;

            using (ct.Register(() =>
            {
                _mediaPlayer.Pause();
                _mediaPlayer.Position = TimeSpan.Zero;
                tcs.TrySetCanceled();
            }))
            {
                IsSpeaking = true;
                _mediaPlayer.Play();
                await tcs.Task;
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TextToSpeech] Synthesis or Playback failed: {ex.Message}");
        }
        finally
        {
            IsSpeaking = false;
        }
    }

    public Task StopAsync()
    {
        _mediaPlayer.Pause();
        _mediaPlayer.Position = TimeSpan.Zero;
        IsSpeaking = false;
        return Task.CompletedTask;
    }

    private static string StripMarkdown(string markdown)
    {
        if (string.IsNullOrEmpty(markdown)) return string.Empty;

        // 1. Remove fenced code blocks
        var text = Regex.Replace(markdown, @"```[\s\S]*?```", "");

        // 2. Remove inline code backticks
        text = Regex.Replace(text, @"`([^`]+)`", "$1");

        // 3. Remove bold/italic formatting
        text = Regex.Replace(text, @"\*\*([^*]+)\*\*", "$1");
        text = Regex.Replace(text, @"\*([^*]+)\*", "$1");
        text = Regex.Replace(text, @"__([^_]+)__", "$1");
        text = Regex.Replace(text, @"_([^_]+)_", "$1");

        // 4. Remove links: [text](url) -> keep text
        text = Regex.Replace(text, @"\[([^\]]+)\]\([^)]+\)", "$1");

        // 5. Remove headers: # Header -> Header
        text = Regex.Replace(text, @"^\s*#+\s*(.*)$", "$1", RegexOptions.Multiline);

        // 6. Remove list markers: * Item, - Item, 1. Item
        text = Regex.Replace(text, @"^\s*[-*+]\s+", "", RegexOptions.Multiline);
        text = Regex.Replace(text, @"^\s*\d+\.\s+", "", RegexOptions.Multiline);

        // 7. Remove blockquote markers: > block
        text = Regex.Replace(text, @"^\s*>\s*", "", RegexOptions.Multiline);

        // 8. Normalize spaces and newlines
        text = Regex.Replace(text, @"\r?\n", " ");
        text = Regex.Replace(text, @"\s+", " ").Trim();

        return text;
    }
}
