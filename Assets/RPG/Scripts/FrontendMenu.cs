using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Riftbound
{
    public class FrontendMenu : MonoBehaviour
    {
        [Serializable]
        public class Character
        {
            public string id = "";
            public string name = "Brute";
            public string faction = "Alliance";
            public string race = "Human";
            public string characterClass = "Warrior";
        }

        [Serializable]
        class Roster
        {
            public List<Character> characters = new List<Character>();
            public int selected;
        }

        enum Page { Title, Characters, Create, Settings, Controls }
        public static bool IsOpen { get; private set; }
        static Character enteringCharacter;

        PlayerController player;
        Roster roster;
        Page page = Page.Title;
        Page returnPage = Page.Title;
        public bool TitleScreenOpen => IsOpen && page == Page.Title;
        Texture2D buttonNormal, buttonHover, buttonPressed, primaryNormal, primaryHover;

        // Creation state
        string newName = "";
        string error = "";
        int factionIndex = 0; // 0 = Alliance, 1 = Horde
        int raceIndex = 0;
        int classIndex = 0;

        readonly string[] allianceRaces = new[] { "Human", "Elf", "Dwarf" };
        readonly string[] hordeRaces = new[] { "Brute", "Undead", "Goblin" };
        readonly string[] availableClasses = new[] { "Warrior", "Mage", "Rogue", "Paladin" };

        static readonly string[] RandomNames = new[]
        {
            "Garrick", "Valeria", "Morgrim", "Drakath", "Theron",
            "Alleria", "Vaelen", "Cheryl", "Torvald", "Halford",
            "Justin", "Aldous", "Sylvan", "Lyra", "Kaelen",
            "Grimjaw", "Brynn", "Cedric", "Morgath", "Zephyrus"
        };

        // Deletion modal state
        bool showDeleteConfirm = false;

        // UI Styles & Assets
        GUIStyle brand, heading, subHeading, body, small, button, enterButton, dangerButton, field, cardTitle, cardSub;
        RenderTexture portrait;
        Camera portraitCamera;
        GameObject preview, previewLight;
        float previewYaw = 180f;
        bool isDraggingPreview;
        Vector2 lastMousePos;

        float volume = 0.8f;
        float sensitivity = 1.0f;
        readonly Color gold = new Color(0.76f, 0.64f, 0.43f);
        readonly Color allianceBlue = new Color(0.18f, 0.42f, 0.88f);
        readonly Color hordeRed = new Color(0.85f, 0.18f, 0.12f);

        string RosterPath => Path.Combine(Application.persistentDataPath, "characters-v1.json");
        Character Selected => (roster != null && roster.characters.Count > 0)
            ? roster.characters[Mathf.Clamp(roster.selected, 0, roster.characters.Count - 1)]
            : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            IsOpen = false;
            enteringCharacter = null;
            PlayerController.SavePathOverride = null;
        }

        public void Initialize(PlayerController owner)
        {
            player = owner;
            volume = PlayerPrefs.GetFloat("Riftbound.Volume", 0.8f);
            sensitivity = PlayerPrefs.GetFloat("Riftbound.Sensitivity", 1.0f);
            ApplySettings();
            Network.CoopSessionConfig.ParseCommandLineOnce();

            if (Network.CoopSessionConfig.AutoSmokeExitSeconds > 0f && enteringCharacter == null)
            {
                IsOpen = false;
                if (player.orbit) player.orbit.SetPaused(false);
                player.CharacterName = Network.CoopSessionConfig.Mode == Network.CoopSessionMode.HostLan ? "HostHero" : "ClientHero";
                player.CharacterRace = "Human";
                player.CharacterClass = Network.CoopSessionConfig.Mode == Network.CoopSessionMode.HostLan ? "Warrior" : "Mage";
                player.Stats.displayName = player.CharacterName;
                player.RefreshAdventureWorld();
                return;
            }

#if UNITY_EDITOR
            if (UnityEditor.EditorPrefs.GetBool("Riftbound.DirectPlayInEditor", false) && enteringCharacter == null)
            {
                IsOpen = false;
                if (player.orbit) player.orbit.SetPaused(false);
                player.Stats.displayName = "Adventurer";
                player.LoadAdventure();
                player.RefreshAdventureWorld();
                return;
            }
#endif

            if (enteringCharacter != null)
            {
                var character = enteringCharacter;
                enteringCharacter = null;
                IsOpen = false;
                player.CharacterSaveId = character.id;
                player.CharacterName = character.name;
                player.CharacterFaction = character.faction;
                player.CharacterRace = character.race;
                player.CharacterClass = character.characterClass;
                player.Stats.displayName = character.name;
                player.LoadAdventure();
                player.RefreshAdventureWorld();
                if (player.orbit) player.orbit.SetPaused(false);
                return;
            }

            roster = new Roster();
            try
            {
                if (File.Exists(RosterPath))
                    roster = JsonUtility.FromJson<Roster>(File.ReadAllText(RosterPath)) ?? new Roster();
            }
            catch (Exception exception)
            {
                error = "Could not read character list. Existing saves are kept.";
                Debug.LogWarning(exception.Message);
            }

            if (roster.characters == null) roster.characters = new List<Character>();
            roster.characters.RemoveAll(c => c == null || !ValidId(c.id));

            if (roster.characters.Count == 0)
            {
                // Default starter character
                roster.characters.Add(new Character { name = "Garrick", faction = "Alliance", race = "Human", characterClass = "Warrior" });
            }

            roster.selected = Mathf.Clamp(roster.selected, 0, roster.characters.Count - 1);
            IsOpen = true;
            if (player.orbit) player.orbit.SetPaused(true);

            page = Page.Title;
            SetupPortraitCamera();
            RefreshPreview();

            if (Network.CoopSessionConfig.UiSmoke && Network.CoopWorldManager.UiSmokeStage == 0)
            {
                StartCoroutine(RunUiFlowSmokeRoutine());
            }
        }

        System.Collections.IEnumerator RunUiFlowSmokeRoutine()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            bool offlineClean = IsOpen && Network.OnlineSessionService.State == Network.OnlineSessionState.Offline &&
                                Network.CoopSessionConfig.Mode == Network.CoopSessionMode.Offline &&
                                UnityEngine.Object.FindObjectsByType<Unity.Netcode.NetworkManager>(FindObjectsSortMode.None).Length == 0;
            OpenCharacterSelect();
            yield return new WaitForSecondsRealtime(0.2f);
            Network.CoopSessionConfig.Mode = Network.CoopSessionMode.JoinOnline;
            Network.CoopSessionConfig.JoinCode = "AB";
            EnterWorld();
            bool shortCodeBlocked = IsOpen && !enteringWorld && !string.IsNullOrEmpty(error);
            if (!offlineClean || !shortCodeBlocked)
            {
                string failMsg = $"FAIL UiFlow stage0: offlineClean={offlineClean}, shortCodeBlocked={shortCodeBlocked}, err={error}";
                Debug.Log(failMsg);
                System.Console.WriteLine(failMsg);
                Application.Quit(1);
                yield break;
            }
            Network.CoopWorldManager.UiSmokeStage = 1;
            Network.CoopSessionConfig.Mode = Network.CoopSessionMode.HostOnline;
            Network.CoopSessionConfig.JoinCode = "";
            EnterWorld();
        }

        static bool ValidId(string id) => id == "" || (id != null && Guid.TryParseExact(id, "N", out _));

        void ApplySettings()
        {
            AudioListener.volume = volume;
            if (player && player.orbit) player.orbit.mouseSensitivity = sensitivity;
        }

        void SaveSettings()
        {
            PlayerPrefs.SetFloat("Riftbound.Volume", volume);
            PlayerPrefs.SetFloat("Riftbound.Sensitivity", sensitivity);
            PlayerPrefs.Save();
        }

        bool SaveRoster()
        {
            try
            {
                Directory.CreateDirectory(Application.persistentDataPath);
                File.WriteAllText(RosterPath, JsonUtility.ToJson(roster, true));
                return true;
            }
            catch (Exception exception)
            {
                error = "Could not save character list. Check disk access.";
                Debug.LogWarning(exception.Message);
                return false;
            }
        }

        bool enteringWorld;

        public void EnterWorld()
        {
            if (Selected == null || enteringWorld || Network.OnlineSessionService.IsBusy) return;
            if (Network.CoopSessionConfig.Mode == Network.CoopSessionMode.JoinOnline)
            {
                string norm = Network.OnlineSessionService.NormalizeJoinCode(Network.CoopSessionConfig.JoinCode);
                if (string.IsNullOrWhiteSpace(norm) || norm.Length < 4)
                {
                    error = "Enter a valid session join code (at least 4 characters) to join Online Co-op.";
                    return;
                }
                Network.CoopSessionConfig.JoinCode = norm;
            }
            if (!Network.CoopSessionConfig.UiSmoke && !SaveRoster()) return;

            enteringWorld = true;
            enteringCharacter = Selected;
            IsOpen = false;
            Time.timeScale = 1;
            CleanupPreview();
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void OpenCharacterSelect()
        {
            if (!IsOpen || roster == null) return;
            page = roster.characters.Count > 0 ? Page.Characters : Page.Create;
            error = "";
            RefreshPreview();
        }

        public void ReturnToMenu()
        {
            player.CloseDialogue();
            if (!player.SaveAdventure(false)) return;
            enteringCharacter = null;
            IsOpen = false;
            Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        string GetCurrentRace()
        {
            string[] pool = factionIndex == 0 ? allianceRaces : hordeRaces;
            return pool[Mathf.Clamp(raceIndex, 0, pool.Length - 1)];
        }

        void CreateCharacter()
        {
            string name = newName.Trim();
            if (name.Length < 2 || name.Length > 16) { error = "Choose a name between 2 and 16 characters."; return; }
            foreach (char c in name)
            {
                if (!char.IsLetter(c) && c != ' ' && c != '-' && c != '\'')
                {
                    error = "Name can only contain letters, spaces, apostrophes or hyphens.";
                    return;
                }
            }

            if (roster.characters.Count >= 8) { error = "All 8 character slots are occupied."; return; }
            if (roster.characters.Exists(c => string.Equals(c.name, name, StringComparison.OrdinalIgnoreCase)))
            {
                error = "A character with that name already exists in your roster.";
                return;
            }

            var created = new Character
            {
                id = Guid.NewGuid().ToString("N"),
                name = name,
                faction = factionIndex == 0 ? "Alliance" : "Horde",
                race = GetCurrentRace(),
                characterClass = availableClasses[Mathf.Clamp(classIndex, 0, availableClasses.Length - 1)]
            };

            roster.characters.Add(created);
            roster.selected = roster.characters.Count - 1;

            if (!SaveRoster())
            {
                roster.characters.Remove(created);
                roster.selected = 0;
                return;
            }

            error = "";
            page = Page.Characters;
            RefreshPreview();
        }

        void DeleteSelectedCharacter()
        {
            if (roster.characters.Count == 0) return;
            int idx = Mathf.Clamp(roster.selected, 0, roster.characters.Count - 1);
            var character = roster.characters[idx];

            string savePath = Path.Combine(Application.persistentDataPath, string.IsNullOrEmpty(character.id) ? "adventure-v1.json" : "adventure-" + character.id + ".json");
            try
            {
                if (File.Exists(savePath)) File.Delete(savePath);
                if (File.Exists(savePath + ".bak")) File.Delete(savePath + ".bak");
                if (File.Exists(savePath + ".tmp")) File.Delete(savePath + ".tmp");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Failed to delete character save: " + ex.Message);
            }

            roster.characters.RemoveAt(idx);
            if (roster.characters.Count == 0)
            {
                page = Page.Create;
                newName = "";
                error = "";
            }
            else
            {
                roster.selected = Mathf.Clamp(idx, 0, roster.characters.Count - 1);
                page = Page.Characters;
            }

            SaveRoster();
            RefreshPreview();
        }

        int GetCharacterLevel(Character c)
        {
            if (c == null) return 1;
            string savePath = Path.Combine(Application.persistentDataPath, string.IsNullOrEmpty(c.id) ? "adventure-v1.json" : "adventure-" + c.id + ".json");
            try
            {
                if (File.Exists(savePath))
                {
                    var data = JsonUtility.FromJson<PlayerController.AdventureSave>(File.ReadAllText(savePath));
                    if (data != null)
                    {
                        int xp = data.experience;
                        if (xp >= 750) return 5;
                        if (xp >= 400) return 4;
                        if (xp >= 200) return 3;
                        if (xp >= 75) return 2;
                        return 1;
                    }
                }
            }
            catch { }
            return 1;
        }

        string GetCharacterZone(Character c)
        {
            if (c == null) return "Emberwatch Outpost";
            string savePath = Path.Combine(Application.persistentDataPath, string.IsNullOrEmpty(c.id) ? "adventure-v1.json" : "adventure-" + c.id + ".json");
            try
            {
                if (File.Exists(savePath))
                {
                    var data = JsonUtility.FromJson<PlayerController.AdventureSave>(File.ReadAllText(savePath));
                    if (data != null)
                    {
                        if (data.hasPosition)
                            return ZoneManager.GetZoneName(new Vector3(data.posX, data.posY, data.posZ));
                        if (data.landmarks != null)
                        {
                            if (Array.IndexOf(data.landmarks, "scarlet-crypts") >= 0) return "Scarlet Crypts (Duskwood)";
                            if (Array.IndexOf(data.landmarks, "redridge-frontier") >= 0) return "Redridge Frontier (Horde)";
                            if (Array.IndexOf(data.landmarks, "greenhollow-post") >= 0) return "Greenhollow Valley";
                        }
                    }
                }
            }
            catch { }
            return PlayerController.GetStartingZoneName(c.race);
        }

        // ---- Title backgrounds & faction preview backdrops ----
        Texture2D[] titleBgs; Texture2D cozyAlliance, cozyHorde;
        const float BgHold = 9f, BgFade = 1.6f;

        void EnsureBackgrounds()
        {
            if (titleBgs != null) return;
            var list = new System.Collections.Generic.List<Texture2D>();
            for (int i = 1; i <= 8; i++) { var t = Resources.Load<Texture2D>("TitleBackgrounds/title_0" + i); if (t) list.Add(t); }
            titleBgs = list.ToArray();
            cozyAlliance = Resources.Load<Texture2D>("TitleBackgrounds/cozy_alliance");
            cozyHorde = Resources.Load<Texture2D>("TitleBackgrounds/cozy_horde");
        }

        static void DrawCover(Rect r, Texture2D tex, float zoom = 1f)
        {
            float ta = (float)tex.width / tex.height, ra = r.width / r.height;
            float uw = 1f, uh = 1f;
            if (ta > ra) uw = ra / ta; else uh = ta / ra;
            uw /= zoom; uh /= zoom;
            GUI.DrawTextureWithTexCoords(r, tex, new Rect((1f - uw) * 0.5f, (1f - uh) * 0.5f, uw, uh));
        }

        void DrawRotatingBackground()
        {
            EnsureBackgrounds();
            if (titleBgs.Length == 0) return;
            var full = new Rect(0, 0, Screen.width, Screen.height);
            float cycle = BgHold + BgFade, t = Time.unscaledTime;
            int idx = (int)(t / cycle) % titleBgs.Length;
            float local = t % cycle;
            var prev = GUI.color;
            GUI.color = Color.white;
            DrawCover(full, titleBgs[idx], 1.04f + 0.06f * (local / cycle));
            if (local > BgHold && titleBgs.Length > 1)
            {
                int next = (idx + 1) % titleBgs.Length;
                GUI.color = new Color(1, 1, 1, (local - BgHold) / BgFade);
                DrawCover(full, titleBgs[next], 1.04f);
            }
            GUI.color = prev;
        }

        void DrawCozyBackdrop(Rect rect)
        {
            EnsureBackgrounds();
            bool horde = page == Page.Create ? factionIndex == 1 : (Selected != null && Selected.faction == "Horde");
            var tex = horde ? cozyHorde : cozyAlliance;
            Fill(new Rect(rect.x - 3, rect.y - 3, rect.width + 6, rect.height + 6), (horde ? hordeRed : allianceBlue) * 0.8f);
            if (tex) { GUI.color = Color.white; DrawCover(rect, tex); }
            else Fill(rect, horde ? new Color(.22f, .08f, .05f) : new Color(.08f, .14f, .25f));
            // soft vignette
            Fill(new Rect(rect.x, rect.yMax - 90, rect.width, 90), new Color(0, 0, 0, .35f));
        }
        void SetupPortraitCamera()
        {
            if (portrait == null)
            {
                portrait = new RenderTexture(700, 850, 24) { name = "WoW Character Portrait RT" };
                portrait.Create();
            }

            if (!portraitCamera)
            {
                var camObj = new GameObject("WoW Character Portrait Camera");
                portraitCamera = camObj.AddComponent<Camera>();
                portraitCamera.targetTexture = portrait;
                portraitCamera.cullingMask = 1 << 31;
                portraitCamera.clearFlags = CameraClearFlags.SolidColor;
                portraitCamera.backgroundColor = new Color(0.02f, 0.035f, 0.045f, 0f);
                portraitCamera.fieldOfView = 30f;
                portraitCamera.nearClipPlane = 0.05f;
                portraitCamera.farClipPlane = 25f;
            }

            if (!previewLight)
            {
                previewLight = new GameObject("WoW Portrait Light");
                var light = previewLight.AddComponent<Light>();
                light.type = LightType.Directional;
                light.cullingMask = 1 << 31;
                light.color = new Color(1.0f, 0.90f, 0.75f);
                light.intensity = 2.4f;
                previewLight.transform.rotation = Quaternion.Euler(32f, -30f, 0f);
            }
        }

        void RefreshPreview()
        {
            if (!IsOpen) return;
            if (preview) Destroy(preview);

            string targetRace = page == Page.Create ? GetCurrentRace() : (Selected != null ? Selected.race : "Human");
            string targetClass = page == Page.Create ? availableClasses[classIndex] : (Selected != null ? Selected.characterClass : "Warrior");

            preview = CharacterVisualFactory.SpawnPreview(targetRace, targetClass, null, previewYaw);
            if (!preview) return;

            var renderers = preview.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                Vector3 center = bounds.center;

                if (portraitCamera)
                {
                    float dist = Mathf.Max(2.8f, bounds.size.y * 2.05f);
                    portraitCamera.transform.position = center + new Vector3(0f, 0.05f, -dist);
                    portraitCamera.transform.LookAt(center);
                }
            }
        }

        void CleanupPreview()
        {
            if (preview) Destroy(preview);
            if (previewLight) Destroy(previewLight);
            if (portraitCamera) Destroy(portraitCamera.gameObject);
            if (portrait) { portrait.Release(); Destroy(portrait); }
        }

        void Update()
        {
            if (!IsOpen || Keyboard.current == null) return;

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (showDeleteConfirm) { showDeleteConfirm = false; return; }
                if (page == Page.Create) { page = Page.Characters; error = ""; RefreshPreview(); return; }
                if (page == Page.Settings || page == Page.Controls) { page = returnPage; SaveSettings(); return; }
                if (page == Page.Characters) { page = Page.Title; return; }
            }

            if (page == Page.Title && Keyboard.current.enterKey.wasPressedThisFrame) OpenCharacterSelect();
            if (page != Page.Characters && page != Page.Create) return;

            if (Mouse.current != null)
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    // Check if clicking in preview portrait center zone
                    float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
                    float previewLeft = (Screen.width - 1280 * scale) * 0.5f + 320 * scale;
                    float previewRight = previewLeft + 540 * scale;
                    if (mousePos.x >= previewLeft && mousePos.x <= previewRight && !showDeleteConfirm)
                    {
                        isDraggingPreview = true;
                        lastMousePos = mousePos;
                    }
                }
                else if (Mouse.current.leftButton.isPressed && isDraggingPreview)
                {
                    float deltaX = mousePos.x - lastMousePos.x;
                    previewYaw -= deltaX * 0.65f;
                    lastMousePos = mousePos;
                    if (preview) preview.transform.rotation = Quaternion.Euler(0f, previewYaw, 0f);
                }
                else if (Mouse.current.leftButton.wasReleasedThisFrame)
                {
                    isDraggingPreview = false;
                }
            }
        }

        void OnDestroy()
        {
            CleanupPreview();
            foreach (var texture in new[] { buttonNormal, buttonHover, buttonPressed, primaryNormal, primaryHover })
                if (texture) Destroy(texture);
        }

        static Texture2D Solid(Color color)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color); texture.Apply(); return texture;
        }

        void InitStyles()
        {
            brand = new GUIStyle(GUI.skin.label) { fontSize = 42, fontStyle = FontStyle.Bold };
            brand.normal.textColor = new Color(.94f, .92f, .86f);

            heading = new GUIStyle(GUI.skin.label) { fontSize = 23, fontStyle = FontStyle.Bold };
            heading.normal.textColor = gold;

            subHeading = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            subHeading.normal.textColor = new Color(0.92f, 0.88f, 0.76f);

            body = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            body.normal.textColor = new Color(0.86f, 0.87f, 0.82f);

            small = new GUIStyle(body) { fontSize = 12 };
            small.normal.textColor = new Color(0.65f, 0.68f, 0.68f);

            buttonNormal = Solid(new Color(.095f, .105f, .115f));
            buttonHover = Solid(new Color(.15f, .165f, .18f));
            buttonPressed = Solid(new Color(.20f, .21f, .22f));
            primaryNormal = Solid(new Color(.30f, .25f, .17f));
            primaryHover = Solid(new Color(.40f, .33f, .22f));
            button = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Normal, border = new RectOffset(), padding = new RectOffset(16, 16, 8, 8) };
            button.normal.background = buttonNormal; button.hover.background = buttonHover;
            button.active.background = buttonPressed; button.focused.background = buttonHover;
            button.normal.textColor = new Color(.86f, .87f, .86f);
            button.hover.textColor = Color.white; button.active.textColor = Color.white;
            WowTheme.StyleButton(GUI.skin.button);
            WowTheme.StyleButton(button);

            enterButton = new GUIStyle(button) { fontSize = 17, fontStyle = FontStyle.Bold };
            enterButton.normal.background = primaryNormal; enterButton.hover.background = primaryHover;
            enterButton.normal.textColor = new Color(1.0f, 0.95f, 0.80f);
            WowTheme.StyleButton(enterButton, true);

            dangerButton = new GUIStyle(button) { fontSize = 13 };
            dangerButton.normal.textColor = new Color(1.0f, 0.40f, 0.40f);

            field = new GUIStyle(GUI.skin.textField) { fontSize = 20, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(12, 12, 6, 6) };

            cardTitle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            cardSub = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = false };
            cardSub.normal.textColor = new Color(0.72f, 0.74f, 0.72f);
        }

        void OnGUI()
        {
            if (!IsOpen || roster == null) return;
            if (brand == null) InitStyles();

            var oldMatrix = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) * 0.5f, (Screen.height - 720 * scale) * 0.5f), Quaternion.identity, Vector3.one * scale);

            GUI.matrix = oldMatrix;
            DrawRotatingBackground();
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) * 0.5f, (Screen.height - 720 * scale) * 0.5f), Quaternion.identity, Vector3.one * scale);
            Fill(new Rect(-1000, -1000, 3280, 2720), new Color(.018f, .023f, .029f, page == Page.Title ? .30f : .86f));
            if (page == Page.Title) Fill(new Rect(0, 0, 640, 720), new Color(.01f, .015f, .02f, .45f));

            // Top WoW Bar
            if (page != Page.Title) DrawTopBar();

            // Active Page
            bool previousEnabled = GUI.enabled;
            if (showDeleteConfirm) GUI.enabled = false;
            if (page == Page.Title) DrawTitlePage();
            else if (page == Page.Characters) DrawCharacterSelectPage();
            else if (page == Page.Create) DrawCharacterCreatePage();
            else if (page == Page.Settings) DrawSettingsPage();
            else if (page == Page.Controls) DrawControlsPage();
            GUI.enabled = previousEnabled;

            // Error display
            if (!string.IsNullOrEmpty(error))
            {
                var errStyle = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter };
                errStyle.normal.textColor = new Color(1.0f, 0.35f, 0.35f);
                GUI.Label(new Rect(340, 630, 600, 28), error, errStyle);
            }

            // Delete confirmation modal (if open)
            if (showDeleteConfirm)
            {
                DrawDeleteConfirmationModal();
            }

            GUI.matrix = oldMatrix;
        }

        void DrawTopBar()
        {
            GUI.Label(new Rect(40, 24, 400, 42), "RIFTBOUND", new GUIStyle(brand) { fontSize = 28 });
            GUI.Label(new Rect(42, 62, 450, 20), "CHRONICLES OF EMBERWATCH", small);
            Fill(new Rect(40, 92, 1200, 1), new Color(.3f, .32f, .34f, .3f));
        }

        void DrawTitlePage()
        {
            if (portrait)
            {
                var tint = GUI.color; GUI.color = new Color(1, 1, 1, .75f);
                GUI.DrawTexture(new Rect(625, 55, 540, 620), portrait, ScaleMode.ScaleToFit);
                GUI.color = tint;
            }
            GUI.Label(new Rect(100, 145, 460, 24), "CHRONICLES OF EMBERWATCH", small);
            GUI.Label(new Rect(96, 179, 530, 82), "RIFTBOUND", new GUIStyle(brand) { fontSize = 56 });
            Fill(new Rect(100, 276, 50, 2), gold);
            GUI.Label(new Rect(100, 302, 410, 65), "Beyond the outpost, a frontier awaits.", new GUIStyle(body) { fontSize = 20 });
            if (GUI.Button(new Rect(100, 404, 290, 54), "Play", enterButton)) OpenCharacterSelect();
            if (GUI.Button(new Rect(100, 472, 138, 38), "Settings", button)) { returnPage = Page.Title; page = Page.Settings; }
            if (GUI.Button(new Rect(252, 472, 138, 38), "Controls", button)) { returnPage = Page.Title; page = Page.Controls; }
            if (GUI.Button(new Rect(100, 526, 138, 36), "Quit", button)) QuitGame();
            GUI.Label(new Rect(100, 650, 600, 24), "A local adventure  /  " + Application.version, small);
        }

        static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void DrawCharacterSelectPage()
        {
            // Center 3D Preview Turntable
            DrawCharacterPreviewArea(new Rect(330, 85, 530, 540));

            // Right Column: Character Selection List (WoW Classic)
            DrawCharacterListColumn(new Rect(880, 85, 360, 480));

            // Bottom Right Action Buttons: Enter World / Create / Delete
            DrawActionButtonsBottomRight(new Rect(880, 575, 360, 125));

            // Bottom Left Navigation Buttons: Settings / Controls / Quit
            GUI.Label(new Rect(28, 155, 290, 30), "Your adventure", subHeading);
            GUI.Label(new Rect(28, 196, 285, 75), "Choose a character to continue, or begin a new story on the frontier.", body);
            DrawSessionModePanel(new Rect(28, 290, 292, 230));
            DrawNavigationButtonsBottomLeft(new Rect(28, 540, 240, 140));
        }

        void DrawSessionModePanel(Rect rect)
        {
            Fill(rect, new Color(0.05f, 0.06f, 0.08f, 0.78f));
            GUI.Label(new Rect(rect.x + 10, rect.y + 8, rect.width - 20, 22), "SESSION MODE", subHeading);

            float bw = (rect.width - 28f) / 3f;
            DrawModeButton(new Rect(rect.x + 10, rect.y + 36, bw, 30), "Offline", Network.CoopSessionMode.Offline);
            DrawModeButton(new Rect(rect.x + 14 + bw, rect.y + 36, bw, 30), "Host Online", Network.CoopSessionMode.HostOnline);
            DrawModeButton(new Rect(rect.x + 18 + bw * 2, rect.y + 36, bw, 30), "Join Online", Network.CoopSessionMode.JoinOnline);

            if (Network.CoopSessionConfig.Mode == Network.CoopSessionMode.Offline)
            {
                GUI.Label(new Rect(rect.x + 10, rect.y + 76, rect.width - 20, 65),
                    "Single-player adventure with local save progression.", small);
            }
            else if (Network.CoopSessionConfig.Mode == Network.CoopSessionMode.HostOnline)
            {
                GUI.Label(new Rect(rect.x + 10, rect.y + 74, rect.width - 20, 82),
                    "Host up to 4 players online.\nGenerates a shareable Join Code.\nSession-only co-op progress (offline saves kept safe).", small);
            }
            else
            {
                GUI.Label(new Rect(rect.x + 10, rect.y + 72, rect.width - 20, 20), "Session Join Code:", small);
                string rawCode = GUI.TextField(
                    new Rect(rect.x + 10, rect.y + 94, rect.width - 20, 28),
                    Network.CoopSessionConfig.JoinCode ?? "", 16);
                Network.CoopSessionConfig.JoinCode = Network.OnlineSessionService.NormalizeJoinCode(rawCode);
                GUI.Label(new Rect(rect.x + 10, rect.y + 126, rect.width - 20, 38),
                    "Session-only co-op progress (offline saves safe).", small);
            }

            if (!string.IsNullOrEmpty(Network.CoopSessionConfig.LastDisconnectReason))
            {
                var errStyle = new GUIStyle(small);
                errStyle.normal.textColor = new Color(1f, 0.4f, 0.4f);
                GUI.Label(new Rect(rect.x + 10, rect.y + 168, rect.width - 20, 55),
                    $"Last session: {Network.CoopSessionConfig.LastDisconnectReason}", errStyle);
            }
        }

        void DrawModeButton(Rect rect, string label, Network.CoopSessionMode mode)
        {
            bool active = Network.CoopSessionConfig.Mode == mode;
            var style = new GUIStyle(active ? enterButton : button)
            {
                fontSize = 11,
                fontStyle = active ? FontStyle.Bold : FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(4, 4, 4, 4),
                wordWrap = false
            };
            if (GUI.Button(rect, label, style))
            {
                Network.CoopSessionConfig.Mode = mode;
                Network.CoopSessionConfig.LastDisconnectReason = string.Empty;
            }
        }

        void DrawCharacterPreviewArea(Rect rect)
        {
            DrawCozyBackdrop(rect);
            if (portrait)
            {
                GUI.DrawTexture(rect, portrait, ScaleMode.ScaleToFit);
            }

            // Rotation Turntable buttons
            if (GUI.Button(new Rect(rect.x + 80, rect.yMax - 36, 55, 32), "<", button))
            {
                previewYaw -= 30f;
                if (preview) preview.transform.rotation = Quaternion.Euler(0f, previewYaw, 0f);
            }

            if (GUI.Button(new Rect(rect.xMax - 135, rect.yMax - 36, 55, 32), ">", button))
            {
                previewYaw += 30f;
                if (preview) preview.transform.rotation = Quaternion.Euler(0f, previewYaw, 0f);
            }

            var tipStyle = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(rect.x + 140, rect.yMax - 32, rect.width - 280, 24), "Drag to rotate", tipStyle);

            // Character Pedestal Info under model
            if (Selected != null && page != Page.Create)
            {
                int lvl = GetCharacterLevel(Selected);
                string zone = GetCharacterZone(Selected);

                Fill(new Rect(rect.x + 60, rect.yMax + 6, rect.width - 120, 46), new Color(0.04f, 0.07f, 0.09f, 0.90f));
                Fill(new Rect(rect.x + 60, rect.yMax + 6, rect.width - 120, 1), gold * 0.6f);

                var nameStyle = new GUIStyle(heading) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
                nameStyle.normal.textColor = GetClassColor(Selected.characterClass);
                GUI.Label(new Rect(rect.x + 60, rect.yMax + 9, rect.width - 120, 24), Selected.name, nameStyle);

                var detailsStyle = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(rect.x + 60, rect.yMax + 29, rect.width - 120, 20), $"Level {lvl} {Selected.race} {Selected.characterClass} / {zone}", detailsStyle);
            }
        }

        void DrawCharacterListColumn(Rect rect)
        {
            // Panel frame
            WowTheme.Panel(new Rect(rect.x - 4, rect.y - 4, rect.width + 8, rect.height + 8));

            GUI.Label(new Rect(rect.x + 16, rect.y + 10, rect.width - 32, 28), "CHARACTERS", heading);

            float cardY = rect.y + 44;
            float cardH = 47f;
            float cardSpacing = 5f;

            for (int i = 0; i < roster.characters.Count && i < 8; i++)
            {
                var character = roster.characters[i];
                bool isSelected = roster.selected == i;
                Rect cardRect = new Rect(rect.x + 10, cardY + i * (cardH + cardSpacing), rect.width - 20, cardH);

                // Card background & selection border
                if (isSelected)
                {
                    Fill(cardRect, new Color(.14f, .15f, .16f, .98f));
                    Fill(new Rect(cardRect.x, cardRect.y, 2, cardRect.height), gold);
                }
                else
                {
                    Fill(cardRect, new Color(0.08f, 0.10f, 0.12f, 0.85f));
                }

                // Invisible button to handle selection
                if (GUI.Button(cardRect, GUIContent.none, GUIStyle.none))
                {
                    if (roster.selected != i)
                    {
                        roster.selected = i;
                        error = "";
                        RefreshPreview();
                    }
                }

                int lvl = GetCharacterLevel(character);
                string zone = GetCharacterZone(character);

                cardTitle.normal.textColor = isSelected ? new Color(.96f, .93f, .85f) : new Color(.78f, .8f, .81f);
                GUI.Label(new Rect(cardRect.x + 14, cardRect.y + 6, cardRect.width - 28, 20), character.name, cardTitle);
                GUI.Label(new Rect(cardRect.x + 14, cardRect.y + 26, cardRect.width - 28, 18), $"Lv {lvl} {character.race} {character.characterClass} / {zone}", cardSub);
            }
        }

        void DrawActionButtonsBottomRight(Rect rect)
        {
            bool canInteract = GUI.enabled && !enteringWorld && !Network.OnlineSessionService.IsBusy;
            GUI.enabled = canInteract;
            // Enter World (Giant Red/Gold WoW Button)
            GUI.backgroundColor = Color.white;
            string enterLabel = Network.CoopSessionConfig.Mode == Network.CoopSessionMode.HostOnline
                ? "HOST ONLINE"
                : (Network.CoopSessionConfig.Mode == Network.CoopSessionMode.JoinOnline ? "JOIN ONLINE" : "ENTER WORLD");
            if (GUI.Button(new Rect(rect.x, rect.y, rect.width, 52), enterLabel, enterButton))
            {
                EnterWorld();
            }
            GUI.backgroundColor = Color.white;

            // Create Character (Left)
            GUI.enabled = canInteract && roster.characters.Count < 8;
            if (GUI.Button(new Rect(rect.x, rect.y + 58, 175, 42), "Create character", button))
            {
                newName = "";
                error = "";
                page = Page.Create;
                RefreshPreview();
            }
            GUI.enabled = canInteract;

            // Delete Character (Right - Red button)
            GUI.enabled = canInteract && roster.characters.Count > 0;
            GUI.backgroundColor = Color.white;
            if (GUI.Button(new Rect(rect.x + 185, rect.y + 58, 175, 42), "Delete character", dangerButton))
            {
                showDeleteConfirm = true;
            }
            GUI.backgroundColor = Color.white;
            GUI.enabled = canInteract;
        }

        void DrawNavigationButtonsBottomLeft(Rect rect)
        {
            if (GUI.Button(new Rect(rect.x, rect.y, 180, 34), "Settings", button))
            {
                returnPage = Page.Characters;
                page = Page.Settings;
            }

            if (GUI.Button(new Rect(rect.x, rect.y + 43, 180, 34), "Controls", button))
            {
                returnPage = Page.Characters;
                page = Page.Controls;
            }

            if (GUI.Button(new Rect(rect.x, rect.y + 86, 180, 34), "Back to title", button))
            {
                page = Page.Title;
            }
        }

        void DrawCharacterCreatePage()
        {
            // Header
            GUI.Label(new Rect(40, 85, 450, 32), "CHARACTER CREATION", heading);
            GUI.Label(new Rect(42, 114, 500, 20), "Choose your allegiance, heritage, and calling in Aethelgard.", small);

            // Left Column: Faction & Race Selection (Alliance vs Horde)
            DrawFactionAndRaceSelection(new Rect(40, 142, 330, 480));

            // Center Column: 3D Character Preview
            DrawCharacterPreviewArea(new Rect(370, 85, 490, 500));

            // Right Column: Class Selection
            DrawClassSelection(new Rect(870, 142, 370, 480));

            // Bottom Bar: Name Input + Randomize + Create Button + Back
            DrawCreationBottomBar(new Rect(280, 595, 720, 105));
        }

        void DrawFactionAndRaceSelection(Rect rect)
        {
            WowTheme.Panel(new Rect(rect.x - 4, rect.y - 4, rect.width + 8, rect.height + 8));

            GUI.Label(new Rect(rect.x + 14, rect.y + 12, rect.width - 28, 24), "1. SELECT FACTION", subHeading);

            // Faction Buttons (Alliance vs Horde)
            GUI.backgroundColor = factionIndex == 0 ? allianceBlue : new Color(0.35f, 0.4f, 0.5f);
            if (GUI.Button(new Rect(rect.x + 14, rect.y + 40, (rect.width - 36) * 0.5f, 42), "ALLIANCE", button))
            {
                if (factionIndex != 0) { factionIndex = 0; raceIndex = 0; RefreshPreview(); }
            }

            GUI.backgroundColor = factionIndex == 1 ? hordeRed : new Color(0.45f, 0.35f, 0.35f);
            if (GUI.Button(new Rect(rect.x + 22 + (rect.width - 36) * 0.5f, rect.y + 40, (rect.width - 36) * 0.5f, 42), "HORDE", button))
            {
                if (factionIndex != 1) { factionIndex = 1; raceIndex = 0; RefreshPreview(); }
            }
            GUI.backgroundColor = Color.white;

            // Race Selection
            GUI.Label(new Rect(rect.x + 14, rect.y + 95, rect.width - 28, 24), "2. SELECT RACE", subHeading);

            string[] currentRaces = factionIndex == 0 ? allianceRaces : hordeRaces;
            for (int r = 0; r < currentRaces.Length; r++)
            {
                string rName = currentRaces[r];
                bool isSel = raceIndex == r;
                GUI.backgroundColor = isSel ? gold : Color.white;
                if (GUI.Button(new Rect(rect.x + 14, rect.y + 124 + r * 46, rect.width - 28, 40), rName, button))
                {
                    if (raceIndex != r) { raceIndex = r; RefreshPreview(); }
                }
            }
            GUI.backgroundColor = Color.white;

            // Race Description Lore Box
            Fill(new Rect(rect.x + 14, rect.y + 275, rect.width - 28, 185), new Color(0.03f, 0.04f, 0.06f, 0.95f));
            Fill(new Rect(rect.x + 14, rect.y + 275, rect.width - 28, 1), gold * 0.5f);

            var raceLoreTitle = new GUIStyle(cardTitle);
            raceLoreTitle.normal.textColor = gold;
            GUI.Label(new Rect(rect.x + 22, rect.y + 283, rect.width - 44, 22), GetCurrentRace() + " Racial Passives", raceLoreTitle);
            GUI.Label(new Rect(rect.x + 22, rect.y + 308, rect.width - 44, 140), ClassSystem.GetRaceDescription(GetCurrentRace()), body);
        }

        void DrawClassSelection(Rect rect)
        {
            WowTheme.Panel(new Rect(rect.x - 4, rect.y - 4, rect.width + 8, rect.height + 8));

            GUI.Label(new Rect(rect.x + 14, rect.y + 12, rect.width - 28, 24), "3. SELECT CLASS", subHeading);

            for (int c = 0; c < availableClasses.Length; c++)
            {
                string cName = availableClasses[c];
                bool isSel = classIndex == c;
                GUI.backgroundColor = isSel ? GetClassColor(cName) : Color.white;
                if (GUI.Button(new Rect(rect.x + 14, rect.y + 40 + c * 46, rect.width - 28, 40), cName, button))
                {
                    if (classIndex != c) { classIndex = c; RefreshPreview(); }
                }
            }
            GUI.backgroundColor = Color.white;

            // Class Description & Role Breakdown Box
            Fill(new Rect(rect.x + 14, rect.y + 235, rect.width - 28, 225), new Color(0.03f, 0.04f, 0.06f, 0.95f));
            Fill(new Rect(rect.x + 14, rect.y + 235, rect.width - 28, 1), gold * 0.5f);

            string chosenClass = availableClasses[classIndex];
            var classTitleStyle = new GUIStyle(cardTitle);
            classTitleStyle.normal.textColor = GetClassColor(chosenClass);
            GUI.Label(new Rect(rect.x + 22, rect.y + 243, rect.width - 44, 22), chosenClass, classTitleStyle);
            GUI.Label(new Rect(rect.x + 22, rect.y + 268, rect.width - 44, 180), ClassSystem.GetClassDescription(chosenClass), body);
        }

        void DrawCreationBottomBar(Rect rect)
        {
            GUI.Label(new Rect(rect.x, rect.y, 160, 24), "CHARACTER NAME", small);

            newName = GUI.TextField(new Rect(rect.x, rect.y + 22, 280, 40), newName, 16, field);

            if (GUI.Button(new Rect(rect.x + 290, rect.y + 22, 110, 40), "RANDOM", button))
            {
                newName = RandomNames[UnityEngine.Random.Range(0, RandomNames.Length)];
                error = "";
            }

            GUI.backgroundColor = Color.white;
            if (GUI.Button(new Rect(rect.x + 415, rect.y + 20, 180, 44), "ACCEPT & CREATE", enterButton))
            {
                CreateCharacter();
            }
            GUI.backgroundColor = Color.white;

            if (GUI.Button(new Rect(rect.x + 605, rect.y + 22, 110, 40), "CANCEL", button))
            {
                page = Page.Characters;
                error = "";
                RefreshPreview();
            }
        }

        void DrawDeleteConfirmationModal()
        {
            // Dim background
            Fill(new Rect(-1000, -1000, 3280, 2720), new Color(0f, 0f, 0f, 0.82f));

            float boxW = 520f;
            float boxH = 270f;
            Rect box = new Rect(1280 * 0.5f - boxW * 0.5f, 720 * 0.5f - boxH * 0.5f, boxW, boxH);

            Fill(new Rect(box.x - 4, box.y - 4, boxW + 8, boxH + 8), new Color(0.75f, 0.15f, 0.15f, 0.95f)); // Crimson Border
            Fill(box, new Color(0.06f, 0.07f, 0.09f, 0.98f));

            var redHeading = new GUIStyle(heading) { alignment = TextAnchor.MiddleCenter };
            redHeading.normal.textColor = new Color(1.0f, 0.30f, 0.30f);
            GUI.Label(new Rect(box.x + 20, box.y + 18, boxW - 40, 32), "DELETE CHARACTER", redHeading);

            var centerBody = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(box.x + 20, box.y + 56, boxW - 40, 24), "Are you sure you want to permanently delete:", centerBody);

            if (Selected != null)
            {
                var charStyle = new GUIStyle(heading) { fontSize = 21, alignment = TextAnchor.MiddleCenter };
                charStyle.normal.textColor = gold;
                GUI.Label(new Rect(box.x + 20, box.y + 86, boxW - 40, 28), $"{Selected.name}  (Level {GetCharacterLevel(Selected)} {Selected.race} {Selected.characterClass})", charStyle);
            }

            var warnStyle = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
            warnStyle.normal.textColor = new Color(0.92f, 0.48f, 0.48f);
            GUI.Label(new Rect(box.x + 20, box.y + 120, boxW - 40, 38), "This action cannot be undone. All adventure progress,\nitems, gold, and talent points will be erased permanently.", warnStyle);

            // Confirm Delete (Red)
            GUI.backgroundColor = new Color(0.85f, 0.18f, 0.15f);
            if (GUI.Button(new Rect(box.x + 40, box.y + 185, 200, 48), "CONFIRM DELETE", enterButton))
            {
                DeleteSelectedCharacter();
                showDeleteConfirm = false;
            }
            GUI.backgroundColor = Color.white;

            // Cancel (Gold/Neutral)
            if (GUI.Button(new Rect(box.x + boxW - 240, box.y + 185, 200, 48), "CANCEL", button))
            {
                showDeleteConfirm = false;
            }
        }

        void DrawSettingsPage()
        {
            GUI.Label(new Rect(60, 100, 500, 40), "SYSTEM SETTINGS", heading);

            GUI.Label(new Rect(60, 160, 500, 30), $"MASTER VOLUME  ({Mathf.RoundToInt(volume * 100)}%)", body);
            volume = GUI.HorizontalSlider(new Rect(60, 195, 450, 25), volume, 0f, 1f);

            GUI.Label(new Rect(60, 245, 500, 30), $"CAMERA ROTATION SENSITIVITY  ({sensitivity:F1}x)", body);
            sensitivity = GUI.HorizontalSlider(new Rect(60, 280, 450, 25), sensitivity, 0.3f, 2.5f);
            ApplySettings();

            GUI.Label(new Rect(580, 160, 500, 160), "All settings are saved locally to your device.\n\nCamera sensitivity controls RMB orbit rotation speed while exploring the open world.\n\nUse [V] to swap between Ground and Flying mounts.", body);

            if (GUI.Button(new Rect(60, 420, 220, 44), "SAVE & RETURN", button))
            {
                SaveSettings();
                page = returnPage;
            }
        }

        void DrawControlsPage()
        {
            GUI.Label(new Rect(60, 100, 600, 40), "GAME CONTROLS", heading);

            GUI.Label(new Rect(60, 155, 540, 340),
                "MOVEMENT & CAMERA:\n" +
                "  W / A / S / D       Run / Turn (RMB + A/D/Q Strafe)\n" +
                "  /  (Numpad /)      Toggle slow RP walk (Default: Run)\n" +
                "  SPACE               Jump (or Ascend while on Flying Mount)\n" +
                "  X / LEFT SHIFT      Descend / Flight Sprint Boost (24 m/s)\n" +
                "  LEFT ALT            Dodge Roll\n" +
                "  RMB / LMB + Mouse   Steer character / Free-orbit camera\n" +
                "  Mouse Wheel         Zoom camera in / out\n\n" +
                "INTERACTION & MOUNTS:\n" +
                "  TAB                 Target nearest enemy\n" +
                "  E                   Interact / Quest / Cache / Revive Ally\n" +
                "  R                   Respawn (when downed)\n" +
                "  Z / V               Summon-Dismount / Swap Mount", body);

            GUI.Label(new Rect(640, 155, 560, 340),
                "COMBAT & TALENTS:\n" +
                "  1 / 2 / 3           Class Strike / AOE / Shield-Heal\n" +
                "  4 / F / Q (Hold)    Health Potion / Interrupt / Shield Block\n" +
                "  N / C               Talents / Character Sheet\n\n" +
                "PANELS & CO-OP:\n" +
                "  I (or B) / M / J    Inventory / World Map / Quest Journal\n" +
                "  F6 / F7             Co-op Map Ping / Ready Check\n" +
                "  F5                  Manual Save Progress (Offline)\n" +
                "  ESC                 Close Window / Cancel Selection / Menu", body);

            if (GUI.Button(new Rect(60, 520, 220, 44), "Back", button))
            {
                page = returnPage;
            }
        }

        Color GetClassColor(string cls)
        {
            switch (cls)
            {
                case "Warrior": return new Color(0.85f, 0.22f, 0.16f); // Red
                case "Mage": return new Color(0.25f, 0.65f, 0.95f);    // Cyan
                case "Rogue": return new Color(0.98f, 0.90f, 0.25f);   // Yellow
                case "Paladin": return new Color(0.96f, 0.55f, 0.73f); // Pink
                default: return gold;
            }
        }

        static void Fill(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }
}



