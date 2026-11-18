using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public class ArchaeologySystem : MonoBehaviour
    {
        public static ArchaeologySystem Instance { get; private set; }

        public enum DigSiteRace
        {
            Fossil,
            Dwarven,
            NightElf,
            Demonic
        }

        [Serializable]
        public class DigSite
        {
            public string id;
            public string name;
            public DigSiteRace race;
            public Vector3 center;
            public float radius;
            public int findsRemaining;
            public Vector3 currentFindPosition;
            public bool isSurveyActive;
            public Vector3 lastSurveyPosition;
            public float surveyBeaconDistance; // Distance from last survey to artifact
            public Color surveyLightColor; // Green (<15m), Yellow (15-40m), Red (>40m)

            public DigSite(string id, string name, DigSiteRace race, Vector3 center, float radius)
            {
                this.id = id;
                this.name = name;
                this.race = race;
                this.center = center;
                this.radius = radius;
                this.findsRemaining = 3;
                GenerateNewFind();
            }

            public void GenerateNewFind()
            {
                // Random position within digsite circle
                Vector2 circle = UnityEngine.Random.insideUnitCircle * (radius * 0.75f);
                currentFindPosition = center + new Vector3(circle.x, 0f, circle.y);
            }
        }

        [Serializable]
        public class ArtifactProject
        {
            public string id;
            public string name;
            public DigSiteRace race;
            public int fragmentsRequired;
            public int fragmentsCurrent;
            public string rarity; // Common, Rare, Epic, Pristine
            public string rewardDescription;
            public bool isCompleted;

            public ArtifactProject(string id, string name, DigSiteRace race, int req, string rarity, string desc)
            {
                this.id = id;
                this.name = name;
                this.race = race;
                this.fragmentsRequired = req;
                this.fragmentsCurrent = 0;
                this.rarity = rarity;
                this.rewardDescription = desc;
                this.isCompleted = false;
            }
        }

        public List<DigSite> digSites = new List<DigSite>();
        public List<ArtifactProject> currentProjects = new List<ArtifactProject>();
        public List<string> completedArtifactHistory = new List<string>();

        // Fragment Inventories
        public int fossilFragments = 12;
        public int dwarvenFragments = 18;
        public int nightElfFragments = 10;
        public int demonicFragments = 6;

        public int selectedRaceTab = 0; // 0=Fossil, 1=Dwarven, 2=NightElf, 3=Demonic
        public bool IsWindowOpen { get; set; } = false;

        // Active Survey Scope
        public bool IsSurveying { get; private set; } = false;
        public float SurveyCooldown { get; private set; } = 0f;
        public Vector3 ActiveSurveyBeaconPos;
        public Color ActiveSurveyColor = Color.red;
        public float SurveyLightDuration = 0f;

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
            SeedDigSitesAndProjects();
        }

        public void SeedDigSitesAndProjects()
        {
            if (digSites.Count == 0)
            {
                digSites.Add(new DigSite("site_amber_vale", "Amber Vale Fossils", DigSiteRace.Fossil, new Vector3(15f, 0f, 35f), 45f));
                digSites.Add(new DigSite("site_ashwood_ruins", "Ruins of Ashwood", DigSiteRace.Dwarven, new Vector3(-25f, 0f, 95f), 40f));
                digSites.Add(new DigSite("site_whispering_woods", "Whispering Woods Barrows", DigSiteRace.NightElf, new Vector3(30f, 0f, 150f), 50f));
                digSites.Add(new DigSite("site_duskwood_crypts", "Cathedral Excavation", DigSiteRace.Demonic, new Vector3(-45f, 0f, 185f), 45f));
            }

            if (currentProjects.Count == 0)
            {
                currentProjects.Add(new ArtifactProject(
                    "art_fossil_raptor",
                    "Fossilized Raptor Hatchling",
                    DigSiteRace.Fossil,
                    25,
                    "Rare",
                    "Teaches pet: Ancient Skeletal Raptor (Companion Pet)"
                ));

                currentProjects.Add(new ArtifactProject(
                    "art_dwarf_chalice",
                    "Chalice of the Mountain King",
                    DigSiteRace.Dwarven,
                    30,
                    "Epic",
                    "Rare Relic: Grants +12 Strength and +35 HP"
                ));

                currentProjects.Add(new ArtifactProject(
                    "art_nelf_mirror",
                    "Highborne Scrying Mirror",
                    DigSiteRace.NightElf,
                    20,
                    "Rare",
                    "Toy: Gazing into the mirror reveals spirits of ancient Kalimdor"
                ));

                currentProjects.Add(new ArtifactProject(
                    "art_demon_orb",
                    "Orb of Fel Annihilation",
                    DigSiteRace.Demonic,
                    35,
                    "Epic",
                    "Trinket: Unlocks +15 Spell Power and Fel Infusion effect"
                ));
            }
        }

        void Update()
        {
            if (SurveyCooldown > 0f) SurveyCooldown -= Time.deltaTime;
            if (SurveyLightDuration > 0f) SurveyLightDuration -= Time.deltaTime;
        }

        public void ToggleWindow()
        {
            IsWindowOpen = !IsWindowOpen;
            AudioEffects.PlayClick();
        }

        public DigSite GetActiveDigSite(Vector3 playerPos)
        {
            foreach (var site in digSites)
            {
                if (Vector3.Distance(new Vector3(playerPos.x, 0f, playerPos.z), new Vector3(site.center.x, 0f, site.center.z)) <= site.radius)
                    return site;
            }
            return null;
        }

        /// <summary>
        /// Classic WoW Survey ability (/survey). Plants a theodolite surveying telescope at player position.
        /// Flashes Green (<15m), Yellow (15-40m), or Red (>40m) toward the hidden artifact.
        /// </summary>
        public bool PerformSurvey(PlayerController p, out string feedback)
        {
            if (!p) p = FindFirstObjectByType<PlayerController>();
            if (!p) { feedback = "No player found."; return false; }

            var site = GetActiveDigSite(p.transform.position);
            if (site == null)
            {
                feedback = "You cannot survey here. You must be within an active digsite area!";
                ChatSystem.LogSystem("<color=#ffaa40>[Archaeology] You are not in an active digsite.</color>");
                return false;
            }

            if (SurveyCooldown > 0f)
            {
                feedback = $"Survey is still recharging ({Mathf.CeilToInt(SurveyCooldown)}s).";
                return false;
            }

            SurveyCooldown = 4.0f;
            float dist = Vector3.Distance(p.transform.position, site.currentFindPosition);

            // Scope direction vector
            Vector3 dir = (site.currentFindPosition - p.transform.position).normalized;
            ActiveSurveyBeaconPos = p.transform.position + Vector3.up * 0.8f;
            SurveyLightDuration = 6.0f;

            if (dist < 8.0f)
            {
                // Found! Excavate fragment node
                ActiveSurveyColor = Color.green;
                ExcavateArtifact(site, p);
                feedback = "★ ARTIFACT REVEALED! You unearthed ancient fragments!";
                return true;
            }
            else if (dist < 28.0f)
            {
                ActiveSurveyColor = Color.yellow;
                feedback = $"Theodolite indicates artifact is VERY CLOSE ({Mathf.RoundToInt(dist)}m)!";
            }
            else
            {
                ActiveSurveyColor = Color.red;
                feedback = $"Theodolite points into the distance ({Mathf.RoundToInt(dist)}m away).";
            }

            ChatSystem.LogSystem($"<color=#dfba50>[Archaeology - {site.name}]</color> {feedback}");
            AudioEffects.PlayClick();
            return true;
        }

        void ExcavateArtifact(DigSite site, PlayerController p)
        {
            int frags = UnityEngine.Random.Range(4, 9);
            switch (site.race)
            {
                case DigSiteRace.Fossil:
                    fossilFragments += frags;
                    break;
                case DigSiteRace.Dwarven:
                    dwarvenFragments += frags;
                    break;
                case DigSiteRace.NightElf:
                    nightElfFragments += frags;
                    break;
                case DigSiteRace.Demonic:
                    demonicFragments += frags;
                    break;
            }

            // Grant character experience
            if (p) p.AddExperience(35);

            site.findsRemaining--;
            ZoneBanner.Show("ARTIFACT UNEARTHED!", $"+{frags} {site.race} Fragments  ·  +35 XP");
            ChatSystem.LogLoot($"{site.race} Artifact Fragments", "Rare", frags);
            AudioEffects.PlayQuestComplete();

            if (site.findsRemaining <= 0)
            {
                site.findsRemaining = 3;
                site.GenerateNewFind();
                ChatSystem.LogSystem($"<color=#70df80>[Archaeology] {site.name} completed and replenished with fresh dig strata!</color>");
            }
            else
            {
                site.GenerateNewFind();
            }
        }

        public bool SolveArtifactProject(int projectIndex, out string feedback)
        {
            if (projectIndex < 0 || projectIndex >= currentProjects.Count)
            {
                feedback = "Invalid project index.";
                return false;
            }

            var proj = currentProjects[projectIndex];
            int currentFrags = GetFragmentCount(proj.race);

            if (currentFrags < proj.fragmentsRequired)
            {
                feedback = $"Requires {proj.fragmentsRequired} {proj.race} fragments (You hold {currentFrags}).";
                return false;
            }

            // Deduct fragments
            DeductFragments(proj.race, proj.fragmentsRequired);
            proj.isCompleted = true;
            completedArtifactHistory.Add($"{proj.name} ({proj.rarity})");

            ZoneBanner.Show("ARTIFACT SOLVED!", proj.name);
            ChatSystem.LogSystem($"<color=#e5a520>[Archaeology] SOLVED: {proj.name}! {proj.rewardDescription}</color>");
            AudioEffects.PlayBossVictory();

            // Grant gold & XP
            var p = FindFirstObjectByType<PlayerController>();
            if (p)
            {
                p.AddExperience(120);
                p.AddGoldDirect(25);
            }

            feedback = $"Successfully assembled {proj.name}!";
            return true;
        }

        public int GetFragmentCount(DigSiteRace race)
        {
            return race switch
            {
                DigSiteRace.Fossil => fossilFragments,
                DigSiteRace.Dwarven => dwarvenFragments,
                DigSiteRace.NightElf => nightElfFragments,
                DigSiteRace.Demonic => demonicFragments,
                _ => 0
            };
        }

        void DeductFragments(DigSiteRace race, int amount)
        {
            switch (race)
            {
                case DigSiteRace.Fossil: fossilFragments = Mathf.Max(0, fossilFragments - amount); break;
                case DigSiteRace.Dwarven: dwarvenFragments = Mathf.Max(0, dwarvenFragments - amount); break;
                case DigSiteRace.NightElf: nightElfFragments = Mathf.Max(0, nightElfFragments - amount); break;
                case DigSiteRace.Demonic: demonicFragments = Mathf.Max(0, demonicFragments - amount); break;
            }
        }

        // GUI Rendering
        public void DrawArchaeologyGUI(float screenWidth, float screenHeight)
        {
            // Survey Active Beacon HUD Indicator
            if (SurveyLightDuration > 0f)
            {
                float bw = 240f, bh = 32f;
                float bx = (screenWidth - bw) * 0.5f;
                float by = screenHeight * 0.22f;

                DrawRect(new Rect(bx - 2, by - 2, bw + 4, bh + 4), ActiveSurveyColor);
                DrawRect(new Rect(bx, by, bw, bh), new Color(0.06f, 0.08f, 0.12f, 0.95f));

                string lightWord = (ActiveSurveyColor == Color.green) ? "★ GREEN: DIRECT HIT (<8m)!" :
                                   (ActiveSurveyColor == Color.yellow) ? "▲ YELLOW: CLOSE RANGE (8-28m)" : "● RED: LONG RANGE (>28m)";
                var bStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                bStyle.normal.textColor = ActiveSurveyColor;
                GUI.Label(new Rect(bx, by + 6, bw, 20), lightWord, bStyle);
            }

            if (!IsWindowOpen) return;

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
            GUI.Label(new Rect(winX + 16, winY + 6, winW - 60, 24), "🏺 ARCHAEOLOGY & ANCIENT ARTIFACTS (/survey)", headStyle);

            if (GUI.Button(new Rect(winX + winW - 32, winY + 6, 24, 24), "✕"))
            {
                IsWindowOpen = false;
                AudioEffects.PlayClick();
            }

            // Top Tabs: Races (Fossil, Dwarven, NightElf, Demonic)
            float tabY = winY + 44f;
            string[] raceTabs = new string[] { $"Fossil ({fossilFragments})", $"Dwarven ({dwarvenFragments})", $"Night Elf ({nightElfFragments})", $"Demonic ({demonicFragments})" };
            selectedRaceTab = GUI.Toolbar(new Rect(winX + 16, tabY, winW - 32, 28), selectedRaceTab, raceTabs);

            DigSiteRace activeRace = (DigSiteRace)selectedRaceTab;

            // Find project for active race
            ArtifactProject proj = currentProjects.Find(p => p.race == activeRace);

            float bodyY = winY + 84f;
            float bodyW = winW - 32f;
            DrawRect(new Rect(winX + 16, bodyY, bodyW, winH - 140), new Color(0.04f, 0.05f, 0.07f, 0.85f));

            if (proj != null)
            {
                float px = winX + 32;
                float py = bodyY + 16;

                var pTitle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
                pTitle.normal.textColor = (proj.rarity == "Epic") ? new Color(0.75f, 0.35f, 1f) : new Color(0.2f, 0.6f, 1f);
                GUI.Label(new Rect(px, py, bodyW - 32, 24), $"{proj.name} ({proj.rarity})", pTitle);

                GUI.Label(new Rect(px, py + 26, bodyW - 32, 20), $"Branch: <b>{proj.race} Archaeology</b>");
                GUI.Label(new Rect(px, py + 48, bodyW - 32, 40), $"Reward Lore: <i>{proj.rewardDescription}</i>");

                // Progress Bar
                int currentFrags = GetFragmentCount(activeRace);
                float progress = Mathf.Clamp01((float)currentFrags / proj.fragmentsRequired);

                GUI.Label(new Rect(px, py + 92, bodyW - 32, 20), $"Fragment Progress: <b>{currentFrags} / {proj.fragmentsRequired}</b>");
                DrawRect(new Rect(px, py + 116, bodyW - 64, 18), new Color(0.12f, 0.14f, 0.18f, 1f));
                DrawRect(new Rect(px, py + 116, (bodyW - 64) * progress, 18), new Color(0.85f, 0.70f, 0.28f, 1f));

                // Solve Button
                bool canSolve = (currentFrags >= proj.fragmentsRequired) && !proj.isCompleted;
                if (!canSolve) GUI.color = new Color(1f, 1f, 1f, 0.5f);
                else GUI.color = new Color(0.4f, 1f, 0.4f);

                if (GUI.Button(new Rect(px, py + 146, 200, 36), proj.isCompleted ? "★ Solved" : "Solve Artifact"))
                {
                    if (canSolve)
                    {
                        int idx = currentProjects.IndexOf(proj);
                        SolveArtifactProject(idx, out _);
                    }
                }
                GUI.color = Color.white;
            }

            // Bottom Action: Survey button shortcut
            float botY = winY + winH - 46f;
            if (GUI.Button(new Rect(winX + 16, botY, 180, 34), "🔭 Survey Area (/survey)"))
            {
                PerformSurvey(player, out _);
            }

            var tipStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Italic };
            tipStyle.normal.textColor = new Color(0.65f, 0.75f, 0.85f);
            GUI.Label(new Rect(winX + 210, botY + 8, winW - 220, 24), "Track shovel icons on minimap to locate active digsites.", tipStyle);
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
