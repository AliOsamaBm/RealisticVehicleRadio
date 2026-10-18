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

using GTA;
using GTA.Native;

namespace RealisticVehicleRadio
{
    public static class KeybindOverlay
    {
        public static string FullText { get; private set; } = string.Empty;
        public static string TypedText { get; private set; } = string.Empty;
        public static int Alpha { get; private set; } = 0;

        // Reads the waiting state directly from Menus to avoid circular dependencies
        public static bool IsActive => Menus.WaitingForKeyAssignment || Alpha > 0;

        private static int _typingIndex;
        private static int _lastTypeTime;
        private static float _alpha;

        private const int _TYPE_SPEED = 25;       // ms per character
        private const float _MAX_ALPHA = 170f;
        private const float _FADE_SPEED = 150f;
        private const float _TEXT_EASING_POWER = 0.75f;

        public static void Start(string text)
        {
            FullText = text ?? string.Empty;
            TypedText = string.Empty;
            _typingIndex = 0;
            _lastTypeTime = Game.GameTime;
        }

        public static void UpdateAndDraw()
        {
            UpdateAlpha();
            UpdateTypewriter();

            if (Alpha > 0)
            {
                Draw();
            }
        }

        private static void UpdateAlpha()
        {
            if (Menus.WaitingForKeyAssignment)
            {
                _alpha = Math.Min(_alpha + _FADE_SPEED * Game.LastFrameTime, _MAX_ALPHA);
            }
            else if (_alpha > 0f)
            {
                _alpha = Math.Max(_alpha - _FADE_SPEED * Game.LastFrameTime, 0f);
            }

            Alpha = (int)_alpha;
        }

        private static void UpdateTypewriter()
        {
            if (!Menus.WaitingForKeyAssignment || _typingIndex >= FullText.Length)
            {
                return;
            }

            if (Game.GameTime - _lastTypeTime < _TYPE_SPEED)
            {
                return;
            }

            _lastTypeTime = Game.GameTime;
            _typingIndex++;

            SkipColorCodeIfPresent();

            TypedText = FullText.Substring(0, _typingIndex);
        }

        private static void SkipColorCodeIfPresent()
        {
            if (_typingIndex >= FullText.Length || FullText[_typingIndex - 1] != '~')
            {
                return;
            }

            // Skip color codes like ~r~ or ~y~ so they do not appear as raw text
            while (_typingIndex < FullText.Length && FullText[_typingIndex] != '~')
            {
                _typingIndex++;
            }

            if (_typingIndex < FullText.Length)
            {
                _typingIndex++;
            }
        }

        private static void Draw()
        {
            int easedAlpha = GetEasedAlpha();

            DrawFullscreenDim(easedAlpha);
            DrawCenteredText(TypedText, GetTextAlpha(easedAlpha));
        }

        private static int GetEasedAlpha()
        {
            float t = Alpha / _MAX_ALPHA;
            t = SmoothStep(t);
            return (int)(t * _MAX_ALPHA);
        }

        private static int GetTextAlpha(int easedAlpha)
        {
            if (!Menus.WaitingForKeyAssignment)
            {
                return easedAlpha;
            }

            float normalizedAlpha = Alpha / _MAX_ALPHA;
            return (int)(Math.Pow(normalizedAlpha, _TEXT_EASING_POWER) * _MAX_ALPHA);
        }

        private static float SmoothStep(float t)
        {
            return t * t * (3f - 2f * t);
        }

        private static void DrawFullscreenDim(int alpha)
        {
            Function.Call(Hash.DRAW_RECT, 0.5f, 0.5f, 1.0f, 1.0f, 0, 0, 0, alpha);
        }

        private static void DrawCenteredText(string text, int alpha)
        {
            Function.Call(Hash.SET_TEXT_FONT, 0);
            Function.Call(Hash.SET_TEXT_SCALE, 0.5f, 0.45f);
            Function.Call(Hash.SET_TEXT_COLOUR, 255, 255, 255, alpha);
            Function.Call(Hash.SET_TEXT_CENTRE, true);
            Function.Call(Hash.SET_TEXT_OUTLINE);
            Function.Call(Hash.SET_TEXT_DROP_SHADOW);

            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "STRING");
            Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, text);
            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, 0.5f, 0.5f);
        }
    }
}