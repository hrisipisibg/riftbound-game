using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public class DungeonFinderSystem : MonoBehaviour
    {
        public static DungeonFinderSystem Instance { get; private set; }

        public enum GroupRole
        {
            Tank,
            Healer,
            Damage
        }

        public enum QueueState
        {
            None,
            Queued,
            ProposalReady,
            InDungeon
        }

        [Serializable]
        public class DungeonEntry
        {
            public string id;
            public string name;
            public string description;
            public int minLevel;
            public int recLevel;
            public string icon;
            public Vector3 entranceCoordinates;
            public DungeonInstanceManager.DungeonDifficulty difficulty;
            public string bonusReward;

            public DungeonEntry(string id, string name, string description, int minLevel, int recLevel, string icon, Vector3 entranceCoordinates, DungeonInstanceManager.DungeonDifficulty difficulty, string bonusReward)
            {
                this.id = id;
                this.name = name;
                this.description = description;
                this.minLevel = minLevel;
                this.recLevel = recLevel;
                this.icon = icon;
                this.entranceCoordinates = entranceCoordinates;
                this.difficulty = difficulty;
                this.bonusReward = bonusReward;
            }
        }

        public List<DungeonEntry> dungeons = new List<DungeonEntry>();
        public int selectedDungeonIndex = 0;
        public bool IsWindowOpen { get; set; } = false;

        // Player Queue State
        public QueueState State { get; private set; } = QueueState.None;
        public GroupRole SelectedRole { get; set; } = GroupRole.Damage;
        public float QueueStartTime { get; private set; } = 0f;
        public float EstimatedWaitTime { get; private set; } = 8f; // Short queue simulation
        public float ProposalCountdown { get; private set; } = 0f;
        public const float ProposalDuration = 15f;

        // Group Proposal Accepted Status (5-man: Tank, Healer, 3 DPS)
        public bool[] proposalReady = new bool[5];
        public string[] groupRoleNames = new string[] { "🛡 Tank (Darien)", "💚 Healer (Valeria)", "⚔ DPS (Player)", "⚔ DPS (Kelsey)", "⚔ DPS (Vark)" };

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
            SeedDungeons();
        }

        public void SeedDungeons()
        {
            if (dungeons.Count > 0) return;

            dungeons.Add(new DungeonEntry(
                "scarlet_crypts_norm",
                "The Scarlet Crypts",
                "Deep beneath Duskwood Cathedral lies an ancient subterranean necropolis corrupted by rogue crusaders and dark necromancy. 4 Boss Encounters.",
                1,
                3,
                "💀",
                DungeonInstanceManager.ScarletCryptsOrigin,
                DungeonInstanceManager.DungeonDifficulty.Normal,
                "Satchel of Helpful Goods + 45 XP + 15 Gold"
            ));

            dungeons.Add(new DungeonEntry(
                "scarlet_crypts_heroic",
                "The Scarlet Crypts (Heroic)",
                "Empowered crusader champions with 250% health, lethal spell rotations, and bonus Truesilver crafting reagents.",
                3,
                5,
                "🔥",
                DungeonInstanceManager.ScarletCryptsOrigin,
                DungeonInstanceManager.DungeonDifficulty.Heroic,
                "Heroic Cache + Emblem of Valor + 40 Gold"
            ));

            dungeons.Add(new DungeonEntry(
                "wyrmcrest_pinnacle",
                "Pinnacle of Wyrmcrest",
                "High summit roost towering over the continent. Face dragonkin lieutenants before challenging Vermithrax the Sovereign.",
                4,
                5,
                "🐉",
                new Vector3(0f, 48.6f, 218f),
                DungeonInstanceManager.DungeonDifficulty.Normal,
                "Wyrmscale Satchel + 60 XP + 25 Gold"
            ));
        }

        void Update()
        {
            if (State == QueueState.Queued)
            {
                float elapsed = Time.time - QueueStartTime;
                if (elapsed >= EstimatedWaitTime)
                {
                    // Match found! Trigger proposal ready popup
                    TriggerDungeonProposal();
                }
            }
            else if (State == QueueState.ProposalReady)
            {
                ProposalCountdown -= Time.deltaTime;
                if (ProposalCountdown <= 0f)
                {
                    // Timeout
                    CancelQueue("Dungeon proposal timed out.");
                }
            }
        }

        public void ToggleWindow()
        {
            IsWindowOpen = !IsWindowOpen;
            AudioEffects.PlayClick();
        }

        public void JoinQueue()
        {
            if (State != QueueState.None) return;

            State = QueueState.Queued;
            QueueStartTime = Time.time;
            // Shorter queue for Tanks/Healers, slightly longer for DPS
            EstimatedWaitTime = SelectedRole == GroupRole.Tank ? 3.5f : SelectedRole == GroupRole.Healer ? 4.5f : 8f;

            string dName = (selectedDungeonIndex >= 0 && selectedDungeonIndex < dungeons.Count) ? dungeons[selectedDungeonIndex].name : "Random Dungeon";
            ChatSystem.LogSystem($"<color=#70df80>[Dungeon Finder] Queued for {dName} as {SelectedRole}. (Est. wait: {Mathf.CeilToInt(EstimatedWaitTime)}s)</color>");
            ZoneBanner.Show("DUNGEON QUEUE", $"Role: {SelectedRole}  ·  Searching for group...");
            AudioEffects.PlayClick();
        }

        public void CancelQueue(string reason = "Left dungeon finder queue.")
        {
            if (State == QueueState.None) return;
            State = QueueState.None;
            ChatSystem.LogSystem($"<color=#ffaa40>[Dungeon Finder] {reason}</color>");
            AudioEffects.PlayClick();
        }

        void TriggerDungeonProposal()
        {
            State = QueueState.ProposalReady;
            ProposalCountdown = ProposalDuration;

            // Reset proposal flags; companions accept automatically within 1.5s
            for (int i = 0; i < proposalReady.Length; i++) proposalReady[i] = false;
            proposalReady[0] = true; // Tank accepted
            proposalReady[1] = true; // Healer accepted
            proposalReady[3] = true; // DPS Kelsey accepted
            proposalReady[4] = true; // DPS Vark accepted

            ZoneBanner.Show("DUNGEON READY!", "Your dungeon group is assembled! Click Enter.");
            AudioEffects.PlayBossVictory();
        }

        public void AcceptDungeonProposal()
        {
            if (State != QueueState.ProposalReady) return;

            proposalReady[2] = true; // Player accepted
            State = QueueState.InDungeon;

            var d = (selectedDungeonIndex >= 0 && selectedDungeonIndex < dungeons.Count) ? dungeons[selectedDungeonIndex] : dungeons[0];

            // Set difficulty
            if (DungeonInstanceManager.Instance && player)
            {
                DungeonInstanceManager.Instance.SetDifficulty(d.difficulty, player, out _);
            }

            // Teleport player directly into the instance
            if (!player) player = FindFirstObjectByType<PlayerController>();
            if (player)
            {
                Vector3 entryPos = d.entranceCoordinates;
                if (Vector3.Distance(entryPos, DungeonInstanceManager.ScarletCryptsOrigin) < 5f)
                    entryPos = new Vector3(-400f, -39.0f, 196f);
                var cc = player.GetComponent<CharacterController>();
                if (cc) cc.enabled = false;
                player.transform.position = entryPos;
                if (cc) cc.enabled = true;
            }

            // Spawn or assemble companions in party
            if (PartySystem.Instance)
            {
                PartySystem.Instance.SeedInitialParty(true);
            }

            ZoneBanner.Show("ENTERED DUNGEON", d.name.ToUpperInvariant());
            ChatSystem.LogSystem($"<color=#70df80>[Dungeon Finder] Teleported to {d.name}! Role bonus will be granted upon boss defeat.</color>");
            AudioEffects.PlayQuestComplete();

            IsWindowOpen = false;
        }

        public void LeaveDungeonGroup()
        {
            State = QueueState.None;
            // Teleport back to safe outpost
            if (!player) player = FindFirstObjectByType<PlayerController>();
            if (player)
            {
                Vector3 recall = (RestedXPSystem.Instance != null && RestedXPSystem.Instance.HearthHomePosition != Vector3.zero) ? RestedXPSystem.Instance.HearthHomePosition : new Vector3(0f, 0.5f, 0f);
                recall = PlayerController.ClampAboveGround(recall, 0.30f);
                var cc = player.GetComponent<CharacterController>();
                if (cc) cc.enabled = false;
                player.transform.position = recall;
                if (cc) cc.enabled = true;
            }
            ZoneBanner.Show("LEFT DUNGEON", "Teleported back to sanctuary.");
            ChatSystem.LogSystem("[Dungeon Finder] You have left the dungeon instance.");
        }

        // GUI Rendering
        public void DrawDungeonFinderGUI(float screenWidth, float screenHeight)
        {
            // Mini Queue Eye Radar Widget (Upper Right under Minimap or Top Center)
            if (State == QueueState.Queued)
            {
                float qx = screenWidth - 220f;
                float qy = 135f;
                DrawRect(new Rect(qx - 2, qy - 2, 204, 32), new Color(0.85f, 0.70f, 0.28f, 0.95f));
                DrawRect(new Rect(qx, qy, 200, 28), new Color(0.06f, 0.08f, 0.12f, 0.95f));

                float pulse = 0.7f + Mathf.PingPong(Time.time * 2.5f, 0.3f);
                var eyeStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
                eyeStyle.normal.textColor = new Color(0.35f, 1f, 0.35f * pulse);

                float elapsed = Time.time - QueueStartTime;
                GUI.Label(new Rect(qx + 8, qy + 4, 150, 20), $"👁 In Queue: {Mathf.FloorToInt(elapsed)}s", eyeStyle);

                if (GUI.Button(new Rect(qx + 165, qy + 4, 28, 20), "✕"))
                {
                    CancelQueue();
                }
            }

            // Dungeon Proposal Modal Dialog (Popped on group found)
            if (State == QueueState.ProposalReady)
            {
                float pw = 360f;
                float ph = 260f;
                float px = (screenWidth - pw) * 0.5f;
                float py = screenHeight * 0.28f;

                DrawRect(new Rect(px - 3, py - 3, pw + 6, ph + 6), new Color(0.85f, 0.70f, 0.28f, 0.95f));
                DrawRect(new Rect(px, py, pw, ph), new Color(0.06f, 0.08f, 0.12f, 0.98f));

                var titleSt = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                titleSt.normal.textColor = new Color(1f, 0.85f, 0.35f);
                GUI.Label(new Rect(px, py + 10, pw, 22), "★ DUNGEON GROUP READY! ★", titleSt);

                var d = (selectedDungeonIndex >= 0 && selectedDungeonIndex < dungeons.Count) ? dungeons[selectedDungeonIndex] : dungeons[0];
                var subSt = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
                subSt.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
                GUI.Label(new Rect(px, py + 34, pw, 20), $"{d.name} ({d.difficulty})", subSt);

                // Group readiness checkboxes
                for (int i = 0; i < proposalReady.Length; i++)
                {
                    string check = proposalReady[i] ? "<color=#40ff40>✔ Ready</color>" : "<color=#ffaa40>... Waiting</color>";
                    GUI.Label(new Rect(px + 28, py + 62 + i * 24, pw - 56, 20), $"{groupRoleNames[i]}: {check}");
                }

                // Countdown bar
                float progress = Mathf.Clamp01(ProposalCountdown / ProposalDuration);
                DrawRect(new Rect(px + 28, py + ph - 64, pw - 56, 12), new Color(0.12f, 0.14f, 0.18f, 1f));
                DrawRect(new Rect(px + 28, py + ph - 64, (pw - 56) * progress, 12), new Color(0.35f, 0.85f, 0.35f, 1f));

                if (GUI.Button(new Rect(px + 28, py + ph - 42, 140, 32), "Enter Dungeon"))
                {
                    AcceptDungeonProposal();
                }
                if (GUI.Button(new Rect(px + pw - 168, py + ph - 42, 140, 32), "Decline"))
                {
                    CancelQueue("Declined dungeon proposal.");
                }
            }

            if (!IsWindowOpen) return;

            // Main Dungeon Finder Window
            float winW = 580f;
            float winH = 460f;
            float winX = (screenWidth - winW) * 0.5f;
            float winY = (screenHeight - winH) * 0.5f;

            DrawRect(new Rect(winX - 3, winY - 3, winW + 6, winH + 6), new Color(0.85f, 0.70f, 0.28f, 0.95f));
            DrawRect(new Rect(winX, winY, winW, winH), new Color(0.06f, 0.08f, 0.12f, 0.97f));

            // Header
            DrawRect(new Rect(winX, winY, winW, 36f), new Color(0.12f, 0.14f, 0.18f, 1f));
            var headStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            headStyle.normal.textColor = new Color(1f, 0.85f, 0.35f);
            GUI.Label(new Rect(winX + 16, winY + 6, winW - 60, 24), "🏰 DUNGEON FINDER & LOOKING FOR GROUP [I]", headStyle);

            if (GUI.Button(new Rect(winX + winW - 32, winY + 6, 24, 24), "✕"))
            {
                IsWindowOpen = false;
                AudioEffects.PlayClick();
            }

            // Role Selection Buttons (Tank 🛡, Healer 💚, Damage ⚔)
            float roleY = winY + 44f;
            GUI.Label(new Rect(winX + 16, roleY + 4, 120, 20), "Select Your Role:");

            void DrawRoleButton(GroupRole role, string icon, string title, float rx)
            {
                bool isSel = (SelectedRole == role);
                if (isSel) GUI.color = new Color(1f, 0.85f, 0.3f);
                if (GUI.Button(new Rect(rx, roleY, 110, 28), $"{icon}  {title}"))
                {
                    SelectedRole = role;
                    AudioEffects.PlayClick();
                }
                GUI.color = Color.white;
            }

            DrawRoleButton(GroupRole.Tank, "🛡", "Tank", winX + 140);
            DrawRoleButton(GroupRole.Healer, "💚", "Healer", winX + 260);
            DrawRoleButton(GroupRole.Damage, "⚔", "Damage", winX + 380);

            // Left Pane: Dungeon Catalog List
            float listW = 210f;
            DrawRect(new Rect(winX + 12, winY + 80, listW, winH - 130), new Color(0.04f, 0.05f, 0.07f, 0.8f));

            var itemBtn = new GUIStyle(GUI.skin.button) { fontSize = 12, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(6, 6, 4, 4) };
            for (int i = 0; i < dungeons.Count; i++)
            {
                var d = dungeons[i];
                bool isSel = (i == selectedDungeonIndex);
                if (isSel) GUI.color = new Color(1f, 0.85f, 0.3f);
                if (GUI.Button(new Rect(winX + 16, winY + 84 + i * 40, listW - 8, 36), $"{d.icon}  {d.name}\n<color=#aaaaaa>Lv {d.minLevel}-{d.recLevel}</color>", itemBtn))
                {
                    selectedDungeonIndex = i;
                    AudioEffects.PlayClick();
                }
                GUI.color = Color.white;
            }

            // Right Pane: Dungeon Details & Bonus Rewards
            float detX = winX + listW + 24;
            float detW = winW - listW - 36;
            if (selectedDungeonIndex >= 0 && selectedDungeonIndex < dungeons.Count)
            {
                var sel = dungeons[selectedDungeonIndex];

                var dTitle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
                dTitle.normal.textColor = new Color(1f, 0.85f, 0.35f);
                GUI.Label(new Rect(detX, winY + 82, detW, 24), $"{sel.icon} {sel.name.ToUpperInvariant()}", dTitle);

                GUI.Label(new Rect(detX, winY + 108, detW, 20), $"Difficulty: <b>{sel.difficulty}</b>  ·  Recommended Level: <b>{sel.minLevel}–{sel.recLevel}</b>");

                var descStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 12 };
                descStyle.normal.textColor = new Color(0.85f, 0.88f, 0.92f);
                GUI.Label(new Rect(detX, winY + 132, detW, 80), sel.description, descStyle);

                // Daily Dungeon Reward Box
                DrawRect(new Rect(detX, winY + 220, detW, 64), new Color(0.12f, 0.14f, 0.18f, 0.9f));
                GUI.Label(new Rect(detX + 8, winY + 224, detW - 16, 20), "★ Daily Role Completion Reward:");
                var rewStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
                rewStyle.normal.textColor = new Color(0.4f, 1f, 0.4f);
                GUI.Label(new Rect(detX + 8, winY + 248, detW - 16, 30), sel.bonusReward, rewStyle);

                // Queue Action Buttons
                if (State == QueueState.None)
                {
                    GUI.color = new Color(0.4f, 1f, 0.4f);
                    if (GUI.Button(new Rect(detX, winY + winH - 46, detW, 36), $"Find Group for {sel.name}"))
                    {
                        JoinQueue();
                    }
                    GUI.color = Color.white;
                }
                else if (State == QueueState.Queued)
                {
                    GUI.color = new Color(1f, 0.45f, 0.3f);
                    if (GUI.Button(new Rect(detX, winY + winH - 46, detW, 36), "Leave Queue"))
                    {
                        CancelQueue();
                    }
                    GUI.color = Color.white;
                }
                else if (State == QueueState.InDungeon)
                {
                    GUI.color = new Color(1f, 0.45f, 0.3f);
                    if (GUI.Button(new Rect(detX, winY + winH - 46, detW, 36), "Leave Dungeon Group (Teleport Out)"))
                    {
                        LeaveDungeonGroup();
                    }
                    GUI.color = Color.white;
                }
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
