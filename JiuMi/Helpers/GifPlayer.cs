using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace JiuMi.Helpers;

public class GifPlayer : IDisposable
{
    private readonly Image _targetImage;
    private readonly DispatcherTimer _timer;
    private readonly List<GifFrame> _frames = [];

    private int _currentFrameIndex = 0;
    private bool _disposed = false;
    private Action? _onCompleteAction;
    private Func<bool>? _checkInitComplete;

    public GifPlayer(Image targetImage)
    {
        _targetImage = targetImage;
        _timer = new DispatcherTimer(DispatcherPriority.Render);
        _timer.Tick += Timer_Tick;
    }

    public void Load(string assetUri)
    {
        var uri = new Uri(assetUri);
        using var asset = AssetLoader.Open(uri);

        using var codec = SKCodec.Create(asset) ?? throw new ArgumentException("Failed to parse GIF file");
        _frames.Clear();
        var info = codec.Info;

        using var frameBitmap = new SKBitmap(info.Width, info.Height);

        for (int i = 0; i < codec.FrameCount; i++)
        {
            var frameInfo = codec.FrameInfo[i];

            codec.GetPixels(info, frameBitmap.GetPixels(), new SKCodecOptions(i));

            using var image = SKImage.FromBitmap(frameBitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = data.AsStream();

            var avaloniaBitmap = new Bitmap(stream);

            _frames.Add(new GifFrame
            {
                Image = avaloniaBitmap,
                Duration = TimeSpan.FromMilliseconds(frameInfo.Duration > 0 ? frameInfo.Duration : 100)
            });
        }
    }

    public void Play(Func<bool> checkInitComplete, Action onComplete)
    {
        if (_frames.Count == 0) return;

        _checkInitComplete = checkInitComplete;
        _onCompleteAction = onComplete;

        _currentFrameIndex = 0;
        ShowFrame(_currentFrameIndex);
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _timer.Stop();

        if (_currentFrameIndex >= _frames.Count - 1)
        {
            if (_checkInitComplete?.Invoke() == true)
            {
                _onCompleteAction?.Invoke();
                return;
            }

            _currentFrameIndex = -1;
        }

        _currentFrameIndex++;
        ShowFrame(_currentFrameIndex);
    }

    private void ShowFrame(int index)
    {
        var frame = _frames[index];
        _targetImage.Source = frame.Image;

        _timer.Interval = frame.Duration;
        _timer.Start();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _timer.Stop();
            _timer.Tick -= Timer_Tick;
            foreach (var frame in _frames)
            {
                frame.Image?.Dispose();
            }
            _frames.Clear();
        }

        _disposed = true;
    }

    class GifFrame
    {
        public Bitmap? Image { get; set; }
        public TimeSpan Duration { get; set; }
    }
}
