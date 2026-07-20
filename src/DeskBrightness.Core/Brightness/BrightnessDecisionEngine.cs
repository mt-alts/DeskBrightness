using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using DeskBrightness.Core.Filtering;
using DeskBrightness.Core.Profiles;
using DeskBrightness.Core.Sensors;

namespace DeskBrightness.Core.Brightness
{
    public sealed class BrightnessDecisionEngine
    {
        private readonly ILuxToBrightnessMapper _mapper;
        private readonly Func<string, string> _t;
        private LowPassLuxFilter _lowPassFilter;
        private ThresholdLuxFilter _thresholdFilter;
        private HysteresisFilter _hysteresisFilter;
        private readonly BrightnessProfile _profile;

        private DateTimeOffset? _lastAppliedAt;
        private double _observedMaxLux;

        public BrightnessDecisionEngine(
            ILuxToBrightnessMapper mapper,
            BrightnessProfile profile,
            Func<string, string>? localize = null
        )
        {
            profile.Validate();

            _mapper = mapper;
            _profile = profile;
            _t = localize ?? (key => key);

            _lowPassFilter = new LowPassLuxFilter(profile.LowPassAlpha);

            _thresholdFilter = new ThresholdLuxFilter(profile.LuxThreshold);

            _hysteresisFilter = new HysteresisFilter(profile.BrightnessStepThreshold);

            _observedMaxLux = profile.SensorMaxLux;
        }

        public void Reconfigure()
        {
            _profile.Validate();
            _lowPassFilter = new LowPassLuxFilter(_profile.LowPassAlpha);
            _thresholdFilter = new ThresholdLuxFilter(_profile.LuxThreshold);
            _hysteresisFilter = new HysteresisFilter(_profile.BrightnessStepThreshold);
            _lastAppliedAt = null;
            _observedMaxLux = _profile.SensorMaxLux;
        }

        public BrightnessDecision Evaluate(LuxSample sample)
        {
            if (sample.Lux > _observedMaxLux)
            {
                _observedMaxLux = sample.Lux;
                _profile.SensorMaxLux = _observedMaxLux;
            }

            var filteredLux = _lowPassFilter.Add(sample.Lux);

            if (!_thresholdFilter.ShouldAccept(filteredLux))
            {
                return BrightnessDecision.Skip(filteredLux, _t("DecisionLuxBelowThreshold"));
            }

            var level = _mapper.Map(filteredLux, _profile);

            if (!_hysteresisFilter.ShouldApply(level))
            {
                return BrightnessDecision.Skip(
                    filteredLux,
                    _t("DecisionStepBelowThreshold")
                );
            }

            if (_lastAppliedAt is not null)
            {
                var elapsed = sample.Timestamp - _lastAppliedAt.Value;

                if (elapsed < _profile.MinimumApplyInterval)
                {
                    return BrightnessDecision.Skip(
                        filteredLux,
                        _t("DecisionMinIntervalNotElapsed")
                    );
                }
            }

            _lastAppliedAt = sample.Timestamp;

            return BrightnessDecision.Apply(filteredLux, level);
        }

        public void Reset()
        {
            _lastAppliedAt = null;
            _lowPassFilter.Reset();
            _thresholdFilter.Reset();
            _hysteresisFilter.Reset();
        }
    }

    public sealed class BrightnessDecision
    {
        public bool ShouldApply { get; }
        public double SmoothedLux { get; }
        public BrightnessLevel? TargetBrightness { get; }
        public string Reason { get; }

        public BrightnessDecision(bool shouldApply, double smoothedLux, BrightnessLevel? targetBrightness, string reason)
        {
            ShouldApply = shouldApply;
            SmoothedLux = smoothedLux;
            TargetBrightness = targetBrightness;
            Reason = reason;
        }

        public static BrightnessDecision Apply(double smoothedLux, BrightnessLevel targetBrightness)
        {
            return new BrightnessDecision(true, smoothedLux, targetBrightness, null);
        }

        public static BrightnessDecision Skip(double smoothedLux, string reason)
        {
            return new BrightnessDecision(false, smoothedLux, null, reason);
        }
    }
}
