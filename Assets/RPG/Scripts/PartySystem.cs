using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public enum RaidTargetMarker
    {
        None = 0,
        Star = 1,       // ⭐ Yellow
        Circle = 2,     // 🟡 Orange
        Diamond = 3,    // 🔷 Purple
        Triangle = 4,   // 🔺 Green
        Moon = 5,       // 🌙 Silver / Light Blue
        Square = 6,     // 🟦 Blue
        Cross = 7,      // ❌ Red
        Skull = 8       // 💀 White
    }

    public enum ReadyStatus
    {
        Pending,
        Ready,
        NotReady
    }

    public class PartySystem : MonoBehaviour
    {
        public static PartySystem Instance
        {
            get
            {
                if (_instance == null) _instance = UnityEngine.Object.FindAnyObjectByType<PartySystem>();
                return _instance;
            }
            private set => _instance = value;
        }
        static PartySystem _instance;

        public class PartyMember
        {
            public string name;
            public string role; // "Healer", "Tank", "DPS"
            public string characterClass; // "Priest", "Warrior", "Mage", "Rogue"
            public int level;
            public float health;
            public float maxHealth;
            public float resource;
            public float maxResource;
            public Color classColor;
            public GameObject companionObject;
            public Combatant combatant;
            public float nextActionTime;
            public ReadyStatus readyStatus = ReadyStatus.Pending;
            public float readyDecisionTime;
        }

        List<PartyMember> members = new List<PartyMember>();
        public IReadOnlyList<PartyMember> Members => members;
        public bool IsCompanionMenuOpen { get; set; }

        static Dictionary<Combatant, RaidTargetMarker> markedUnits = new Dictionary<Combatant, RaidTargetMarker>();

        public static int MarkedUnitsCount => markedUnits.Count;

        // Ready Check state
        public bool ReadyCheckActive { get; private set; }
        public float ReadyCheckRemaining { get; private set; }
        public const float ReadyCheckDuration = 25f;
        public bool PlayerIsReady { get; private set; }
        string readyCheckInitiator = "";

        // Target marker flyout UI
        public bool MarkerFlyoutOpen { get; set; }

        PlayerController player;

        GUIStyle nameStyle, roleStyle, smallStyle, modalTitleStyle, modalBodyStyle, buttonStyle, markerStyle;
        Texture2D cardBgTex, borderTex, hpTex, manaTex, rageTex, energyTex, modalBgTex;

        void Awake()
        {
            if (_instance != null && _instance != this && _instance.gameObject.scene == gameObject.scene)
            {
                if (Application.isPlaying) Destroy(this); else DestroyImmediate(this);
                return;
            }
            Instance = this;
            player = GetComponent<PlayerController>();

        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
            foreach (var existing in members)
            {
                if (existing != null && existing.companionObject)
                {
                    if (Application.isPlaying) Destroy(existing.companionObject);
                    else DestroyImmediate(existing.companionObject);
                }
            }
        }

        public void SeedInitialParty(bool fullRoster = true)
        {
            foreach (var existing in members)
            {
                if (existing != null && existing.companionObject)
                {
                    if (Application.isPlaying) Destroy(existing.companionObject);
                    else DestroyImmediate(existing.companionObject);
                }
            }
            members.Clear();

            // Default classic party companions
            AddPartyMemberInternal("Valeria", "Priest", "Healer", new Color(1f, 1f, 1f));
            if (fullRoster)
            {
                AddPartyMemberInternal("Darien", "Warrior", "Tank", new Color(0.78f, 0.61f, 0.43f));
                AddPartyMemberInternal("Kelsey", "Mage", "DPS", new Color(0.25f, 0.78f, 0.92f));
                AddPartyMemberInternal("Vark", "Rogue", "DPS", new Color(1.0f, 0.96f, 0.41f));
            }
        }

        static (string race, string visualClass) ResolveCompanionArchetype(string cls, string role)
        {
            string c = (cls ?? "").Trim();
            if (c.Equals("Priest", StringComparison.OrdinalIgnoreCase) || role == "Healer") return ("Elf", "Paladin");
            if (c.Equals("Mage", StringComparison.OrdinalIgnoreCase)) return ("Human", "Mage");
            if (c.Equals("Rogue", StringComparison.OrdinalIgnoreCase)) return ("Goblin", "Rogue");
            if (c.Equals("Paladin", StringComparison.OrdinalIgnoreCase)) return ("Dwarf", "Paladin");
            return ("Human", "Warrior");
        }

        void AddPartyMemberInternal(string name, string cls, string role, Color col)
        {
            int slotIndex = members.Count;
            var member = new PartyMember
            {
                name = name,
                characterClass = cls,
                role = role,
                level = player ? player.Level : 3,
                health = 180f,
                maxHealth = 180f,
                resource = 100f,
                maxResource = 100f,
                classColor = col,
                readyStatus = ReadyStatus.Pending
            };

            if (Application.isPlaying)
            {
                // Create real 3D character follower in world (not a primitive capsule)
                var follower = new GameObject($"PartyCompanion_{name}");
                Vector3[] formationOffsets =
                {
                    new Vector3(-2.4f, 0f, -2.4f),
                    new Vector3( 2.4f, 0f, -1.8f),
                    new Vector3(-3.6f, 0f, -0.8f),
                    new Vector3( 3.4f, 0f, -2.8f)
                };
                Vector3 offset = formationOffsets[slotIndex % formationOffsets.Length];
                Vector3 spawnPos = (player ? player.transform.position : Vector3.zero) + offset;
                if (Physics.Raycast(spawnPos + Vector3.up * 3f, Vector3.down, out var hit, 10f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                    spawnPos.y = hit.point.y;
                follower.transform.position = spawnPos;

                var (race, visClass) = ResolveCompanionArchetype(cls, role);
                var visual = CharacterVisualFactory.CreateVisualInstance(race, visClass, follower.transform, isPreview: false);
                Animator compAnim = visual ? visual.GetComponentInChildren<Animator>() : null;
                if (compAnim)
                {
                    compAnim.applyRootMotion = false;
                    compAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    compAnim.Play("Idle", 0, 0f);
                }

                // Non-blocking trigger collider so companion is targetable/markable without pushing the player
                var triggerCol = follower.AddComponent<CapsuleCollider>();
                triggerCol.isTrigger = true;
                triggerCol.height = 1.85f;
                triggerCol.radius = 0.42f;
                triggerCol.center = new Vector3(0f, 0.92f, 0f);

                // Add Combatant component so companion is targetable and markable
                var cbt = follower.AddComponent<Combatant>();
                cbt.displayName = name;
                cbt.ConfigureVitals(member.maxHealth, 2f);
                cbt.Restore();
                member.combatant = cbt;

                var compAI = follower.AddComponent<CompanionAI>();
                compAI.Init(this, member, slotIndex, compAnim);

                member.companionObject = follower;
            }

            members.Add(member);
        }

        public void SpawnCompanion(string name, string cls, string role, Color col)
        {
            if (members.Count >= 4 || members.Exists(m => m.name.Equals(name, StringComparison.OrdinalIgnoreCase))) return;
            AddPartyMemberInternal(name, cls, role, col);
            ChatSystem.LogSystem($"[{name}] has joined your party as [{role}].");
        }

        public bool KickMember(string name)
        {
            int index = members.FindIndex(m => m.name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                var m = members[index];
                if (m.combatant) markedUnits.Remove(m.combatant);
                if (m.companionObject)
                {
                    if (Application.isPlaying) Destroy(m.companionObject);
                    else DestroyImmediate(m.companionObject);
                }
                members.RemoveAt(index);
                ChatSystem.LogSystem($"[{name}] was removed from the party.");
                return true;
            }
            return false;
        }

        // --- RAID TARGET MARKERS ENGINE ---

        public static string GetMarkerGlyph(RaidTargetMarker marker)
        {
            switch (marker)
            {
                case RaidTargetMarker.Star: return "⭐";
                case RaidTargetMarker.Circle: return "🟡";
                case RaidTargetMarker.Diamond: return "🔷";
                case RaidTargetMarker.Triangle: return "🔺";
                case RaidTargetMarker.Moon: return "🌙";
                case RaidTargetMarker.Square: return "🟦";
                case RaidTargetMarker.Cross: return "❌";
                case RaidTargetMarker.Skull: return "💀";
                default: return "";
            }
        }

        public static string GetMarkerName(RaidTargetMarker marker)
        {
            switch (marker)
            {
                case RaidTargetMarker.Star: return "Star";
                case RaidTargetMarker.Circle: return "Circle";
                case RaidTargetMarker.Diamond: return "Diamond";
                case RaidTargetMarker.Triangle: return "Triangle";
                case RaidTargetMarker.Moon: return "Moon";
                case RaidTargetMarker.Square: return "Square";
                case RaidTargetMarker.Cross: return "Cross";
                case RaidTargetMarker.Skull: return "Skull";
                default: return "None";
            }
        }

        public static Color GetMarkerColor(RaidTargetMarker marker)
        {
            switch (marker)
            {
                case RaidTargetMarker.Star: return new Color(1f, 0.92f, 0.2f);
                case RaidTargetMarker.Circle: return new Color(1f, 0.55f, 0.12f);
                case RaidTargetMarker.Diamond: return new Color(0.78f, 0.35f, 0.95f);
                case RaidTargetMarker.Triangle: return new Color(0.2f, 0.88f, 0.25f);
                case RaidTargetMarker.Moon: return new Color(0.75f, 0.88f, 1f);
                case RaidTargetMarker.Square: return new Color(0.25f, 0.55f, 1f);
                case RaidTargetMarker.Cross: return new Color(0.95f, 0.25f, 0.25f);
                case RaidTargetMarker.Skull: return new Color(0.95f, 0.95f, 0.95f);
                default: return Color.white;
            }
        }

        public static RaidTargetMarker GetMarker(Combatant unit)
        {
            if (!unit) return RaidTargetMarker.None;
            return markedUnits.TryGetValue(unit, out var m) ? m : RaidTargetMarker.None;
        }

        public static void SetMarker(Combatant unit, RaidTargetMarker marker)
        {
            if (!unit) return;

            if (marker == RaidTargetMarker.None)
            {
                if (markedUnits.ContainsKey(unit))
                {
                    markedUnits.Remove(unit);
                    ChatSystem.LogSystem($"Raid marker cleared from {unit.displayName}.");
                }
                return;
            }

            // If another unit currently has this marker, remove it from that unit
            var toRemove = new List<Combatant>();
            foreach (var kvp in markedUnits)
            {
                if (kvp.Value == marker && kvp.Key != unit) toRemove.Add(kvp.Key);
            }
            for (int i = 0; i < toRemove.Count; i++) markedUnits.Remove(toRemove[i]);

            markedUnits[unit] = marker;
            AudioEffects.PlayClick();
            ChatSystem.LogSystem($"[{GetMarkerGlyph(marker)} {GetMarkerName(marker)}] placed on {unit.displayName}.");
        }

        public static void ClearAllMarkers()
        {
            markedUnits.Clear();
            AudioEffects.PlayClick();
            ChatSystem.LogSystem("All raid target markers have been cleared.");
        }

        public static RaidTargetMarker ParseMarker(string input)
        {
            if (string.IsNullOrEmpty(input)) return RaidTargetMarker.None;
            string lower = input.Trim().ToLowerInvariant();
            switch (lower)
            {
                case "1":
                case "star": return RaidTargetMarker.Star;
                case "2":
                case "circle":
                case "orange": return RaidTargetMarker.Circle;
                case "3":
                case "diamond":
                case "purple": return RaidTargetMarker.Diamond;
                case "4":
                case "triangle":
                case "green": return RaidTargetMarker.Triangle;
                case "5":
                case "moon":
                case "silver": return RaidTargetMarker.Moon;
                case "6":
                case "square":
                case "blue": return RaidTargetMarker.Square;
                case "7":
                case "cross":
                case "red": return RaidTargetMarker.Cross;
                case "8":
                case "skull": return RaidTargetMarker.Skull;
                case "0":
                case "clear":
                case "none": return RaidTargetMarker.None;
                default: return RaidTargetMarker.None;
            }
        }

        // --- READY CHECK ENGINE ---

        public void StartReadyCheck(string initiator = "Player")
        {
            if (ReadyCheckActive) return;

            ReadyCheckActive = true;
            ReadyCheckRemaining = ReadyCheckDuration;
            readyCheckInitiator = initiator;
            PlayerIsReady = false;

            float now = Time.time;
            for (int i = 0; i < members.Count; i++)
            {
                members[i].readyStatus = ReadyStatus.Pending;
                // Companions decide to ready up after 1.2s to 3.0s
                members[i].readyDecisionTime = now + UnityEngine.Random.Range(1.2f, 3.2f);
            }

            ChatSystem.LogSystem($"[Ready Check] initiated by {initiator}. 25s remaining.");
            ZoneBanner.Show("READY CHECK INITIATED", "25 seconds remaining");
            AudioEffects.PlayLevelUp();
        }

        public void SetPlayerReady(bool ready)
        {
            if (!ReadyCheckActive) return;

            PlayerIsReady = ready;
            if (!ready)
            {
                ChatSystem.LogSystem($"[Ready Check] You voted: NOT READY.");
                ZoneBanner.Show("READY CHECK: NOT READY", "Member declined");
                EndReadyCheck(false);
            }
        }

        void EndReadyCheck(bool success)
        {
            ReadyCheckActive = false;
            ReadyCheckRemaining = 0f;

            if (success)
            {
                ChatSystem.LogSystem("[Ready Check] Everyone is READY!");
                ZoneBanner.Show("EVERYONE IS READY!", "All group members confirmed");
                AudioEffects.PlayQuestComplete();
            }
            else
            {
                ChatSystem.LogSystem("[Ready Check] Failed - not all members were ready.");
            }
        }

        void Update()
        {
            if (!player) player = GetComponent<PlayerController>();
            if (!player) return;

            // Sync party member levels and vitals
            for (int i = 0; i < members.Count; i++)
            {
                var m = members[i];
                m.level = player.Level;
                m.maxHealth = 150f + player.Level * 30f;
                m.health = Mathf.Min(m.health + 5f * Time.deltaTime, m.maxHealth);
                m.resource = Mathf.Min(m.resource + 8f * Time.deltaTime, m.maxResource);

                if (m.combatant)
                {
                    m.combatant.ConfigureVitals(m.maxHealth, 2f);
                }
            }

            // Ready Check update
            if (ReadyCheckActive)
            {
                ReadyCheckRemaining -= Time.deltaTime;

                float now = Time.time;
                bool allCompanionsReady = true;

                for (int i = 0; i < members.Count; i++)
                {
                    var m = members[i];
                    if (m.readyStatus == ReadyStatus.Pending)
                    {
                        if (now >= m.readyDecisionTime)
                        {
                            m.readyStatus = ReadyStatus.Ready;
                            ChatSystem.LogSystem($"[Ready Check] {m.name} is READY.");
                        }
                        else
                        {
                            allCompanionsReady = false;
                        }
                    }
                }

                if (PlayerIsReady && allCompanionsReady)
                {
                    EndReadyCheck(true);
                }
                else if (ReadyCheckRemaining <= 0f)
                {
                    EndReadyCheck(false);
                }
            }
        }

        // --- GUI DRAWING ---

        public void DrawPartyGUI(float screenW, float screenH)
        {
            if (FrontendMenu.IsOpen) return;

            InitStyles();

            // 1. Draw 3D Floating Raid Markers over all marked units
            DrawWorldSpaceRaidMarkers();

            // 2. Draw Target Frame Raid Marker & Flyout
            if (members.Count > 0 || MarkerFlyoutOpen) DrawTargetMarkerWidget();

            // 3. Draw Low-Profile Party Member Frames
            DrawPartyFrames();
            if (IsCompanionMenuOpen) DrawCompanionMenu(screenW, screenH);

            // 4. Draw Ready Check Modal Dialog (only offline; online peers use CoopWorldManager Ready Check)
            if (ReadyCheckActive && !Network.CoopSessionConfig.IsMultiplayer)
            {
                DrawReadyCheckModal(screenW, screenH);
            }
        }

        public void ToggleCompanionMenu() { IsCompanionMenuOpen = !IsCompanionMenuOpen; }
        public void CloseCompanionMenu() { IsCompanionMenuOpen = false; }

        void DrawCompanionMenu(float screenW, float screenH)
        {
            float x = (screenW - 480f) * .5f, y = (screenH - 350f) * .5f;
            GUI.DrawTexture(new Rect(x, y, 480f, 350f), modalBgTex);
            GUI.Label(new Rect(x + 20f, y + 16f, 420f, 30f), "COMPANIONS", modalTitleStyle);
            GUI.Label(new Rect(x + 20f, y + 52f, 440f, 40f), "Adventure solo, or call allies when you need support.", modalBodyStyle);
            string[] names = { "Valeria", "Darien", "Kelsey", "Vark" };
            string[] classes = { "Priest", "Warrior", "Mage", "Rogue" };
            string[] roles = { "Healer", "Tank", "DPS", "DPS" };
            Color[] colors = { Color.white, new Color(.78f, .61f, .43f), new Color(.25f, .78f, .92f), new Color(1f, .96f, .41f) };
            bool canChange = player && player.Stats && player.Stats.Alive && !player.InCombat();
            for (int i = 0; i < names.Length; i++)
            {
                float rowY = y + 102f + i * 45f;
                string memberName = names[i];
                bool recruited = members.Exists(m => m.name == memberName);
                GUI.Label(new Rect(x + 20f, rowY, 300f, 28f), names[i] + "  ·  " + roles[i], modalBodyStyle);
                GUI.enabled = canChange;
                if (GUI.Button(new Rect(x + 342f, rowY, 116f, 30f), recruited ? "Dismiss" : "Call ally", buttonStyle))
                {
                    if (recruited) KickMember(memberName);
                    else SpawnCompanion(memberName, classes[i], roles[i], colors[i]);
                }
                GUI.enabled = true;
            }
            GUI.Label(new Rect(x + 20f, y + 286f, 300f, 22f), canChange ? "Allies reset at character selection." : "Leave combat to change companions.", smallStyle);
            if (GUI.Button(new Rect(x + 342f, y + 305f, 116f, 28f), "Close", buttonStyle)) CloseCompanionMenu();
        }

        void DrawWorldSpaceRaidMarkers()
        {
            if (markedUnits.Count == 0 || Camera.main == null) return;

            var cam = Camera.main;
            foreach (var kvp in markedUnits)
            {
                var unit = kvp.Key;
                var marker = kvp.Value;
                if (!unit || !unit.Alive || marker == RaidTargetMarker.None) continue;

                float dist = player ? Vector3.Distance(player.transform.position, unit.transform.position) : 10f;
                if (dist > 45f) continue;

                Vector3 worldTarget = unit.transform.position + Vector3.up * 2.85f;
                Vector3 screenPos = cam.WorldToScreenPoint(worldTarget);
                if (screenPos.z <= 0.1f) continue;

                float size = Mathf.Clamp(34f - dist * 0.35f, 22f, 34f);
                float rx = screenPos.x - size * 0.5f;
                float ry = Screen.height - screenPos.y - size * 0.5f;

                // Dark circular backing
                GUI.DrawTexture(new Rect(rx - 2f, ry - 2f, size + 4f, size + 4f), modalBgTex);
                GUI.DrawTexture(new Rect(rx, ry, size, size), cardBgTex);

                // Marker Glyph
                markerStyle.fontSize = Mathf.RoundToInt(size * 0.65f);
                markerStyle.normal.textColor = GetMarkerColor(marker);
                GUI.Label(new Rect(rx, ry, size, size), GetMarkerGlyph(marker), markerStyle);
            }
        }

        void DrawTargetMarkerWidget()
        {
            if (!player || !player.Target)
            {
                MarkerFlyoutOpen = false;
                return;
            }

            // Target frame position in PrototypeHUD:
            // tfx = pfx + pfw + 10 = 24 + 224 + 10 = 258f, tfy = 20f, tfw = 205f
            float tfx = 258f, tfy = 20f, tfw = 205f;

            // Small Raid Marker Button at top-right corner of target frame
            var currentMarker = GetMarker(player.Target);
            string btnText = currentMarker != RaidTargetMarker.None ? GetMarkerGlyph(currentMarker) : "⭐";
            Color btnCol = currentMarker != RaidTargetMarker.None ? GetMarkerColor(currentMarker) : new Color(0.8f, 0.75f, 0.4f);

            var oldCol = GUI.color;
            GUI.color = btnCol;
            if (GUI.Button(new Rect(tfx + tfw - 22f, tfy + 3f, 19f, 16f), btnText, smallStyle))
            {
                MarkerFlyoutOpen = !MarkerFlyoutOpen;
                AudioEffects.PlayClick();
            }
            GUI.color = oldCol;

            // Marker Flyout Bar (Horizontal row of 8 icons + clear)
            if (MarkerFlyoutOpen)
            {
                float barW = 220f, barH = 28f;
                float barX = tfx + tfw - barW;
                float barY = tfy - barH - 4f;

                GUI.DrawTexture(new Rect(barX - 1f, barY - 1f, barW + 2f, barH + 2f), borderTex);
                GUI.DrawTexture(new Rect(barX, barY, barW, barH), cardBgTex);

                float btnW = 22f;
                for (int i = 1; i <= 8; i++)
                {
                    var m = (RaidTargetMarker)i;
                    var mCol = GetMarkerColor(m);
                    var style = new GUIStyle(markerStyle) { fontSize = 13 };
                    style.normal.textColor = mCol;

                    float bx = barX + 3f + (i - 1) * (btnW + 2f);
                    if (GUI.Button(new Rect(bx, barY + 3f, btnW, 22f), GetMarkerGlyph(m), style))
                    {
                        SetMarker(player.Target, m);
                        MarkerFlyoutOpen = false;
                    }
                }

                // Clear button
                float cx = barX + 3f + 8 * (btnW + 2f);
                if (GUI.Button(new Rect(cx, barY + 3f, btnW, 22f), "✖", smallStyle))
                {
                    SetMarker(player.Target, RaidTargetMarker.None);
                    MarkerFlyoutOpen = false;
                }
            }
        }

        void DrawPartyFrames()
        {
            if (members.Count == 0) return;

            // Stack below player unit frame (Player frame height = 52, y = 20)
            float startX = 24f;
            float startY = 78f;
            float cardW = 155f;
            float cardH = 34f;
            float gap = 4f;

            for (int i = 0; i < members.Count; i++)
            {
                var m = members[i];
                float cy = startY + i * (cardH + gap);
                Rect cardRect = new Rect(startX, cy, cardW, cardH);

                // Backing and border
                GUI.DrawTexture(new Rect(startX - 1f, cy - 1f, cardW + 2f, cardH + 2f), borderTex);
                GUI.DrawTexture(cardRect, cardBgTex);

                // Role Icon: 🛡 Tank, 💚 Healer, ⚔ DPS
                string roleIcon = m.role == "Healer" ? "💚" : m.role == "Tank" ? "🛡" : "⚔";
                roleStyle.normal.textColor = m.role == "Healer" ? new Color(0.3f, 1f, 0.4f) :
                                             m.role == "Tank" ? new Color(0.4f, 0.75f, 1f) :
                                             new Color(1f, 0.4f, 0.4f);
                GUI.Label(new Rect(startX + 4f, cy + 2f, 18f, 16f), roleIcon, roleStyle);

                // Name in Class Color
                nameStyle.normal.textColor = m.classColor;
                GUI.Label(new Rect(startX + 22f, cy + 2f, 95f, 16f), $"{m.name}", nameStyle);

                // Target Marker Icon on party frame (if marked)
                var unitMarker = m.combatant ? GetMarker(m.combatant) : RaidTargetMarker.None;
                if (unitMarker != RaidTargetMarker.None)
                {
                    var mStyle = new GUIStyle(markerStyle) { fontSize = 12 };
                    mStyle.normal.textColor = GetMarkerColor(unitMarker);
                    GUI.Label(new Rect(startX + cardW - 36f, cy + 2f, 16f, 16f), GetMarkerGlyph(unitMarker), mStyle);
                }

                // Ready Check Status Badge (if ready check active)
                if (ReadyCheckActive)
                {
                    string readyGlyph = m.readyStatus == ReadyStatus.Ready ? "✔" :
                                        m.readyStatus == ReadyStatus.NotReady ? "✖" : "?";
                    Color readyCol = m.readyStatus == ReadyStatus.Ready ? new Color(0.2f, 0.9f, 0.3f) :
                                     m.readyStatus == ReadyStatus.NotReady ? new Color(0.9f, 0.2f, 0.2f) :
                                     new Color(1f, 0.75f, 0.2f);
                    var rStyle = new GUIStyle(roleStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 11 };
                    rStyle.normal.textColor = readyCol;
                    GUI.Label(new Rect(startX + cardW - 18f, cy + 2f, 16f, 16f), readyGlyph, rStyle);
                }

                // Health Bar (11px)
                float hpRatio = Mathf.Clamp01(m.health / m.maxHealth);
                float barW = cardW - 8f;
                GUI.DrawTexture(new Rect(startX + 4f, cy + 18f, barW, 10f), cardBgTex);
                GUI.DrawTexture(new Rect(startX + 4f, cy + 18f, barW * hpRatio, 10f), hpTex);
                GUI.Label(new Rect(startX + 4f, cy + 16f, barW, 12f), $"{Mathf.CeilToInt(m.health)}/{m.maxHealth}", smallStyle);

                // Resource Bar (3px)
                float resRatio = Mathf.Clamp01(m.resource / m.maxResource);
                Texture2D resTex = m.characterClass == "Warrior" ? rageTex :
                                   m.characterClass == "Rogue" ? energyTex : manaTex;
                GUI.DrawTexture(new Rect(startX + 4f, cy + 29f, barW * resRatio, 3f), resTex);

                // Clicking party frame selects that member as player's target
                if (Event.current.type == EventType.MouseDown && cardRect.Contains(Event.current.mousePosition))
                {
                    if (player)
                    {
                        if (m.combatant) player.SetTarget(m.combatant);
                        AudioEffects.PlayClick();
                    }
                    Event.current.Use();
                }
            }
        }

        void DrawReadyCheckModal(float screenW, float screenH)
        {
            float modalW = 320f, modalH = 135f;
            float modalX = (screenW - modalW) * 0.5f;
            float modalY = screenH * 0.28f;

            // Modal backing
            GUI.DrawTexture(new Rect(modalX - 2f, modalY - 2f, modalW + 4f, modalH + 4f), borderTex);
            GUI.DrawTexture(new Rect(modalX, modalY, modalW, modalH), modalBgTex);

            // Title
            GUI.Label(new Rect(modalX, modalY + 12f, modalW, 22f), "READY CHECK", modalTitleStyle);

            // Timer & prompt
            int secs = Mathf.CeilToInt(ReadyCheckRemaining);
            string prompt = PlayerIsReady ? $"You are READY! Waiting for party... ({secs}s)" :
                                            $"Are you ready? ({secs}s remaining)";
            GUI.Label(new Rect(modalX + 15f, modalY + 38f, modalW - 30f, 32f), prompt, modalBodyStyle);

            // Action buttons
            if (!PlayerIsReady)
            {
                var greenCol = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.2f, 0.85f, 0.3f);
                if (GUI.Button(new Rect(modalX + 30f, modalY + 84f, 120f, 32f), "✔ YES, READY", buttonStyle))
                {
                    SetPlayerReady(true);
                    AudioEffects.PlayQuestComplete();
                }

                GUI.backgroundColor = new Color(0.85f, 0.25f, 0.25f);
                if (GUI.Button(new Rect(modalX + 170f, modalY + 84f, 120f, 32f), "✖ NOT READY", buttonStyle))
                {
                    SetPlayerReady(false);
                }
                GUI.backgroundColor = greenCol;
            }
            else
            {
                // When already confirmed
                var greenStyle = new GUIStyle(modalBodyStyle) { fontStyle = FontStyle.Bold };
                greenStyle.normal.textColor = new Color(0.2f, 0.9f, 0.3f);
                GUI.Label(new Rect(modalX, modalY + 88f, modalW, 24f), "✔ Confirmed: READY", greenStyle);
            }
        }

        void InitStyles()
        {
            if (cardBgTex != null) return;

            cardBgTex = new Texture2D(1, 1);
            cardBgTex.SetPixel(0, 0, new Color(0.06f, 0.08f, 0.12f, 0.90f));
            cardBgTex.Apply();

            modalBgTex = new Texture2D(1, 1);
            modalBgTex.SetPixel(0, 0, new Color(0.04f, 0.05f, 0.08f, 0.96f));
            modalBgTex.Apply();

            borderTex = new Texture2D(1, 1);
            borderTex.SetPixel(0, 0, new Color(0.85f, 0.70f, 0.28f, 0.95f)); // Gold WoW border
            borderTex.Apply();

            hpTex = new Texture2D(1, 1);
            hpTex.SetPixel(0, 0, new Color(0.18f, 0.75f, 0.22f));
            hpTex.Apply();

            manaTex = new Texture2D(1, 1);
            manaTex.SetPixel(0, 0, new Color(0.18f, 0.48f, 0.95f));
            manaTex.Apply();

            rageTex = new Texture2D(1, 1);
            rageTex.SetPixel(0, 0, new Color(0.85f, 0.18f, 0.12f));
            rageTex.Apply();

            energyTex = new Texture2D(1, 1);
            energyTex.SetPixel(0, 0, new Color(0.95f, 0.85f, 0.15f));
            energyTex.Apply();

            nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };

            roleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft
            };

            markerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };

            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 9,
                alignment = TextAnchor.MiddleCenter
            };
            smallStyle.normal.textColor = Color.white;

            modalTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            modalTitleStyle.normal.textColor = new Color(1f, 0.85f, 0.25f);

            modalBodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter
            };
            modalBodyStyle.normal.textColor = Color.white;

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
        }
    }

    public class CompanionAI : MonoBehaviour
    {
        PartySystem party;
        PartySystem.PartyMember data;
        PlayerController player;
        Animator anim;
        int slotIndex;
        string animState = "";
        float attackAnimUntil;

        static readonly Vector3[] FormationSlots =
        {
            new Vector3(-2.4f, 0f, -2.4f),
            new Vector3( 2.4f, 0f, -1.8f),
            new Vector3(-3.6f, 0f, -1.0f),
            new Vector3( 3.4f, 0f, -2.8f)
        };

        public void Init(PartySystem p, PartySystem.PartyMember m, int index = 0, Animator animator = null)
        {
            party = p;
            data = m;
            slotIndex = index;
            anim = animator ?? GetComponentInChildren<Animator>();
        }

        static Vector3 SnapGround(Vector3 pos)
        {
            if (Physics.Raycast(pos + Vector3.up * 2.5f, Vector3.down, out var hit, 8f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                pos.y = hit.point.y;
            return pos;
        }

        void PlayState(string next)
        {
            if (!anim || animState == next) return;
            if (anim.HasState(0, Animator.StringToHash(next)))
            {
                anim.CrossFadeInFixedTime(next, 0.16f, 0);
                animState = next;
            }
        }

        void Update()
        {
            if (!player) player = FindFirstObjectByType<PlayerController>();
            if (!player || !player.Stats || !player.Stats.Alive) return;
            if (FrontendMenu.IsOpen)
            {
                PlayState("Idle");
                return;
            }

            Vector3 localSlot = FormationSlots[Mathf.Abs(slotIndex) % FormationSlots.Length];
            Vector3 followTarget = SnapGround(player.transform.position + player.transform.right * localSlot.x + player.transform.forward * localSlot.z);
            Vector3 flatDelta = followTarget - transform.position;
            flatDelta.y = 0f;
            float distToPlayer = flatDelta.magnitude;

            // Follow player locomotion
            if (distToPlayer > 22f)
            {
                transform.position = followTarget;
                PlayState("Idle");
            }
            else if (distToPlayer > 1.35f)
            {
                Vector3 moveDir = flatDelta.normalized;
                bool walk = player.Walking || distToPlayer < 2.4f;
                float spd = walk ? 2.4f : 5.8f;
                transform.position = SnapGround(transform.position + moveDir * spd * Time.deltaTime);
                if (moveDir.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(moveDir), Time.deltaTime * 7f);
                if (Time.time >= attackAnimUntil)
                    PlayState(walk ? "Walk" : "Run");
            }
            else
            {
                transform.position = SnapGround(transform.position);
                if (Time.time >= attackAnimUntil)
                    PlayState("Idle");
            }

            // Combat AI
            if (Time.time >= data.nextActionTime)
            {
                // 1. Healer AI: prioritize low-health player
                if (data.role == "Healer" && player.Stats.Health < player.Stats.maxHealth * 0.70f && data.resource >= 25f)
                {
                    data.resource -= 25f;
                    data.nextActionTime = Time.time + 3.0f;
                    float healVal = 45f + player.Level * 6f;
                    player.Stats.Heal(healVal);
                    CombatTextManager.SpawnHeal(player.transform.position, healVal);
                    ChatSystem.LogCombat($"{data.name}'s Holy Radiance heals you for {Mathf.RoundToInt(healVal)} health.");
                    attackAnimUntil = Time.time + 0.85f;
                    animState = "";
                    PlayState("Attack");
                    return;
                }

                // 2. Attack player's target
                if (player.Target && player.Target.Alive && Vector3.Distance(transform.position, player.Target.transform.position) < 18f && data.resource >= 15f)
                {
                    data.resource -= 15f;
                    data.nextActionTime = Time.time + 2.2f;
                    float dmg = data.role == "Tank" ? 14f + player.Level * 2f : 22f + player.Level * 4f;
                    player.Target.Damage(dmg);
                    CombatTextManager.SpawnDamage(player.Target.transform.position, dmg, false);
                    string ability = data.role == "Tank" ? "Shield Slam" : data.characterClass == "Mage" ? "Fireblast" : "Sinister Strike";
                    ChatSystem.LogCombat($"{data.name}'s {ability} strikes {player.Target.displayName} for {Mathf.RoundToInt(dmg)} damage.");
                    Vector3 faceTarget = player.Target.transform.position - transform.position;
                    faceTarget.y = 0f;
                    if (faceTarget.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(faceTarget);
                    attackAnimUntil = Time.time + 0.85f;
                    animState = "";
                    PlayState("Attack");
                }
            }
        }
    }
}
