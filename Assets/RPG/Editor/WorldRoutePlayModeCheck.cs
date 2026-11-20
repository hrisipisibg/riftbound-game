using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    [InitializeOnLoad]
    public static class WorldRoutePlayModeCheck
    {
        const string Key = "Riftbound.WorldProductionPlayCheck";
        static readonly BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        enum PreviewSite { None, Settlements, Coilbreak, FullSettlementPreview, ThreeChimes }

        sealed class Case
        {
            internal string name; internal Vector3[] points; internal bool mounted, walk, chest;
            internal Func<Vector3, Vector3> surface;
            internal Case(string name, Vector3[] points, bool mounted, bool reverse = false, bool walk = false, bool chest = false)
            { this.name = name + (reverse ? " return" : " outbound"); this.points = reverse ? points.Reverse().ToArray() : points; this.mounted = mounted; this.walk = walk; this.chest = chest; }
        }
        static List<Case> cases;
        static readonly StringBuilder Report = new StringBuilder();
        static PlayerController player;
        static Keyboard keyboard;
        static int stage, caseIndex, target, captureIndex, pauseClears;
        static double due, deadline, caseDeadline, lastProgress, readyDeadline, traceDue;
        static Vector3 lastPosition;
        static string folder;
        static int goldAfterClaim;
        static WorldAmbient.CivilianRouteWalker porter;
        static int noticeIndex;
        static SettlementWaystop readingNotice;

        static WorldRoutePlayModeCheck() { EditorApplication.playModeStateChanged += Changed; }

        public static void Run()
        {
            Start(false,false);
        }

        public static void RunGateDiagnostic() { Start(true,false); }
        public static void CaptureFrames() { Start(false,true); }
        public static void CaptureSupplyPreview() { Start(false,true,supplies:"compare"); }
        public static void CaptureSupplyFinal() { Start(false,true,supplies:"final"); }
        public static void RunDepartureVerification() { Start(false,false,departure:true); }

        public static void RunSettlementVerification() { Start(false,false,preview:PreviewSite.Settlements); }

        public static void RunCoilbreakVerification() { Start(false,false,preview:PreviewSite.Coilbreak); }

        public static void RunFullSettlementVerification() { Start(false,false,preview:PreviewSite.FullSettlementPreview); }

        public static void RunThreeChimesVerification() { Start(false,false,preview:PreviewSite.ThreeChimes); }

        static void Start(bool gateOnly, bool captureOnly,string supplies="",bool departure=false,PreviewSite preview=PreviewSite.None)
        {
            WorldProductionAudit.Open();
            folder = WorldProductionAudit.Evidence + (preview switch
            {
                PreviewSite.Settlements => "settlement-play-",
                PreviewSite.Coilbreak => "coilbreak-play-",
                PreviewSite.FullSettlementPreview => "full-settlement-play-",
                PreviewSite.ThreeChimes => "three-chimes-play-",
                _ => captureOnly ? "frames-" : "play-"
            }) + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(folder);
            SessionState.SetString(Key + ".Folder",folder);
            SessionState.SetBool(Key,true); SessionState.SetBool(Key + ".Failed",false);
            SessionState.SetBool(Key+".GateOnly",gateOnly); SessionState.SetBool(Key+".CaptureOnly",captureOnly);
            SessionState.SetString(Key+".Supplies",supplies);SessionState.SetBool(Key+".Departure",departure);
            SessionState.SetInt(Key + ".PreviewSite",(int)preview);
            SessionState.SetBool(Key + ".DirectPlay",EditorPrefs.GetBool("Riftbound.DirectPlayInEditor",false));
            EditorPrefs.SetBool("Riftbound.DirectPlayInEditor",false);
            UIArtSetup.PrepareGameView();
            EditorApplication.isPaused = false;
            EditorApplication.isPlaying = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void IsolateSaves()
        {
            if (!SessionState.GetBool(Key,false)) return;
            PlayerController.SavePathOverride = Path.GetFullPath(SessionState.GetString(Key + ".Folder","") + "/test-adventure.json");
            Application.runInBackground = true;
        }

        static void Changed(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(Key,false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                folder = SessionState.GetString(Key + ".Folder","");
                Report.Clear(); Report.AppendLine($"PLAY MODE WORLD CHECK UTC={DateTime.UtcNow:O}");
                Report.AppendLine("Queued keyboard input through actual PlayerController/mount code, with scripted heading and isolated test saves. Fixture teleports between cases; not continuous/manual adventure or online acceptance. No scene/asset saved.");
                stage = 0; caseIndex = 0; pauseClears=0; due = EditorApplication.timeSinceStartup + 2;
                deadline = due + (SessionState.GetBool(Key+".CaptureOnly",false) ? 120 : 480);
                EditorApplication.update += Tick;
            }
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key,false);
                EditorPrefs.SetBool("Riftbound.DirectPlayInEditor",SessionState.GetBool(Key + ".DirectPlay",false));
                EditorApplication.Exit(SessionState.GetBool(Key + ".Failed",false) ? 1 : 0);
            }
        }

        static void Keys(params UnityEngine.InputSystem.Key[] keys) { InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys)); }
        static float FlatDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z));

        static PreviewSite CurrentPreview => (PreviewSite)SessionState.GetInt(Key + ".PreviewSite", 0);

        static void Tick()
        {
            try
            {
                double now = EditorApplication.timeSinceStartup;
                if (now > deadline) throw new TimeoutException("Whole route check exceeded eight-minute bound.");
                if(EditorApplication.isPlaying && EditorApplication.isPaused)
                {
                    pauseClears++;
                    EditorApplication.isPaused=false;
                }
                EditorApplication.QueuePlayerLoopUpdate();
                if (!EditorApplication.isPlaying || now < due) return;
                if (stage == 0)
                {
                    player = Object.FindFirstObjectByType<PlayerController>();
                    var frontend=player ? player.GetComponent<FrontendMenu>() : null;
                    if(!frontend || !frontend.TitleScreenOpen || !FrontendMenu.IsOpen || Time.frameCount<5)
                    {
                        Report.AppendLine($"WAIT startup frame={Time.frameCount} menu={FrontendMenu.IsOpen} title={(frontend && frontend.TitleScreenOpen)} timeScale={Time.timeScale}");
                        File.WriteAllText(folder+"/progress.txt",Report.ToString()); due=now+.5; return;
                    }
                    PlayerController.SavePathOverride=Path.GetFullPath(folder+"/test-adventure.json");
                    typeof(FrontendMenu).GetField("enteringCharacter",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,
                        new FrontendMenu.Character { id=Guid.NewGuid().ToString("N"),name="WorldRouteCheck",faction="Alliance",race="Human",characterClass="Warrior" });
                    typeof(FrontendMenu).GetProperty("IsOpen").SetValue(null,false); player.orbit.SetPaused(false);
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name); stage=1; due=now+.5; readyDeadline=now+20; return;
                }
                if (stage == 1)
                {
                    player=Object.FindFirstObjectByType<PlayerController>();
                    if(!player || player.CharacterName!="WorldRouteCheck" || FrontendMenu.IsOpen)
                    {
                        if(now>readyDeadline) throw new Exception($"Isolated entry failed: name={(player ? player.CharacterName : "absent")} menu={FrontendMenu.IsOpen} frame={Time.frameCount}");
                        due=now+.5; return;
                    }
                    Report.AppendLine($"Isolated player ready: name={player.CharacterName} frame={Time.frameCount} save={player.SavePath}");
                    InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                    keyboard=InputSystem.AddDevice<Keyboard>("WorldRouteCheckKeyboard"); keyboard.MakeCurrent();
                    cases = CurrentPreview switch
                    {
                        PreviewSite.Settlements => SettlementCases(),
                        PreviewSite.Coilbreak => CoilbreakCases(),
                        PreviewSite.FullSettlementPreview => FullSettlementCases(),
                        PreviewSite.ThreeChimes => FullSettlementCases(threeChimes: true),
                        _ => ExistingCases()
                    };
                    stage=2;
                }
                if (stage == 2)
                {
                    if (caseIndex >= cases.Count)
                    {
                        if (CurrentPreview == PreviewSite.FullSettlementPreview || CurrentPreview == PreviewSite.ThreeChimes) { noticeIndex = 0; stage = 30; return; }
                        if (CurrentPreview != PreviewSite.None) { Finish(false); return; }
                        stage = 20; captureIndex = 0; return;
                    }
                    var c=cases[caseIndex]; Keys(); player.Dismount(); player.CloseDialogue();
                    var motor=player.GetComponent<CharacterController>(); motor.enabled=false;
                    player.transform.position=(c.surface?.Invoke(c.points[0]) ?? WorldTraversalVerification.Surface(c.points[0])) + Vector3.up * .12f;
                    var direction=c.points[1]-c.points[0]; direction.y=0; player.transform.rotation=Quaternion.LookRotation(direction);
                    motor.enabled=true; Physics.SyncTransforms();
                    player.orbit.SetPaused(false);
                    typeof(OrbitCamera).GetField("yaw",Hidden).SetValue(player.orbit,player.transform.eulerAngles.y);
                    typeof(OrbitCamera).GetField("pitch",Hidden).SetValue(player.orbit,23f);
                    stage=3; due=now+.6; return;
                }
                if (stage == 3)
                {
                    if(player.Walking!=cases[caseIndex].walk) Keys(UnityEngine.InputSystem.Key.Slash);
                    stage=4; due=now+.2; return;
                }
                if (stage == 4)
                {
                    Keys();
                    if(cases[caseIndex].mounted) { Keys(UnityEngine.InputSystem.Key.Z); stage=5; due=now+.2; return; }
                    StartMoving(now); return;
                }
                if (stage == 5) { Keys(); stage=6; due=now+1.8; return; }
                if (stage == 6)
                {
                    if(!player.Mounted) throw new Exception($"Mount summon rejected for {cases[caseIndex].name}: {player.Message}");
                    StartMoving(now); return;
                }
                if (stage == 7)
                {
                    var c=cases[caseIndex];
                    if(now>traceDue)
                    {
                        var motor=player.GetComponent<CharacterController>();
                        Report.AppendLine($"TRACE {c.name} frame={Time.frameCount} position={player.transform.position:F2} ground={WorldRegions.GroundHeight(player.transform.position):F2} grounded={motor.isGrounded} hp={player.Stats.Health} target={target} message={player.Message}");
                        File.WriteAllText(folder+"/progress.txt",Report.ToString()); traceDue=now+1;
                    }
                    if(!player.Stats.Alive || player.Mounted!=c.mounted)
                        throw new Exception($"Player died or mount state changed: {c.name} at={player.transform.position:F2} hp={player.Stats.Health} mounted={player.Mounted} lastHit={player.Stats.LastHit} message={player.Message}");
                    while(target<c.points.Length && FlatDistance(player.transform.position,c.points[target])<.5f) target++;
                    if(target==c.points.Length)
                    {
                        Keys();
                        if (c.surface != null) { stage = 10; due = now + .25; return; }
                        Report.AppendLine($"PASS INPUT {c.name} mounted={c.mounted} slowWalk={c.walk} end={player.transform.position:F2}");
                        File.WriteAllText(folder+"/progress.txt",Report.ToString());
                        if(c.chest) { Keys(UnityEngine.InputSystem.Key.E); stage=8; due=now+.2; return; }
                        caseIndex++; stage=2; due=now+.2; return;
                    }
                    if(FlatDistance(player.transform.position,lastPosition)>.03f) { lastProgress=now; lastPosition=player.transform.position; }
                    if (now > caseDeadline || now - lastProgress > 3)
                    {
                        var position = player.transform.position;
                        var nearby = Physics.OverlapCapsule(position + Vector3.up * .35f, position + Vector3.up * 1.75f,
                                .9f, ~(1 << 2), QueryTriggerInteraction.Ignore)
                            .Where(collider => !(collider is TerrainCollider) && collider.transform.root != player.transform)
                            .Select(collider => collider.name).Distinct();
                        throw new Exception($"Route stalled: {c.name} mounted={c.mounted} at={position:F2} target={target}, nearby={string.Join(", ", nearby)}, message={player.Message}");
                    }
                    var direction=c.points[target]-player.transform.position; direction.y=0;
                    player.transform.rotation=Quaternion.LookRotation(direction);
                    // Ground movement on foot follows camera yaw. Aim the fixture at each bend.
                    typeof(OrbitCamera).GetField("yaw",Hidden).SetValue(player.orbit,player.transform.eulerAngles.y);
                    Keys(UnityEngine.InputSystem.Key.W); return;
                }
                if(stage==8) { Keys(); stage=9; due=now+.5; return; }
                if(stage==9)
                {
                    var chest=Object.FindObjectsByType<LootChest>(FindObjectsSortMode.None).Single(c=>c.chestId=="willow-rest-cache");
                    if(!player.HasOpenedChest(chest.chestId)) throw new Exception("Queued E did not claim Willow Rest: "+player.Message);
                    goldAfterClaim=player.Gold;
                    if(player.OpenChest(chest) || player.Gold!=goldAfterClaim) throw new Exception("Duplicate cache claim changed gold.");
                    player.LoadAdventure();
                    if(!player.HasOpenedChest(chest.chestId) || player.Gold!=goldAfterClaim) throw new Exception("Cache claim did not survive isolated save/load.");
                    Report.AppendLine("PASS INPUT Willow Rest E interaction; duplicate rejected; claim/gold persisted in isolated save/load.");
                    caseIndex++; stage=2; due=now+.3; return;
                }
                if (stage == 10)
                {
                    var c = cases[caseIndex];
                    var position = player.transform.position;
                    var support = c.surface(position);
                    var motor = player.GetComponent<CharacterController>();
                    float gap = position.y - support.y;
                    if (!player.Stats.Alive || player.Mounted != c.mounted || !motor.isGrounded
                        || gap < -.05f || gap > .30f || FlatDistance(position, c.points.Last()) > .85f)
                        throw new Exception($"Settlement endpoint {c.name} at={position:F2} gap={gap:F2} grounded={motor.isGrounded} mounted={player.Mounted}");
                    Report.AppendLine($"PASS INPUT {c.name} mounted={c.mounted} end={position:F2} supportGap={gap:F2}m cameraDistance={Vector3.Distance(player.orbit.transform.position, position):F2}m");
                    if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                        ScreenCapture.CaptureScreenshot(Path.Combine(folder, $"route-{caseIndex + 1:00}.png"));
                    caseIndex++; stage = 2; due = now + .35; return;
                }
                if (stage == 30)
                {
                    if (noticeIndex == SettlementWaystop.Active.Count)
                    {
                        if (noticeIndex != 4 || !porter || porter.CompletedLoops < 1)
                            throw new Exception($"Settlement content incomplete: notices={noticeIndex}, porter loops={porter?.CompletedLoops}");
                        Report.AppendLine($"PASS court porter completed {porter.CompletedLoops} loops in the combined scene.");
                        Keys(UnityEngine.InputSystem.Key.M); stage = 32; due = now + .25; return;
                    }
                    readingNotice = SettlementWaystop.Active[noticeIndex];
                    player.Dismount();
                    var motor = player.GetComponent<CharacterController>();
                    motor.enabled = false;
                    player.transform.position = readingNotice.transform.position + Vector3.up * .12f;
                    motor.enabled = true;
                    Physics.SyncTransforms();
                    Keys(UnityEngine.InputSystem.Key.E); stage = 31; due = now + .25; return;
                }
                if (stage == 31)
                {
                    Keys();
                    if (player.Message != readingNotice.notice)
                        throw new Exception($"Could not read {readingNotice.displayName} with E. UI message: {player.Message}");
                    Report.AppendLine($"PASS INPUT read {readingNotice.displayName} notice.");
                    noticeIndex++; stage = 30; due = now + .25; return;
                }
                if (stage == 32)
                {
                    Keys();
                    if (!player.MapOpen) throw new Exception("M did not open the expanded atlas.");
                    MinimapRadar.SetWaypoint(readingNotice.transform.position, readingNotice.displayName);
                    if (!MinimapRadar.Instance.TryGetNavigationTarget(out var point, out var title)
                        || title != readingNotice.displayName || FlatDistance(point, readingNotice.transform.position) > .01f)
                        throw new Exception("Waypoint did not resolve to the selected settlement.");
                    Report.AppendLine("PASS INPUT M opens atlas. Settlement destination verified via the existing waypoint API.");
                    if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                        ScreenCapture.CaptureScreenshot(Path.Combine(folder, "settlement-atlas.png"));
                    stage = 33; due = now + .5; return;
                }
                if (stage == 33) { MinimapRadar.ClearWaypoint(); Finish(false); return; }
                if(stage==20) Capture(now);
            }
            catch(Exception e) { Report.AppendLine("FAIL "+e); Finish(true); }
        }

        static List<Case> ExistingCases()
        {
            var routeCases = new List<Case>();
            var bridge=new[] { new Vector3(-278,6,50),new Vector3(-235,6,50) };
            var gate=SettlementRoutePass.ReadPath("Highspire north footway");
            var green=SettlementRoutePass.ReadPath("Greenhollow arrival footway");
            var gateObject=GameObject.Find("Highspire north footway");
            Report.AppendLine($"Path source check: rendererStaticBatch={gateObject.GetComponent<MeshRenderer>().isPartOfStaticBatch} liveMesh={gateObject.GetComponent<MeshFilter>().sharedMesh.name} liveVertices={gateObject.GetComponent<MeshFilter>().sharedMesh.vertexCount} sourcePoints={gate.Length} sourceStart={gate[0]:F2} sourceEnd={gate[gate.Length-1]:F2}");
            if(FlatDistance(gate[0],new Vector3(-200,12,-60))>.1f || FlatDistance(gate[gate.Length-1],new Vector3(-225,9,0))>.1f)
                throw new Exception("Gate path fixture is not the reviewed per-route source mesh.");
            foreach(var pair in new[] { ("Bridge",bridge),("Highspire north gate",gate),("Greenhollow arrival",green) })
                foreach(bool mounted in new[] { false,true }) foreach(bool reverse in new[] { false,true }) routeCases.Add(new Case(pair.Item1,pair.Item2,mounted,reverse));
            routeCases.Add(new Case("Bridge slow walk",bridge,false,false,true));
            var fullNorth=WorldProductionAudit.Routes().Single(r=>r.name=="Complete Emberwatch to Greenhollow").points;
            routeCases.Add(new Case("Full north road",fullNorth,false));
            routeCases.Add(new Case("Full north road",fullNorth,true,true));
            routeCases.Add(new Case("Willow Rest interaction",new[] {new Vector3(-285,6,50),new Vector3(-288,7,51),new Vector3(-288.7f,7.1f,52)},false,chest:true));
            routeCases.Add(new Case("Reaver approach",new[] {new Vector3(-285,6,50),RiverReaverCamp.Position},true));
            if(SessionState.GetBool(Key+".GateOnly",false))
                routeCases=new List<Case> {new Case("Isolated Highspire north gate",gate,false)};
            if(SessionState.GetBool(Key+".CaptureOnly",false)) routeCases.Clear();
            if(SessionState.GetBool(Key+".Departure",false))
            {
                var exit=SettlementRoutePass.ReadPath("Emberwatch west departure");routeCases.Clear();
                foreach(bool mounted in new[] {false,true}) foreach(bool reverse in new[] {false,true}) routeCases.Add(new Case("Emberwatch departure",exit,mounted,reverse));
            }
            return routeCases;
        }

        static List<Case> SettlementCases(bool threeChimes = false)
        {
            var placement = SettlementPlacementPreview.Create(Report, threeChimes);
            var routeCases = SpurCases(placement.Ramp, placement.RampSurface);
            if (threeChimes)
            {
                var relay = ThreeChimesPreview.Create(Report);
                routeCases.AddRange(SpurCases(ThreeChimesPreview.Approach, relay.Surface));
            }
            else
                routeCases.AddRange(SpurCases(placement.CampApproach));
            return routeCases;
        }

        static List<Case> CoilbreakCases()
        {
            CoilbreakYardPreview.Create(Report);
            return SpurCases(CoilbreakYardPreview.Approach);
        }

        static List<Case> FullSettlementCases(bool threeChimes = false)
        {
            var routeCases = SettlementCases(threeChimes);
            routeCases.AddRange(CoilbreakCases());
            WyrmcrestRefugePreview.Create(Report);
            routeCases.AddRange(SpurCases(WyrmcrestRefugePreview.Approach));
            porter = SettlementActivityPreview.Create(Report, threeChimes);
            return routeCases;
        }

        static List<Case> SpurCases(WorldTraversalVerification.Route route, Func<Vector3, Vector3> surface = null)
        {
            var routeCases = new List<Case>();
            foreach (bool mounted in new[] { false, true })
            foreach (bool reverse in new[] { false, true })
                routeCases.Add(new Case(route.name, route.points, mounted, reverse) { surface = surface ?? WorldTraversalVerification.Surface });
            return routeCases;
        }

        static void StartMoving(double now)
        {
            target=1; lastProgress=now; caseDeadline=now+120; lastPosition=player.transform.position; stage=7; due=now; traceDue=now;
            Report.AppendLine($"START INPUT {cases[caseIndex].name} mounted={cases[caseIndex].mounted} frame={Time.frameCount}");
            File.WriteAllText(folder+"/progress.txt",Report.ToString());
        }

        static void Capture(double now)
        {
            string supplies=SessionState.GetString(Key+".Supplies","");
            if(supplies.Length>0)
            {
                if(SettlementSupplyPass.RenderPreviewTick(player,folder,supplies=="final"))
                {Report.AppendLine("Native supply preview completed; inspect PNGs and separate timing receipt before applying.");Finish(false);}
                return;
            }
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)
            { Report.AppendLine("NOT RUN: native GPU frame captures (headless device)."); Finish(false); return; }
            var names=new[] {"Highspire","Westbank","Reaver-lower","Reaver-upper","Greenhollow","Highspire-gate"};
            var positions=new[] {new Vector3(-190,34,-28),new Vector3(-273,18,36),new Vector3(-275,18,53),new Vector3(-285,18.5f,69),new Vector3(-17,27,376),new Vector3(-191,29,9)};
            var targets=new[] {new Vector3(-206,12,-42),new Vector3(-290,7,54),new Vector3(-285,9,64),new Vector3(-285,15.8f,83),new Vector3(-39,13,397),new Vector3(-207,12,-28)};
            if(captureIndex>=names.Length*2) { Finish(false); return; }
            int i=captureIndex/2;
            if(captureIndex%2==0)
            {
                if(captureIndex==0)
                {
                    player.Dismount();
                    var motor=player.GetComponent<CharacterController>(); motor.enabled=false;
                    player.transform.position=WorldTraversalVerification.Surface(new Vector3(-225,9,0))+Vector3.up*.12f;
                    motor.enabled=true;
                }
                player.orbit.enabled=false;
                var camera=player.orbit.GetComponent<Camera>(); camera.transform.position=positions[i]; camera.transform.LookAt(targets[i]);
                captureIndex++; due=now+1;
            }
            else
            {
                string path=folder+"/"+names[i]+".png"; ScreenCapture.CaptureScreenshot(path);
                Report.AppendLine("GPU frame capture requested: "+path+"; file/visual verification required separately.");
                captureIndex++; due=now+1;
            }
        }

        static void Finish(bool failed)
        {
            EditorApplication.update-=Tick;
            if(SessionState.GetString(Key+".Supplies","").Length>0) SettlementSupplyPass.EndPreview();
            if(keyboard!=null) { Keys(); InputSystem.RemoveDevice(keyboard); }
            Report.AppendLine($"Editor error-pause clears={pauseClears}; headless graphics errors/editor exceptions require separate classification.");
            File.WriteAllText(folder+"/result.txt",Report.ToString());
            SessionState.SetBool(Key+".Failed",failed); SessionState.SetBool(Key+".GateOnly",false); EditorApplication.isPlaying=false;
        }
    }
}
