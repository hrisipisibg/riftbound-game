using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Riftbound
{
    public class SettingsSystem : MonoBehaviour
    {
        public static SettingsSystem Instance { get; private set; }

        public enum SettingsPage
        {
            GameMenu,
            Video,
            Audio,
            Interface,
            HelpKeybinds
        }

        public bool IsMenuOpen { get; set; } = false;
        public SettingsPage CurrentPage { get; set; } = SettingsPage.GameMenu;

        // Settings variables
        public float masterVolume = 1.0f;
        public float musicVolume = 0.75f;
        public float sfxVolume = 0.85f;
        public float ambienceVolume = 0.70f;

        public bool showCombatText = true;
        public bool showEnemyNameplates = true;
        public bool autoDismount = true;
        public bool lockActionBars = false;

        public int graphicsQualityIndex = 2; // 0=Low, 1=Medium, 2=High, 3=Ultra
        public static readonly string[] QualityLevels = new string[] { "Low", "Medium", "High", "Ultra" };

        // Logout countdown
        public bool IsLoggingOut { get; private set; } = false;
        public float LogoutCountdown { get; private set; } = 0f;
        public const float LogoutDuration = 20f;

        PlayerController player;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            LoadSettings();
            player = GetComponent<PlayerController>() ?? FindFirstObjectByType<PlayerController>();
        }

        public void LoadSettings()
        {
            masterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("Riftbound.Volume", 0.8f));
            musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("Riftbound.MusicVolume", 0.75f));
            sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("Riftbound.SfxVolume", 0.85f));
            ambienceVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("Riftbound.AmbienceVolume", 0.70f));
            showCombatText = PlayerPrefs.GetInt("Riftbound.ShowCombatText", 1) != 0;
            showEnemyNameplates = PlayerPrefs.GetInt("Riftbound.ShowNameplates", 1) != 0;
            autoDismount = PlayerPrefs.GetInt("Riftbound.AutoDismount", 1) != 0;
            lockActionBars = PlayerPrefs.GetInt("Riftbound.LockActionBars", 0) != 0;
            AudioListener.volume = masterVolume;
        }

        public void SaveSettings()
        {
            PlayerPrefs.SetFloat("Riftbound.Volume", masterVolume);
            PlayerPrefs.SetFloat("Riftbound.MusicVolume", musicVolume);
            PlayerPrefs.SetFloat("Riftbound.SfxVolume", sfxVolume);
            PlayerPrefs.SetFloat("Riftbound.AmbienceVolume", ambienceVolume);
            PlayerPrefs.SetInt("Riftbound.ShowCombatText", showCombatText ? 1 : 0);
            PlayerPrefs.SetInt("Riftbound.ShowNameplates", showEnemyNameplates ? 1 : 0);
            PlayerPrefs.SetInt("Riftbound.AutoDismount", autoDismount ? 1 : 0);
            PlayerPrefs.SetInt("Riftbound.LockActionBars", lockActionBars ? 1 : 0);
            PlayerPrefs.Save();
            AudioListener.volume = masterVolume;
        }

        void Update()
        {
            if (IsLoggingOut)
            {
                // Check if player moved or entered combat to cancel logout
                if (player != null)
                {
                    if (player.InCombat() || (Keyboard.current != null && (Keyboard.current.wKey.isPressed || Keyboard.current.sKey.isPressed || Keyboard.current.aKey.isPressed || Keyboard.current.dKey.isPressed)))
                    {
                        CancelLogout("Logout cancelled - movement or combat detected.");
                        return;
                    }
                }

                LogoutCountdown -= Time.deltaTime;
                if (LogoutCountdown <= 0f)
                {
                    IsLoggingOut = false;
                    ExecuteLogout();
                }
            }
        }

        public void OpenMenu()
        {
            IsMenuOpen = true;
            CurrentPage = SettingsPage.GameMenu;
            AudioEffects.PlayClick();
        }

        public void CloseMenu()
        {
            IsMenuOpen = false;
            if (IsLoggingOut) CancelLogout("Logout cancelled.");
            AudioEffects.PlayClick();
        }

        public void ToggleMenu()
        {
            if (IsMenuOpen) CloseMenu();
            else OpenMenu();
        }

        public void StartLogout()
        {
            if (!player) player = FindFirstObjectByType<PlayerController>();

            // If player is in a rest zone, instant logout!
            bool isRested = RestedXPSystem.Instance != null && RestedXPSystem.Instance.IsInRestZone;
            if (isRested)
            {
                ExecuteLogout();
            }
            else
            {
                IsLoggingOut = true;
                LogoutCountdown = LogoutDuration;
                ZoneBanner.Show("LOGGING OUT", $"Please wait {Mathf.CeilToInt(LogoutCountdown)} seconds outside safe zones.");
                ChatSystem.LogSystem($"Logging out in {Mathf.CeilToInt(LogoutCountdown)} seconds. Movement or combat will cancel.");
            }
        }

        public void CancelLogout(string reason = "")
        {
            if (!IsLoggingOut) return;
            IsLoggingOut = false;
            LogoutCountdown = 0f;
            if (!string.IsNullOrEmpty(reason))
                ChatSystem.LogSystem(reason);
        }

        void ExecuteLogout()
        {
            var menu = player ? player.GetComponent<FrontendMenu>() : FindFirstObjectByType<FrontendMenu>();
            if (menu)
            {
                menu.ReturnToMenu();
            }
            else
            {
                ChatSystem.LogSystem("Returning to Character Selection...");
            }
        }

        public void HelpUnstuck()
        {
            CloseMenu();
            if (ChatSystem.Instance)
                ChatSystem.Instance.ProcessInputString("/stuck");
        }

        // GUI Rendering
        public void DrawSettingsGUI(float screenWidth, float screenHeight)
        {
            // Logout Progress HUD overlay (even if menu is closed)
            if (IsLoggingOut)
            {
                float barW = 340f;
                float barH = 50f;
                float bx = (screenWidth - barW) * 0.5f;
                float by = screenHeight * 0.28f;

                DrawRect(new Rect(bx - 3, by - 3, barW + 6, barH + 6), new Color(0.85f, 0.70f, 0.28f, 0.95f));
                DrawRect(new Rect(bx, by, barW, barH), new Color(0.06f, 0.08f, 0.12f, 0.95f));

                var titleSt = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                titleSt.normal.textColor = new Color(1f, 0.85f, 0.35f);
                GUI.Label(new Rect(bx, by + 4, barW, 20), $"LOGGING OUT IN {Mathf.CeilToInt(LogoutCountdown)}s", titleSt);

                // Countdown bar
                float progress = Mathf.Clamp01(1f - (LogoutCountdown / LogoutDuration));
                DrawRect(new Rect(bx + 16, by + 28, barW - 32, 14), new Color(0.12f, 0.14f, 0.18f, 1f));
                DrawRect(new Rect(bx + 16, by + 28, (barW - 32) * progress, 14), new Color(0.35f, 0.85f, 0.35f, 1f));

                if (GUI.Button(new Rect(bx + barW - 68, by + 4, 60, 20), "Cancel"))
                {
                    CancelLogout("Logout cancelled by user.");
                }
            }

            if (!IsMenuOpen) return;

            float winW = 380f;
            float winH = CurrentPage == SettingsPage.GameMenu ? 420f : 440f;
            float winX = (screenWidth - winW) * 0.5f;
            float winY = (screenHeight - winH) * 0.5f;

            // Backdrop modal
            DrawRect(new Rect(winX - 3, winY - 3, winW + 6, winH + 6), new Color(0.85f, 0.70f, 0.28f, 0.95f));
            DrawRect(new Rect(winX, winY, winW, winH), new Color(0.06f, 0.08f, 0.12f, 0.97f));

            // Header
            DrawRect(new Rect(winX, winY, winW, 36f), new Color(0.12f, 0.14f, 0.18f, 1f));
            var headStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            headStyle.normal.textColor = new Color(1f, 0.85f, 0.35f);

            string title = CurrentPage == SettingsPage.GameMenu ? "GAME MENU" :
                           CurrentPage == SettingsPage.Video ? "SYSTEM: GRAPHICS" :
                           CurrentPage == SettingsPage.Audio ? "SYSTEM: AUDIO" :
                           CurrentPage == SettingsPage.Interface ? "INTERFACE OPTIONS" : "HELP & UNSTUCK";

            GUI.Label(new Rect(winX, winY + 6, winW, 24), title, headStyle);

            if (GUI.Button(new Rect(winX + winW - 32, winY + 6, 24, 24), "✕"))
            {
                CloseMenu();
            }

            switch (CurrentPage)
            {
                case SettingsPage.GameMenu:
                    DrawMainGameMenu(winX, winY, winW, winH);
                    break;
                case SettingsPage.Video:
                    DrawVideoSettings(winX, winY, winW, winH);
                    break;
                case SettingsPage.Audio:
                    DrawAudioSettings(winX, winY, winW, winH);
                    break;
                case SettingsPage.Interface:
                    DrawInterfaceSettings(winX, winY, winW, winH);
                    break;
                case SettingsPage.HelpKeybinds:
                    DrawHelpMenu(winX, winY, winW, winH);
                    break;
            }
        }

        void DrawMainGameMenu(float x, float y, float w, float h)
        {
            float btnW = 260f;
            float btnH = 32f;
            float btnX = x + (w - btnW) * 0.5f;
            float startY = y + 50f;
            float step = 38f;

            var menuBtn = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold };

            if (GUI.Button(new Rect(btnX, startY + step * 0, btnW, btnH), "Help & Unstuck", menuBtn))
            {
                CurrentPage = SettingsPage.HelpKeybinds;
                AudioEffects.PlayClick();
            }

            if (GUI.Button(new Rect(btnX, startY + step * 1, btnW, btnH), "System: Graphics", menuBtn))
            {
                CurrentPage = SettingsPage.Video;
                AudioEffects.PlayClick();
            }

            if (GUI.Button(new Rect(btnX, startY + step * 2, btnW, btnH), "System: Audio", menuBtn))
            {
                CurrentPage = SettingsPage.Audio;
                AudioEffects.PlayClick();
            }

            if (GUI.Button(new Rect(btnX, startY + step * 3, btnW, btnH), "Interface Options", menuBtn))
            {
                CurrentPage = SettingsPage.Interface;
                AudioEffects.PlayClick();
            }

            if (GUI.Button(new Rect(btnX, startY + step * 4, btnW, btnH), "Macros & Scripts (/macro)", menuBtn))
            {
                CloseMenu();
                if (MacroSystem.Instance) MacroSystem.Instance.IsOpen = true;
                AudioEffects.PlayClick();
            }

            if (GUI.Button(new Rect(btnX, startY + step * 5, btnW, btnH), "Log Out (Character Select)", menuBtn))
            {
                CloseMenu();
                StartLogout();
                AudioEffects.PlayClick();
            }

            if (GUI.Button(new Rect(btnX, startY + step * 6, btnW, btnH), "Exit Game", menuBtn))
            {
                Application.Quit();
                #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
                #endif
            }

            // Return to Game
            GUI.color = new Color(0.9f, 0.8f, 0.3f);
            if (GUI.Button(new Rect(btnX, startY + step * 7.5f, btnW, 36f), "Return to Game", menuBtn))
            {
                CloseMenu();
            }
            GUI.color = Color.white;
        }

        void DrawVideoSettings(float x, float y, float w, float h)
        {
            float contentX = x + 30f;
            float contentY = y + 54f;

            GUI.Label(new Rect(contentX, contentY, 200, 24), "Graphics Quality Preset:");
            graphicsQualityIndex = GUI.SelectionGrid(new Rect(contentX, contentY + 28, w - 60, 30), graphicsQualityIndex, QualityLevels, 4);

            GUI.Label(new Rect(contentX, contentY + 70, 280, 24), $"Current Preset: <b>{QualityLevels[graphicsQualityIndex]}</b>");
            GUI.Label(new Rect(contentX, contentY + 95, 280, 24), $"Screen Resolution: {Screen.width}x{Screen.height} (Windowed)");

            DrawBackButton(x, y, w, h);
        }

        void DrawAudioSettings(float x, float y, float w, float h)
        {
            float cx = x + 30f;
            float cy = y + 54f;

            GUI.Label(new Rect(cx, cy, 280, 20), $"Master Volume: {Mathf.RoundToInt(masterVolume * 100)}%");
            masterVolume = GUI.HorizontalSlider(new Rect(cx, cy + 22, w - 60, 20), masterVolume, 0f, 1f);

            GUI.Label(new Rect(cx, cy + 46, 280, 20), $"Music Volume: {Mathf.RoundToInt(musicVolume * 100)}%");
            musicVolume = GUI.HorizontalSlider(new Rect(cx, cy + 68, w - 60, 20), musicVolume, 0f, 1f);

            GUI.Label(new Rect(cx, cy + 92, 280, 20), $"SFX & Combat Volume: {Mathf.RoundToInt(sfxVolume * 100)}%");
            sfxVolume = GUI.HorizontalSlider(new Rect(cx, cy + 114, w - 60, 20), sfxVolume, 0f, 1f);

            GUI.Label(new Rect(cx, cy + 138, 280, 20), $"Ambience & Environment: {Mathf.RoundToInt(ambienceVolume * 100)}%");
            ambienceVolume = GUI.HorizontalSlider(new Rect(cx, cy + 160, w - 60, 20), ambienceVolume, 0f, 1f);

            // Apply to AudioListener and persist if changed
            AudioListener.volume = masterVolume;
            if (GUI.changed) SaveSettings();

            DrawBackButton(x, y, w, h);
        }

        void DrawInterfaceSettings(float x, float y, float w, float h)
        {
            float cx = x + 30f;
            float cy = y + 54f;

            showCombatText = GUI.Toggle(new Rect(cx, cy, w - 60, 24), showCombatText, " Show Scrolling Combat Text");
            showEnemyNameplates = GUI.Toggle(new Rect(cx, cy + 30, w - 60, 24), showEnemyNameplates, " Always Show Enemy Health Bars");
            autoDismount = GUI.Toggle(new Rect(cx, cy + 60, w - 60, 24), autoDismount, " Auto-Dismount on Spell Cast");
            lockActionBars = GUI.Toggle(new Rect(cx, cy + 90, w - 60, 24), lockActionBars, " Lock Action Bars in Combat");

            if (GUI.changed) SaveSettings();

            DrawBackButton(x, y, w, h);
        }

        void DrawHelpMenu(float x, float y, float w, float h)
        {
            float cx = x + 30f;
            float cy = y + 54f;

            var tip = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 12 };
            GUI.Label(new Rect(cx, cy, w - 60, 80),
                "If your character is trapped in terrain, fallen beneath the world, or unable to move, click 'Unstuck' below to recall safely to the nearest sanctuary (Emberwatch Outpost).", tip);

            var unstuckBtn = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold };
            GUI.color = new Color(0.4f, 1f, 0.4f);
            if (GUI.Button(new Rect(cx, cy + 90, w - 60, 36), "🌀 Teleport Unstuck (/stuck)", unstuckBtn))
            {
                HelpUnstuck();
            }
            GUI.color = Color.white;

            DrawBackButton(x, y, w, h);
        }

        void DrawBackButton(float x, float y, float w, float h)
        {
            if (GUI.Button(new Rect(x + (w - 200) * 0.5f, y + h - 45, 200, 32), "Back to Menu"))
            {
                CurrentPage = SettingsPage.GameMenu;
                AudioEffects.PlayClick();
            }
        }

        void DrawRect(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
