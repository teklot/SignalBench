using Microsoft.Extensions.Logging;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Timers;

namespace SignalBench.ViewModels;

public partial class MainWindowViewModel
{
    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (SetProperty(ref _isPlaying, value))
            {
                OnPropertyChanged(nameof(PlayPauseIcon));
                OnPropertyChanged(nameof(PlayPauseText));
            }
        }
    }

    public string PlayPauseIcon => IsPlaying ? "Pause" : "Play";
    public string PlayPauseText => IsPlaying ? "Pause" : "Play";

    public ObservableCollection<string> PlaybackSpeeds { get; } = ["0.5x", "1x", "2x", "5x", "10x", "100x", "1000x"];

    private double _playbackSpeed = 1.0;
    public double PlaybackSpeed
    {
        get => _playbackSpeed;
        set => SetProperty(ref _playbackSpeed, value);
    }

    private int _currentPlaybackIndex = 0;
    public int CurrentPlaybackIndex
    {
        get => _currentPlaybackIndex;
        set
        {
            if (SelectedPlot != null && SelectedPlot.TotalRecords > 0)
            {
                var val = Math.Clamp(value, 0, SelectedPlot.TotalRecords - 1);
                if (SetProperty(ref _currentPlaybackIndex, val))
                {
                    SelectedPlot.CurrentPlaybackIndex = val;
                    OnPropertyChanged(nameof(PlaybackProgress));
                    OnPropertyChanged(nameof(CurrentPlaybackTime));
                    OnPropertyChanged(nameof(FormattedPlaybackTime));
                    RefreshCurrentValues();
                    
                    // Update the plot cursor
                    if (SelectedPlot.PlaybackTimestamps.Count > val)
                    {
                        SelectedPlot.RequestCursorUpdate?.Invoke(SelectedPlot.PlaybackTimestamps[val]);
                    }
                }
            }
        }
    }

    private double _playbackProgressValue;
    public double PlaybackProgress
    {
        get => _playbackProgressValue;
        set
        {
            if (SelectedPlot != null && SelectedPlot.TotalRecords > 1)
            {
                var index = (int)(value / 100.0 * (SelectedPlot.TotalRecords - 1));
                CurrentPlaybackIndex = index;
            }
            SetProperty(ref _playbackProgressValue, value);
        }
    }

    private int _totalRecords;
    public int TotalRecords => SelectedPlot?.TotalRecords ?? 0;

    private List<DateTime> _playbackTimestamps = [];
    private Dictionary<string, List<double>> _playbackSignalData = [];

    public DateTime CurrentPlaybackTime
    {
        get
        {
            if (SelectedPlot == null || SelectedPlot.PlaybackTimestamps.Count == 0) return DateTime.MinValue;
            var idx = Math.Clamp(CurrentPlaybackIndex, 0, SelectedPlot.PlaybackTimestamps.Count - 1);
            return SelectedPlot.PlaybackTimestamps[idx];
        }
    }

    public string FormattedPlaybackTime => CurrentPlaybackTime == DateTime.MinValue ? "--:--:--.---" : CurrentPlaybackTime.ToString("HH:mm:ss.fff");

    private System.Timers.Timer? _playbackTimer;
    private Stopwatch? _playbackStopwatch;
    private double _savedElapsedSeconds = 0;
    private double _fullDuration = 0;

    private void PlayPause()
    {
        if (IsPlaying)
        {
            Pause();
        }
        else
        {
            Play();
        }
    }

    private void Play()
    {
        if (SelectedPlot == null || SelectedPlot.TotalRecords == 0) return;

        if (CurrentPlaybackIndex >= SelectedPlot.TotalRecords - 1)
        {
            CurrentPlaybackIndex = 0;
            _savedElapsedSeconds = 0;
        }

        IsPlaying = true;
        _playbackStopwatch = Stopwatch.StartNew();

        if (_playbackTimer == null)
        {
            _playbackTimer = new System.Timers.Timer(16); // ~60 FPS
            _playbackTimer.Elapsed += PlaybackTimer_Elapsed;
        }

        // Calculate full duration for speed control
        if (SelectedPlot.PlaybackTimestamps.Count > 1)
        {
            _fullDuration = (SelectedPlot.PlaybackTimestamps.Last() - SelectedPlot.PlaybackTimestamps.First()).TotalSeconds;
        }

        _playbackTimer.Start();
    }

    private void Pause()
    {
        IsPlaying = false;
        _playbackTimer?.Stop();
        if (_playbackStopwatch != null)
        {
            _savedElapsedSeconds += _playbackStopwatch.Elapsed.TotalSeconds * PlaybackSpeed;
            _playbackStopwatch = null;
        }
    }

    private void PlaybackTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        if (SelectedPlot == null || _playbackStopwatch == null) return;

        var elapsedSeconds = _savedElapsedSeconds + (_playbackStopwatch.Elapsed.TotalSeconds * PlaybackSpeed);
        var startTime = SelectedPlot.PlaybackTimestamps.FirstOrDefault();

        // Find the index where timestamp is >= startTime + elapsedSeconds
        var targetTime = startTime.AddSeconds(elapsedSeconds);

        // Binary search for the index
        int index = SelectedPlot.PlaybackTimestamps.BinarySearch(targetTime);
        if (index < 0) index = ~index;

        if (index >= SelectedPlot.TotalRecords)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                CurrentPlaybackIndex = SelectedPlot.TotalRecords - 1;
                Pause();
            });
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                CurrentPlaybackIndex = index;
            });
        }
    }

    private void SetSpeed(string speedStr)
    {
        if (double.TryParse(speedStr.Replace("x", ""), out var speed))
        {
            var wasPlaying = IsPlaying;
            if (wasPlaying) Pause();
            PlaybackSpeed = speed;
            if (wasPlaying) Play();
        }
    }

    private void Seek(double percent)
    {
        PlaybackProgress = percent;
        if (SelectedPlot != null)
        {
            var startTime = SelectedPlot.PlaybackTimestamps.FirstOrDefault();
            var currentTime = CurrentPlaybackTime;
            _savedElapsedSeconds = (currentTime - startTime).TotalSeconds;
            if (_playbackStopwatch != null) _playbackStopwatch = Stopwatch.StartNew();
        }
    }

    private void StepForward()
    {
        Pause();
        CurrentPlaybackIndex++;
    }

    private void StepBackward()
    {
        Pause();
        CurrentPlaybackIndex--;
    }

    private void FastForward()
    {
        Pause();
        CurrentPlaybackIndex += 100;
    }

    private void FastBackward()
    {
        Pause();
        CurrentPlaybackIndex -= 100;
    }

    private void Restart()
    {
        var wasPlaying = IsPlaying;
        Pause();
        CurrentPlaybackIndex = 0;
        _savedElapsedSeconds = 0;
        if (wasPlaying) Play();
    }
}
