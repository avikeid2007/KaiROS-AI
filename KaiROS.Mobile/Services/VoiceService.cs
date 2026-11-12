using CommunityToolkit.Maui.Media;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace KaiROS.Mobile.Services;

/// <summary>
/// Service for speech-to-text and text-to-speech functionality.
/// Uses CommunityToolkit.Maui.Media and MAUI Essentials APIs.
/// </summary>
public class VoiceService
{
    // Listening ends if no speech update arrives within this window
    private static readonly TimeSpan SilenceTimeout = TimeSpan.FromSeconds(10);

    private readonly ILogger<VoiceService> _logger;
    private CancellationTokenSource? _listeningCts;
    private CancellationTokenSource? _speakingCts;
    private bool _isListening;
    private bool _isSpeaking;

    public VoiceService(ILogger<VoiceService> logger)
    {
        _logger = logger;
    }

    public bool IsListening => _isListening;
    public bool IsSpeaking => _isSpeaking;

    public event EventHandler<string>? SpeechRecognized;
    public event EventHandler<string>? StatusChanged;
    public event EventHandler? ListeningStarted;
    public event EventHandler? ListeningStopped;

    /// <summary>
    /// Start listening for speech input.
    /// </summary>
    public async Task<string?> ListenAsync(CancellationToken cancellationToken = default)
    {
        if (_isListening)
            return null;

        try
        {
            _isListening = true;
            _listeningCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            ListeningStarted?.Invoke(this, EventArgs.Empty);
            StatusChanged?.Invoke(this, "Listening...");

            // Check microphone permission
            var status = await Permissions.CheckStatusAsync<Permissions.Microphone>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.Microphone>();
                if (status != PermissionStatus.Granted)
                {
                    StatusChanged?.Invoke(this, "Microphone permission denied");
                    return null;
                }
            }

            // Use SpeechToText from CommunityToolkit.Maui.Media
            var speechToText = SpeechToText.Default;

            var tcs = new TaskCompletionSource<SpeechToTextResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            string? lastPartial = null;

            using var silenceCts = new CancellationTokenSource(SilenceTimeout);
            using var listenCts = CancellationTokenSource.CreateLinkedTokenSource(_listeningCts.Token, silenceCts.Token);

            void OnUpdated(object? sender, SpeechToTextRecognitionResultUpdatedEventArgs e)
            {
                lastPartial = e.RecognitionResult;
                silenceCts.CancelAfter(SilenceTimeout);
                SpeechRecognized?.Invoke(this, e.RecognitionResult);
            }

            void OnCompleted(object? sender, SpeechToTextRecognitionResultCompletedEventArgs e) =>
                tcs.TrySetResult(e.RecognitionResult);

            speechToText.RecognitionResultUpdated += OnUpdated;
            speechToText.RecognitionResultCompleted += OnCompleted;

            SpeechToTextResult recognitionResult;
            try
            {
                await speechToText.StartListenAsync(
                    new SpeechToTextOptions
                    {
                        Culture = CultureInfo.CurrentCulture,
                        ShouldReportPartialResults = true
                    },
                    listenCts.Token);

                using (listenCts.Token.Register(() => tcs.TrySetCanceled()))
                {
                    recognitionResult = await tcs.Task;
                }
            }
            catch (OperationCanceledException) when (silenceCts.IsCancellationRequested && !_listeningCts.IsCancellationRequested)
            {
                // Timed out waiting for speech: keep whatever was heard so far
                if (string.IsNullOrWhiteSpace(lastPartial))
                {
                    StatusChanged?.Invoke(this, "No speech detected");
                    return null;
                }

                StatusChanged?.Invoke(this, "Got it!");
                return lastPartial;
            }
            finally
            {
                speechToText.RecognitionResultUpdated -= OnUpdated;
                speechToText.RecognitionResultCompleted -= OnCompleted;

                try
                {
                    await speechToText.StopListenAsync(CancellationToken.None);
                }
                catch (Exception stopEx)
                {
                    _logger.LogWarning(stopEx, "Failed to stop speech recognition");
                }
            }

            if (recognitionResult.IsSuccessful)
            {
                StatusChanged?.Invoke(this, "Got it!");
                return recognitionResult.Text;
            }
            else
            {
                StatusChanged?.Invoke(this, $"Recognition failed: {recognitionResult.Exception?.Message}");
                _logger.LogError(recognitionResult.Exception, "Speech recognition failed");
                return null;
            }
        }
        catch (OperationCanceledException)
        {
            StatusChanged?.Invoke(this, "Listening cancelled");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Listen error");
            StatusChanged?.Invoke(this, $"Error: {ex.Message}");
            return null;
        }
        finally
        {
            _isListening = false;
            _listeningCts?.Dispose();
            _listeningCts = null;
            ListeningStopped?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Stop listening for speech.
    /// </summary>
    public void StopListening()
    {
        _listeningCts?.Cancel();
    }

    /// <summary>
    /// Speak the given text aloud.
    /// </summary>
    public async Task SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text) || _isSpeaking)
            return;

        try
        {
            _isSpeaking = true;
            _speakingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            StatusChanged?.Invoke(this, "Speaking...");

            var options = new SpeechOptions
            {
                Pitch = 1.0f,
                Volume = 1.0f
            };

            await TextToSpeech.Default.SpeakAsync(text, options, _speakingCts.Token);

            StatusChanged?.Invoke(this, "Done speaking");
        }
        catch (OperationCanceledException)
        {
            StatusChanged?.Invoke(this, "Speech cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Speak error");
            StatusChanged?.Invoke(this, $"Speech error: {ex.Message}");
        }
        finally
        {
            _isSpeaking = false;
            _speakingCts?.Dispose();
            _speakingCts = null;
        }
    }

    /// <summary>
    /// Stop speaking.
    /// </summary>
    public void StopSpeaking()
    {
        _speakingCts?.Cancel();
    }

    /// <summary>
    /// Check if speech recognition is available.
    /// </summary>
    public async Task<bool> IsSpeechRecognitionAvailableAsync()
    {
        try
        {
            var speechToText = SpeechToText.Default;
            return await speechToText.RequestPermissions(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Speech recognition availability check failed");
            return false;
        }
    }
}

