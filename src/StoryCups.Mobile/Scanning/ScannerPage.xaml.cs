using System.Diagnostics;
using BarcodeScanning;

namespace StoryCups.Mobile.Scanning;

/// <summary>
/// Native страница с камерата (BarcodeScanning.Native.Maui; на Android с Google ML Kit).
/// Камерата се включва при показване и се изключва при затваряне, както препоръчва пакетът.
/// </summary>
public partial class ScannerPage : ContentPage
{
    private readonly TaskCompletionSource<ScanOutcome> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Stopwatch stopwatch = new();

    public ScannerPage()
    {
        InitializeComponent();
    }

    public Task<ScanOutcome> Result => completion.Task;

    protected override void OnAppearing()
    {
        base.OnAppearing();
        stopwatch.Restart();
        Camera.CameraEnabled = true;
    }

    protected override void OnDisappearing()
    {
        Camera.CameraEnabled = false;

        // Бутонът „назад“ на Android затваря страницата без резултат.
        completion.TrySetResult(new ScanOutcome(ScanStatus.Cancelled));
        base.OnDisappearing();
    }

    private void OnDetectionFinished(object? sender, OnDetectionFinishedEventArg e)
    {
        var first = e.BarcodeResults.FirstOrDefault();
        if (first is null || completion.Task.IsCompleted)
        {
            return;
        }

        stopwatch.Stop();
        Camera.PauseScanning = true;
        completion.TrySetResult(new ScanOutcome(ScanStatus.Scanned, first.RawValue, stopwatch.Elapsed));
        MainThread.BeginInvokeOnMainThread(async () => await Navigation.PopModalAsync());
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        completion.TrySetResult(new ScanOutcome(ScanStatus.Cancelled));
        await Navigation.PopModalAsync();
    }
}
