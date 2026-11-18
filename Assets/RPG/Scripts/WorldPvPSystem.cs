using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public class WorldPvPSystem : MonoBehaviour
    {
        public static WorldPvPSystem Instance { get; private set; }

        public enum TowerFaction
        {
            Neutral,
            Alliance,
            Horde
        }

        [Serializable]
        public class WorldPvPTower
        {
            public string id;
            public string name;
            public string zone;
            public Vector3 position;
            public float captureRadius = 22f;
            public float captureProgress = 0f; // -100 (Horde) to +100 (Alliance)
            public TowerFaction controllingFaction = TowerFaction.Neutral;

            public int alliancePresent = 0;
            public int hordePresent = 0;

            public GameObject flagBannerObj;

            public WorldPvPTower(string id, string name, string zone, Vector3 pos)
            {
                this.id = id;
                this.name = name;
                this.zone = zone;
                this.position = pos;
                this.captureProgress = 0f;
                this.controllingFaction = TowerFaction.Neutral;
            }

            public bool IsInsideCaptureZone(Vector3 pos)
            {
                Vector3 p1 = new Vector3(position.x, 0f, position.z);
                Vector3 p2 = new Vector3(pos.x, 0f, pos.z);
                return Vector3.Distance(p1, p2) <= captureRadius;
            }
        }

        public List<WorldPvPTower> towers = new List<WorldPvPTower>();
        public bool IsOverviewOpen { get; set; } = false;

        PlayerController player;
        float tickTimer = 0f;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            player = GetComponent<PlayerController>() ?? FindFirstObjectByType<PlayerController>();
            SeedTowers();
        }

        void Start()
        {
            SpawnPhysicalTowerBanners();
        }

        void Update()
        {
            tickTimer += Time.deltaTime;
            if (tickTimer >= 0.5f)
            {
                tickTimer = 0f;
                UpdateCaptureState(0.5f);
            }
        }

        public void SeedTowers()
        {
            if (towers.Count > 0) return;

            // 1. Crown Guard Tower (Central buffer between Amber Vale and Crossroads)
            towers.Add(new WorldPvPTower("wpvp_crownguard", "Crown Guard Tower", "Amber Vale Buffer", new Vector3(15f, 0.5f, 95f)));

            // 2. Pass of the Damned Watchtower (North-West path towards Duskwood Cathedral)
            towers.Add(new WorldPvPTower("wpvp_northpass", "Pass of the Damned Tower", "Duskwood Pass", new Vector3(-30f, 0.5f, 175f)));

            // 3. Bloodrock Redoubt (East ridge guarding Redridge Canyons)
            towers.Add(new WorldPvPTower("wpvp_bloodrock", "Bloodrock Redoubt", "Redridge Border", new Vector3(45f, 0.5f, 160f)));
        }

        void SpawnPhysicalTowerBanners()
        {
            var woodMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            woodMat.SetColor("_BaseColor", new Color(0.28f, 0.17f, 0.09f));
            woodMat.SetFloat("_Smoothness", 0.15f);

            foreach (var tower in towers)
            {
                if (tower.flagBannerObj != null) continue;

                Vector3 basePos = tower.position;
                if (Physics.Raycast(basePos + Vector3.up * 25f, Vector3.down, out var hit, 60f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                    basePos.y = hit.point.y;
                tower.position = basePos;

                // Create a visible timber flag pole & war banner
                var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pole.name = $"PvP_FlagPole_{tower.name}";
                pole.transform.position = basePos + Vector3.up * 3.2f;
                pole.transform.localScale = new Vector3(0.22f, 3.2f, 0.22f);
                var poleRend = pole.GetComponent<Renderer>();
                if (poleRend) poleRend.sharedMaterial = woodMat;

                var banner = GameObject.CreatePrimitive(PrimitiveType.Quad);
                banner.name = $"BannerCloth_{tower.name}";
                banner.transform.position = basePos + Vector3.up * 5.0f + Vector3.right * 0.75f;
                banner.transform.localScale = new Vector3(1.4f, 1.8f, 1f);
                banner.transform.SetParent(pole.transform, true);

                // Disable physical colliders
                var c1 = pole.GetComponent<Collider>(); if (c1) Destroy(c1);
                var c2 = banner.GetComponent<Collider>(); if (c2) Destroy(c2);

                tower.flagBannerObj = banner;
                UpdateBannerVisual(tower);
            }
        }

        void UpdateBannerVisual(WorldPvPTower tower)
        {
            if (!tower.flagBannerObj) return;
            var rend = tower.flagBannerObj.GetComponent<Renderer>();
            if (!rend) return;

            Color bannerCol = tower.controllingFaction switch
            {
                TowerFaction.Alliance => new Color(0.12f, 0.45f, 0.95f), // Alliance Blue
                TowerFaction.Horde => new Color(0.85f, 0.15f, 0.15f), // Horde Crimson
                _ => new Color(0.6f, 0.56f, 0.48f) // Neutral Parchment Grey
            };

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.SetColor("_BaseColor", bannerCol);
            mat.SetFloat("_Smoothness", 0.12f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            rend.material = mat;
        }

        public void UpdateCaptureState(float dt)
        {
            if (!player) player = FindFirstObjectByType<PlayerController>();

            Vector3 playerPos = player ? player.transform.position : Vector3.zero;
            TowerFaction playerFaction = GetPlayerFaction();

            foreach (var tower in towers)
            {
                tower.captureProgress = Mathf.Clamp(tower.captureProgress, -100f, 100f);
                tower.alliancePresent = 0;
                tower.hordePresent = 0;

                if (player && tower.IsInsideCaptureZone(playerPos))
                {
                    if (playerFaction == TowerFaction.Alliance) tower.alliancePresent++;
                    else if (playerFaction == TowerFaction.Horde) tower.hordePresent++;
                }

                int netDelta = tower.alliancePresent - tower.hordePresent;
                if (netDelta != 0)
                {
                    float captureSpeed = 10f * dt; // 10% per second
                    float oldProgress = tower.captureProgress;
                    tower.captureProgress = Mathf.Clamp(tower.captureProgress + (netDelta * captureSpeed), -100f, 100f);

                    // Check for faction flip
                    if (oldProgress < 100f && tower.captureProgress >= 100f && tower.controllingFaction != TowerFaction.Alliance)
                    {
                        ClaimTower(tower, TowerFaction.Alliance);
                    }
                    else if (oldProgress > -100f && tower.captureProgress <= -100f && tower.controllingFaction != TowerFaction.Horde)
                    {
                        ClaimTower(tower, TowerFaction.Horde);
                    }
                    else if (tower.controllingFaction != TowerFaction.Neutral && Mathf.Abs(tower.captureProgress) < 25f)
                    {
                        tower.controllingFaction = TowerFaction.Neutral;
                        UpdateBannerVisual(tower);
                    }
                }
            }
        }

        public void ClaimTower(WorldPvPTower tower, TowerFaction newFaction)
        {
            tower.controllingFaction = newFaction;
            tower.captureProgress = (newFaction == TowerFaction.Alliance) ? 100f : -100f;
            UpdateBannerVisual(tower);

            string factionName = newFaction.ToString();
            string colorHex = (newFaction == TowerFaction.Alliance) ? "#3399ff" : "#ff3333";
            string worldDefMsg = $"<color={colorHex}>⚔ <b>[World Defense]</b> {tower.name} has been captured by the {factionName}!</color>";
            ChatSystem.LogSystem(worldDefMsg);

            ZoneBanner.Show("TOWER CAPTURED!", $"{tower.name} is now held by the {factionName}!");
            AudioEffects.PlayQuestComplete();

            // Reward players in zone
            if (player && tower.IsInsideCaptureZone(player.transform.position))
            {
                TowerFaction pf = GetPlayerFaction();
                if (pf == newFaction)
                {
                    player.AddExperience(150);
                    ChatSystem.LogSystem($"<color=#ffd700>★ Awarded +150 Experience and +75 Honor for territorial defense!</color>");
                }
            }
        }

        public void ForceCapture(string towerId, TowerFaction faction)
        {
            var tower = towers.Find(t => t.id == towerId);
            if (tower != null)
            {
                ClaimTower(tower, faction);
            }
        }

        public TowerFaction GetControllingFaction(string towerId)
        {
            var tower = towers.Find(t => t.id == towerId);
            return tower != null ? tower.controllingFaction : TowerFaction.Neutral;
        }

        public TowerFaction GetContinentalDominance()
        {
            int ally = 0;
            int horde = 0;
            foreach (var t in towers)
            {
                if (t.controllingFaction == TowerFaction.Alliance) ally++;
                else if (t.controllingFaction == TowerFaction.Horde) horde++;
            }

            if (ally >= 2) return TowerFaction.Alliance;
            if (horde >= 2) return TowerFaction.Horde;
            return TowerFaction.Neutral;
        }

        public WorldPvPTower GetNearbyTower(Vector3 pos, float range = 40f)
        {
            foreach (var tower in towers)
            {
                if (Vector3.Distance(new Vector3(tower.position.x, 0, tower.position.z), new Vector3(pos.x, 0, pos.z)) <= range)
                    return tower;
            }
            return null;
        }

        public TowerFaction GetPlayerFaction()
        {
            if (!player) player = FindFirstObjectByType<PlayerController>();
            if (!player) return TowerFaction.Alliance;

            string fac = player.CharacterFaction;
            if (fac == "Horde" || player.CharacterRace == "Orc" || player.CharacterRace == "Undead" || player.CharacterRace == "Goblin")
                return TowerFaction.Horde;

            return TowerFaction.Alliance;
        }

        public void ToggleOverview()
        {
            IsOverviewOpen = !IsOverviewOpen;
            AudioEffects.PlayClick();
        }

        // GUI Rendering
        public void DrawWorldPvPGUI(float screenWidth, float screenHeight)
        {
            if (!player) player = FindFirstObjectByType<PlayerController>();
            if (!player) return;

            // 1. Top HUD Capture Slider (when near any tower)
            var activeTower = GetNearbyTower(player.transform.position, 45f);
            if (activeTower != null)
            {
                DrawCaptureProgressHUD(screenWidth, activeTower);
            }

            // 2. Overview / Strategy Window
            if (IsOverviewOpen)
            {
                DrawOverviewWindow(screenWidth, screenHeight);
            }
        }

        void DrawCaptureProgressHUD(float screenWidth, WorldPvPTower tower)
        {
            float barW = 340f;
            float barH = 34f;
            float barX = (screenWidth - barW) * 0.5f;
            float barY = 14f;

            // Backing & Gold Border
            DrawRect(new Rect(barX - 2, barY - 2, barW + 4, barH + 4), new Color(0.85f, 0.70f, 0.28f, 0.95f));
            DrawRect(new Rect(barX, barY, barW, barH), new Color(0.06f, 0.08f, 0.12f, 0.95f));

            // Tug of War Slider: Left half = Alliance (Blue), Right half = Horde (Red)
            float halfW = (barW - 8) * 0.5f;
            float sliderY = barY + 4;
            float sliderH = 14f;

            // Alliance Bar (0 to 100)
            DrawRect(new Rect(barX + 4, sliderY, halfW, sliderH), new Color(0.08f, 0.15f, 0.30f, 0.9f));
            if (tower.captureProgress > 0)
            {
                float allyPct = tower.captureProgress / 100f;
                DrawRect(new Rect(barX + 4 + halfW * (1f - allyPct), sliderY, halfW * allyPct, sliderH), new Color(0.2f, 0.55f, 1.0f));
            }

            // Horde Bar (-100 to 0)
            DrawRect(new Rect(barX + 4 + halfW, sliderY, halfW, sliderH), new Color(0.30f, 0.10f, 0.10f, 0.9f));
            if (tower.captureProgress < 0)
            {
                float hordePct = Mathf.Abs(tower.captureProgress) / 100f;
                DrawRect(new Rect(barX + 4 + halfW, sliderY, halfW * hordePct, sliderH), new Color(0.95f, 0.2f, 0.2f));
            }

            // Center Dividing Pin
            DrawRect(new Rect(barX + 4 + halfW - 1, sliderY - 2, 2, sliderH + 4), Color.white);

            // Active Needle Marker
            float normalized = (tower.captureProgress + 100f) / 200f; // 0 to 1
            float needleX = barX + 4 + ((barW - 8) * normalized);
            DrawRect(new Rect(needleX - 2, sliderY - 3, 4, sliderH + 6), new Color(1f, 0.85f, 0.2f));

            // Status Text below slider
            var txtStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            txtStyle.normal.textColor = Color.white;

            string statusText;
            if (tower.controllingFaction == TowerFaction.Alliance)
                statusText = $"🦁 {tower.name} — ALLIANCE CONTROLLED";
            else if (tower.controllingFaction == TowerFaction.Horde)
                statusText = $"⚔ {tower.name} — HORDE CONTROLLED";
            else
                statusText = $"⚖ {tower.name} — CONTESTED ({Mathf.RoundToInt(Mathf.Abs(tower.captureProgress))}%)";

            GUI.Label(new Rect(barX, barY + 16, barW, 18), statusText, txtStyle);
        }

        void DrawOverviewWindow(float screenWidth, float screenHeight)
        {
            float winW = 580f;
            float winH = 380f;
            float winX = (screenWidth - winW) * 0.5f;
            float winY = (screenHeight - winH) * 0.5f;

            // Border & Backing
            DrawRect(new Rect(winX - 3, winY - 3, winW + 6, winH + 6), new Color(0.85f, 0.70f, 0.28f, 0.95f));
            DrawRect(new Rect(winX, winY, winW, winH), new Color(0.06f, 0.08f, 0.12f, 0.98f));

            // Header Banner
            DrawRect(new Rect(winX, winY, winW, 38f), new Color(0.12f, 0.15f, 0.22f, 1f));
            var headStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            headStyle.normal.textColor = new Color(1f, 0.85f, 0.35f);
            GUI.Label(new Rect(winX + 16, winY + 6, winW - 60, 26), "⚔ CONTINENTAL WORLD PVP & TERRITORIAL CONTROL", headStyle);

            if (GUI.Button(new Rect(winX + winW - 32, winY + 6, 24, 24), "✕"))
            {
                IsOverviewOpen = false;
                AudioEffects.PlayClick();
            }

            // Continental Dominance Banner
            TowerFaction dom = GetContinentalDominance();
            Color domBg = dom switch
            {
                TowerFaction.Alliance => new Color(0.12f, 0.35f, 0.65f, 0.85f),
                TowerFaction.Horde => new Color(0.65f, 0.15f, 0.15f, 0.85f),
                _ => new Color(0.15f, 0.18f, 0.22f, 0.85f)
            };
            DrawRect(new Rect(winX + 16, winY + 48, winW - 32, 50), domBg);

            var domStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            domStyle.normal.textColor = Color.white;
            string domTitle = dom switch
            {
                TowerFaction.Alliance => "🦁 ALLIANCE CONTINENTAL VANGUARD ACTIVE!",
                TowerFaction.Horde => "⚔ HORDE BLOOD SOVEREIGN ACTIVE!",
                _ => "⚖ CONTESTED REALM — NEITHER FACTION CONTROLS 2+ TOWERS"
            };
            GUI.Label(new Rect(winX + 28, winY + 52, winW - 56, 22), domTitle, domStyle);

            var subStyle = new GUIStyle(GUI.skin.label) { fontSize = 11 };
            subStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            string domBenefit = (dom != TowerFaction.Neutral) ? "Grants +10% Experience, +5% Physical/Spell Damage, and +15% Honor Gains." : "Capture 2 or more towers to claim the continental dominance blessing.";
            GUI.Label(new Rect(winX + 28, winY + 72, winW - 56, 20), domBenefit, subStyle);

            // Towers Roster
            float listY = winY + 110;
            float rowH = 68f;

            for (int i = 0; i < towers.Count; i++)
            {
                var t = towers[i];
                float rY = listY + (i * (rowH + 10));
                DrawRect(new Rect(winX + 16, rY, winW - 32, rowH), new Color(0.04f, 0.05f, 0.07f, 0.85f));

                // Faction Badge
                Color fCol = t.controllingFaction switch
                {
                    TowerFaction.Alliance => new Color(0.2f, 0.55f, 1f),
                    TowerFaction.Horde => new Color(0.95f, 0.2f, 0.2f),
                    _ => new Color(0.6f, 0.6f, 0.6f)
                };
                DrawRect(new Rect(winX + 22, rY + 8, 48, 52), fCol * 0.3f);
                var fIcon = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
                string iconChar = (t.controllingFaction == TowerFaction.Alliance) ? "🦁" : (t.controllingFaction == TowerFaction.Horde) ? "⚔" : "⚖";
                GUI.Label(new Rect(winX + 22, rY + 8, 48, 52), iconChar, fIcon);

                // Tower Details
                var nmStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
                nmStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(winX + 80, rY + 8, 300, 20), t.name, nmStyle);

                var zStyle = new GUIStyle(GUI.skin.label) { fontSize = 11 };
                zStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
                GUI.Label(new Rect(winX + 80, rY + 28, 300, 18), $"Territory: {t.zone} · Radius: {t.captureRadius}m", zStyle);

                // Progress Bar
                float pW = 140f;
                float pX = winX + winW - pW - 32;
                DrawRect(new Rect(pX, rY + 20, pW, 14), new Color(0.12f, 0.12f, 0.14f, 1f));
                float fillRatio = (t.captureProgress + 100f) / 200f;
                DrawRect(new Rect(pX, rY + 20, pW * fillRatio, 14), fCol);

                var pVal = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                pVal.normal.textColor = Color.white;
                GUI.Label(new Rect(pX, rY + 20, pW, 14), $"{t.controllingFaction} ({Mathf.RoundToInt(Mathf.Abs(t.captureProgress))}%)", pVal);

                // Waypoint button
                if (GUI.Button(new Rect(pX, rY + 40, pW, 20), "📍 Pinned Waypoint"))
                {
                    MinimapRadar.SetWaypoint(t.position, t.name);
                    AudioEffects.PlayClick();
                }
            }

            // Bottom info
            var botStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleCenter };
            botStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
            GUI.Label(new Rect(winX + 16, winY + winH - 30, winW - 32, 22), "Stand within a tower's perimeter to claim it for your faction. Type /wpvp to view status.", botStyle);
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
