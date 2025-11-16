using Playnite.SDK;
using PlayState.Enums;
using PlayState.ViewModels;
using PlayState.XInputDotNetPure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlayState
{
    public class GamePadHandler
    {
        private readonly PlayState _playState;
        private readonly PlayStateSettings _settings;
        private readonly PlayStateManagerViewModel _playStateManager;
        private readonly Timer _controllersStateCheckTimer;
        private bool _isCheckRunning;
        private const int _pollingRate = 80;
        private readonly Dictionary<PlayerIndex, GuideButtonTapState> _guideButtonTapStates;
        private static readonly TimeSpan guideButtonTapMaxInterval = TimeSpan.FromMilliseconds(700);
        private static readonly TimeSpan guideButtonTapIdleResetInterval = TimeSpan.FromMilliseconds(2000);

        public GamePadHandler(PlayState playState, PlayStateSettings settings, PlayStateManagerViewModel playStateManager)
        {
            _playState = playState;
            _settings = settings;
            _playStateManager = playStateManager;
            _controllersStateCheckTimer = new Timer(OnControllerTimerElapsed, null, 0, _pollingRate);
            _guideButtonTapStates = Enum.GetValues(typeof(PlayerIndex))
                .Cast<PlayerIndex>()
                .ToDictionary(index => index, _ => new GuideButtonTapState());
        }

        public bool IsAnyControllerConnected()
        {
            for (int i = 0; i <= 3; i++)
            {
                var playerIndex = (PlayerIndex)i;
                var gamePadState = GamePad.GetState(playerIndex);
                if (gamePadState.IsConnected)
                {
                    return true;
                }
            }

            return false;
        }

        private async void OnControllerTimerElapsed(object state)
        {
            if (_isCheckRunning)
            {
                return;
            }

            if (!_settings.EnableControllersHotkeys && !_settings.GuideButtonTriplePressSwitchToFullscreen)
            {
                return;
            }

            _isCheckRunning = true;
            try
            {
                await CheckControllersAsync();
            }
            finally
            {
                _isCheckRunning = false;
            }
        }

        private async Task CheckControllersAsync()
        {
            var maxCheckIndex = _settings.GamePadHotkeysEnableAllControllers ? 3 : 0;
            var anySignalSent = false;
            for (int i = 0; i <= maxCheckIndex; i++)
            {
                var playerIndex = (PlayerIndex)i;
                var gamePadState = GamePad.GetState(playerIndex);

                if (!gamePadState.IsConnected || !gamePadState.IsAnyButtonOrDpadPressed)
                {
                    continue;
                }

                if (HandleGuideButtonTriplePress(gamePadState, playerIndex))
                {
                    anySignalSent = true;
                    continue;
                }

                if (_playState.IsAnyGameRunning())
                {
                    if (HandleGameRunningHotkeys(gamePadState))
                    {
                        anySignalSent = true;
                    }
                }
                else
                {
                    if (HandleGameNotRunningHotkeys(gamePadState))
                    {
                        anySignalSent = true;
                    }
                }
            }

            // To prevent events from firing continously if the
            // buttons keep being pressed
            if (anySignalSent)
            {
                await Task.Delay(350);
            }
        }

        private bool HandleGameRunningHotkeys(GamePadState gamePadState)
        {
            if (_settings.GamePadInformationHotkeyEnable && _settings.GamePadInformationHotkey?.IsGamePadStateEqual(gamePadState) == true)
            {
                _playStateManager.ShowCurrentGameStatusNotification();
                return true;
            }

            if (_settings.GamePadSuspendHotkeyEnable && _settings.GamePadSuspendHotkey?.IsGamePadStateEqual(gamePadState) == true)
            {
                _playStateManager.SwitchCurrentGameState();
                return true;
            }

            if (_settings.GamePadMinimizeMaximizeHotkeyEnable && _settings.GamePadMinimizeMaximizeHotkey?.IsGamePadStateEqual(gamePadState) == true)
            {
                _playStateManager.SwitchMinimizeMaximizeCurrentGame();
                return true;
            }

            foreach (var comboHotkey in _settings.GamePadToHotkeyCollection)
            {
                if (IsValidGamePadHotkeyMode(comboHotkey.Mode, GamePadToKeyboardHotkeyModes.OnGameRunning))
                {
                    if (comboHotkey.GamePadHotKey.IsGamePadStateEqual(gamePadState))
                    {
                        Input.InputSender.SendHotkeyInput(comboHotkey.KeyboardHotkey);
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HandleGameNotRunningHotkeys(GamePadState gamePadState)
        {
            foreach (var comboHotkey in _settings.GamePadToHotkeyCollection)
            {
                if (IsValidGamePadHotkeyMode(comboHotkey.Mode, GamePadToKeyboardHotkeyModes.OnGameNotRunning))
                {
                    if (comboHotkey.GamePadHotKey.IsGamePadStateEqual(gamePadState))
                    {
                        Input.InputSender.SendHotkeyInput(comboHotkey.KeyboardHotkey);
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsValidGamePadHotkeyMode(GamePadToKeyboardHotkeyModes mode, GamePadToKeyboardHotkeyModes targetMode)
        {
            return mode == GamePadToKeyboardHotkeyModes.Always || mode == targetMode;
        }

        private bool HandleGuideButtonTriplePress(GamePadState gamePadState, PlayerIndex playerIndex)
        {
            if (!_settings.GuideButtonTriplePressSwitchToFullscreen)
            {
                return false;
            }

            if (!_guideButtonTapStates.TryGetValue(playerIndex, out var tracker))
            {
                return false;
            }

            var now = DateTime.UtcNow;
            var guidePressed = gamePadState.Buttons.Guide == ButtonState.Pressed;
            if (guidePressed)
            {
                if (!tracker.WasPressed)
                {
                    if ((now - tracker.LastPressTime) <= guideButtonTapMaxInterval)
                    {
                        tracker.TapCount += 1;
                    }
                    else
                    {
                        tracker.TapCount = 1;
                    }

                    tracker.LastPressTime = now;
                    if (tracker.TapCount >= 3)
                    {
                        tracker.Reset();
                        return _playState.TryHandleGuideTriplePressSwitch();
                    }
                }

                tracker.WasPressed = true;
            }
            else
            {
                if (tracker.WasPressed && (now - tracker.LastPressTime) > guideButtonTapIdleResetInterval)
                {
                    tracker.Reset();
                }

                tracker.WasPressed = false;
            }

            return false;
        }

        private class GuideButtonTapState
        {
            public int TapCount { get; set; }
            public DateTime LastPressTime { get; set; } = DateTime.MinValue;
            public bool WasPressed { get; set; }

            public void Reset()
            {
                TapCount = 0;
                LastPressTime = DateTime.MinValue;
                WasPressed = false;
            }
        }
    }
}
