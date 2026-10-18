/*
 * Realistic Vehicle Radio
 * Copyright (C) 2026 Ali Osama
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 2 of the License.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
 *
 * See the GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

using System;

using static RealisticVehicleRadio.Helpers;

using GTA;

namespace RealisticVehicleRadio
{
    public sealed class ThunderInterference
    {
        /// <summary>
        /// Returns true if the radio should be completely muted (interference burst).
        /// </summary>
        public bool IsActive => _isActive;

        /// <summary>
        /// The raw target signal level dictated by the storm (1.0 = perfect, lower = static).
        /// </summary>
        public float TargetSignalLevel { get; private set; } = 1f;

        private enum State
        {
            Inactive,
            Waiting,
            Interfering
        }

        private State _state = State.Inactive;
        private bool _isActive = false;
        private int _nextTransitionTime = 0;

        public void Update(Configuration.ThunderstormSettings thunderCfg, int now)
        {
            bool isThunderstorm = World.Weather == Weather.ThunderStorm;

            if (!isThunderstorm)
            {
                if (_isActive || _state != State.Inactive)
                {
                    _isActive = false;
                    _state = State.Inactive;
                }
                TargetSignalLevel = 1f;
                return;
            }

            // ==========================================
            // 1. CALCULATE BACKGROUND STORM STATIC
            // ==========================================
            TargetSignalLevel = 1f;
            if (thunderCfg.EnableThunderstormStatic)
            {
                float time = now / 1000f;

                // 1. PERLIN NOISE: Smooth, organic signal drift (The "Swell")
                float baseNoise = MathFunctions.PerlinNoise(time * thunderCfg.StaticDriftSpeed, 0f);
                float normalizedBase = MathFunctions.Clamp(0.5f + baseNoise, 0f, 1f);

                // Map to the configured signal range
                float signalRange = thunderCfg.StaticSignalMax - thunderCfg.StaticSignalMin;
                TargetSignalLevel = thunderCfg.StaticSignalMin + (normalizedBase * signalRange);

                // 2. XORSHIFT RNG: Sharp, unpredictable static pops (The "Crackle")

                if (thunderCfg.EnableStaticPop &&
                    thunderCfg.StaticPopMaxIntensity > 0f)
                {
                    float frameTime = Game.LastFrameTime;

                    if (frameTime > 0f)
                    {
                        float chancePerSecond = thunderCfg.StaticPopChancePercentage / 100f;

                        double chancePerFrame = 1.0 - Math.Pow(1.0 - chancePerSecond, frameTime);

                        if (MathFunctions.NextDouble() < chancePerFrame)
                        {
                            float spike = (float)MathFunctions.NextDouble(0f, thunderCfg.StaticPopMaxIntensity);

                            TargetSignalLevel -= spike;
                        }
                    }
                }

                // 3. HIGH-FREQUENCY PERLIN: Fast hiss
                float crackleNoise = MathFunctions.PerlinNoise(time * 15f, 100f);
                float crackleSpike = Math.Max(0f, crackleNoise - 0.2f) * 1.5f;
                TargetSignalLevel -= crackleSpike * thunderCfg.StaticHissIntensity;

                // Clamp final target
                TargetSignalLevel = MathFunctions.Clamp(TargetSignalLevel, 0f, 1f);
            }

            // ==========================================
            // 2. STATE MACHINE FOR RADIO MUTE BURSTS
            // ==========================================
            if (!thunderCfg.EnableThunderstormInterference)
            {
                if (_isActive || _state != State.Inactive)
                {
                    _isActive = false;
                    _state = State.Inactive;
                }
                return;
            }

            if (_state == State.Inactive)
            {
                _isActive = false;
                _state = State.Waiting;
                float delay = RandomThunderSeconds(thunderCfg.InitialInterferenceDelayMin, thunderCfg.InitialInterferenceDelayMax);
                _nextTransitionTime = now + (int)(delay * 1000f);
                return;
            }

            if (now < _nextTransitionTime)
            {
                return;
            }

            if (_state == State.Waiting)
            {
                _isActive = true;
                _state = State.Interfering;
                float duration = RandomThunderSeconds(thunderCfg.InterferenceDurationMin, thunderCfg.InterferenceDurationMax);
                _nextTransitionTime = now + (int)(duration * 1000f);
            }
            else if (_state == State.Interfering)
            {
                _isActive = false;
                _state = State.Waiting;
                float nextDelay = RandomThunderSeconds(thunderCfg.NextInterferenceDelayMin, thunderCfg.NextInterferenceDelayMax);
                _nextTransitionTime = now + (int)(nextDelay * 1000f);
            }
        }

        private float RandomThunderSeconds(float min, float max)
        {
            if (max < min)
            {
                (max, min) = (min, max);
            }

            if (max <= 0f)
            {
                return 0f;
            }

            return (float)MathFunctions.NextDouble(min, max);
        }
    }
}