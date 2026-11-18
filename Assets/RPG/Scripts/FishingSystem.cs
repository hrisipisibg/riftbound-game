using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public class FishingSystem : MonoBehaviour
    {
        public static FishingSystem Instance { get; private set; }

        public enum FishingState
        {
            Idle,
            Casting,
            WaitingForBite,
            BiteActive,     // Bobber splashes!
            Reeling
        }

        [Serializable]
        public class FishCatch
        {
            public string id;
            public string name;
            public string rarity; // Common, Uncommon, Rare, Epic
            public int skillRequired;
            public int sellPriceGold;
            public string description;
            public string buffEffect; // e.g., "Well Fed: +10 Max Health"

            public FishCatch(string id, string name, string rarity, int skillReq, int price, string desc, string buff)
            {
                this.id = id;
                this.name = name;
                this.rarity = rarity;
                this.skillRequired = skillReq;
                this.sellPriceGold = price;
                this.description = desc;
                this.buffEffect = buff;
            }
        }

        public int FishingSkill { get; set; } = 45;
        public const int MaxFishingSkill = 150;
        public FishingState State { get; private set; } = FishingState.Idle;

        // Bobber world location
        public Vector3 BobberPosition { get; private set; }
        public float CastStartTime { get; private set; }
        public float CastDuration { get; private set; } = 15.0f; // Max wait before bobber expires
        public float BiteTime { get; private set; }            // Exact timestamp when bobber splashes
        public float BiteWindowDuration { get; private set; } = 2.8f; // Time player has to click bobber

        public List<FishCatch> catchCatalog = new List<FishCatch>();
        public List<string> fishLog = new List<string>();
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
            SeedFishCatalog();
        }

        public void SeedFishCatalog()
        {
            if (catchCatalog.Count > 0) return;

            catchCatalog.Add(new FishCatch("fish_brilliant_sunfish", "Raw Brilliant Smallfish", "Common", 1, 1, "A shimmering freshwater sunfish caught along Ember Vale streams.", "Restores 45 HP over 18s"));
            catchCatalog.Add(new FishCatch("fish_longjaw_mud_snapper", "Longjaw Mud Snapper", "Common", 15, 2, "A whiskered river fish found near muddy stream banks.", "Restores 65 HP over 21s"));
            catchCatalog.Add(new FishCatch("fish_bristle_whisker_catfish", "Bristle Whisker Catfish", "Uncommon", 35, 4, "A prized game fish with heavy spiked fins.", "Well Fed: +15 Max Health for 15 min"));
            catchCatalog.Add(new FishCatch("fish_firefin_snapper", "Firefin Snapper", "Uncommon", 50, 6, "Warm-blooded coastal fish with fiery scales used by Alchemists for Fire Oil.", "Reagent: Used in Elixir of Firepower"));
            catchCatalog.Add(new FishCatch("fish_oily_blackmouth", "Oily Blackmouth", "Uncommon", 60, 8, "Dark oily fish essential for brewing Free Action Potions.", "Reagent: Used in Blackmouth Oil"));
            catchCatalog.Add(new FishCatch("fish_sagefish", "Greater Sagefish", "Rare", 80, 12, "Delicious freshwater delicacy prized by spellcasters.", "Well Fed: +10 MP per 5s for 15 min"));
            catchCatalog.Add(new FishCatch("fish_ironscale_trout", "Ironscale Trout", "Rare", 100, 15, "Tough, armored trout capable of denting basic hooks.", "Well Fed: +15 Strength and +25 HP for 15 min"));
            catchCatalog.Add(new FishCatch("fish_deviate_fish", "Savory Deviate Delight", "Epic", 120, 30, "Rare metamorphic fish that transforms the consumer into a Ninja or Pirate!", "Transforms into Pirate/Ninja for 60 min"));
        }

        void Update()
        {
            if (State == FishingState.WaitingForBite)
            {
                if (Time.time >= BiteTime)
                {
                    // Bobber splashes!
                    State = FishingState.BiteActive;
                    AudioEffects.PlayClick();
                    CombatTextManager.Spawn(BobberPosition + Vector3.up * 0.8f, "★ SPLASH! CLICK BOBBER! ★", new Color(0.35f, 0.85f, 1f), true);
                }
                else if (Time.time - CastStartTime > CastDuration)
                {
                    CancelFishing("The fish got away... No bite.");
                }
            }
            else if (State == FishingState.BiteActive)
            {
                if (Time.time > BiteTime + BiteWindowDuration)
                {
                    CancelFishing("The fish bit your hook, but got away!");
                }
            }
        }

        public void ToggleWindow()
        {
            IsWindowOpen = !IsWindowOpen;
            AudioEffects.PlayClick();
        }

        public bool StartCast(PlayerController p, out string reason)
        {
            if (!p) p = FindFirstObjectByType<PlayerController>();
            if (!p) { reason = "No player found."; return false; }

            if (p.InCombat())
            {
                reason = "Cannot fish while in combat!";
                ChatSystem.LogSystem("<color=#ff4444>[Fishing] Cannot fish while engaged in combat!</color>");
                return false;
            }

            // Check proximity to water or rivers
            bool nearWater = false;
            var waters = FindObjectsByType<FlowingWater>(FindObjectsSortMode.None);
            foreach (var w in waters)
            {
                if (Vector3.Distance(p.transform.position, w.transform.position) < 32f)
                {
                    nearWater = true;
                    break;
                }
            }

            // Also allow in starter town Emberwatch Outpost (stream bank)
            string curZone = ZoneManager.GetZoneName(p.transform.position);
            if (curZone == "Emberwatch Outpost" || curZone == "Duskwood Cathedral" || curZone == "Camp Bloodrock")
                nearWater = true;

            if (!nearWater)
            {
                reason = "No fishable open water found nearby!";
                ChatSystem.LogSystem("<color=#ffaa40>[Fishing] You must stand near a river, lake, or coastline to cast your line.</color>");
                return false;
            }

            State = FishingState.WaitingForBite;
            CastStartTime = Time.time;
            BiteTime = CastStartTime + UnityEngine.Random.Range(3.5f, 8.0f);

            // Plant bobber in front of player
            BobberPosition = p.transform.position + p.transform.forward * 8f + Vector3.up * 0.1f;

            CastBarSystem.StartCast("Fishing...", CastDuration, null, true, true);
            ZoneBanner.Show("FISHING CAST", "Watch your bobber for a splash!");
            ChatSystem.LogSystem("<color=#38c8ff>[Fishing] You cast your line into the water. Watch for a splash!</color>");
            AudioEffects.PlayClick();

            reason = "Line cast into the water.";
            return true;
        }

        public bool HookFish(PlayerController p, out FishCatch caughtFish, out string feedback)
        {
            caughtFish = null;
            if (!p) p = FindFirstObjectByType<PlayerController>();

            if (State != FishingState.BiteActive)
            {
                if (State == FishingState.WaitingForBite)
                {
                    CancelFishing("You reeled in too early! The fish wasn't hooked yet.");
                    feedback = "Reeled in too early.";
                    return false;
                }

                feedback = "No active bite to reel in.";
                return false;
            }

            // Successfully hooked!
            State = FishingState.Idle;
            CastBarSystem.CancelCast();

            // Select fish catch based on skill
            var eligible = catchCatalog.FindAll(f => f.skillRequired <= FishingSkill);
            caughtFish = (eligible.Count > 0) ? eligible[UnityEngine.Random.Range(0, eligible.Count)] : catchCatalog[0];

            // Chance to increase fishing skill
            if (FishingSkill < MaxFishingSkill && UnityEngine.Random.value < 0.65f)
            {
                FishingSkill++;
                ChatSystem.LogSystem($"<color=#ffff00>Your skill in Fishing has increased to {FishingSkill}/{MaxFishingSkill}!</color>");
            }

            // Grant rewards
            if (p)
            {
                p.AddExperience(25);
                p.AddGoldDirect(caughtFish.sellPriceGold);
            }

            fishLog.Add($"{caughtFish.name} ({caughtFish.rarity})");
            ZoneBanner.Show("CAUGHT FISH!", $"{caughtFish.name}  ·  +{caughtFish.sellPriceGold}g");
            ChatSystem.LogLoot(caughtFish.name, caughtFish.rarity);
            AudioEffects.PlayQuestComplete();

            feedback = $"You caught a {caughtFish.name}!";
            return true;
        }

        public void CancelFishing(string reason = "")
        {
            State = FishingState.Idle;
            CastBarSystem.CancelCast();
            if (!string.IsNullOrEmpty(reason))
                ChatSystem.LogSystem($"<color=#aaaaaa>[Fishing] {reason}</color>");
        }

        // GUI Rendering
        public void DrawFishingGUI(float screenWidth, float screenHeight)
        {
            // Interactive 3D Bobber Clickable Reticle (During active cast)
            if (State == FishingState.WaitingForBite || State == FishingState.BiteActive)
            {
                var cam = Camera.main;
                if (cam)
                {
                    Vector3 screenPos = cam.WorldToScreenPoint(BobberPosition);
                    if (screenPos.z > 0f)
                    {
                        float bx = screenPos.x;
                        float by = Screen.height - screenPos.y; // Invert GUI Y
                        float bw = (State == FishingState.BiteActive) ? 68f : 52f;
                        float bh = (State == FishingState.BiteActive) ? 40f : 32f;

                        Rect bobberRect = new Rect(bx - bw * 0.5f, by - bh * 0.5f, bw, bh);

                        if (State == FishingState.BiteActive)
                        {
                            float pulse = 0.8f + Mathf.PingPong(Time.time * 6f, 0.4f);
                            GUI.color = new Color(0.35f, 0.85f * pulse, 1f);
                        }
                        else
                        {
                            GUI.color = new Color(1f, 0.9f, 0.7f, 0.9f);
                        }

                        string bobberText = (State == FishingState.BiteActive) ? "🎣 REEL!" : "🔴 Bobber";
                        var bobberBtn = new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold };
                        if (GUI.Button(bobberRect, bobberText, bobberBtn))
                        {
                            HookFish(player, out _, out _);
                        }
                        GUI.color = Color.white;
                    }
                }
            }

            if (!IsWindowOpen) return;

            float winW = 560f;
            float winH = 450f;
            float winX = (screenWidth - winW) * 0.5f;
            float winY = (screenHeight - winH) * 0.5f;

            DrawRect(new Rect(winX - 3, winY - 3, winW + 6, winH + 6), new Color(0.35f, 0.75f, 0.95f, 0.95f)); // Aqua trim
            DrawRect(new Rect(winX, winY, winW, winH), new Color(0.06f, 0.08f, 0.12f, 0.97f));

            // Header
            DrawRect(new Rect(winX, winY, winW, 36f), new Color(0.12f, 0.15f, 0.22f, 1f));
            var headStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            headStyle.normal.textColor = new Color(0.40f, 0.85f, 1f);
            GUI.Label(new Rect(winX + 16, winY + 6, winW - 60, 24), "🎣 ANGLER'S EXPEDITION & FISHING LOG (/fish)", headStyle);

            if (GUI.Button(new Rect(winX + winW - 32, winY + 6, 24, 24), "✕"))
            {
                IsWindowOpen = false;
                AudioEffects.PlayClick();
            }

            // Skill Progress Bar
            float barY = winY + 44f;
            GUI.Label(new Rect(winX + 16, barY, 200, 20), $"Fishing Proficiency: <b>{FishingSkill} / {MaxFishingSkill}</b>");
            float skillPct = (float)FishingSkill / MaxFishingSkill;
            DrawRect(new Rect(winX + 16, barY + 22, winW - 32, 14), new Color(0.12f, 0.14f, 0.18f, 1f));
            DrawRect(new Rect(winX + 16, barY + 22, (winW - 32) * skillPct, 14), new Color(0.35f, 0.75f, 0.95f, 1f));

            // Catch Catalog List
            float listY = barY + 46f;
            float listW = winW - 32f;
            float listH = winH - 150f;
            DrawRect(new Rect(winX + 16, listY, listW, listH), new Color(0.04f, 0.05f, 0.07f, 0.85f));

            var catchStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            for (int i = 0; i < catchCatalog.Count; i++)
            {
                var f = catchCatalog[i];
                float ry = listY + 8 + i * 36;
                bool canCatch = (FishingSkill >= f.skillRequired);

                var fNameStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
                fNameStyle.normal.textColor = canCatch ?
                    (f.rarity == "Epic" ? new Color(0.75f, 0.35f, 1f) : f.rarity == "Rare" ? new Color(0.2f, 0.6f, 1f) : Color.white) :
                    new Color(0.5f, 0.5f, 0.5f);

                GUI.Label(new Rect(winX + 24, ry, 220, 20), $"🐟 {f.name} ({f.rarity})", fNameStyle);
                GUI.Label(new Rect(winX + 250, ry, 120, 20), $"Skill: <b>{f.skillRequired}</b>", catchStyle);
                GUI.Label(new Rect(winX + 340, ry, winW - 380, 20), $"<i>{f.buffEffect}</i>", catchStyle);
            }

            // Cast Fishing Line action button
            float botY = winY + winH - 46f;
            GUI.color = new Color(0.35f, 0.75f, 0.95f);
            if (GUI.Button(new Rect(winX + 16, botY, 180, 36), "🎣 Cast Fishing Line"))
            {
                StartCast(player, out _);
            }
            GUI.color = Color.white;

            var tipStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Italic };
            tipStyle.normal.textColor = new Color(0.65f, 0.75f, 0.85f);
            GUI.Label(new Rect(winX + 210, botY + 8, winW - 220, 24), "Cast near open water. Click the bobber the instant it splashes!", tipStyle);
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
