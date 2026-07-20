using System;
using System.Threading;
using System.Threading.Tasks;
using DeskBrightness.Config;
using DeskBrightness.Core.Brightness;

namespace DeskBrightness.Services
{
    public sealed class SmoothBrightnessController : IBrightnessController
    {
        private readonly IBrightnessController _inner;
        private readonly object _gate = new();
        private byte? _current;
        private CancellationTokenSource? _runningCts;

        public SmoothBrightnessController(IBrightnessController inner)
        {
            _inner = inner;
        }

        public async Task SetBrightnessAsync(byte percent, CancellationToken cancellationToken = default)
        {
            if (percent > 100) percent = 100;

            var previous = _current;
            if (previous == percent) return;

            CancellationTokenSource? prev;
            lock (_gate) { prev = _runningCts; _runningCts = null; }
            if (prev is not null) { try { prev.Cancel(); } catch { } try { prev.Dispose(); } catch { } }

            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            lock (_gate) { _runningCts = cts; }

            int dir = percent > (previous ?? percent) ? 1 : -1;
            byte start = previous ?? percent;

            try
            {
                if (previous is null || Math.Abs(percent - previous.Value) <= AppConfig.Timing.SmoothStepSmallChange)
                {
                    await _inner.SetBrightnessAsync(percent, cts.Token);
                    _current = percent;
                    return;
                }

                for (int v = start + dir; ; v += dir)
                {
                    byte applied = (byte)Math.Clamp(v, 0, 100);
                    await _inner.SetBrightnessAsync(applied, cts.Token);
                    _current = applied;

                    if (applied == percent) break;

                    cts.Token.ThrowIfCancellationRequested();
                    try { await Task.Delay(25, cts.Token); }
                    catch (OperationCanceledException) { return; }
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                lock (_gate) { if (_runningCts == cts) _runningCts = null; }
                cts.Dispose();
            }
        }
    }
}