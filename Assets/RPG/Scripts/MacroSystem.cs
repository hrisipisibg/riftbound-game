using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public class MacroSystem : MonoBehaviour
    {
        public static MacroSystem Instance { get; private set; }

        [Serializable]
        public class Macro
        {
            public string id;
            public string name;
            public string icon;
            [TextArea(3, 8)]
            public string body;

            public Macro(string id, string name, string icon, string body)
            {
                this.id = id;
                this.name = name;
                this.icon = icon;
                this.body = body;
            }
        }

        public List<Macro> macros = new List<Macro>();
        public int selectedMacroIndex = 0;
        public bool IsOpen { get; set; } = false;

        public static readonly string[] AvailableIcons = new string[]
        {
            "⚔", "🛡", "🏹", "🔥", "❄", "⚡", "💚", "💀", "⭐", "🌀", "🐾", "💎", "🍺", "🎺", "📜", "🎒"
        };

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            SeedDefaultMacros();
        }

        public void SeedDefaultMacros()
        {
            if (macros.Count > 0) return;

            macros.Add(new Macro(
                "macro_assist_burst",
                "Assist & Burst",
                "⚔",
                "/target Darien\n/assist\n/cast Cleave\n/party Assisting the tank on current target!"
            ));

            macros.Add(new Macro(
                "macro_emergency_heal",
                "Emergency Bandage",
                "💚",
                "/say Consuming emergency healing potion!\n/use Potion\n/cast Second Wind"
            ));

            macros.Add(new Macro(
                "macro_mark_skull",
                "Mark Skull Focus",
                "💀",
                "/mark 8\n/yell FOCUS THE SKULL TARGET FIRST!\n/cast Cleave"
            ));

            macros.Add(new Macro(
                "macro_guild_rally",
                "Guild Rally",
                "🛡",
                "/g Vanguard assemble! Ready check initiating.\n/rc\n/cheer"
            ));
        }

        public void ToggleWindow()
        {
            IsOpen = !IsOpen;
            AudioEffects.PlayClick();
        }

        public void ExecuteMacro(int index)
        {
            if (index < 0 || index >= macros.Count) return;
            ExecuteMacro(macros[index]);
        }

        public void ExecuteMacro(Macro macro)
        {
            if (macro == null || string.IsNullOrEmpty(macro.body)) return;

            string[] lines = macro.body.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var player = FindFirstObjectByType<PlayerController>();

            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;

                // Handle macro commands
                if (line.StartsWith("/"))
                {
                    ExecuteCommandLine(line, player);
                }
            }
        }

        void ExecuteCommandLine(string line, PlayerController player)
        {
            int firstSpace = line.IndexOf(' ');
            string cmd = (firstSpace > 0 ? line.Substring(1, firstSpace - 1) : line.Substring(1)).ToLowerInvariant();
            string args = firstSpace > 0 ? line.Substring(firstSpace + 1).Trim() : "";

            switch (cmd)
            {
                case "say":
                case "s":
                    if (ChatSystem.Instance)
                        ChatSystem.Instance.AddMessage(ChatSystem.Channel.Say, player ? player.CharacterName : "Player", args, Color.white);
                    break;

                case "yell":
                case "y":
                    if (ChatSystem.Instance)
                        ChatSystem.Instance.AddMessage(ChatSystem.Channel.Yell, player ? player.CharacterName : "Player", args, new Color(1f, 0.35f, 0.35f));
                    break;

                case "p":
                case "party":
                    if (ChatSystem.Instance)
                        ChatSystem.Instance.AddMessage(ChatSystem.Channel.Party, player ? player.CharacterName : "Player", args, new Color(0.67f, 0.83f, 1f));
                    break;

                case "g":
                case "guild":
                    if (ChatSystem.Instance)
                        ChatSystem.Instance.AddMessage(ChatSystem.Channel.Guild, player ? player.CharacterName : "Player", args, new Color(0.25f, 1f, 0.25f));
                    break;

                case "target":
                    if (player && !string.IsNullOrEmpty(args))
                    {
                        var combatants = FindObjectsByType<Combatant>(FindObjectsSortMode.None);
                        foreach (var c in combatants)
                        {
                            if (c != null && c.displayName.IndexOf(args, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                player.SetTarget(c);
                                ChatSystem.LogSystem($"Target set to {c.displayName}.");
                                break;
                            }
                        }
                    }
                    break;

                case "assist":
                    if (player && player.Target != null)
                    {
                        var party = PartySystem.Instance;
                        if (party != null)
                        {
                            foreach (var member in party.Members)
                            {
                                if (member.name.Equals(player.Target.displayName, StringComparison.OrdinalIgnoreCase))
                                {
                                    var boss = FindFirstObjectByType<WorldBossEncounter>();
                                    if (boss != null && boss.bossCombatant != null && boss.bossCombatant.Alive)
                                        player.SetTarget(boss.bossCombatant);
                                    break;
                                }
                            }
                        }
                    }
                    break;

                case "cast":
                    if (player && !string.IsNullOrEmpty(args))
                    {
                        if (args.IndexOf("Cleave", StringComparison.OrdinalIgnoreCase) >= 0 || args.IndexOf("Ability 2", StringComparison.OrdinalIgnoreCase) >= 0)
                            player.CastAbility2Public();
                        else if (args.IndexOf("Second Wind", StringComparison.OrdinalIgnoreCase) >= 0 || args.IndexOf("Heal", StringComparison.OrdinalIgnoreCase) >= 0 || args.IndexOf("Ability 3", StringComparison.OrdinalIgnoreCase) >= 0)
                            player.CastAbility3Public();
                        else
                            player.CastAbility1Public();
                    }
                    break;

                case "use":
                    if (player && !string.IsNullOrEmpty(args))
                    {
                        if (args.IndexOf("Potion", StringComparison.OrdinalIgnoreCase) >= 0)
                            player.UsePotionPublic();
                        else if (args.IndexOf("Hearthstone", StringComparison.OrdinalIgnoreCase) >= 0)
                            player.TriggerHearthstonePublic();
                    }
                    break;

                case "mark":
                case "rtm":
                    if (player && player.Target != null && int.TryParse(args, out int markIdx))
                    {
                        if (markIdx >= 1 && markIdx <= 8)
                            PartySystem.SetMarker(player.Target, (RaidTargetMarker)markIdx);
                    }
                    break;

                case "rc":
                case "readycheck":
                    if (PartySystem.Instance)
                        PartySystem.Instance.StartReadyCheck();
                    break;

                case "cheer":
                    if (ChatSystem.Instance)
                        ChatSystem.Instance.AddMessage(ChatSystem.Channel.Emote, player ? player.CharacterName : "Player", "cheers enthusiastically with battle vigor!", new Color(1f, 0.5f, 0.25f));
                    break;

                default:
                    // Fallback to chat parser
                    if (ChatSystem.Instance)
                        ChatSystem.Instance.ProcessInputString("/" + line.Substring(1));
                    break;
            }
        }

        public void CreateNewMacro(string name = "New Macro", string icon = "⚡", string body = "/say For Azeroth!")
        {
            var m = new Macro($"macro_{Guid.NewGuid().ToString().Substring(0, 8)}", name, icon, body);
            macros.Add(m);
            selectedMacroIndex = macros.Count - 1;
        }

        public void DeleteSelectedMacro()
        {
            if (macros.Count > 0 && selectedMacroIndex >= 0 && selectedMacroIndex < macros.Count)
            {
                macros.RemoveAt(selectedMacroIndex);
                if (selectedMacroIndex >= macros.Count) selectedMacroIndex = Mathf.Max(0, macros.Count - 1);
            }
        }

        // GUI Rendering
        public void DrawMacroGUI(float screenWidth, float screenHeight)
        {
            if (!IsOpen) return;

            float winW = 560f;
            float winH = 460f;
            float winX = (screenWidth - winW) * 0.5f;
            float winY = (screenHeight - winH) * 0.5f;

            // Background & Borders
            DrawRect(new Rect(winX - 3, winY - 3, winW + 6, winH + 6), new Color(0.85f, 0.70f, 0.28f, 0.95f));
            DrawRect(new Rect(winX, winY, winW, winH), new Color(0.06f, 0.08f, 0.12f, 0.97f));

            // Header
            DrawRect(new Rect(winX, winY, winW, 36f), new Color(0.12f, 0.14f, 0.18f, 1f));
            var headStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            headStyle.normal.textColor = new Color(1f, 0.85f, 0.35f);
            GUI.Label(new Rect(winX + 16, winY + 6, winW - 60, 24), "⚔ MACRO MANAGER & SCRIPT EDITOR (/macro)", headStyle);

            if (GUI.Button(new Rect(winX + winW - 32, winY + 6, 24, 24), "✕"))
            {
                IsOpen = false;
                AudioEffects.PlayClick();
            }

            // Left List: Macro Roster
            float listW = 200f;
            DrawRect(new Rect(winX + 12, winY + 46, listW, winH - 96), new Color(0.04f, 0.05f, 0.07f, 0.8f));

            var itemStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 8, 4, 4)
            };

            for (int i = 0; i < macros.Count; i++)
            {
                var m = macros[i];
                bool isSel = (i == selectedMacroIndex);
                if (isSel) GUI.color = new Color(1f, 0.85f, 0.3f);
                if (GUI.Button(new Rect(winX + 16, winY + 50 + i * 36, listW - 8, 32), $"{m.icon}  {m.name}", itemStyle))
                {
                    selectedMacroIndex = i;
                    AudioEffects.PlayClick();
                }
                GUI.color = Color.white;
            }

            // New & Delete Macro buttons
            if (GUI.Button(new Rect(winX + 12, winY + winH - 44, 95, 32), "+ New"))
            {
                CreateNewMacro();
                AudioEffects.PlayClick();
            }
            if (GUI.Button(new Rect(winX + 115, winY + winH - 44, 95, 32), "Delete"))
            {
                DeleteSelectedMacro();
                AudioEffects.PlayClick();
            }

            // Right Pane: Editor for Selected Macro
            float editX = winX + listW + 24;
            float editW = winW - listW - 36;
            if (selectedMacroIndex >= 0 && selectedMacroIndex < macros.Count)
            {
                var sel = macros[selectedMacroIndex];

                GUI.Label(new Rect(editX, winY + 48, 60, 20), "Name:", headStyle);
                sel.name = GUI.TextField(new Rect(editX + 55, winY + 48, editW - 120, 24), sel.name, 32);

                // Icon button (cycles through AvailableIcons)
                if (GUI.Button(new Rect(editX + editW - 55, winY + 46, 50, 28), sel.icon, headStyle))
                {
                    int curIconIdx = Array.IndexOf(AvailableIcons, sel.icon);
                    sel.icon = AvailableIcons[(curIconIdx + 1) % AvailableIcons.Length];
                    AudioEffects.PlayClick();
                }

                GUI.Label(new Rect(editX, winY + 82, editW, 20), "Macro Commands (e.g. /cast, /use, /say, /target):");
                sel.body = GUI.TextArea(new Rect(editX, winY + 106, editW, 250), sel.body, 500);

                // Action Buttons: Run Test & Save/Done
                if (GUI.Button(new Rect(editX, winY + winH - 44, 150, 32), "▶ Run Macro Test"))
                {
                    ExecuteMacro(sel);
                    AudioEffects.PlayClick();
                }

                var labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Italic };
                labelStyle.normal.textColor = new Color(0.65f, 0.75f, 0.85f);
                GUI.Label(new Rect(editX + 160, winY + winH - 40, editW - 160, 28), "Drag or assign to hotbars, or run via /runmacro.", labelStyle);
            }
            else
            {
                GUI.Label(new Rect(editX, winY + 100, editW, 40), "Select or create a macro from the list.");
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
