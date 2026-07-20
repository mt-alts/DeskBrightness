using System;
using System.Collections.Generic;
using System.Text;
using DeskBrightness.Core.Brightness;

namespace DeskBrightness.Services
{
    public sealed class DeskBrightnessRuntime
    {
        private readonly ForwardedLuxSource _luxSource;
        private readonly BrightnessDecisionEngine _decisionEngine;
        private readonly IBrightnessController _brightnessController;
        private readonly LocalizationService _localization;
        private readonly System.Threading.SemaphoreSlim _semaphore = new(1, 1);

        private CancellationTokenSource? _cts;
        private bool _isRunning;

        public event EventHandler<DeskBrightnessRuntimeState>? StateChanged;

        public event EventHandler<DeskBrightnessRuntimeLog>? LogReceived;

        public DeskBrightnessRuntime(
            ForwardedLuxSource luxSource,
            BrightnessDecisionEngine decisionEngine,
            IBrightnessController brightnessController,
            LocalizationService localization
        )
        {
            _luxSource = luxSource;
            _decisionEngine = decisionEngine;
            _brightnessController = brightnessController;
            _localization = localization;

            _luxSource.SampleReceived += OnSampleReceived;
            _luxSource.BrightnessReceived += OnBrightnessReceived;
            _luxSource.LogReceived += (_, log) => PublishLog(log.Level, log.Message);
        }

        public bool IsRunning => _isRunning;

        public async Task StartAsync()
        {
            if (_isRunning)
                return;

            _cts = new CancellationTokenSource();

            PublishLog("INFO", _localization.Get("RuntimePreparing"));

            await _luxSource.StartAsync(_cts.Token);

            _isRunning = true;

            PublishState("Started", null, null, null);
            PublishLog("INFO", _localization.Get("RuntimeStarted"));
        }

        public async Task StopAsync()
        {
            lock (this)
            {
                if (!_isRunning)
                    return;
                _isRunning = false;
            }

            if (_cts is not null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            await _luxSource.StopAsync();

            PublishState("Stopped", null, null, null);
            PublishLog("INFO", _localization.Get("RuntimeStopped"));
        }

        private async void OnSampleReceived(
            object? sender,
            DeskBrightness.Core.Sensors.LuxSample sample
        )
        {
            await _semaphore.WaitAsync();
            try
            {
                var decision = _decisionEngine.Evaluate(sample);

                if (decision.ShouldApply && decision.TargetBrightness is not null)
                {

                    await _brightnessController.SetBrightnessAsync(
                        decision.TargetBrightness.Value.Percent
                    );

                    PublishState(
                        "Applied",
                        sample.Lux,
                        decision.SmoothedLux,
                        decision.TargetBrightness.Value.Percent
                    );
                }
                else
                {
                    PublishState("NoChange", sample.Lux, decision.SmoothedLux, null);
                }
            }
            catch (Exception ex)
            {
                if (ex is AggregateException aggEx)
                {
                    PublishLog("ERROR", $"{_localization.Get("BrightnessApplyError")}: {aggEx.Message}");
                }
                else
                {
                    PublishLog("ERROR", $"{_localization.Get("BrightnessApplyError")}: {ex.Message}");
                }

                PublishState(ex.Message, sample.Lux, null, null);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private async void OnBrightnessReceived(
            object? sender,
            MobileBrightnessSample sample
        )
        {
            await _semaphore.WaitAsync();
            try
            {
                PublishLog("INFO", _localization.Get("AndroidBrightnessReceived"));

                await _brightnessController.SetBrightnessAsync(sample.Percent);

                PublishState("AndroidBrightness", null, null, sample.Percent);
            }
            catch (Exception ex)
            {
                if (ex is AggregateException aggEx)
                {
                    PublishLog("ERROR", $"{_localization.Get("BrightnessApplyFailed")}: {aggEx.Message}");
                }
                else
                {
                    PublishLog("ERROR", $"{_localization.Get("BrightnessApplyFailed")}: {ex.Message}");
                }

                PublishState(ex.Message, null, null, null);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private void PublishState(
            string message,
            double? rawLux,
            double? smoothedLux,
            byte? brightness
        )
        {
            StateChanged?.Invoke(
                this,
                new DeskBrightnessRuntimeState(
                    message,
                    rawLux,
                    smoothedLux,
                    brightness,
                    DateTimeOffset.Now
                )
            );
        }

        private void PublishLog(string level, string message)
        {
            LogReceived?.Invoke(
                this,
                new DeskBrightnessRuntimeLog(level, message, DateTimeOffset.Now)
            );
        }
    }

    public sealed record DeskBrightnessRuntimeState(
        string Message,
        double? RawLux,
        double? SmoothedLux,
        byte? Brightness,
        DateTimeOffset Timestamp
    );

    public sealed record DeskBrightnessRuntimeLog(
        string Level,
        string Message,
        DateTimeOffset Timestamp
    );
}
