using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riftbound.Editor
{
    [InitializeOnLoad]
    public static class CharacterArtSetup
    {
        static double nextPoll;
        static readonly System.Collections.Generic.List<string> probeLog = new System.Collections.Generic.List<string>();
        static double probeStart = -1;
        static void PlayProbeTick()
        {
            if (!SessionState.GetBool("Riftbound.PlayProbe", false) || !EditorApplication.isPlaying) { probeStart = -1; return; }
            Application.runInBackground = true;
            EditorApplication.QueuePlayerLoopUpdate();
            if (probeStart < 0) probeStart = EditorApplication.timeSinceStartup;
            double elapsed = EditorApplication.timeSinceStartup - probeStart;
            int stage = SessionState.GetInt("Riftbound.PlayProbeStage", 0);
            if (stage == 0 && elapsed >= 2.5)
            {
                var menu = UnityEngine.Object.FindAnyObjectByType<FrontendMenu>();
                var s0 = $"[STAGE 0 - MENU] FrontendMenu.IsOpen={FrontendMenu.IsOpen} menuComp={(menu != null)}\n";
                File.AppendAllText("PlayProbeResult.txt", s0);
                SessionState.SetInt("Riftbound.PlayProbeStage", 1);
                if (menu != null && FrontendMenu.IsOpen) menu.EnterWorld();
                return;
            }
            if (stage == 1 && elapsed >= 6.5)
            {
                var sb = new StringBuilder();
                var pc = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
                var cam = Camera.main;
                var orbit = UnityEngine.Object.FindAnyObjectByType<OrbitCamera>();
                var hud = UnityEngine.Object.FindAnyObjectByType<PrototypeHUD>();
                sb.AppendLine($"[STAGE 1 - IN WORLD] FrontendMenu.IsOpen={FrontendMenu.IsOpen} timeScale={Time.timeScale}");
                sb.AppendLine(pc ? $"Player name={pc.CharacterName} race={pc.CharacterRace} class={pc.CharacterClass} pos={pc.transform.position} active={pc.gameObject.activeInHierarchy} alive={(pc.Stats ? pc.Stats.Alive.ToString() : "NULL")} anim={(pc.animator != null)}" : "Player: NONE");
                sb.AppendLine(cam ? $"Camera.main={cam.name} pos={cam.transform.position} enabled={cam.enabled}" : "Camera.main: NONE");
                sb.AppendLine(orbit ? $"Orbit enabled={orbit.enabled} paused={orbit.Paused} target={(orbit.target ? orbit.target.name : "NULL")}" : "Orbit: NONE");
                sb.AppendLine(hud ? $"HUD enabled={hud.enabled} player={(hud.player ? hud.player.name : "NULL")}" : "HUD: NONE");
                var petSys = PetCompanionSystem.Instance ?? UnityEngine.Object.FindAnyObjectByType<PetCompanionSystem>();
                if (petSys && petSys.petInstance)
                {
                    var pr = petSys.petInstance.GetComponentsInChildren<Renderer>();
                    Bounds pb = pr.Length > 0 ? pr[0].bounds : new Bounds(petSys.petInstance.transform.position, Vector3.zero);
                    for (int i = 1; i < pr.Length; i++) pb.Encapsulate(pr[i].bounds);
                    sb.AppendLine($"Pet name='{petSys.petInstance.name}' size=({pb.size.x:F2},{pb.size.y:F2},{pb.size.z:F2}) pos={petSys.petInstance.transform.position}");
                }
                else
                {
                    sb.AppendLine($"Pet: sys={(petSys != null)} inst={(petSys && petSys.petInstance != null)}");
                }
                var partySys = PartySystem.Instance ?? UnityEngine.Object.FindAnyObjectByType<PartySystem>();
                if (partySys != null && partySys.Members != null)
                {
                    int skinnedParty = partySys.Members.Count(m => m.companionObject && m.companionObject.GetComponentInChildren<SkinnedMeshRenderer>() != null);
                    sb.AppendLine($"Party companions={partySys.Members.Count} with3DSkinnedMesh={skinnedParty}");
                }
                else
                {
                    sb.AppendLine($"Party: sys={(partySys != null)}");
                }
                if (pc != null && Terrain.activeTerrain != null)
                {
                    var terr = Terrain.activeTerrain;
                    string origRace = pc.CharacterRace, origFac = pc.CharacterFaction;
                    Vector3 origPos = pc.transform.position;
                    foreach (var (r, f) in new[] { ("Brute", "Horde"), ("Goblin", "Horde"), ("Undead", "Horde"), ("Human", "Alliance"), ("Dwarf", "Alliance"), ("Elf", "Alliance") })
                    {
                        pc.CharacterRace = r; pc.CharacterFaction = f;
                        Vector3 sp = pc.GetRacialSpawnPosition();
                        float ty = terr.SampleHeight(sp) + terr.transform.position.y;
                        sb.AppendLine($"[LIVE SPAWN PROBE] {f}/{r}: spawn={sp} terrainY={ty:F2} clearance={(sp.y - ty):F2}m");
                    }
                    // Also teleport player to Horde Camp Bloodrock spawn in Play Mode & verify grounded clearance!
                    pc.CharacterRace = "Brute"; pc.CharacterFaction = "Horde";
                    Vector3 hordeSpawn = pc.GetRacialSpawnPosition();
                    var cc = pc.GetComponent<CharacterController>();
                    if (cc) cc.enabled = false;
                    pc.transform.position = hordeSpawn;
                    if (cc) cc.enabled = true;
                    float hTerr = terr.SampleHeight(pc.transform.position) + terr.transform.position.y;
                    sb.AppendLine($"[HORDE TELEPORT PROBE] Player at Camp Bloodrock pos={pc.transform.position} terrainY={hTerr:F2} zone='{ZoneManager.GetZoneName(pc.transform.position)}' delta={(pc.transform.position.y - hTerr):F2}m");
                    if (cc) cc.enabled = false;
                    pc.transform.position = origPos;
                    if (cc) cc.enabled = true;
                    pc.CharacterRace = origRace; pc.CharacterFaction = origFac;
                }
                var fmProbe = UnityEngine.Object.FindAnyObjectByType<FlightMaster>();
                if (pc != null && fmProbe != null)
                {
                    SessionState.SetFloat("Riftbound.ProbeOrigX", pc.transform.position.x);
                    SessionState.SetFloat("Riftbound.ProbeOrigY", pc.transform.position.y);
                    SessionState.SetFloat("Riftbound.ProbeOrigZ", pc.transform.position.z);
                    FlightMaster.OpenTaxiWindow(fmProbe);
                    sb.AppendLine($"[FLIGHT MASTER OPEN] fm='{fmProbe.nodeName}' TaxiMapOpen={FlightMaster.IsAnyTaxiMapOpen}");
                    // Grant temporary 5g for taxi test and fly to Gor'Kresh Stronghold (index 4)
                    pc.AddGoldDirect(5);
                    Time.timeScale = 4f;
                    fmProbe.TakeFlight(FlightMaster.WorldFlightNodes[4]);
                    sb.AppendLine($"[FLIGHT MASTER LAUNCH] InFlight={FlightMaster.IsAnyTaxiInFlight} TaxiMapOpen={FlightMaster.IsAnyTaxiMapOpen}");
                    File.AppendAllText("PlayProbeResult.txt", sb.ToString());
                    SessionState.SetInt("Riftbound.PlayProbeStage", 2);
                    return;
                }
                sb.AppendLine($"Errors captured: {probeLog.Count}");
                foreach (var e in probeLog) sb.AppendLine(e);
                File.AppendAllText("PlayProbeResult.txt", sb.ToString());
                SessionState.SetBool("Riftbound.PlayProbe", false);
                SessionState.SetInt("Riftbound.PlayProbeStage", 0);
                EditorApplication.isPlaying = false;
                return;
            }
            if (stage == 2 && !FlightMaster.IsAnyTaxiInFlight && elapsed >= 8.5)
            {
                Time.timeScale = 1f;
                var sb = new StringBuilder();
                var pc = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
                var terr = Terrain.activeTerrain;
                if (pc != null)
                {
                    var cc = pc.GetComponent<CharacterController>();
                    float ty = terr ? terr.SampleHeight(pc.transform.position) + terr.transform.position.y : 0f;
                    sb.AppendLine($"[STAGE 2 - TAXI LANDED] pos={pc.transform.position} terrainY={ty:F2} clearance={(pc.transform.position.y - ty):F2}m ccEnabled={(cc && cc.enabled)} mounted={pc.Mounted} zone='{ZoneManager.GetZoneName(pc.transform.position)}'");
                    Vector3 origPos = new Vector3(
                        SessionState.GetFloat("Riftbound.ProbeOrigX", pc.transform.position.x),
                        SessionState.GetFloat("Riftbound.ProbeOrigY", pc.transform.position.y),
                        SessionState.GetFloat("Riftbound.ProbeOrigZ", pc.transform.position.z));
                    if (cc) cc.enabled = false;
                    pc.transform.position = origPos;
                    if (cc) cc.enabled = true;
                }
                sb.AppendLine($"Errors captured: {probeLog.Count}");
                foreach (var e in probeLog) sb.AppendLine(e);
                File.AppendAllText("PlayProbeResult.txt", sb.ToString());
                SessionState.SetBool("Riftbound.PlayProbe", false);
                SessionState.SetInt("Riftbound.PlayProbeStage", 0);
                EditorApplication.isPlaying = false;
            }
        }
        public static void BatchValidate()
        {
            try
            {
                EditorSceneManager.OpenScene("Assets/RPG/Scenes/Outpost.unity");
                ApplyNativePlayer();
                if (Environment.GetCommandLineArgs().Contains("-buildDemo")) PrototypeBuild.BuildWindows();
                EditorApplication.Exit(0);
            }
            catch (Exception exception) { File.WriteAllText("CharacterArtResult.txt", "FAILED: " + exception); Debug.LogException(exception); EditorApplication.Exit(1); }
        }
        static CharacterArtSetup()
        {
            if (EditorSettings.enterPlayModeOptionsEnabled) EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorApplication.update += Poll;
            EditorApplication.update += PlayProbeTick;
            Application.logMessageReceived += (msg, stack, type) =>
            {
                if (SessionState.GetBool("Riftbound.PlayProbe", false) && (type == LogType.Exception || type == LogType.Error) && probeLog.Count < 40)
                    probeLog.Add($"[{type}] {msg}\n{stack}");
            };
            const string afterReload = "CharacterArtAfterReload.request";
            if (File.Exists(afterReload))
            {
                string command = File.ReadAllText(afterReload).Trim();
                if (command.Length > 0) { File.WriteAllText("CharacterArtWork.request", command); File.WriteAllText(afterReload, ""); }
            }
        }
        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            nextPoll = EditorApplication.timeSinceStartup + 2;
            const string request = "CharacterArtWork.request";
            if (!File.Exists(request)) return;
            string command = File.ReadAllText(request).Trim(); if (command.Length == 0) return;
            File.WriteAllText(request, "");
            try
            {
                if (command == "inspect") Inspect();
                else if (command == "prepare") Prepare();
                else if (command == "apply") Apply();
                else if (command == "native") ApplyNativePlayer();
                else if (command == "wownpc") WoWNPCSetup.Setup();
                else if (command == "build_characters") CharacterModelBuilder.BuildAll();
                else if (command == "repair_models") { CharacterModelBuilder.RepairAllModelsAndSceneVisuals(); ModelVisualAudit.RunAudit(); }
                else if (command == "audit") ModelVisualAudit.RunAudit();
                else if (command == "test") GameExpansionSmokeTest.RunAllTests();
                else if (command == "refresh") AssetDatabase.Refresh();
                else if (command == "play") UnityEditor.EditorApplication.isPlaying = true;
                else if (command == "play_probe")
                {
                    probeLog.Clear();
                    if (File.Exists("PlayProbeResult.txt")) File.Delete("PlayProbeResult.txt");
                    Application.runInBackground = true;
                    SessionState.SetInt("Riftbound.PlayProbeStage", 0);
                    SessionState.SetBool("Riftbound.PlayProbe", true);
                    EditorApplication.isPlaying = true;
                }
                else if (command == "scene_status")
                {
                    var s = EditorSceneManager.GetActiveScene();
                    var sb = new StringBuilder();
                    sb.AppendLine($"Scene: {s.path} dirty={s.isDirty}");
                    var pc = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
                    if (pc)
                    {
                        sb.AppendLine($"Player: {pc.name} pos={pc.transform.position} active={pc.gameObject.activeInHierarchy}");
                        sb.AppendLine("Player components: " + string.Join(", ", pc.GetComponents<Component>().Select(c => c ? c.GetType().Name : "MISSING")));
                    }
                    var cam = UnityEngine.Object.FindAnyObjectByType<OrbitCamera>();
                    sb.AppendLine(cam ? $"OrbitCamera: {cam.name} pos={cam.transform.position} enabled={cam.enabled}" : "OrbitCamera: NONE");
                    sb.AppendLine("Roots: " + string.Join(", ", s.GetRootGameObjects().Select(g => g.name)));
                    File.WriteAllText("CharacterArtResult.txt", sb.ToString());
                }
                else if (command == "revert_scene")
                {
                    string path = EditorSceneManager.GetActiveScene().path;
                    EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    File.WriteAllText("CharacterArtResult.txt", "Reverted scene from disk: " + path);
                }
                else if (command == "open_outpost")
                {
                    Riftbound.Editor.SceneQuickOpener.OpenOutpostScene();
                    File.WriteAllText("CharacterArtResult.txt", "Active Scene: " + UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path);
                }
            }
            catch (Exception exception) { File.WriteAllText("CharacterArtResult.txt", "FAILED: " + exception); Debug.LogException(exception); }
        }
        [MenuItem("Riftbound/Apply native Brute animations")]
        public static void ApplyNativePlayer()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (!player || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(e => !e.trainingDummy && !e.nativeDeathAnimation)) { Apply(); return; }
            const string folder = "Assets/RPG/Art/BruteNative/";
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder.TrimEnd('/') }))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                importer.animationType = ModelImporterAnimationType.Generic; importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.motionNodeName = "mixamorig:Hips"; PrototypeImport.ConfigureAnimations(importer, importer.assetPath); importer.SaveAndReimport();
            }
            string path = "Assets/RPG/Animations/Brute Native.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            foreach (var entry in new[] { ("Idle", "standing idle"), ("Walk", "unarmed walk forward"), ("WalkBack", "unarmed walk back"), ("Run", "Brute@Unarmed Run Forward"), ("RunBack", "Brute@Unarmed Run Back"), ("Attack", "standing melee attack horizontal"), ("Cleave", "standing melee attack 360 low"), ("Jump", "standing jump") })
            {
                var state = controller.layers[0].stateMachine.states.FirstOrDefault(s => s.state.name == entry.Item1).state;
                if (!state) state = controller.layers[0].stateMachine.AddState(entry.Item1);
                var clip = AssetDatabase.LoadAllAssetsAtPath(folder + entry.Item2 + ".fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__"));
                state.motion = clip; state.speed = entry.Item1 == "Attack" || entry.Item1 == "Cleave" ? 1.45f : 1;
                state.iKOnFeet = false; EditorUtility.SetDirty(state);
                if (entry.Item1 == "Idle") controller.layers[0].stateMachine.defaultState = state;
                if (entry.Item1 == "Attack") player.attackDuration = clip.length / state.speed;
                if (entry.Item1 == "Cleave") player.cleaveDuration = clip.length / state.speed;
            }
            var avatar = AssetDatabase.LoadAllAssetsAtPath(folder + "Brute.fbx").OfType<Avatar>().First(a => a.isValid);
            Undo.RecordObject(player.animator, "Use matching native Brute skeleton");
            player.animator.avatar = avatar; player.animator.runtimeAnimatorController = controller; player.animator.applyRootMotion = false;
            player.walkStrideSpeed = Stride(player.animator, "Walk", 1.5f); player.runStrideSpeed = Stride(player.animator, "Run", 2.7f);
            player.backWalkStrideSpeed = Stride(player.animator, "WalkBack", 1.1f); player.backRunStrideSpeed = Stride(player.animator, "RunBack", 2.5f);
            if (File.Exists("Assets/RPG/Animations/Player Death.anim")) PolishSetup.ConfigureAnimations(player);
            ValidateNative(player, controller);
            EditorUtility.SetDirty(player); EditorUtility.SetDirty(player.animator); EditorUtility.SetDirty(controller);
            Directory.CreateDirectory("Assets/RPG/Scenes/Backups");
            string backup = "Assets/RPG/Scenes/Backups/Outpost-before-native-animation.unity";
            if (!File.Exists(backup)) EditorSceneManager.SaveScene(scene, backup, true);
            string diskBackup = "Assets/RPG/Scenes/Backups/Outpost-last-saved-before-native-animation.unity";
            if (!File.Exists(diskBackup)) File.Copy(scene.path, diskBackup);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the updated scene.");
            File.AppendAllText("CharacterArtResult.txt", $"\nPASS: native Generic Brute skeleton and authored clips; no Humanoid retargeting or foot rotation overrides. Strides {player.walkStrideSpeed:F2}/{player.runStrideSpeed:F2}/{player.backRunStrideSpeed:F2}m/s. Scene saved, backups retained.\n");
        }
        static void ValidateNative(PlayerController player, AnimatorController controller)
        {
            var copy = UnityEngine.Object.Instantiate(player.animator.gameObject); copy.hideFlags = HideFlags.HideAndDontSave;
            copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); copy.transform.localScale = player.animator.transform.lossyScale;
            var expected = UnityEngine.Object.Instantiate(copy); expected.hideFlags = HideFlags.HideAndDontSave;
            var animator = copy.GetComponent<Animator>(); animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            expected.GetComponent<Animator>().enabled = false;
            var report = new StringBuilder(); bool passed = true;
            try
            {
                foreach (string name in new[] { "Walk", "Run", "WalkBack", "RunBack" })
                {
                    var clip = (AnimationClip)controller.layers[0].stateMachine.states.First(s => s.state.name == name).state.motion;
                    float maxError = 0, minX = 999, maxX = -999;
                    for (int i = 0; i < 60; i++)
                    {
                        animator.Play(name, 0, i / 60f); animator.Update(0);
                        clip.SampleAnimation(expected, clip.length * i / 60f);
                        foreach (string boneName in new[] { "mixamorig:LeftFoot", "mixamorig:RightFoot", "mixamorig:LeftToeBase", "mixamorig:RightToeBase" })
                        {
                            var actualBone = copy.GetComponentsInChildren<Transform>().First(t => t.name == boneName);
                            var expectedBone = expected.GetComponentsInChildren<Transform>().First(t => t.name == boneName);
                            maxError = Mathf.Max(maxError, Quaternion.Angle(actualBone.localRotation, expectedBone.localRotation));
                        }
                        var hips = copy.GetComponentsInChildren<Transform>().First(t => t.name == "mixamorig:Hips");
                        minX = Mathf.Min(minX, hips.position.x); maxX = Mathf.Max(maxX, hips.position.x);
                    }
                    bool valid = clip.isLooping && maxError < 2 && maxX - minX < .2f;
                    report.AppendLine($"{(valid ? "PASS" : "FAIL")} {name}: loops={clip.isLooping}; max ankle/toe error from authored clip {maxError:F3} degrees; hip sway {maxX - minX:F3}m."); passed &= valid;
                    if (name == "Walk" || name == "Run")
                    {
                        copy.transform.position = new Vector3(0, -400, 0); animator.Play(name, 0, .45f); animator.Update(0); RenderPose(copy, "Brute-Native-" + name + ".png"); copy.transform.position = Vector3.zero;
                    }
                }
                File.WriteAllText("NativeLocomotionValidation.txt", report.ToString());
                if (!passed) throw new InvalidOperationException("Native animation validation failed. See NativeLocomotionValidation.txt.");
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); UnityEngine.Object.DestroyImmediate(expected); }
        }
        internal static void RenderPose(GameObject model, string filename)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            foreach (var child in model.GetComponentsInChildren<Transform>()) child.gameObject.layer = 30;
            var camera = new GameObject("Temporary pose camera").AddComponent<Camera>();
            var light = new GameObject("Temporary pose light").AddComponent<Light>();
            var texture = new RenderTexture(800, 700, 24); var previous = RenderTexture.active;
            bool previousAsync = ShaderUtil.allowAsyncCompilation;
            try
            {
                light.type = LightType.Directional; light.intensity = 2; light.cullingMask = 1 << 30; light.transform.rotation = Quaternion.Euler(35, -35, 0);
                camera.cullingMask = 1 << 30; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .15f, .17f);
                camera.transform.position = model.transform.position + new Vector3(3.4f, 1.2f, 2.3f); camera.transform.LookAt(model.transform.position + Vector3.up);
                camera.fieldOfView = 35; camera.nearClipPlane = .05f; camera.farClipPlane = 20; camera.targetTexture = texture;
                ShaderUtil.allowAsyncCompilation = false;
                camera.Render(); camera.Render(); RenderTexture.active = texture;
                var image = new Texture2D(800, 700, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 800, 700), 0, 0); image.Apply();
                File.WriteAllBytes(filename, image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
            }
            finally { ShaderUtil.allowAsyncCompilation = previousAsync; RenderTexture.active = previous; texture.Release(); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(camera.gameObject); UnityEngine.Object.DestroyImmediate(light.gameObject); }
        }
        [MenuItem("Riftbound/Apply new animations and enemies")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != "Assets/RPG/Scenes/Outpost.unity") throw new InvalidOperationException("Open Outpost first.");
            bool wasDirty = scene.isDirty;
            string backup = "Assets/RPG/Scenes/Backups/Outpost-before-character-art.unity";
            Directory.CreateDirectory("Assets/RPG/Scenes/Backups");
            if (!File.Exists(backup)) EditorSceneManager.SaveScene(scene, backup, true);
            Prepare();
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            var report = new StringBuilder();
            foreach (var enemy in UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (enemy.trainingDummy) continue;
                string type = enemy.isBoss || enemy.enemyId == "camp-guard-1" ? "Warrior" : enemy.enemyId == "road-raider-2" ? "Rogue" : enemy.enemyId == "road-raider-4" || enemy.enemyId == "camp-guard-2" ? "Mage" : "Minion";
                ReplaceEnemy(enemy, type); report.AppendLine(enemy.enemyId + ": Skeleton " + type + (enemy.ranged ? " / ranged shadow bolts" : " / melee"));
            }
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
            if (!wasDirty) EditorSceneManager.SaveScene(scene);
            report.AppendLine("Scene saved: " + !wasDirty + ". Backup retained. Gameplay tests left to user.");
            File.WriteAllText("CharacterArtResult.txt", "PASS\n" + report);
            ApplyNativePlayer();
        }
        static float Stride(Animator original, string state, float fallback)
        {
            var copy = UnityEngine.Object.Instantiate(original.gameObject); copy.hideFlags = HideFlags.HideAndDontSave;
            copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); copy.transform.localScale = original.transform.lossyScale;
            try
            {
                var animator = copy.GetComponent<Animator>(); animator.Rebind(); animator.Update(0);
                var foot = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.LeftFoot) : copy.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "foot.l" || t.name.EndsWith(":LeftFoot"));
                if (!foot) return fallback;
                animator.Play(state, 0, 0); animator.Update(0); float length = animator.GetCurrentAnimatorStateInfo(0).length;
                var speeds = new System.Collections.Generic.List<float>(); var positions = new Vector3[61]; float low = 999, high = -999;
                for (int i = 0; i <= 60; i++) { animator.Play(state, 0, i / 60f); animator.Update(0); positions[i] = foot.position; low = Mathf.Min(low, foot.position.y); high = Mathf.Max(high, foot.position.y); }
                for (int i = 1; i <= 60; i++)
                {
                    float speed = Mathf.Abs(positions[i - 1].z - positions[i].z) * 60 / Mathf.Max(.1f, length);
                    if (positions[i].y <= low + (high - low) * .3f && speed > .2f && speed < 10) speeds.Add(speed);
                }
                speeds.Sort(); return speeds.Count > 0 ? Mathf.Clamp(speeds[speeds.Count / 2], .5f, 8) : fallback;
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }
        static void ReplaceEnemy(EnemyController enemy, string type)
        {
            string path = "Assets/RPG/Art/Skeletons/Skeleton_" + type + ".fbx";
            string controllerPath = "Assets/RPG/Animations/Skeleton " + type + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            var machine = controller.layers[0].stateMachine;
            bool mage = type == "Mage", rogue = type == "Rogue";
            foreach (var entry in new[] { ("Idle", "Idle"), ("Walk", "Walking_A"), ("Run", "Running_A"), ("Attack", mage ? "Spellcast_Shoot" : rogue ? "1H_Melee_Attack_Slice_Horizontal" : "2H_Melee_Attack_Slice"), ("Cleave", "2H_Melee_Attack_Spin"), ("Hit", "Hit_A"), ("Death", "Death_A") })
            {
                var state = machine.states.FirstOrDefault(s => s.state.name == entry.Item1).state;
                if (!state) state = machine.AddState(entry.Item1);
                var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")).ToArray();
                state.motion = clips.FirstOrDefault(c => c.name == entry.Item2) ?? clips.FirstOrDefault(c => mage && entry.Item1 == "Attack" && c.name.StartsWith("Spellcast")) ?? clips.First(c => c.name == "Idle");
                state.speed = rogue && entry.Item1 == "Attack" ? 1.3f : 1; state.iKOnFeet = false; EditorUtility.SetDirty(state);
                if (entry.Item1 == "Idle") machine.defaultState = state;
                if (entry.Item1 == "Attack") enemy.attackDuration = ((AnimationClip)state.motion).length / state.speed;
                if (entry.Item1 == "Cleave") enemy.cleaveDuration = ((AnimationClip)state.motion).length;
            }
            var old = enemy.animator;
            var visual = enemy.transform.Find("Skeleton visual");
            if (!visual)
            {
                visual = new GameObject("Skeleton visual").transform; visual.SetParent(enemy.transform, false);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path)); model.transform.SetParent(visual, false);
                enemy.animator = model.GetComponentInChildren<Animator>(); enemy.animator.runtimeAnimatorController = controller;
                enemy.animator.applyRootMotion = false; enemy.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                enemy.animator.Rebind(); enemy.animator.Play("Idle", 0, 0); enemy.animator.Update(0);
                var renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>();
                var bounds = renderers[0].bounds; foreach (var renderer in renderers) { renderer.updateWhenOffscreen = true; bounds.Encapsulate(renderer.bounds); }
                float height = enemy.isBoss ? 2.6f : 1.9f;
                visual.localScale *= height / Mathf.Max(.1f, bounds.size.y);
                bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                visual.position += Vector3.up * (enemy.transform.position.y - bounds.min.y);
                string matPath = "Assets/RPG/Materials/Skeleton " + (enemy.isBoss ? "Captain" : type) + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (!material)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RPG/Art/Skeletons/skeleton_texture.png"));
                    material.SetColor("_BaseColor", enemy.isBoss ? new Color(1, .83f, .55f) : Color.white); material.SetFloat("_Smoothness", .1f); AssetDatabase.CreateAsset(material, matPath);
                }
                foreach (var renderer in renderers) renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
                var hand = visual.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "handslot.r");
                if (hand)
                {
                    string weapon = mage ? "Staff" : rogue ? "Blade" : "Axe";
                    var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RPG/Art/Skeletons/Skeleton_" + weapon + ".fbx");
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(asset); go.transform.SetParent(hand, false);
                    foreach (var renderer in go.GetComponentsInChildren<Renderer>()) renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
                }
                if (old)
                {
                    Transform top = old.transform; while (top.parent && top.parent != enemy.transform) top = top.parent;
                    Undo.RecordObject(top.gameObject, "Archive Brute enemy visual"); top.gameObject.SetActive(false);
                }
            }
            else enemy.animator = visual.GetComponentInChildren<Animator>();
            // KayKit's authored front is -Z; controller movement and aiming use +Z.
            visual.localRotation = Quaternion.Euler(0, 180, 0);
            enemy.ranged = mage; enemy.nativeDeathAnimation = true;
            enemy.attackRange = mage ? 9 : 2.1f; enemy.attackInterval = mage ? 2.7f : rogue ? 1.1f : 1.8f;
            enemy.movementMultiplier = enemy.isBoss ? .65f : mage ? .5f : rogue ? .9f : .65f;
            enemy.attackDamage = enemy.isBoss ? 16 : mage ? 14 : rogue ? 8 : 12;
            enemy.walkStrideSpeed = Stride(enemy.animator, "Walk", 1.5f); enemy.runStrideSpeed = Stride(enemy.animator, "Run", 3);
            var stats = enemy.GetComponent<Combatant>(); stats.displayName = enemy.isBoss ? "Ashwood Bone Captain" : mage ? "Ashwood Hexer" : rogue ? "Ashwood Stalker" : type == "Warrior" ? "Ashwood Boneguard" : "Ashwood Bone Raider";
            var motor = enemy.GetComponent<CharacterController>(); motor.height = enemy.isBoss ? 2.6f : 1.9f; motor.center = Vector3.up * motor.height * .5f;
            var grounding = enemy.GetComponent<GroundedVisual>(); if (grounding) grounding.animator = enemy.animator;
            EditorUtility.SetDirty(enemy); EditorUtility.SetDirty(stats); EditorUtility.SetDirty(motor); EditorUtility.SetDirty(controller);
        }
        [MenuItem("Riftbound/Prepare new animation imports")]
        public static void Prepare()
        {
            foreach (string folder in new[] { "Assets/RPG/Art/UniversalAnimations", "Assets/RPG/Art/Skeletons" })
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                importer.animationType = folder.Contains("Skeletons") ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; importer.bakeAxisConversion = true;
                PrototypeImport.ConfigurePackClips(importer); importer.SaveAndReimport();
            }
            Inspect();
        }
        [MenuItem("Riftbound/Inspect new character assets")]
        public static void Inspect()
        {
            var report = new StringBuilder();
            foreach (string folder in new[] { "Assets/RPG/Art/UniversalAnimations", "Assets/RPG/Art/Skeletons" })
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var animator = model.GetComponentInChildren<Animator>();
                report.AppendLine(path + " avatar valid=" + (animator && animator.avatar && animator.avatar.isValid) + " human=" + (animator && animator.isHuman));
                foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")))
                    report.AppendLine($"  {clip.name} | {clip.length:F3}s | loop={clip.isLooping} | speed={clip.averageSpeed}");
                report.AppendLine("  Bones: " + string.Join(", ", model.GetComponentsInChildren<Transform>().Take(30).Select(t => t.name)));
            }
            File.WriteAllText("NewAnimationAssets.txt", report.ToString());
        }
    }
}
