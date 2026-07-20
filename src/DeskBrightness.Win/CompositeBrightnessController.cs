using System;
using System.Collections.Generic;
using System.Text;
using DeskBrightness.Core.Brightness;

namespace DeskBrightness.Win
{
    public sealed class CompositeBrightnessController : IBrightnessController
    {
        private readonly IReadOnlyList<IBrightnessController> _controllers;

        public CompositeBrightnessController(IEnumerable<IBrightnessController> controllers)
        {
            _controllers = controllers.ToArray();
        }

        public async Task SetBrightnessAsync(
            byte percent,
            CancellationToken cancellationToken = default
        )
        {
            var successCount = 0;
            var errors = new List<Exception>();

            foreach (var controller in _controllers)
            {
                try
                {
                    await controller.SetBrightnessAsync(percent, cancellationToken);
                    successCount++;
                }
                catch (Exception ex)
                {
                    errors.Add(
                        new InvalidOperationException(
                            $"{controller.GetType().Name} failed: {ex.Message}",
                            ex
                        )
                    );
                }
            }

            if (successCount == 0)
                throw new AggregateException(
                    "No brightness controller could apply the requested brightness.",
                    errors
                );
        }
    }
}
