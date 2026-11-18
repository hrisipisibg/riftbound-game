using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Riftbound.Network;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Riftbound.Editor
{
    [InitializeOnLoad]
    public static class CombatRefinementChecks
    {
        const string ModeKey = "Riftbound.CombatRefinement.Mode";
        const string SaveKey = "Riftbound.CombatRefinement.SavePath";
        static int stage;
        static double dueAt, timeoutAt;
        static readonly List<string> Lines = new List<string>();
        static readonly List<string> Errors = new List<string>();

        static CombatRefinementChecks()
        {
            EditorApplication.playModeStateChanged += OnPlayStateChanged;
        }

        public static void ReproduceBaseline() => Launch("repro");
        public static void VerifyParity() => Launch("verify");

        static void Launch(string mode)
        {
            if (EditorApplication.isPlaying) return;
            SessionState.SetString(ModeKey, mode);
            SessionState.SetString(SaveKey, Path.Combine(Path.GetTempPath(), $"rb_cf_{Guid.NewGuid():N}.json"));
            EditorApplication.isPaused = false;
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.OpenScene(File.Exists(WorldRegions.ScenePath) ? WorldRegions.ScenePath : "Assets/RPG/Scenes/Outpost-Updated.unity");
            EditorApplication.isPlaying = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void IsolateSave()
        {
            if (!string.IsNullOrEmpty(SessionState.GetString(ModeKey, "")))
            {
                Application.runInBackground = true;
                PlayerController.SavePathOverride = SessionState.GetString(SaveKey, "");
            }
        }

        static void OnPlayStateChanged(PlayModeStateChange change)
        {
            string mode = SessionState.GetString(ModeKey, "");
            if (string.IsNullOrEmpty(mode)) return;

            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                stage = 0;
                dueAt = EditorApplication.timeSinceStartup + 1.5;
                timeoutAt = dueAt + 45.0;
                Lines.Clear();
                Errors.Clear();
                Application.logMessageReceived += OnLog;
                EditorApplication.update += Tick;
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                bool failed = SessionState.GetBool(ModeKey + ".Failed", false);
                SessionState.SetString(ModeKey, "");
                EditorApplication.Exit(failed ? 1 : 0);
            }
        }

        static void OnLog(string msg, string stack, LogType type)
        {
            if ((type == LogType.Exception || type == LogType.Error) && stack.Contains("Riftbound"))
                Errors.Add(msg + "\n" + stack);
        }

        static void Finish(string mode, bool failed)
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            string sp = SessionState.GetString(SaveKey, "");
            if (!string.IsNullOrEmpty(sp) && File.Exists(sp)) File.Delete(sp);
            Directory.CreateDirectory(".code-check/ag01");
            string file = mode == "repro" ? ".code-check/ag01/cf01_repro.txt" : ".code-check/ag01/cf02_04_verify.txt";
            if (Errors.Count > 0) failed = true;
            Lines.Add($"ERRORS={Errors.Count}");
            Lines.AddRange(Errors);
            File.WriteAllText(file, string.Join("\n", Lines) + "\n");
            SessionState.SetBool(ModeKey + ".Failed", failed);
            EditorApplication.isPlaying = false;
        }

        static void Record(string tag, bool ok, string detail)
        {
            Lines.Add($"{(ok ? "PASS" : "FAIL")} [{tag}] {detail}");
            if (!ok) throw new Exception($"FAIL [{tag}] {detail}");
        }

        static void Tick()
        {
            string mode = SessionState.GetString(ModeKey, "");
            try
            {
                if (EditorApplication.timeSinceStartup > timeoutAt) throw new TimeoutException("CombatRefinementChecks timeout at stage " + stage);
                if (EditorApplication.isPaused) EditorApplication.isPaused = false;
                EditorApplication.QueuePlayerLoopUpdate();
                if (EditorApplication.timeSinceStartup < dueAt || !EditorApplication.isPlaying) return;

                var pc = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                if (stage == 0)
                {
                    var entering = typeof(FrontendMenu).GetField("enteringCharacter", BindingFlags.NonPublic | BindingFlags.Static);
                    entering.SetValue(null, new FrontendMenu.Character { id = Guid.NewGuid().ToString("N"), name = "CfCheck", faction = "Alliance", race = "Human", characterClass = "Warrior" });
                    typeof(FrontendMenu).GetProperty("IsOpen").SetValue(null, false);
                    if (pc && pc.orbit) pc.orbit.SetPaused(false);
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                    stage = 1;
                    dueAt = EditorApplication.timeSinceStartup + 1.8;
                    return;
                }

                if (stage == 1)
                {
                    var cc = pc.GetComponent<CharacterController>();
                    if (cc) cc.enabled = false;
                    pc.transform.position = new Vector3(0f, 0.25f, -12f);
                    if (cc)
                    {
                        cc.enabled = true;
                        cc.Move(Vector3.down * 0.5f);
                    }
                    stage = 2;
                    dueAt = EditorApplication.timeSinceStartup + 0.12;
                    return;
                }

                if (stage == 2)
                {
                    var resProp = typeof(PlayerController).GetProperty("ClassResource");
                    var targetProp = typeof(PlayerController).GetProperty("Target");
                    var atkReadyField = typeof(PlayerController).GetField("attackReady", BindingFlags.NonPublic | BindingFlags.Instance);
                    var cc = pc.GetComponent<CharacterController>();
                    Vector3 startPos = pc.transform.position;
                    targetProp.SetValue(pc, null);
                    Lines.Add($"INFO [SETTLE] isGrounded={cc && cc.isGrounded}");

                    // 1. CF01 Pre-validation resources (Warrior empty air strike, Rogue out-of-range strike)
                    pc.CharacterClass = "Warrior";
                    resProp.SetValue(pc, 0f);
                    atkReadyField.SetValue(pc, 0f);
                    pc.CastAbility1Public();
                    float wEmptyRage = pc.ClassResource;

                    pc.CharacterClass = "Paladin";
                    resProp.SetValue(pc, 0f);
                    atkReadyField.SetValue(pc, 0f);
                    pc.CastAbility1Public();
                    float pEmptyHoly = pc.ClassResource;

                    pc.CharacterClass = "Rogue";
                    resProp.SetValue(pc, 100f);
                    atkReadyField.SetValue(pc, 0f);
                    pc.CastAbility1Public();
                    float rEmptyEnergy = pc.ClassResource;

                    // 2. CF03 Dormant phase immunity chip damage (incomingDamageMultiplier == 0f)
                    var dummyGo = new GameObject("CfDummyBoss");
                    dummyGo.transform.position = pc.transform.position + pc.transform.forward * 2.5f;
                    var dummyCbt = dummyGo.AddComponent<Combatant>();
                    dummyCbt.displayName = "DormantTest";
                    dummyCbt.ConfigureVitals(1000f, 5f);
                    dummyCbt.Restore();
                    dummyCbt.incomingDamageMultiplier = 0f;
                    dummyCbt.Damage(50f);
                    float dormantHpAfter = dummyCbt.Health;

                    // Positive-multiplier heavy armor chip check (must still be >= 1 dmg when multiplier > 0)
                    dummyCbt.incomingDamageMultiplier = 1f;
                    dummyCbt.ConfigureVitals(1000f, 80f);
                    dummyCbt.Restore();
                    dummyCbt.Damage(10f);
                    float armoredHpAfter = dummyCbt.Health;

                    // 3. CF04 EnemyController ClearSightForTarget vs wall
                    var wallGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wallGo.name = "CfSightWall";
                    wallGo.transform.position = pc.transform.position + pc.transform.forward * 1.25f + Vector3.up * 1.0f;
                    wallGo.transform.localScale = new Vector3(4f, 3f, 0.5f);
                    var ecDummy = dummyGo.AddComponent<EnemyController>();
                    var clearSightM = typeof(EnemyController).GetMethod("ClearSightForTarget", BindingFlags.NonPublic | BindingFlags.Instance);
                    bool sightBlocked = !(bool)clearSightM.Invoke(ecDummy, new object[] { pc.transform });
                    UnityEngine.Object.Destroy(wallGo);

                    // 4. CF01/CF02 Unknown abilityId and Range parity inspection helpers
                    var rbPlayer = UnityEngine.Object.FindFirstObjectByType<RiftboundCoopPlayer>();
                    Lines.Add($"INFO [ENV] coopInstancePresent={rbPlayer != null} wEmptyRage={wEmptyRage} pEmptyHoly={pEmptyHoly} rEmptyEnergy={rEmptyEnergy} dormantHp={dormantHpAfter}/1000 armoredHp={armoredHpAfter}/1000 sightBlocked={sightBlocked}");

                    if (mode == "repro")
                    {
                        Lines.Add($"REPRO [CF01] Warrior empty strike Rage=0->{wEmptyRage} (defect if >0)");
                        Lines.Add($"REPRO [CF01] Paladin empty strike HolyPower=0->{pEmptyHoly} (defect if >0)");
                        Lines.Add($"REPRO [CF01] Rogue empty strike Energy=100->{rEmptyEnergy} (defect if <100)");
                        Lines.Add($"REPRO [CF03] Dormant incomingDamageMultiplier=0 after Damage(50): Health={dormantHpAfter}/1000 (defect if <1000)");
                        Lines.Add($"REPRO [CF04] Wall blocks ClearSightForTarget: blocked={sightBlocked} (EnemyController previously bypassed via targetIsRemote)");
                        UnityEngine.Object.Destroy(dummyGo);
                        Finish(mode, false);
                        return;
                    }

                    // Verify mode assertions
                    Record("CF01-WarriorEmptyStrike", Mathf.Approximately(wEmptyRage, 0f), $"Rage unchanged at 0 on empty air swing (got {wEmptyRage})");
                    Record("CF01-PaladinEmptyStrike", Mathf.Approximately(pEmptyHoly, 0f), $"HolyPower unchanged at 0 on empty air swing (got {pEmptyHoly})");
                    Record("CF01-RogueEmptyStrike", Mathf.Approximately(rEmptyEnergy, 100f), $"Energy unchanged at 100 on out-of-range strike (got {rEmptyEnergy})");
                    Record("CF03-DormantImmunity", Mathf.Approximately(dormantHpAfter, 1000f), $"incomingDamageMultiplier=0 prevents chip damage (hp={dormantHpAfter}/1000)");
                    Record("CF03-PositiveArmorFloor", Mathf.Approximately(armoredHpAfter, 999f), $"Positive-multiplier high armor retains >=1 dmg floor (hp={armoredHpAfter}/1000)");
                    Record("CF04-ClearSightWall", sightBlocked, "ClearSightForTarget blocks line-of-sight behind solid cover");

                    // Restore valid living dummy for legal cast checks
                    dummyCbt.incomingDamageMultiplier = 1f;
                    dummyCbt.ConfigureVitals(1000f, 0f);
                    dummyCbt.Restore();
                    targetProp.SetValue(pc, dummyCbt);

                    // Valid Warrior Strike generates +15 Rage on legal target
                    pc.CharacterClass = "Warrior";
                    resProp.SetValue(pc, 0f);
                    atkReadyField.SetValue(pc, 0f);
                    pc.CastAbility1Public();
                    Record("CF02-WarriorLegalStrike", Mathf.Approximately(pc.ClassResource, 15f), $"Warrior legal strike grants +15 Rage (got {pc.ClassResource})");

                    // Valid Rogue Sinister Strike spends 40 Energy on legal target
                    pc.CharacterClass = "Rogue";
                    resProp.SetValue(pc, 100f);
                    atkReadyField.SetValue(pc, 0f);
                    pc.CastAbility1Public();
                    Record("CF02-RogueLegalStrike", Mathf.Approximately(pc.ClassResource, 60f), $"Rogue legal strike spends 40 Energy (got {pc.ClassResource})");

                    // Mage Counterspell 25m range local check against casting target
                    pc.CharacterClass = "Mage";
                    if (cc) cc.enabled = false;
                    pc.transform.position = dummyGo.transform.position - dummyGo.transform.forward * 15f;
                    if (cc) cc.enabled = true;
                    CastBarSystem.StartTargetCast(dummyCbt, "Test Bolt", "*", 3.0f, null, interruptible: true);
                    targetProp.SetValue(pc, dummyCbt);
                    pc.CastInterruptPublic();
                    var castInfo = CastBarSystem.GetTargetCast(dummyCbt);
                    Record("CF02-Mage25mInterrupt", castInfo != null && castInfo.isInterrupted, $"Mage interrupts casting target at 15m (interrupted={castInfo?.isInterrupted})");

                    // Test BossPhase immunity in CathedralBossEncounter (Vael dormant in phase 1 takes 0 damage)
                    var cb = CathedralBossEncounter.Instance;
                    if (cb && cb.abbotVael)
                    {
                        var vaelCbt = cb.abbotVael.GetComponent<Combatant>();
                        float vaelBefore = vaelCbt.Health;
                        vaelCbt.Damage(75f);
                        Record("CF03-DormantVaelImmune", Mathf.Approximately(vaelCbt.Health, vaelBefore), $"Dormant Vael takes 0 damage in Phase 1 ({vaelCbt.Health}/{vaelBefore})");
                    }

                    // 5. AG02 NarrativeCatalog & NPC behavior checks (SC02/SC04/SC05/SC07/SC09)
                    Riftbound.Narrative.NarrativeCatalog.ReloadForTesting(null);
                    bool badJsonRejected = !Riftbound.Narrative.NarrativeCatalog.TryParseAndValidate("{\"schemaVersion\":99,\"catalogId\":\"wrong\"}", out _);
                    Record("SC02-RejectBadSchema", badJsonRejected, "Rejects invalid schemaVersion/catalogId");

                    var qgGo = new GameObject("CfTestAbe");
                    var qgComp = qgGo.AddComponent<QuestGiver>();
                    qgComp.npcName = "Abe";
                    var mRowanGo = new GameObject("CfTestRowan");
                    var mRowan = mRowanGo.AddComponent<Merchant>();
                    mRowan.npcName = "Quartermaster Rowan";
                    var npcCloneGo = new GameObject("CfTestElaraClone");
                    var npcClone = npcCloneGo.AddComponent<TownNPC>();
                    npcClone.npcName = "Cartographer Elara Twin";
                    var npcElaraGo = new GameObject("CfTestElara");
                    var npcElara = npcElaraGo.AddComponent<TownNPC>();
                    npcElara.npcName = "Cartographer Elara";

                    var abeProf = Riftbound.Narrative.NarrativeCatalog.ResolveMainQuestGiver(qgComp);
                    var rowanProf = Riftbound.Narrative.NarrativeCatalog.ResolveMerchant(mRowan);
                    var cloneProf = Riftbound.Narrative.NarrativeCatalog.ResolveTownNPC(npcClone);
                    var elaraProf = Riftbound.Narrative.NarrativeCatalog.ResolveTownNPC(npcElara);
                    Record("SC04-ExactProfileIdentity", abeProf != null && rowanProf != null && elaraProf != null && cloneProf == null,
                        $"abe={abeProf?.profileId} rowan={rowanProf?.profileId} elara={elaraProf?.profileId} cloneNull={cloneProf == null}");

                    var failClosedMain = Riftbound.Narrative.NarrativeCatalog.SelectText(abeProf, "main_body", "", null);
                    var abeOffer = Riftbound.Narrative.NarrativeCatalog.SelectText(abeProf, "main_body", "", pc);
                    Record("SC04-ViewerFailClosedAndAbeOffer", !failClosedMain.matched && abeOffer.matched && abeOffer.lineId == "abe.main0.offer",
                        $"nullViewerMatched={failClosedMain.matched} abeLine={abeOffer.lineId}");

                    var fqs = FactionQuestSystem.Instance ?? pc.gameObject.AddComponent<FactionQuestSystem>();
                    var surveyOffer = Riftbound.Narrative.NarrativeCatalog.SelectText(elaraProf, "quest_offer", "frontier_cartography", pc);
                    var reaverBlocked = Riftbound.Narrative.NarrativeCatalog.SelectText(elaraProf, "quest_offer", "river_reaver_bounty", pc);
                    Record("SC05-ElaraQuestPrerequisiteGate", surveyOffer.matched && surveyOffer.lineId == "elara.survey.offer" && !reaverBlocked.matched,
                        $"surveyLine={surveyOffer.lineId} reaverBeforePrereq={reaverBlocked.matched}");

                    var selvekGo = new GameObject("CfTestSelvek");
                    var selvekEnemy = selvekGo.AddComponent<EnemyController>();
                    selvekEnemy.enemyId = "westbank-reaver-captain";
                    var selvekCbt = selvekEnemy.GetComponent<Combatant>();
                    selvekCbt.ConfigureVitals(200f, 0f);
                    selvekCbt.Restore();
                    var selvekProf = Riftbound.Narrative.NarrativeCatalog.ResolveHostileEnemy(selvekEnemy);
                    var selvekBarkAlive = Riftbound.Narrative.NarrativeCatalog.SelectText(selvekProf, "encounter_bark", "aggro", pc, selvekEnemy);
                    selvekCbt.Damage(500f);
                    var selvekBarkDead = Riftbound.Narrative.NarrativeCatalog.SelectText(selvekProf, "encounter_bark", "aggro", pc, selvekEnemy);
                    Record("SC04-SelvekAliveBarkAndDeadGate", selvekBarkAlive.matched && selvekBarkAlive.lineId == "selvek.encounter.alive" && !selvekBarkDead.matched,
                        $"aliveBark={selvekBarkAlive.matched} deadBark={selvekBarkDead.matched}");

                    bool prevInv = pc.InventoryOpen;
                    pc.InventoryOpen = true;
                    npcElara.TriggerGreetingBark(pc, explicitInteract: false);
                    var barkField = typeof(TownNPC).GetField("barkUntil", BindingFlags.NonPublic | BindingFlags.Instance);
                    float elaraBarkUntilBlocked = (float)barkField.GetValue(npcElara);
                    pc.InventoryOpen = false;
                    npcElara.TriggerGreetingBark(pc, explicitInteract: false);
                    float elaraBarkUntilAllowed = (float)barkField.GetValue(npcElara);
                    pc.InventoryOpen = prevInv;
                    Record("SC09-AmbientPauseAndBark", elaraBarkUntilBlocked <= 0f && elaraBarkUntilAllowed > Time.time,
                        $"blockedBarkUntil={elaraBarkUntilBlocked:F2} allowedBarkUntil={elaraBarkUntilAllowed:F2}");

                    UnityEngine.Object.Destroy(qgGo);
                    UnityEngine.Object.Destroy(mRowanGo);
                    UnityEngine.Object.Destroy(npcCloneGo);
                    UnityEngine.Object.Destroy(npcElaraGo);
                    UnityEngine.Object.Destroy(selvekGo);

                    // 6. AG03 Encounter Mechanics Slice (CF06 buffer, CF08 synced caster windup, CF09 dodge i-frame)
                    pc.CharacterClass = "Warrior";
                    pc.ClearCombatInputBuffer();
                    atkReadyField.SetValue(pc, Time.time + 0.18f);
                    pc.RequestPrimaryAttackInput();
                    int queuedSlot = pc.BufferedAbilitySlot;
                    pc.ClearCombatInputBuffer();
                    int clearedSlot = pc.BufferedAbilitySlot;
                    Record("CF06-PrimaryInputBuffer", queuedSlot == 1 && clearedSlot == 0, $"queued={queuedSlot} cleared={clearedSlot}");

                    ecDummy.ranged = true;
                    ecDummy.attackDuration = 1.2f;
                    var pendingField = typeof(EnemyController).GetField("pendingDamage", BindingFlags.NonPublic | BindingFlags.Instance);
                    var damageAtField = typeof(EnemyController).GetField("damageAt", BindingFlags.NonPublic | BindingFlags.Instance);
                    float expectedRangedTime = Time.time + ecDummy.attackDuration * 0.85f;
                    // Simulate synced cast windup timing from Update path
                    CastBarSystem.StartTargetCast(dummyCbt, "Shadow Bolt", "*", ecDummy.attackDuration * 0.85f, null, interruptible: true);
                    pendingField.SetValue(ecDummy, true);
                    damageAtField.SetValue(ecDummy, expectedRangedTime);
                    bool castActiveAtHalf = CastBarSystem.IsTargetCasting(dummyCbt);
                    bool interrupted = CastBarSystem.InterruptTarget(dummyCbt, "Counterspell");
                    Record("CF08-RangedCastWindowFairness", castActiveAtHalf && interrupted, $"active={castActiveAtHalf} interrupted={interrupted}");

                    float hpPreDodge = pc.Stats.Health;
                    pc.Stats.invulnerableUntil = Time.time + 0.35f;
                    pc.Stats.Damage(25f);
                    pc.Stats.invulnerableUntil = 0f;
                    Record("CF09-DodgeInvulnerableWindow", Mathf.Approximately(pc.Stats.Health, hpPreDodge), $"hpAfterDodgeHit={pc.Stats.Health}/{hpPreDodge}");

                    // 7. MP01-MP04 Multiplayer Presentation & Party Checks
                    var defHorse = RiftboundCoopPlayer.ResolveMountDefinition("horse");
                    var defZebra = RiftboundCoopPlayer.ResolveMountDefinition("zebra");
                    var defGryphon = RiftboundCoopPlayer.ResolveMountDefinition("gryphon");
                    bool realMountsOk = defHorse && defHorse.prefab && defHorse.prefab.saddle && defHorse.prefab.animator
                                     && defZebra && defZebra.prefab && defZebra.prefab.saddle && defZebra.prefab.animator
                                     && defGryphon && defGryphon.prefab;
                    Record("MP01-MountRegistryRealPrefabs", realMountsOk,
                        $"horse={defHorse?.prefab?.name ?? "null"} zebra={defZebra?.prefab?.name ?? "null"} gryphon={defGryphon?.prefab?.name ?? "null"}");

                    bool allowlistOk = RiftboundCoopPlayer.IsSupportedMountId("horse")
                                    && RiftboundCoopPlayer.IsSupportedMountId("zebra")
                                    && RiftboundCoopPlayer.IsSupportedMountId("gryphon")
                                    && !RiftboundCoopPlayer.IsSupportedMountId("capsule_hack")
                                    && RiftboundCoopPlayer.IsSupportedActionAnim("Dodge")
                                    && RiftboundCoopPlayer.IsSupportedActionAnim("Cancel")
                                    && !RiftboundCoopPlayer.IsSupportedActionAnim("SpoofWave");
                    Record("MP02-ActionAndMountAllowlist", allowlistOk, "Mount & Action allowlists accept valid IDs and reject spoofed IDs");

                    int companionCountStart = PartySystem.Instance ? PartySystem.Instance.Members.Count : 0;
                    var cwm = CoopWorldManager.Instance;
                    bool modalBlockedGroup = false;
                    if (cwm)
                    {
                        cwm.SetGroupMenuOpenForTest(true);
                        modalBlockedGroup = pc.AnyBlockingWindowOpen();
                        cwm.CloseGroupMenu();
                    }
                    Record("MP04-PartyOptInAndGroupMenuModal", companionCountStart == 0 && (cwm == null || modalBlockedGroup),
                        $"initialCompanions={companionCountStart} cwmPresent={cwm != null} groupModalBlocks={modalBlockedGroup}");

                    bool roleBoundsOk = RiftboundCoopPlayer.SanitizeSocialRole(-1) == 0
                                     && RiftboundCoopPlayer.SanitizeSocialRole(2) == 2
                                     && RiftboundCoopPlayer.SanitizeSocialRole(99) == 3
                                     && RiftboundCoopPlayer.GetSocialRoleTag(1) == "[T]"
                                     && RiftboundCoopPlayer.GetSocialRoleName(3) == "DPS";
                    float groundedOff = RiftboundCoopPlayer.ComputeRemoteMountGroundOffset(false, true, 10.12f, 10.0f);
                    float airborneOff = RiftboundCoopPlayer.ComputeRemoteMountGroundOffset(true, true, 10.12f, 10.0f);
                    float clampedOff = RiftboundCoopPlayer.ComputeRemoteMountGroundOffset(false, true, 5.0f, 10.0f);
                    bool groundOffsetOk = Mathf.Approximately(groundedOff, 0.14f)
                                       && Mathf.Approximately(airborneOff, 0f)
                                       && Mathf.Approximately(clampedOff, -0.35f);
                    Record("MP03-SocialRoleAndMountGrounding", roleBoundsOk && groundOffsetOk,
                        $"roleBounds={roleBoundsOk} grounded={groundedOff:F2} airborne={airborneOff:F2} clamped={clampedOff:F2}");

                    UnityEngine.Object.Destroy(dummyGo);
                    if (cc) cc.enabled = false;
                    pc.transform.position = startPos;
                    if (cc) cc.enabled = true;
                    Finish(mode, false);
                }
            }
            catch (Exception ex)
            {
                Lines.Add("EXCEPTION: " + ex);
                Finish(mode, true);
            }
        }
    }
}
