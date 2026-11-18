using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public class GlyphSystem : MonoBehaviour
    {
        public static GlyphSystem Instance { get; private set; }

        public enum GlyphType
        {
            Major,
            Minor
        }

        [Serializable]
        public class Glyph
        {
            public string id;
            public string name;
            public GlyphType type;
            public string characterClass;
            public string icon;
            public string description;
            public bool isLearned;

            public Glyph(string id, string name, GlyphType type, string cls, string icon, string desc, bool learned = true)
            {
                this.id = id;
                this.name = name;
                this.type = type;
                this.characterClass = cls;
                this.icon = icon;
                this.description = desc;
                this.isLearned = learned;
            }
        }

        public List<Glyph> glyphCatalog = new List<Glyph>();
        public string[] majorSockets = new string[3] { "", "", "" };
        public string[] minorSockets = new string[3] { "", "", "" };
        public bool IsWindowOpen { get; set; } = false;

        PlayerController player;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            player = GetComponent<PlayerController>() ?? FindFirstObjectByType<PlayerController>();
            SeedGlyphCatalog();
        }

        public void SeedGlyphCatalog()
        {
            if (glyphCatalog.Count > 0) return;

            // Warrior Glyphs
            glyphCatalog.Add(new Glyph("gly_cleave", "Glyph of Cleave", GlyphType.Major, "Warrior", "⚔", "Your Cleave ability now strikes 1 additional enemy."));
            glyphCatalog.Add(new Glyph("gly_mortal", "Glyph of Mortal Strike", GlyphType.Major, "Warrior", "🩸", "Increases the damage of Mortal Strike by 10%."));
            glyphCatalog.Add(new Glyph("gly_whirl", "Glyph of Whirlwind", GlyphType.Major, "Warrior", "🌀", "Reduces the cooldown of Whirlwind by 2 seconds."));
            glyphCatalog.Add(new Glyph("gly_charge", "Glyph of Charge", GlyphType.Minor, "Warrior", "⚡", "Increases the range of your Charge ability by 5 yards."));
            glyphCatalog.Add(new Glyph("gly_shout", "Glyph of Battle", GlyphType.Minor, "Warrior", "📢", "Increases the duration of Battle Shout by 2 minutes."));

            // Paladin Glyphs
            glyphCatalog.Add(new Glyph("gly_holy", "Glyph of Holy Light", GlyphType.Major, "Paladin", "✨", "Your Holy Light heals up to 5 nearby allies for 10% of amount healed."));
            glyphCatalog.Add(new Glyph("gly_judg", "Glyph of Judgement", GlyphType.Major, "Paladin", "⚖", "Increases the damage and critical chance of Judgement by 10%."));
            glyphCatalog.Add(new Glyph("gly_lay", "Glyph of Lay on Hands", GlyphType.Minor, "Paladin", "🙏", "Reduces cooldown of Lay on Hands by 5 minutes."));

            // Mage Glyphs
            glyphCatalog.Add(new Glyph("gly_fnova", "Glyph of Frost Nova", GlyphType.Major, "Mage", "❄", "Targets of Frost Nova can absorb 20% more damage before the freeze breaks."));
            glyphCatalog.Add(new Glyph("gly_fball", "Glyph of Fireball", GlyphType.Major, "Mage", "🔥", "Increases initial damage of Fireball by 5% and reduces cast time."));
            glyphCatalog.Add(new Glyph("gly_slowfall", "Glyph of Slow Fall", GlyphType.Minor, "Mage", "🪶", "Slow Fall no longer requires a feather reagent."));

            // General Minor Glyphs
            glyphCatalog.Add(new Glyph("gly_hearth", "Glyph of the Hearth", GlyphType.Minor, "All", "🍺", "Reduces the cooldown of your Hearthstone by 5 minutes."));
            glyphCatalog.Add(new Glyph("gly_breath", "Glyph of Water Walking", GlyphType.Minor, "All", "🌊", "Increases swim speed and allows walking on water surfaces."));
        }

        public bool SocketGlyph(GlyphType type, int slotIndex, string glyphId, out string feedback, bool bypassRestZone = false)
        {
            if (!player) player = FindFirstObjectByType<PlayerController>();

            if (slotIndex < 0 || slotIndex >= 3)
            {
                feedback = "Invalid socket index.";
                return false;
            }

            var glyph = glyphCatalog.Find(g => g.id == glyphId);
            if (glyph == null)
            {
                feedback = $"Glyph '{glyphId}' not found.";
                return false;
            }

            if (glyph.type != type)
            {
                feedback = $"Cannot socket a {glyph.type} glyph into a {type} slot.";
                return false;
            }

            // In Classic WoW: changing glyphs requires being in a rest sanctuary or tavern
            bool inRest = bypassRestZone || RestedXPSystem.Instance == null || RestedXPSystem.Instance.IsInRestZone;
            if (!inRest)
            {
                feedback = "Must be in a rest sanctuary (Tavern or Outpost) to scribe active glyphs.";
                ChatSystem.LogSystem($"<color=#ffaa00>📜 <b>[Inscription]</b> {feedback}</color>");
                AudioEffects.PlayClick();
                return false;
            }

            if (type == GlyphType.Major)
            {
                majorSockets[slotIndex] = glyphId;
            }
            else
            {
                minorSockets[slotIndex] = glyphId;
            }

            feedback = $"Inscribed {glyph.name} ({glyph.icon}) into {type} Slot {slotIndex + 1}!";
            ChatSystem.LogSystem($"<color=#ffd700>📜 <b>[Inscription]</b> {feedback}</color>");
            ZoneBanner.Show("GLYPH INSCRIBED", $"{glyph.icon} {glyph.name}");
            AudioEffects.PlayQuestComplete();
            return true;
        }

        public bool UnsocketGlyph(GlyphType type, int slotIndex, out string feedback)
        {
            if (slotIndex < 0 || slotIndex >= 3)
            {
                feedback = "Invalid socket index.";
                return false;
            }

            string currentId = (type == GlyphType.Major) ? majorSockets[slotIndex] : minorSockets[slotIndex];
            if (string.IsNullOrEmpty(currentId))
            {
                feedback = "Slot is already empty.";
                return false;
            }

            var glyph = glyphCatalog.Find(g => g.id == currentId);
            string gName = glyph != null ? glyph.name : "Glyph";

            if (type == GlyphType.Major) majorSockets[slotIndex] = "";
            else minorSockets[slotIndex] = "";

            feedback = $"Unsocketed {gName} from {type} Slot {slotIndex + 1}.";
            ChatSystem.LogSystem($"<color=#aaa>📜 [Inscription] {feedback}</color>");
            AudioEffects.PlayClick();
            return true;
        }

        public bool IsGlyphActive(string glyphId)
        {
            for (int i = 0; i < 3; i++)
            {
                if (majorSockets[i] == glyphId || minorSockets[i] == glyphId) return true;
            }
            return false;
        }

        public List<Glyph> GetLearnedGlyphsForClass(string cls)
        {
            return glyphCatalog.FindAll(g => g.isLearned && (g.characterClass.Equals(cls, StringComparison.OrdinalIgnoreCase) || g.characterClass == "All"));
        }

        public void ToggleWindow()
        {
            IsWindowOpen = !IsWindowOpen;
            AudioEffects.PlayClick();
        }

        // GUI Rendering
        public void DrawGlyphGUI(float screenWidth, float screenHeight)
        {
            if (!IsWindowOpen) return;

            float winW = 620f;
            float winH = 450f;
            float winX = (screenWidth - winW) * 0.5f;
            float winY = (screenHeight - winH) * 0.5f;

            // Border & Backing
            DrawRect(new Rect(winX - 3, winY - 3, winW + 6, winH + 6), new Color(0.85f, 0.70f, 0.28f, 0.95f));
            DrawRect(new Rect(winX, winY, winW, winH), new Color(0.06f, 0.08f, 0.12f, 0.98f));

            // Header Banner
            DrawRect(new Rect(winX, winY, winW, 38f), new Color(0.12f, 0.15f, 0.22f, 1f));
            var headStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            headStyle.normal.textColor = new Color(1f, 0.85f, 0.35f);
            GUI.Label(new Rect(winX + 16, winY + 6, winW - 60, 26), "📜 CLASS INSCRIPTION & GLYPH EMBROIDERY", headStyle);

            if (GUI.Button(new Rect(winX + winW - 32, winY + 6, 24, 24), "✕"))
            {
                IsWindowOpen = false;
                AudioEffects.PlayClick();
            }

            // Left Pane: 6 Inscription Sockets Wheel
            float wheelW = 280f;
            float wheelH = winH - 90f;
            DrawRect(new Rect(winX + 14, winY + 46, wheelW, wheelH), new Color(0.04f, 0.05f, 0.07f, 0.85f));

            var secHeader = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
            secHeader.normal.textColor = new Color(1f, 0.85f, 0.35f);

            // 3 Major Sockets
            GUI.Label(new Rect(winX + 22, winY + 52, wheelW - 16, 20), "MAJOR GLYPHS (3 SLOTS):", secHeader);
            for (int m = 0; m < 3; m++)
            {
                DrawSocketRow(winX + 20, winY + 74 + (m * 50), GlyphType.Major, m, majorSockets[m], wheelW - 12);
            }

            // 3 Minor Sockets
            GUI.Label(new Rect(winX + 22, winY + 234, wheelW - 16, 20), "MINOR GLYPHS (3 SLOTS):", secHeader);
            for (int n = 0; n < 3; n++)
            {
                DrawSocketRow(winX + 20, winY + 256 + (n * 50), GlyphType.Minor, n, minorSockets[n], wheelW - 12);
            }

            // Right Pane: Available Learned Glyphs for Class
            float grimoireX = winX + wheelW + 24;
            float grimoireW = winW - wheelW - 38;
            DrawRect(new Rect(grimoireX, winY + 46, grimoireW, wheelH), new Color(0.04f, 0.05f, 0.07f, 0.85f));

            string pClass = player ? player.CharacterClass : "Warrior";
            var learned = GetLearnedGlyphsForClass(pClass);
            GUI.Label(new Rect(grimoireX + 10, winY + 52, grimoireW - 20, 20), $"AVAILABLE GLYPHS ({pClass.ToUpperInvariant()}):", secHeader);

            float gY = winY + 76;
            for (int i = 0; i < learned.Count; i++)
            {
                var g = learned[i];
                if (gY + 56 > winY + 46 + wheelH) break;

                bool isSocketed = IsGlyphActive(g.id);
                Color gBg = isSocketed ? new Color(0.15f, 0.25f, 0.35f, 0.9f) : new Color(0.08f, 0.10f, 0.14f, 0.8f);
                DrawRect(new Rect(grimoireX + 8, gY, grimoireW - 16, 52), gBg);

                var icStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(grimoireX + 12, gY + 4, 38, 38), g.icon, icStyle);

                var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
                titleStyle.normal.textColor = isSocketed ? new Color(0.4f, 1f, 0.5f) : Color.white;
                string sockTag = isSocketed ? " [Active]" : "";
                GUI.Label(new Rect(grimoireX + 54, gY + 4, grimoireW - 120, 18), $"{g.name}{sockTag}", titleStyle);

                var descStyle = new GUIStyle(GUI.skin.label) { fontSize = 10 };
                descStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
                GUI.Label(new Rect(grimoireX + 54, gY + 22, grimoireW - 120, 28), g.description, descStyle);

                // Quick Inscribe Button
                if (!isSocketed)
                {
                    if (GUI.Button(new Rect(grimoireX + grimoireW - 68, gY + 12, 54, 28), "Socket"))
                    {
                        // Find first empty socket of corresponding type
                        int targetSlot = (g.type == GlyphType.Major) ? Array.IndexOf(majorSockets, "") : Array.IndexOf(minorSockets, "");
                        if (targetSlot < 0) targetSlot = 0; // overwrite slot 0
                        SocketGlyph(g.type, targetSlot, g.id, out _);
                    }
                }

                gY += 58;
            }

            // Bottom Footer
            var footStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter };
            footStyle.normal.textColor = new Color(0.65f, 0.65f, 0.65f);
            GUI.Label(new Rect(winX + 14, winY + winH - 36, winW - 28, 28), "Scribe ancient runes to augment class strikes, reduce cooldowns, and eliminate reagent costs.", footStyle);
        }

        void DrawSocketRow(float x, float y, GlyphType type, int slotIndex, string glyphId, float width)
        {
            var glyph = glyphCatalog.Find(g => g.id == glyphId);
            bool hasGlyph = glyph != null;

            DrawRect(new Rect(x, y, width, 44), new Color(0.08f, 0.10f, 0.14f, 0.85f));

            // Socket Rune Icon
            var rStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            string iconStr = hasGlyph ? glyph.icon : "⭕";
            GUI.Label(new Rect(x + 4, y + 2, 36, 36), iconStr, rStyle);

            // Name
            var nStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
            nStyle.normal.textColor = hasGlyph ? Color.white : new Color(0.5f, 0.5f, 0.5f);
            string nameStr = hasGlyph ? glyph.name : $"Empty {type} Socket";
            GUI.Label(new Rect(x + 44, y + 4, width - 85, 18), nameStr, nStyle);

            // Subtitle
            var subStyle = new GUIStyle(GUI.skin.label) { fontSize = 10 };
            subStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
            string subStr = hasGlyph ? glyph.description : "Click a glyph from the right grimoire to inscribe.";
            GUI.Label(new Rect(x + 44, y + 22, width - 85, 20), subStr, subStyle);

            // Remove Button
            if (hasGlyph)
            {
                if (GUI.Button(new Rect(x + width - 36, y + 8, 28, 26), "✕"))
                {
                    UnsocketGlyph(type, slotIndex, out _);
                }
            }
        }

        static Texture2D _flatTex;
        static void DrawRect(Rect rect, Color col)
        {
            if (_flatTex == null)
            {
                _flatTex = new Texture2D(1, 1);
                _flatTex.SetPixel(0, 0, Color.white);
                _flatTex.Apply();
            }
            var prev = GUI.color;
            GUI.color = col;
            GUI.DrawTexture(rect, _flatTex);
            GUI.color = prev;
        }
    }
}
