using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using Whisper.net;
using Windows.Media.SpeechRecognition;

namespace KaiROS.AI.WinUI.Services;

public class SpeechToTextService : ISpeechToTextService
{
    private readonly IDownloadService _downloadService;
    private readonly IUserPreferencesService _preferences;

    private readonly string _modelsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KaiROS.AI", "whisper-models");

    private WaveInEvent? _waveIn;
    private readonly List<byte> _recordedBytes = new();
    private readonly object _audioLock = new();
    private System.Timers.Timer? _partialTranscribeTimer;
    private bool _isProcessingPartial = false;
    private string _modelPath = "";
    private WhisperFactory? _activeFactory;
    private WhisperProcessor? _activeProcessor;

    // Fallback fields
    private SpeechRecognizer? _fallbackRecognizer;
    private string _accumulatedFallbackText = string.Empty;

    public bool IsListening { get; private set; }
    public bool IsAvailable => true; // Built-in Windows SpeechRecognizer fallback is always available

    public bool IsDownloadingModel { get; private set; }
    public double DownloadProgress { get; private set; }

    public event EventHandler<string>? TranscriptionCompleted;
    public event EventHandler<string>? PartialTranscription;
    public event EventHandler<float>? AudioLevelChanged;
    public event EventHandler<double>? DownloadProgressChanged;

    public SpeechToTextService(IDownloadService downloadService, IUserPreferencesService preferences)
    {
        _downloadService = downloadService;
        _preferences = preferences;
        Directory.CreateDirectory(_modelsDir);
    }

    public async Task<bool> EnsureModelDownloadedAsync(string modelSize, CancellationToken ct = default)
    {
        var fileName = $"ggml-{modelSize}.bin";
        var localPath = Path.Combine(_modelsDir, fileName);

        if (File.Exists(localPath))
        {
            return true;
        }

        IsDownloadingModel = true;
        DownloadProgress = 0;
        DownloadProgressChanged?.Invoke(this, 0);

        var url = $"https://huggingface.co/sandrohanea/whisper.net/resolve/main/classic/ggml-{modelSize}.bin";

        var progress = new Progress<double>(p =>
        {
            DownloadProgress = p;
            DownloadProgressChanged?.Invoke(this, p);
        });

        try
        {
            var success = await _downloadService.DownloadFileAsync(url, localPath, progress, ct);
            return success && File.Exists(localPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Whisper] Download failed: {ex.Message}");
            return false;
        }
        finally
        {
            IsDownloadingModel = false;
            DownloadProgress = 100;
            DownloadProgressChanged?.Invoke(this, 100);
        }
    }

    public async Task StartListeningAsync(CancellationToken ct = default)
    {
        if (IsListening) return;

        _recordedBytes.Clear();
        _accumulatedFallbackText = string.Empty;

        var modelSize = _preferences.WhisperModelSize;
        var modelFile = $"ggml-{modelSize}.bin";
        _modelPath = Path.Combine(_modelsDir, modelFile);

        if (File.Exists(_modelPath))
        {
            // Use local Whisper
            IsListening = true;

            int deviceNumber = 0;
            if (!string.IsNullOrEmpty(_preferences.SelectedInputDevice))
            {
                for (int i = 0; i < WaveIn.DeviceCount; i++)
                {
                    var capabilities = WaveIn.GetCapabilities(i);
                    if (capabilities.ProductName == _preferences.SelectedInputDevice)
                    {
                        deviceNumber = i;
                        break;
                    }
                }
            }

            try
            {
                // Cache factory and processor for active session
                _activeFactory = WhisperFactory.FromPath(_modelPath);
                _activeProcessor = _activeFactory.CreateBuilder()
                    .WithLanguage("auto")
                    .Build();

                _waveIn = new WaveInEvent
                {
                    DeviceNumber = deviceNumber,
                    WaveFormat = new WaveFormat(16000, 16, 1)
                };

                _waveIn.DataAvailable += (s, e) =>
                {
                    lock (_audioLock)
                    {
                        _recordedBytes.AddRange(e.Buffer.Take(e.BytesRecorded));
                    }

                    // Calculate peak level for level meter (0.0 to 1.0)
                    float max = 0;
                    for (int i = 0; i < e.BytesRecorded; i += 2)
                    {
                        if (i + 1 < e.BytesRecorded)
                        {
                            short sample = (short)((e.Buffer[i + 1] << 8) | e.Buffer[i]);
                            float val = Math.Abs(sample / 32768f);
                            if (val > max) max = val;
                        }
                    }
                    AudioLevelChanged?.Invoke(this, max);
                };

                _waveIn.StartRecording();

                _partialTranscribeTimer = new System.Timers.Timer(1500);
                _partialTranscribeTimer.Elapsed += async (s, e) => await ProcessPartialTranscriptionAsync();
                _partialTranscribeTimer.Start();
            }
            catch (Exception ex)
            {
                IsListening = false;
                CleanupWhisperResources();
                System.Diagnostics.Debug.WriteLine($"[SpeechToText] Whisper init/recording failed: {ex.Message}");
                throw;
            }
        }
        else
        {
            // Use Windows SpeechRecognizer Fallback
            IsListening = true;
            try
            {
                await StartFallbackListeningAsync();
            }
            catch (Exception ex)
            {
                IsListening = false;
                System.Diagnostics.Debug.WriteLine($"[SpeechToText] UWP Fallback failed: {ex.Message}");
                throw;
            }
        }
    }

    private void CleanupWhisperResources()
    {
        try
        {
            _activeProcessor?.Dispose();
        }
        catch { }
        _activeProcessor = null;

        try
        {
            _activeFactory?.Dispose();
        }
        catch { }
        _activeFactory = null;
    }

    private async Task StartFallbackListeningAsync()
    {
        _fallbackRecognizer = new SpeechRecognizer();
        await _fallbackRecognizer.CompileConstraintsAsync();
        _fallbackRecognizer.ContinuousRecognitionSession.ResultGenerated += (s, e) =>
        {
            if (e.Result.Status == SpeechRecognitionResultStatus.Success)
            {
                var text = e.Result.Text;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    _accumulatedFallbackText += " " + text;
                    PartialTranscription?.Invoke(this, _accumulatedFallbackText.Trim());
                }
            }
        };
        await _fallbackRecognizer.ContinuousRecognitionSession.StartAsync();
    }

    private async Task ProcessPartialTranscriptionAsync()
    {
        if (_isProcessingPartial) return;
        _isProcessingPartial = true;

        try
        {
            byte[] bytesToProcess;
            lock (_audioLock)
            {
                bytesToProcess = _recordedBytes.ToArray();
            }

            if (bytesToProcess.Length == 0 || _activeProcessor == null) return;

            using var ms = new MemoryStream();
            using (var writer = new WaveFileWriter(ms, new WaveFormat(16000, 16, 1)))
            {
                writer.Write(bytesToProcess, 0, bytesToProcess.Length);
                writer.Flush();
            }
            ms.Position = 0;

            string partialText = "";
            await foreach (var segment in _activeProcessor.ProcessAsync(ms))
            {
                partialText += segment.Text;
            }

            PartialTranscription?.Invoke(this, partialText.Trim());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Whisper] Partial transcription failed: {ex.Message}");
        }
        finally
        {
            _isProcessingPartial = false;
        }
    }

    public async Task StopListeningAsync()
    {
        if (!IsListening) return;

        if (_waveIn != null)
        {
            _partialTranscribeTimer?.Stop();
            _partialTranscribeTimer?.Dispose();
            _partialTranscribeTimer = null;

            try
            {
                _waveIn.StopRecording();
            }
            catch { }
            _waveIn.Dispose();
            _waveIn = null;

            byte[] finalBytes;
            lock (_audioLock)
            {
                finalBytes = _recordedBytes.ToArray();
            }

            IsListening = false;

            string result = "";
            if (finalBytes.Length > 0 && _activeProcessor != null)
            {
                try
                {
                    using var ms = new MemoryStream();
                    using (var writer = new WaveFileWriter(ms, new WaveFormat(16000, 16, 1)))
                    {
                        writer.Write(finalBytes, 0, finalBytes.Length);
                        writer.Flush();
                    }
                    ms.Position = 0;

                    await foreach (var segment in _activeProcessor.ProcessAsync(ms))
                    {
                        result += segment.Text;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Whisper] Final transcription failed: {ex.Message}");
                }
            }

            CleanupWhisperResources();
            TranscriptionCompleted?.Invoke(this, result.Trim());
        }
        else if (_fallbackRecognizer != null)
        {
            IsListening = false;
            try
            {
                await _fallbackRecognizer.ContinuousRecognitionSession.StopAsync();
            }
            catch { }

            _fallbackRecognizer.Dispose();
            _fallbackRecognizer = null;
            TranscriptionCompleted?.Invoke(this, _accumulatedFallbackText.Trim());
        }
    }

    public async Task<string> TranscribeFileAsync(string audioFilePath)
    {
        if (!File.Exists(audioFilePath)) return "";

        var modelSize = _preferences.WhisperModelSize;
        var modelFile = $"ggml-{modelSize}.bin";
        _modelPath = Path.Combine(_modelsDir, modelFile);

        if (!File.Exists(_modelPath))
        {
            return "";
        }

        try
        {
            using var stream = File.OpenRead(audioFilePath);
            using var factory = WhisperFactory.FromPath(_modelPath);
            using var processor = factory.CreateBuilder()
                .WithLanguage("auto")
                .Build();

            string text = "";
            await foreach (var segment in processor.ProcessAsync(stream))
            {
                text += segment.Text;
            }
            return text.Trim();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Whisper] File transcription failed: {ex.Message}");
            return "";
        }
    }
}
