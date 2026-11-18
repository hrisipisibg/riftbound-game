using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Riftbound
{
    public class PetBattleSystem : MonoBehaviour
    {
        public static PetBattleSystem Instance { get; private set; }

        public enum PetFamily
        {
            Beast,
            Dragonkin,
            Flying,
            Undead,
            Mechanical,
            Elemental,
            Aquatic,
            Magic
        }

        [Serializable]
        public class PetAbility
        {
            public string id;
            public string name;
            public string icon;
            public int baseDamage;
            public int healAmount;
            public int cooldownTurns;
            public int currentCooldown;
            public string description;

            public PetAbility(string id, string name, string icon, int dmg, int heal, int cd, string desc)
            {
                this.id = id;
                this.name = name;
                this.icon = icon;
                this.baseDamage = dmg;
                this.healAmount = heal;
                this.cooldownTurns = cd;
                this.currentCooldown = 0;
                this.description = desc;
            }

            public bool IsReady => currentCooldown <= 0;
        }

        [Serializable]
        public class BattlePet
        {
            public string petId;
            public string name;
            public string icon;
            public PetFamily family;
            public int level;
            public int currentXP;
            public int maxXP;
            public int maxHealth;
            public int currentHealth;
            public int power;
            public int speed;
            public List<PetAbility> abilities = new List<PetAbility>();
            public bool isFavorite;

            public BattlePet(string id, string name, string icon, PetFamily family, int level, int hp, int power, int speed, List<PetAbility> abilities)
            {
                this.petId = id;
                this.name = name;
                this.icon = icon;
                this.family = family;
                this.level = level;
                this.currentXP = 0;
                this.maxXP = level * 100;
                this.maxHealth = hp;
                this.currentHealth = hp;
                this.power = power;
                this.speed = speed;
                this.abilities = abilities;
            }

            public bool IsAlive => currentHealth > 0;

            public void HealToFull()
            {
                currentHealth = maxHealth;
                foreach (var a in abilities) a.currentCooldown = 0;
            }

            public bool AddXP(int amount, out int levelsGained)
            {
                levelsGained = 0;
                if (level >= 25) return false;

                currentXP += amount;
                while (currentXP >= maxXP && level < 25)
                {
                    currentXP -= maxXP;
                    level++;
                    levelsGained++;
                    maxXP = level * 100;
                    maxHealth += 15;
                    currentHealth = maxHealth;
                    power += 3;
                    speed += 2;
                }
                return levelsGained > 0;
            }
        }

        public enum BattleTurnState
        {
            Inactive,
            PlayerTurn,
            EnemyTurn,
            RoundResolving,
            BattleOver
        }

        public List<BattlePet> petRoster = new List<BattlePet>();
        public BattlePet ActiveCompanion { get; private set; }
        public int selectedRosterIndex = 0;
        public bool IsJournalOpen { get; set; } = false;

        // Turn-Based Battle State
        public BattleTurnState BattleState { get; private set; } = BattleTurnState.Inactive;
        public bool IsBattleActive => BattleState != BattleTurnState.Inactive;
        public BattlePet ActivePlayerPet { get; private set; }
        public BattlePet ActiveWildPet { get; private set; }
        public int BattleRound { get; private set; } = 1;
        public List<string> battleLog = new List<string>();

        // Overworld Follower Entity
        GameObject companionFollowerObj;
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
            SeedInitialPets();
        }

        void Update()
        {
            // Keyboard shortcut Shift+P or /pets to toggle pet journal
            if (Keyboard.current != null)
            {
                bool shiftPressed = Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
                if (shiftPressed && Keyboard.current.pKey.wasPressedThisFrame)
                {
                    ToggleJournal();
                }
            }

            // Smooth companion follower movement in the overworld
            UpdateCompanionFollower();
        }

        public void SeedInitialPets()
        {
            if (petRoster.Count > 0) return;

            // 1. Phoenix Hatchling (Elemental)
            var phoenixAbilities = new List<PetAbility>
            {
                new PetAbility("phx_burn", "Burn", "🔥", 24, 0, 0, "Ignites enemy for 24 Elemental damage."),
                new PetAbility("phx_cauterize", "Cauterize", "✨", 0, 32, 2, "Heals this pet for 32 health."),
                new PetAbility("phx_immolate", "Immolation", "☄", 45, 0, 3, "Calls down fiery comets for massive 45 damage.")
            };
            petRoster.Add(new BattlePet("pet_phoenix", "Phoenix Hatchling", "🦅", PetFamily.Elemental, 3, 160, 22, 18, phoenixAbilities));

            // 2. Crimson Whelpling (Dragonkin)
            var dragonAbilities = new List<PetAbility>
            {
                new PetAbility("drg_breath", "Flame Breath", "🐲", 22, 0, 0, "Breathes fire dealing 22 Dragonkin damage."),
                new PetAbility("drg_shield", "Emerald Presence", "🛡", 0, 18, 2, "Hardens scales, shielding 18 damage and healing."),
                new PetAbility("drg_deep", "Deep Breath", "🔥", 48, 0, 3, "Draws a deep breath and scorches all for 48 damage.")
            };
            petRoster.Add(new BattlePet("pet_whelp", "Crimson Whelpling", "🐲", PetFamily.Dragonkin, 4, 180, 24, 15, dragonAbilities));

            // 3. Mechanical Squirrel (Mechanical)
            var mechAbilities = new List<PetAbility>
            {
                new PetAbility("mech_fist", "Metal Fist", "⚡", 20, 0, 0, "Strikes with copper fists for 20 Mechanical damage."),
                new PetAbility("mech_rebuild", "Rebuild", "🔧", 0, 35, 2, "Tightens gears, repairing 35 health."),
                new PetAbility("mech_overclock", "Overclock", "⚙", 40, 0, 3, "Supercharges engines for 40 electrical damage.")
            };
            petRoster.Add(new BattlePet("pet_squirrel", "Mechanical Squirrel", "🐿", PetFamily.Mechanical, 2, 140, 19, 24, mechAbilities));

            // 4. Fossilized Hatchling (Undead)
            var undeadAbilities = new List<PetAbility>
            {
                new PetAbility("und_bite", "Bone Bite", "🦴", 25, 0, 0, "Snaps ancient skeletal jaws for 25 damage."),
                new PetAbility("und_bone", "Bone Shield", "🛡", 0, 20, 2, "Whirls bones around to block attacks."),
                new PetAbility("und_storm", "Bonestorm", "💀", 50, 0, 4, "Unleashes a devastating whirlwind of skeletal shards.")
            };
            petRoster.Add(new BattlePet("pet_raptor", "Fossilized Hatchling", "🦖", PetFamily.Undead, 5, 200, 26, 16, undeadAbilities));

            // 5. Frostwolf Pup (Beast)
            var wolfAbilities = new List<PetAbility>
            {
                new PetAbility("fst_bite", "Bite", "🐺", 23, 0, 0, "Savage bite dealing 23 Beast damage."),
                new PetAbility("fst_howl", "Howl", "📢", 10, 15, 2, "Intimidating howl dealing 10 damage and invigorating."),
                new PetAbility("fst_flurry", "Frost Flurry", "❄", 42, 0, 3, "Slashes rapidly with icy fangs for 42 damage.")
            };
            petRoster.Add(new BattlePet("pet_wolf", "Frostwolf Pup", "🐺", PetFamily.Beast, 3, 155, 23, 21, wolfAbilities));
        }

        public void ToggleJournal()
        {
            IsJournalOpen = !IsJournalOpen;
            AudioEffects.PlayClick();
        }

        public void SummonCompanion(string petId)
        {
            var targetPet = petRoster.Find(p => p.petId == petId);
            if (targetPet == null) return;

            ActiveCompanion = targetPet;
            CreateCompanionFollowerObject(targetPet);
            ChatSystem.LogSystem($"<color=#ffcc00><b>[Pets]</b> You summon your {targetPet.name} ({targetPet.icon}) to your side!</color>");
            AudioEffects.PlaySpellCast();
        }

        public void DismissCompanion()
        {
            if (ActiveCompanion != null)
            {
                ChatSystem.LogSystem($"<color=#aaa><b>[Pets]</b> You dismissed {ActiveCompanion.name}.</color>");
                ActiveCompanion = null;
            }
            if (PetCompanionSystem.Instance != null && companionFollowerObj == PetCompanionSystem.Instance.petInstance)
            {
                PetCompanionSystem.Instance.DismissActiveCompanion();
                companionFollowerObj = null;
            }
            else if (companionFollowerObj != null)
            {
                if (Application.isPlaying) Destroy(companionFollowerObj);
                else DestroyImmediate(companionFollowerObj);
                companionFollowerObj = null;
            }
            AudioEffects.PlayClick();
        }

        void CreateCompanionFollowerObject(BattlePet pet)
        {
            // Share the real 3D animated companion with PetCompanionSystem in Play Mode instead of spawning a second sphere
            if (Application.isPlaying && PetCompanionSystem.Instance != null)
            {
                if (companionFollowerObj != null && companionFollowerObj != PetCompanionSystem.Instance.petInstance)
                {
                    Destroy(companionFollowerObj);
                }
                companionFollowerObj = PetCompanionSystem.Instance.SetCompanionFromBattlePet(pet.petId, pet.name);
                return;
            }

            if (companionFollowerObj != null)
            {
                if (Application.isPlaying) Destroy(companionFollowerObj);
                else DestroyImmediate(companionFollowerObj);
                companionFollowerObj = null;
            }

            string resKey = PetCompanionSystem.ResolvePetResourceKey(pet.petId);
            var petPrefab = Resources.Load<GameObject>(resKey) ?? Resources.Load<GameObject>("Pets/Pet_Pug");
            if (petPrefab != null)
            {
                companionFollowerObj = Instantiate(petPrefab);
                companionFollowerObj.name = $"CompanionPet_{pet.name}";
                companionFollowerObj.transform.localScale = Vector3.one;
                foreach (var col in companionFollowerObj.GetComponentsInChildren<Collider>(true))
                {
                    if (Application.isPlaying) Destroy(col);
                    else DestroyImmediate(col);
                }
            }
            else
            {
                companionFollowerObj = new GameObject($"CompanionPet_{pet.name}");
            }

            if (player)
            {
                Vector3 pos = player.transform.position - (player.transform.forward * 1.5f) + (player.transform.right * 0.8f);
                if (Physics.Raycast(pos + Vector3.up * 2.5f, Vector3.down, out var hit, 8f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                    pos.y = hit.point.y;
                companionFollowerObj.transform.position = pos;
            }
        }

        void UpdateCompanionFollower()
        {
            if (!companionFollowerObj || !player) return;
            // When shared with PetCompanionSystem, PetCompanionSystem handles locomotion & animations
            if (PetCompanionSystem.Instance != null && companionFollowerObj == PetCompanionSystem.Instance.petInstance)
                return;

            Vector3 targetPos = player.transform.position - (player.transform.forward * 1.6f) + (player.transform.right * 0.8f);
            if (Physics.Raycast(targetPos + Vector3.up * 2.5f, Vector3.down, out var hit, 8f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                targetPos.y = hit.point.y;
            companionFollowerObj.transform.position = Vector3.Lerp(companionFollowerObj.transform.position, targetPos, Time.deltaTime * 6f);
            Vector3 lookDir = player.transform.position - companionFollowerObj.transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.01f)
                companionFollowerObj.transform.rotation = Quaternion.Slerp(companionFollowerObj.transform.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 8f);
        }

        public static Color GetFamilyColor(PetFamily family)
        {
            switch (family)
            {
                case PetFamily.Beast: return new Color(0.85f, 0.45f, 0.20f); // Warm Orange
                case PetFamily.Dragonkin: return new Color(0.20f, 0.85f, 0.35f); // Emerald
                case PetFamily.Flying: return new Color(0.35f, 0.70f, 1.0f); // Sky Blue
                case PetFamily.Undead: return new Color(0.65f, 0.25f, 0.85f); // Dark Purple
                case PetFamily.Mechanical: return new Color(0.75f, 0.75f, 0.75f); // Silver Steel
                case PetFamily.Elemental: return new Color(1.0f, 0.40f, 0.15f); // Fiery Crimson
                case PetFamily.Aquatic: return new Color(0.15f, 0.65f, 0.95f); // Ocean Blue
                default: return Color.white;
            }
        }

        // ==========================================
        // Turn-Based Battle Pet Arena System
        // ==========================================

        public void StartWildPetBattle(BattlePet playerPet = null, BattlePet wildPet = null)
        {
            if (playerPet == null)
            {
                if (ActiveCompanion != null && ActiveCompanion.IsAlive) playerPet = ActiveCompanion;
                else playerPet = petRoster.Find(p => p.IsAlive) ?? petRoster[0];
            }

            if (wildPet == null)
            {
                wildPet = GenerateWildPet(playerPet.level);
            }

            ActivePlayerPet = playerPet;
            ActiveWildPet = wildPet;
            BattleRound = 1;
            BattleState = BattleTurnState.PlayerTurn;
            battleLog.Clear();

            battleLog.Add($"<color=#ffff00>⚔ A wild {wildPet.name} ({wildPet.icon} Lvl {wildPet.level}) jumped out!</color>");
            battleLog.Add($"<color=#33ccff>Go, {playerPet.name}!</color>");

            ZoneBanner.Show("PET BATTLE INITIATED!", $"{playerPet.name} VS {wildPet.name}");
            AudioEffects.PlayQuestAccept();
        }

        public BattlePet GenerateWildPet(int baseLevel)
        {
            int lvl = Mathf.Max(1, baseLevel + UnityEngine.Random.Range(-1, 2));
            var wildFamilies = new[] { PetFamily.Beast, PetFamily.Aquatic, PetFamily.Flying, PetFamily.Elemental };
            var fam = wildFamilies[UnityEngine.Random.Range(0, wildFamilies.Length)];

            string petName;
            string icon;
            switch (fam)
            {
                case PetFamily.Beast: petName = "Wild Savannah Cub"; icon = "🐾"; break;
                case PetFamily.Aquatic: petName = "Shoreline Mire Croaker"; icon = "🐸"; break;
                case PetFamily.Flying: petName = "Highland Fire Falcon"; icon = "🦅"; break;
                default: petName = "Cinder Elemental Spark"; icon = "🔥"; break;
            }

            var abilities = new List<PetAbility>
            {
                new PetAbility("w_strike", "Wild Strike", "💥", 18 + lvl * 2, 0, 0, "Basic wild attack."),
                new PetAbility("w_shield", "Harden", "🛡", 0, 15 + lvl * 2, 2, "Recovers stamina."),
                new PetAbility("w_burst", "Primal Burst", "⚡", 35 + lvl * 3, 0, 3, "High-damage wild burst.")
            };

            return new BattlePet("wild_" + UnityEngine.Random.Range(100, 999), petName, icon, fam, lvl, 100 + lvl * 18, 15 + lvl * 3, 14 + lvl * 2, abilities);
        }

        public bool ExecutePlayerTurn(int abilityIndex, out string actionLog)
        {
            if (BattleState != BattleTurnState.PlayerTurn)
            {
                actionLog = "Not your turn!";
                return false;
            }

            if (abilityIndex < 0 || abilityIndex >= ActivePlayerPet.abilities.Count)
            {
                actionLog = "Invalid ability index.";
                return false;
            }

            var ability = ActivePlayerPet.abilities[abilityIndex];
            if (!ability.IsReady)
            {
                actionLog = $"{ability.name} is on cooldown for {ability.currentCooldown} turn(s).";
                return false;
            }

            // Tick player cooldowns
            foreach (var a in ActivePlayerPet.abilities)
            {
                if (a.currentCooldown > 0) a.currentCooldown--;
            }

            // Execute ability
            if (ability.healAmount > 0)
            {
                int heal = ability.healAmount + (ActivePlayerPet.power / 2);
                ActivePlayerPet.currentHealth = Mathf.Min(ActivePlayerPet.maxHealth, ActivePlayerPet.currentHealth + heal);
                actionLog = $"{ActivePlayerPet.name} used {ability.icon} {ability.name} and healed for {heal} HP!";
                battleLog.Add($"<color=#00ff88>💚 {actionLog}</color>");
                AudioEffects.PlaySpellCast();
            }
            else
            {
                int damage = ability.baseDamage + (ActivePlayerPet.power / 2);
                ActiveWildPet.currentHealth = Mathf.Max(0, ActiveWildPet.currentHealth - damage);
                actionLog = $"{ActivePlayerPet.name} used {ability.icon} {ability.name} for {damage} damage!";
                battleLog.Add($"<color=#ffaa00>⚔ {actionLog}</color>");
                AudioEffects.PlayBladeHit();
            }

            ability.currentCooldown = ability.cooldownTurns;

            // Check if wild pet was defeated
            if (!ActiveWildPet.IsAlive)
            {
                EndBattle(true);
                return true;
            }

            // Proceed to enemy turn
            BattleState = BattleTurnState.EnemyTurn;
            ExecuteEnemyTurn();
            return true;
        }

        public void ExecuteEnemyTurn()
        {
            if (BattleState != BattleTurnState.EnemyTurn || !ActiveWildPet.IsAlive) return;

            // Tick enemy cooldowns
            foreach (var a in ActiveWildPet.abilities)
            {
                if (a.currentCooldown > 0) a.currentCooldown--;
            }

            // Choose enemy move (prioritize ready highest damage or heal if low)
            PetAbility chosen = null;
            if (ActiveWildPet.currentHealth < ActiveWildPet.maxHealth * 0.4f && ActiveWildPet.abilities.Exists(a => a.healAmount > 0 && a.IsReady))
            {
                chosen = ActiveWildPet.abilities.Find(a => a.healAmount > 0 && a.IsReady);
            }
            else
            {
                var ready = ActiveWildPet.abilities.FindAll(a => a.IsReady && a.baseDamage > 0);
                if (ready.Count > 0)
                {
                    ready.Sort((x, y) => y.baseDamage.CompareTo(x.baseDamage));
                    chosen = ready[0];
                }
                else
                {
                    chosen = ActiveWildPet.abilities[0];
                }
            }

            if (chosen != null)
            {
                if (chosen.healAmount > 0)
                {
                    int heal = chosen.healAmount + (ActiveWildPet.power / 2);
                    ActiveWildPet.currentHealth = Mathf.Min(ActiveWildPet.maxHealth, ActiveWildPet.currentHealth + heal);
                    battleLog.Add($"<color=#ff8888>💚 Wild {ActiveWildPet.name} used {chosen.name} and healed for {heal} HP!</color>");
                }
                else
                {
                    int damage = chosen.baseDamage + (ActiveWildPet.power / 2);
                    ActivePlayerPet.currentHealth = Mathf.Max(0, ActivePlayerPet.currentHealth - damage);
                    battleLog.Add($"<color=#ff4444>💥 Wild {ActiveWildPet.name} used {chosen.name} hitting for {damage} damage!</color>");
                    AudioEffects.PlayBladeHit();
                }
                chosen.currentCooldown = chosen.cooldownTurns;
            }

            // Check if player pet died
            if (!ActivePlayerPet.IsAlive)
            {
                EndBattle(false);
                return;
            }

            BattleRound++;
            BattleState = BattleTurnState.PlayerTurn;
        }

        public bool ThrowPetTrap(out string outcomeMsg, bool guaranteedCatch = false)
        {
            if (BattleState != BattleTurnState.PlayerTurn)
            {
                outcomeMsg = "Can only throw trap during your turn.";
                return false;
            }

            float hpPct = (float)ActiveWildPet.currentHealth / ActiveWildPet.maxHealth;
            if (hpPct > 0.35f)
            {
                outcomeMsg = $"Target health too high ({Mathf.RoundToInt(hpPct * 100)}%)! Lower below 35% first.";
                AudioEffects.PlayClick();
                return false;
            }

            // 85% catch success rate (or 100% if guaranteed test flag)
            bool caught = guaranteedCatch || (UnityEngine.Random.value < 0.85f);
            if (caught)
            {
                outcomeMsg = $"Gotcha! Successfully captured {ActiveWildPet.name} ({ActiveWildPet.icon})!";
                battleLog.Add($"<color=#ffd700>★ {outcomeMsg}</color>");

                // Add to roster
                ActiveWildPet.petId = "caught_" + (petRoster.Count + 1);
                ActiveWildPet.HealToFull();
                petRoster.Add(ActiveWildPet);

                AudioEffects.PlayQuestComplete();
                ZoneBanner.Show("PET CAPTURED!", $"{ActiveWildPet.name} added to your Battle Pet Journal!");
                EndBattle(true, true);
                return true;
            }
            else
            {
                outcomeMsg = $"The wild {ActiveWildPet.name} broke free from the trap!";
                battleLog.Add($"<color=#ff4444>✖ {outcomeMsg}</color>");
                AudioEffects.PlayBladeHit();

                // Trap cost a turn!
                BattleState = BattleTurnState.EnemyTurn;
                ExecuteEnemyTurn();
                return false;
            }
        }

        public void ForfeitBattle()
        {
            if (!IsBattleActive) return;
            battleLog.Add("<color=#aaaaaa>You fled the pet battle.</color>");
            BattleState = BattleTurnState.Inactive;
            AudioEffects.PlayClick();
        }

        void EndBattle(bool playerWon, bool wasCaptured = false)
        {
            BattleState = BattleTurnState.BattleOver;
            if (playerWon)
            {
                if (!wasCaptured)
                {
                    int xpGain = 120 + ActiveWildPet.level * 40;
                    bool leveled = ActivePlayerPet.AddXP(xpGain, out int levelsGained);
                    battleLog.Add($"<color=#ffd700>★ VICTORY! {ActivePlayerPet.name} gained {xpGain} Pet XP!</color>");
                    if (leveled)
                    {
                        battleLog.Add($"<color=#ffff55>🎉 {ActivePlayerPet.name} reached Level {ActivePlayerPet.level}!</color>");
                    }
                    ZoneBanner.Show("PET BATTLE VICTORY!", $"{ActivePlayerPet.name} defeated wild {ActiveWildPet.name}!");
                    AudioEffects.PlayQuestComplete();
                }
            }
            else
            {
                battleLog.Add($"<color=#ff2222>💀 DEFEAT! {ActivePlayerPet.name} was knocked out!</color>");
                ZoneBanner.Show("PET DEFEATED!", "Your companion pet needs rest and healing.");
                AudioEffects.PlayClick();
            }
        }

        public void CloseBattleArena()
        {
            BattleState = BattleTurnState.Inactive;
        }

        public void ReviveAndHealAllPets()
        {
            foreach (var pet in petRoster)
            {
                pet.HealToFull();
            }
            ChatSystem.LogSystem("<color=#00ff88><b>[Pets]</b> All companion battle pets have been restored to full vitality!</color>");
            AudioEffects.PlaySpellCast();
        }

        // ==========================================
        // GUI System
        // ==========================================

        public void DrawPetSystemGUI(float screenWidth, float screenHeight)
        {
            if (IsBattleActive)
            {
                DrawBattleArenaGUI(screenWidth, screenHeight);
            }
            else if (IsJournalOpen)
            {
                DrawPetJournalGUI(screenWidth, screenHeight);
            }
        }

        void DrawPetJournalGUI(float screenWidth, float screenHeight)
        {
            float winW = 680f;
            float winH = 460f;
            float winX = (screenWidth - winW) * 0.5f;
            float winY = (screenHeight - winH) * 0.5f;

            // Border & Backing
            DrawRect(new Rect(winX - 3, winY - 3, winW + 6, winH + 6), new Color(0.85f, 0.70f, 0.28f, 0.95f));
            DrawRect(new Rect(winX, winY, winW, winH), new Color(0.06f, 0.08f, 0.12f, 0.98f));

            // Header
            DrawRect(new Rect(winX, winY, winW, 38f), new Color(0.12f, 0.15f, 0.22f, 1f));
            var headStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            headStyle.normal.textColor = new Color(1f, 0.85f, 0.35f);
            GUI.Label(new Rect(winX + 16, winY + 6, winW - 60, 26), $"🐾 COMPANION & BATTLE PET JOURNAL ({petRoster.Count} Collected)", headStyle);

            if (GUI.Button(new Rect(winX + winW - 32, winY + 6, 24, 24), "✕"))
            {
                IsJournalOpen = false;
                AudioEffects.PlayClick();
            }

            // Left Pane: Pet Roster
            float listW = 280f;
            float listH = winH - 95f;
            DrawRect(new Rect(winX + 12, winY + 46, listW, listH), new Color(0.04f, 0.05f, 0.07f, 0.85f));

            float rowH = 50f;
            for (int i = 0; i < petRoster.Count; i++)
            {
                var p = petRoster[i];
                float rowY = winY + 50 + (i * (rowH + 4));
                if (rowY + rowH > winY + 46 + listH) break;

                bool isSel = (i == selectedRosterIndex);
                Color rowBg = isSel ? new Color(0.25f, 0.35f, 0.50f, 0.9f) : new Color(0.08f, 0.10f, 0.14f, 0.8f);
                DrawRect(new Rect(winX + 16, rowY, listW - 8, rowH), rowBg);

                // Pet icon badge
                var famCol = GetFamilyColor(p.family);
                DrawRect(new Rect(winX + 20, rowY + 5, 40, 40), famCol * 0.3f);
                var icStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(winX + 20, rowY + 5, 40, 40), p.icon, icStyle);

                // Name & level
                var nmStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
                nmStyle.normal.textColor = isSel ? Color.white : new Color(0.9f, 0.9f, 0.9f);
                string compTag = (ActiveCompanion == p) ? " <color=#00ffcc>[Active]</color>" : "";
                GUI.Label(new Rect(winX + 66, rowY + 4, listW - 80, 20), $"{p.name}{compTag}", nmStyle);

                // Family & stats
                var subStyle = new GUIStyle(GUI.skin.label) { fontSize = 11 };
                subStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
                GUI.Label(new Rect(winX + 66, rowY + 24, listW - 80, 20), $"Lvl {p.level} {p.family} · HP {p.currentHealth}/{p.maxHealth}", subStyle);

                if (GUI.Button(new Rect(winX + 16, rowY, listW - 8, rowH), GUIContent.none, GUIStyle.none))
                {
                    selectedRosterIndex = i;
                    AudioEffects.PlayClick();
                }
            }

            // Right Pane: Selected Pet Details
            float detX = winX + listW + 24;
            float detW = winW - listW - 36;
            DrawRect(new Rect(detX, winY + 46, detW, listH), new Color(0.04f, 0.05f, 0.07f, 0.85f));

            if (selectedRosterIndex >= 0 && selectedRosterIndex < petRoster.Count)
            {
                var selPet = petRoster[selectedRosterIndex];

                // Showcase banner
                var famCol = GetFamilyColor(selPet.family);
                DrawRect(new Rect(detX + 10, winY + 56, detW - 20, 80), famCol * 0.25f);
                var bigIconStyle = new GUIStyle(GUI.skin.label) { fontSize = 38, alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(detX + 16, winY + 60, 68, 68), selPet.icon, bigIconStyle);

                var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
                titleStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(detX + 90, winY + 62, detW - 100, 22), selPet.name, titleStyle);

                var statStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
                statStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
                GUI.Label(new Rect(detX + 90, winY + 86, detW - 100, 20), $"Level {selPet.level} {selPet.family} · XP {selPet.currentXP}/{selPet.maxXP}", statStyle);
                GUI.Label(new Rect(detX + 90, winY + 106, detW - 100, 20), $"❤ Health: {selPet.currentHealth}/{selPet.maxHealth}   ⚔ Power: {selPet.power}   ⚡ Speed: {selPet.speed}", statStyle);

                // Ability Cards
                GUI.Label(new Rect(detX + 12, winY + 148, detW - 24, 20), "<b>BATTLE ABILITIES:</b>", new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = new Color(1f, 0.85f, 0.35f) } });

                float abY = winY + 172;
                for (int a = 0; a < selPet.abilities.Count; a++)
                {
                    var ab = selPet.abilities[a];
                    DrawRect(new Rect(detX + 12, abY, detW - 24, 52), new Color(0.08f, 0.10f, 0.14f, 0.9f));

                    var abIconStyle = new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
                    GUI.Label(new Rect(detX + 16, abY + 4, 44, 44), ab.icon, abIconStyle);

                    var abTitle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
                    abTitle.normal.textColor = Color.white;
                    string cdNote = ab.cooldownTurns > 0 ? $" ({ab.cooldownTurns} Round CD)" : " (No CD)";
                    GUI.Label(new Rect(detX + 64, abY + 4, detW - 90, 20), $"{ab.name}{cdNote}", abTitle);

                    var abDesc = new GUIStyle(GUI.skin.label) { fontSize = 11 };
                    abDesc.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
                    GUI.Label(new Rect(detX + 64, abY + 24, detW - 90, 24), ab.description, abDesc);

                    abY += 58;
                }

                // Action Buttons at bottom of details
                bool isCompanion = (ActiveCompanion == selPet);
                string sumLabel = isCompanion ? "Dismiss Companion" : "Summon Companion";
                if (GUI.Button(new Rect(detX + 12, winY + listH - 4, 150, 28), sumLabel))
                {
                    if (isCompanion) DismissCompanion();
                    else SummonCompanion(selPet.petId);
                }

                if (GUI.Button(new Rect(detX + 170, winY + listH - 4, 170, 28), "⚔ Start Wild Pet Duel"))
                {
                    StartWildPetBattle(selPet);
                }
            }

            // Bottom Global Toolbar
            if (GUI.Button(new Rect(winX + 14, winY + winH - 40, 160, 28), "💚 Revive Battle Pets"))
            {
                ReviveAndHealAllPets();
            }

            var noteStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleRight };
            noteStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
            GUI.Label(new Rect(winX + 180, winY + winH - 40, winW - 194, 28), "Press [Shift+P] to toggle journal. Battle wild pets across the realm to capture new species!", noteStyle);
        }

        void DrawBattleArenaGUI(float screenWidth, float screenHeight)
        {
            float arenaW = 760f;
            float arenaH = 460f;
            float arenaX = (screenWidth - arenaW) * 0.5f;
            float arenaY = (screenHeight - arenaH) * 0.5f;

            // Backdrop
            DrawRect(new Rect(arenaX - 3, arenaY - 3, arenaW + 6, arenaH + 6), new Color(0.85f, 0.70f, 0.28f, 0.95f));
            DrawRect(new Rect(arenaX, arenaY, arenaW, arenaH), new Color(0.04f, 0.06f, 0.09f, 0.98f));

            // Header Banner
            DrawRect(new Rect(arenaX, arenaY, arenaW, 36f), new Color(0.12f, 0.15f, 0.22f, 1f));
            var arenaTitle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            arenaTitle.normal.textColor = new Color(1f, 0.85f, 0.35f);
            GUI.Label(new Rect(arenaX, arenaY + 6, arenaW, 24), $"⚔ PET BATTLE ARENA — ROUND {BattleRound}", arenaTitle);

            // Left Unit Frame: Active Player Pet
            DrawPetCombatantFrame(new Rect(arenaX + 20, arenaY + 50, 320, 100), ActivePlayerPet, true);

            // Right Unit Frame: Active Enemy Pet
            DrawPetCombatantFrame(new Rect(arenaX + arenaW - 340, arenaY + 50, 320, 100), ActiveWildPet, false);

            // Center Combat Log
            float logY = arenaY + 160;
            float logH = 150;
            DrawRect(new Rect(arenaX + 20, logY, arenaW - 40, logH), new Color(0.02f, 0.03f, 0.05f, 0.9f));
            float logLineY = logY + 6;
            int startIdx = Mathf.Max(0, battleLog.Count - 6);
            for (int l = startIdx; l < battleLog.Count; l++)
            {
                var logStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true };
                GUI.Label(new Rect(arenaX + 30, logLineY, arenaW - 60, 20), battleLog[l], logStyle);
                logLineY += 22;
            }

            // Bottom Action Bar
            float barY = arenaY + 320;
            DrawRect(new Rect(arenaX + 20, barY, arenaW - 40, 120), new Color(0.08f, 0.10f, 0.14f, 0.9f));

            if (BattleState == BattleTurnState.BattleOver)
            {
                if (GUI.Button(new Rect(arenaX + (arenaW - 160) * 0.5f, barY + 40, 160, 36), "Leave Battle Arena"))
                {
                    CloseBattleArena();
                }
            }
            else
            {
                // 3 Pet Abilities
                for (int a = 0; a < ActivePlayerPet.abilities.Count; a++)
                {
                    var ab = ActivePlayerPet.abilities[a];
                    float btnX = arenaX + 35 + (a * 150);
                    bool canUse = (BattleState == BattleTurnState.PlayerTurn && ab.IsReady);

                    GUI.enabled = canUse;
                    string cdText = ab.currentCooldown > 0 ? $" ({ab.currentCooldown})" : "";
                    if (GUI.Button(new Rect(btnX, barY + 15, 140, 48), $"{ab.icon} {ab.name}{cdText}"))
                    {
                        ExecutePlayerTurn(a, out _);
                    }
                    GUI.enabled = true;
                }

                // Pet Trap Button
                float wildPct = (float)ActiveWildPet.currentHealth / ActiveWildPet.maxHealth;
                bool trapReady = (BattleState == BattleTurnState.PlayerTurn && wildPct <= 0.35f);
                GUI.enabled = trapReady;
                string trapTitle = trapReady ? "★ THROW TRAP! (Ready)" : $"Trap (<35% HP)";
                if (GUI.Button(new Rect(arenaX + 495, barY + 15, 140, 48), trapTitle))
                {
                    ThrowPetTrap(out _);
                }
                GUI.enabled = true;

                // Forfeit / Run Button
                if (GUI.Button(new Rect(arenaX + 645, barY + 15, 80, 48), "🏃 Run"))
                {
                    ForfeitBattle();
                }

                // Status banner
                var stateStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
                stateStyle.normal.textColor = (BattleState == BattleTurnState.PlayerTurn) ? new Color(0.4f, 1f, 0.4f) : new Color(1f, 0.8f, 0.2f);
                string turnNotice = (BattleState == BattleTurnState.PlayerTurn) ? "👉 YOUR TURN — SELECT AN ABILITY OR THROW TRAP!" : "⏳ ENEMY ROUND RESOLVING...";
                GUI.Label(new Rect(arenaX + 20, barY + 75, arenaW - 40, 30), turnNotice, stateStyle);
            }
        }

        void DrawPetCombatantFrame(Rect rect, BattlePet pet, bool isPlayer)
        {
            DrawRect(rect, new Color(0.06f, 0.08f, 0.12f, 0.9f));
            DrawRect(new Rect(rect.x, rect.y, rect.width, 2), GetFamilyColor(pet.family));

            var iconStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(rect.x + 8, rect.y + 10, 50, 50), pet.icon, iconStyle);

            var nmStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            nmStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(rect.x + 65, rect.y + 10, rect.width - 70, 20), $"{pet.name} (Lvl {pet.level})", nmStyle);

            var typeStyle = new GUIStyle(GUI.skin.label) { fontSize = 11 };
            typeStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            GUI.Label(new Rect(rect.x + 65, rect.y + 30, rect.width - 70, 18), $"{pet.family} · Power {pet.power} · Spd {pet.speed}", typeStyle);

            // Health Bar
            float hpPct = (float)pet.currentHealth / pet.maxHealth;
            float barW = rect.width - 75;
            float barH = 16f;
            DrawRect(new Rect(rect.x + 65, rect.y + 54, barW, barH), new Color(0.15f, 0.02f, 0.02f, 1f));
            Color hpCol = isPlayer ? new Color(0.1f, 0.85f, 0.2f) : new Color(0.85f, 0.2f, 0.1f);
            DrawRect(new Rect(rect.x + 65, rect.y + 54, barW * hpPct, barH), hpCol);

            var valStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            valStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(rect.x + 65, rect.y + 54, barW, barH), $"{pet.currentHealth} / {pet.maxHealth} ({Mathf.RoundToInt(hpPct * 100)}%)", valStyle);
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
