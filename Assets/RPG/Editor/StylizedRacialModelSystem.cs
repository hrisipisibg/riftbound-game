using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Riftbound.Editor
{
    /// <summary>
    /// Unifies texture/material styling across all playable characters, town NPCs, flight masters,
    /// world enemies, mounts, and companion pets, and rigs authentic 3D racial proportions & features
    /// (Dwarven braided beards & stout builds, High Elf swept ears & glowing arcane eyes, Warcraft-green
    /// Orcs with curved ivory tusks & spiked pauldrons, Forsaken Undead with hand-painted bone/armor &
    /// glowing eyes, and Bilgewater Goblins with wide pointed ears, hooked noses & tinker gear).
    /// </summary>
    public static class StylizedRacialModelSystem
    {
        public const string StylizedTexDir = "Assets/RPG/Materials/StylizedTextures/";
        public const string StylizedMatDir = "Assets/RPG/Materials/Characters/";
        public const string RacialMeshDir  = "Assets/RPG/Art/RacialMeshes/";
        const string ArtWowDir             = "Assets/RPG/Art/NPCs_WoW/";
        const string SkeletonsDir          = "Assets/RPG/Art/Skeletons/";
        const string AnimDir               = "Assets/RPG/Animations/";
        const string StampPath             = StylizedTexDir + "StylizedTextures_v3.stamp";

        public enum PaletteMode
        {
            HumanAlliance,
            HumanCrimson,
            HumanArcane,
            HumanShadow,
            DwarfClan,
            DwarfGold,
            DwarfFrost,
            ElfSunwell,
            ElfSilvermoon,
            ElfFarstrider,
            OrcWarsong,
            OrcKorkron,
            OrcShadowmoon,
            OrcShatteredHand,
            OrcOutcast,
            GoblinBruiser,
            GoblinCartelGold,
            GoblinTinker,
            GoblinCutthroat,
            GoblinScavenger,
            UndeadWarrior,
            UndeadPaladin,
            UndeadMage,
            UndeadRogue,
            UndeadGhoul,
            CreatureTrogg,
            CreatureManaSprite
        }

        public struct ActorRacialSpec
        {
            public string race;          // "Human", "Dwarf", "Elf", "Orc", "Undead", "Goblin", "Trogg", "ManaSprite", "Ghoul"
            public string archetype;     // "Warrior", "Paladin", "Mage", "Rogue", "Smith", "Innkeeper", "Noble"
            public bool isFemale;
            public float targetHeight;
            public Vector3 bodyScale;
            public PaletteMode palette;
            public string fbxPath;
            public string weaponRightFbx;
            public string weaponLeftFbx;
            public bool attachPauldrons;
            public bool attachHeadgear;
            public float nameplateHeight;
        }

        [MenuItem("Riftbound/🎨 Rebuild All Racial Models & Unified Textures", priority = -8)]
        public static void RebuildEverythingMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[StylizedRacialModelSystem] Exit Play Mode before rebuilding models.");
                return;
            }
            EnsureUnifiedAssetsReady(true);
            CharacterModelBuilder.BuildAll();
            var sb = new StringBuilder();
            UpgradeAllSceneModelsAndMaterials(sb);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
            Debug.Log($"<color=#70df80><b>[StylizedRacialModelSystem]</b> Rebuilt all 24 playable models, pets, mounts, NPCs & enemies with unified stylized textures & racial anatomy!</color>\n{sb}");
        }

        // =========================================================================
        // 1. UNIFIED STYLIZED TEXTURE BAKER
        // =========================================================================
        public static void EnsureUnifiedAssetsReady(bool forceRebuild = false)
        {
            Directory.CreateDirectory(StylizedTexDir);
            Directory.CreateDirectory(StylizedMatDir);
            Directory.CreateDirectory(RacialMeshDir);

            EnsureProceduralSurfaceTextures(forceRebuild);
            EnsureRacialMeshes(forceRebuild);
        }

        static void EnsureProceduralSurfaceTextures(bool force)
        {
            bool needsImport = false;

            needsImport |= WriteProceduralTextureIfMissing("Tex_Stylized_Fur.png", 256, force, (u, v) =>
            {
                // Layered directional painted fur/hide strokes
                float streak = Fbm(u * 10f, v * 42f, 3);
                float tuft = Fbm(u * 24f + 3.1f, v * 12f + 1.7f, 2);
                float shade = Mathf.Lerp(0.76f, 1.04f, streak * 0.68f + tuft * 0.32f);
                shade = QuantizeSoft(shade, 7f, 0.35f);
                return new Color(shade, shade * 0.99f, shade * 0.96f, 1f);
            });

            needsImport |= WriteProceduralTextureIfMissing("Tex_Stylized_ForgedMetal.png", 256, force, (u, v) =>
            {
                float bevel = Fbm(u * 9f, v * 9f, 3);
                float brush = Fbm(u * 36f, v * 8f, 2);
                float vVal = Mathf.Lerp(0.70f, 1.05f, bevel * 0.65f + brush * 0.35f);
                vVal = QuantizeSoft(vVal, 6f, 0.45f);
                return new Color(vVal * 0.96f, vVal * 0.98f, vVal * 1.02f, 1f);
            });

            needsImport |= WriteProceduralTextureIfMissing("Tex_Stylized_BoneHorn.png", 256, force, (u, v) =>
            {
                float ridge = Mathf.Sin(u * Mathf.PI * 18f) * 0.5f + 0.5f;
                float grain = Fbm(u * 14f, v * 32f, 3);
                float baseGrad = Mathf.Lerp(0.72f, 1.04f, Mathf.Clamp01(v * 0.55f + 0.45f));
                float val = baseGrad * Mathf.Lerp(0.84f, 1.05f, grain * 0.75f + ridge * 0.25f);
                return new Color(val, val * 0.95f, val * 0.84f, 1f);
            });

            needsImport |= WriteProceduralTextureIfMissing("Tex_Stylized_HairBeard.png", 256, force, (u, v) =>
            {
                float locks = Mathf.Sin(u * Mathf.PI * 22f + Fbm(u * 6f, v * 10f, 2) * 3.0f) * 0.5f + 0.5f;
                float strands = Fbm(u * 38f, v * 10f, 2);
                float band = Mathf.Lerp(0.68f, 1.08f, locks * 0.62f + strands * 0.38f);
                band = QuantizeSoft(band, 6f, 0.4f);
                return new Color(band, band * 0.97f, band * 0.93f, 1f);
            });

            needsImport |= WriteProceduralTextureIfMissing("Tex_Stylized_SkinGrain.png", 256, force, (u, v) =>
            {
                float warm = Fbm(u * 10f, v * 10f, 3);
                float brush = Fbm(u * 26f, v * 18f, 2);
                float val = Mathf.Lerp(0.88f, 1.04f, warm * 0.65f + brush * 0.35f);
                return new Color(val, val * 0.98f, val * 0.96f, 1f);
            });

            needsImport |= WriteProceduralTextureIfMissing("Tex_Stylized_ArcaneCrystal.png", 256, force, (u, v) =>
            {
                float c1 = Fbm(u * 8f, v * 8f, 3);
                float c2 = Fbm(u * 18f + 5f, v * 18f + 2f, 2);
                float facet = QuantizeSoft(c1 * 0.65f + c2 * 0.35f, 5f, 0.65f);
                return Color.Lerp(new Color(0.28f, 0.62f, 0.98f, 1f), new Color(0.72f, 0.96f, 1.0f, 1f), facet);
            });

            needsImport |= WriteProceduralTextureIfMissing("Tex_Stylized_LeafCanopy.png", 256, force, (u, v) =>
            {
                float clusters = Fbm(u * 10f, v * 10f, 3);
                float leaves = Fbm(u * 26f + 3.1f, v * 26f + 1.7f, 2);
                float vein = QuantizeSoft(clusters * 0.6f + leaves * 0.4f, 6f, 0.45f);
                return Color.Lerp(new Color(0.48f, 0.64f, 0.42f, 1f), new Color(0.95f, 1.0f, 0.82f, 1f), vein);
            });

            needsImport |= WriteProceduralTextureIfMissing("Tex_Stylized_PineNeedles.png", 256, force, (u, v) =>
            {
                float bough = Fbm(u * 14f, v * 8f, 3);
                float needles = Fbm(u * 38f, v * 18f, 2);
                float val = QuantizeSoft(bough * 0.55f + needles * 0.45f, 6f, 0.4f);
                return Color.Lerp(new Color(0.45f, 0.58f, 0.46f, 1f), new Color(0.88f, 0.98f, 0.84f, 1f), val);
            });

            needsImport |= WriteProceduralTextureIfMissing("Tex_Stylized_BirchLeaves.png", 256, force, (u, v) =>
            {
                float clusters = Fbm(u * 11f + 2f, v * 11f + 4f, 3);
                float detail = Fbm(u * 24f, v * 24f, 2);
                float val = QuantizeSoft(clusters * 0.6f + detail * 0.4f, 6f, 0.45f);
                return Color.Lerp(new Color(0.68f, 0.58f, 0.38f, 1f), new Color(1.0f, 0.96f, 0.78f, 1f), val);
            });

            needsImport |= WriteProceduralTextureIfMissing("Tex_Stylized_TreeBark.png", 256, force, (u, v) =>
            {
                float ridge = Fbm(u * 22f, v * 5f, 3);
                float grain = Fbm(u * 40f, v * 12f, 2);
                float crevice = QuantizeSoft(ridge * 0.7f + grain * 0.3f, 7f, 0.45f);
                return Color.Lerp(new Color(0.42f, 0.36f, 0.32f, 1f), new Color(0.92f, 0.86f, 0.78f, 1f), crevice);
            });

            needsImport |= WriteProceduralTextureIfMissing("Tex_Stylized_BirchBark.png", 256, force, (u, v) =>
            {
                float baseBark = Fbm(u * 8f, v * 6f, 2);
                float knot = Fbm(u * 5f, v * 28f, 3);
                float darkStripe = knot < 0.26f ? 0.35f : 1.0f;
                float val = Mathf.Lerp(0.76f, 0.98f, baseBark) * darkStripe;
                return new Color(val, val * 0.98f, val * 0.94f, 1f);
            });

            needsImport |= WriteProceduralTextureIfMissing("Tex_Stylized_FlowerPetals.png", 256, force, (u, v) =>
            {
                float bloom = Fbm(u * 16f, v * 16f, 3);
                float petal = Fbm(u * 32f + 1.4f, v * 32f + 2.8f, 2);
                float t = QuantizeSoft(bloom * 0.65f + petal * 0.35f, 5f, 0.5f);
                return Color.Lerp(new Color(0.55f, 0.42f, 0.62f, 1f), new Color(1.0f, 0.92f, 0.76f, 1f), t);
            });

            needsImport |= WriteProceduralTextureIfMissing("Tex_Stylized_WaterRipples.png", 256, force, (u, v) =>
            {
                float wave1 = Fbm(u * 10f, v * 14f, 3);
                float wave2 = Fbm(u * 20f + 4f, v * 24f + 2f, 2);
                float foam = Mathf.SmoothStep(0.62f, 0.84f, wave1 * 0.6f + wave2 * 0.4f);
                return Color.Lerp(new Color(0.52f, 0.76f, 0.92f, 1f), new Color(0.92f, 0.98f, 1.0f, 1f), foam);
            });

            if (needsImport)
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                ConfigureStylizedTextureImports(true);
            }
        }

        static bool WriteProceduralTextureIfMissing(string fileName, int size, bool force, Func<float, float, Color> generator)
        {
            string assetPath = StylizedTexDir + fileName;
            string diskPath = Path.GetFullPath(assetPath);
            if (!force && File.Exists(diskPath)) return false;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = y / (float)(size - 1);
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1);
                    pixels[y * size + x] = generator(u, v);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(diskPath, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            return true;
        }

        /// <summary>
        /// Bakes (or loads cached) a cohesive WoW-style hand-painted character diffuse texture
        /// from any raw Mixamo / KayKit / legacy PNG texture, applying painterly smoothing,
        /// tonal normalization, brushstroke weave, and pixel-accurate racial/class color remapping.
        /// </summary>
        public static Texture2D GetOrBakeStylizedDiffuse(string sourceAssetPath, PaletteMode mode, bool preserveAlpha = false)
        {
            if (string.IsNullOrEmpty(sourceAssetPath))
                return AssetDatabase.LoadAssetAtPath<Texture2D>(StylizedTexDir + "Tex_Stylized_SkinGrain.png");

            string srcName = Path.GetFileNameWithoutExtension(sourceAssetPath);
            string outFile = $"Tex_Char_{srcName}_{mode}.png";
            string outAssetPath = StylizedTexDir + outFile;
            string outDiskPath = Path.GetFullPath(outAssetPath);

            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(outAssetPath);
            if (existing != null && File.Exists(StampPath))
                return existing;

            string srcDiskPath = Path.GetFullPath(sourceAssetPath);
            if (!File.Exists(srcDiskPath))
                return AssetDatabase.LoadAssetAtPath<Texture2D>(sourceAssetPath);

            var rawTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(rawTex, File.ReadAllBytes(srcDiskPath)))
            {
                UnityEngine.Object.DestroyImmediate(rawTex);
                return AssetDatabase.LoadAssetAtPath<Texture2D>(sourceAssetPath);
            }

            bool isAtlas = srcName.IndexOf("skeleton", StringComparison.OrdinalIgnoreCase) >= 0;
            int outSize = 384;
            int srcW = rawTex.width;
            int srcH = rawTex.height;
            Color[] srcPixels = rawTex.GetPixels();
            UnityEngine.Object.DestroyImmediate(rawTex);

            // Step 1: Resample to crisp 384x384 working buffer
            Color[] work = new Color[outSize * outSize];
            for (int y = 0; y < outSize; y++)
            {
                float vf = y / (float)(outSize - 1);
                int sy = Mathf.Clamp(Mathf.RoundToInt(vf * (srcH - 1)), 0, srcH - 1);
                for (int x = 0; x < outSize; x++)
                {
                    float uf = x / (float)(outSize - 1);
                    int sx = Mathf.Clamp(Mathf.RoundToInt(uf * (srcW - 1)), 0, srcW - 1);
                    work[y * outSize + x] = srcPixels[sy * srcW + sx];
                }
            }

            // Step 2: Edge-preserving painterly smoothing pass (unifies noisy photo scans with cleaner models)
            if (!isAtlas && !preserveAlpha)
            {
                Color[] smoothed = new Color[work.Length];
                for (int y = 0; y < outSize; y++)
                {
                    int y0 = Mathf.Max(0, y - 1), y1 = Mathf.Min(outSize - 1, y + 1);
                    for (int x = 0; x < outSize; x++)
                    {
                        int x0 = Mathf.Max(0, x - 1), x1 = Mathf.Min(outSize - 1, x + 1);
                        Color center = work[y * outSize + x];
                        float rSum = 0f, gSum = 0f, bSum = 0f, wSum = 0f;
                        for (int ny = y0; ny <= y1; ny++)
                        {
                            for (int nx = x0; nx <= x1; nx++)
                            {
                                Color c = work[ny * outSize + nx];
                                float diff = Mathf.Abs(c.r - center.r) + Mathf.Abs(c.g - center.g) + Mathf.Abs(c.b - center.b);
                                float w = diff < 0.22f ? 1.0f : 0.15f;
                                rSum += c.r * w; gSum += c.g * w; bSum += c.b * w; wSum += w;
                            }
                        }
                        smoothed[y * outSize + x] = new Color(rSum / wSum, gSum / wSum, bSum / wSum, center.a);
                    }
                }
                work = smoothed;
            }

            // Step 3: Racial & Class Palette Remapping + Unified Hand-Painted Tonal Curve + Brushstroke Grain
            bool isBearOrcTex = srcName.IndexOf("bear", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isGoblinTex = srcName.IndexOf("goblin", StringComparison.OrdinalIgnoreCase) >= 0;

            for (int y = 0; y < outSize; y++)
            {
                float vf = y / (float)outSize;
                for (int x = 0; x < outSize; x++)
                {
                    float uf = x / (float)outSize;
                    Color c = work[y * outSize + x];
                    float origA = c.a;

                    Color mapped = TransformPixelForPalette(c, mode, isBearOrcTex, isGoblinTex, isAtlas, uf, vf);

                    // Unified WoW painterly tonal curve: lift crushed shadows, roll off harsh white glare, add subtle brushstroke weave
                    Color.RGBToHSV(mapped, out float h, out float s, out float v);
                    v = Mathf.Lerp(0.18f, 0.96f, Mathf.Pow(Mathf.Clamp01(v), 0.85f));
                    s = Mathf.Clamp01(s * 1.12f);

                    float grain = Fbm(uf * 28f, vf * 28f, 2) - 0.5f;
                    float stroke = Fbm(uf * 12f, vf * 44f, 2) - 0.5f;
                    float paintDelta = isAtlas ? (grain * 0.10f + stroke * 0.08f) : (grain * 0.045f + stroke * 0.035f);
                    v = Mathf.Clamp01(QuantizeSoft(v + paintDelta, 9f, 0.28f));

                    Color finalC = Color.HSVToRGB(h, s, v);
                    finalC.a = preserveAlpha ? origA : 1f;
                    work[y * outSize + x] = finalC;
                }
            }

            var outTex = new Texture2D(outSize, outSize, TextureFormat.RGBA32, false);
            outTex.SetPixels(work);
            outTex.Apply();
            File.WriteAllBytes(outDiskPath, outTex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(outTex);

            AssetDatabase.ImportAsset(outAssetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(outAssetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.alphaIsTransparency = preserveAlpha;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.maxTextureSize = 512;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(outAssetPath);
        }

        public static void MarkTextureStampComplete()
        {
            Directory.CreateDirectory(StylizedTexDir);
            File.WriteAllText(Path.GetFullPath(StampPath), DateTime.UtcNow.ToString("O"));
        }

        static void ConfigureStylizedTextureImports(bool tileable)
        {
            foreach (string file in Directory.GetFiles(StylizedTexDir, "Tex_Stylized_*.png"))
            {
                string p = file.Replace('\\', '/');
                var imp = AssetImporter.GetAtPath(p) as TextureImporter;
                if (imp == null) continue;
                imp.textureType = TextureImporterType.Default;
                imp.sRGBTexture = true;
                imp.wrapMode = tileable ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                imp.filterMode = FilterMode.Bilinear;
                imp.maxTextureSize = 256;
                imp.SaveAndReimport();
            }
        }

        static Color TransformPixelForPalette(Color c, PaletteMode mode, bool isBearOrcTex, bool isGoblinTex, bool isAtlas, float u, float v)
        {
            Color.RGBToHSV(c, out float h, out float s, out float val);
            bool isWarmFleshOrFur = (h <= 0.12f || h >= 0.93f) && s >= 0.14f && s <= 0.68f && val >= 0.18f;

            switch (mode)
            {
                // -------------------- ORCS (Warcraft Emerald-Olive Skin + Clan Gear) --------------------
                case PaletteMode.OrcWarsong:
                case PaletteMode.OrcKorkron:
                case PaletteMode.OrcShadowmoon:
                case PaletteMode.OrcShatteredHand:
                case PaletteMode.OrcOutcast:
                {
                    // On Crag_Orc (bear_diffuse), warm brown/tan fur/hide becomes muscular Warcraft Orc green skin;
                    // very dark straps/armor stay dark leather/iron; mid-dark accents take clan trim color.
                    bool orcSkinRegion = isBearOrcTex
                        ? (val > 0.20f && s < 0.72f && (h < 0.14f || h > 0.90f))
                        : (isWarmFleshOrFur && val > 0.30f && s < 0.55f);

                    if (orcSkinRegion)
                    {
                        if (mode == PaletteMode.OrcOutcast)
                        {
                            // Fel-scarred dark olive-ash Outcast Orc skin
                            h = 0.26f;
                            s = Mathf.Clamp01(0.36f + s * 0.25f);
                            val = Mathf.Clamp(val * 0.92f + 0.05f, 0.18f, 0.68f);
                        }
                        else
                        {
                            // Iconic Warcraft Orc vibrant emerald-olive skin
                            h = mode == PaletteMode.OrcShatteredHand ? 0.295f : 0.278f;
                            s = Mathf.Clamp01(0.54f + s * 0.25f);
                            val = Mathf.Clamp(val * 1.08f + 0.06f, 0.22f, 0.78f);
                        }
                        return Color.HSVToRGB(h, s, val);
                    }

                    // Clan cloth / harness accents
                    if (s > 0.22f && val > 0.16f)
                    {
                        if (mode == PaletteMode.OrcWarsong)       { h = 0.99f; s = Mathf.Max(s, 0.62f); } // Warsong Crimson
                        else if (mode == PaletteMode.OrcKorkron)  { h = 0.10f; s = Mathf.Max(s, 0.58f); val = Mathf.Min(0.88f, val * 1.15f); } // Bronze-Gold
                        else if (mode == PaletteMode.OrcShadowmoon) { h = 0.76f; s = Mathf.Max(s, 0.55f); } // Ancestral Violet
                        else if (mode == PaletteMode.OrcShatteredHand) { h = 0.60f; s = Mathf.Min(s, 0.22f); val *= 0.78f; } // Dark Assassin Leather
                    }
                    return Color.HSVToRGB(h, s, val);
                }

                // -------------------- GOBLINS (Vivid Bilgewater Green Skin + Cartel Gear) --------------------
                case PaletteMode.GoblinBruiser:
                case PaletteMode.GoblinCartelGold:
                case PaletteMode.GoblinTinker:
                case PaletteMode.GoblinCutthroat:
                case PaletteMode.GoblinScavenger:
                {
                    bool goblinGreenAlready = h >= 0.19f && h <= 0.42f && s >= 0.18f;
                    bool humanSkinToGoblin = !isGoblinTex && isWarmFleshOrFur && val > 0.34f && s < 0.52f;

                    if (goblinGreenAlready || humanSkinToGoblin)
                    {
                        h = mode == PaletteMode.GoblinScavenger ? 0.245f : 0.272f;
                        s = Mathf.Clamp01(0.60f + s * 0.20f);
                        val = Mathf.Clamp(val * 1.10f + 0.05f, 0.26f, 0.84f);
                        return Color.HSVToRGB(h, s, val);
                    }

                    // Outfit / armor accents on non-skin pixels
                    if (val > 0.16f && (s > 0.18f || isGoblinTex))
                    {
                        if (mode == PaletteMode.GoblinBruiser && val < 0.58f)    { h = 0.01f; s = 0.58f; val = Mathf.Max(val, 0.28f); }
                        else if (mode == PaletteMode.GoblinCartelGold && val < 0.65f) { h = 0.12f; s = 0.66f; val = Mathf.Clamp(val * 1.25f, 0.30f, 0.88f); }
                        else if (mode == PaletteMode.GoblinTinker && val < 0.62f) { h = 0.56f; s = 0.56f; val = Mathf.Max(val, 0.30f); }
                        else if (mode == PaletteMode.GoblinCutthroat && val < 0.60f) { h = 0.72f; s = 0.24f; val *= 0.80f; }
                        else if (mode == PaletteMode.GoblinScavenger && val < 0.62f) { h = 0.06f; s = 0.62f; }
                    }
                    return Color.HSVToRGB(h, s, val);
                }

                // -------------------- HIGH ELVES (Luminous Rose-Ivory Skin + Silvermoon Gold/Crimson/Azure) --------------------
                case PaletteMode.ElfSunwell:
                case PaletteMode.ElfSilvermoon:
                case PaletteMode.ElfFarstrider:
                {
                    if (isWarmFleshOrFur && val > 0.42f && s < 0.48f)
                    {
                        // Radiant Quel'dorei / Sin'dorei porcelain-rose complexion
                        h = 0.045f;
                        s = Mathf.Clamp(s * 0.72f, 0.14f, 0.30f);
                        val = Mathf.Clamp(val * 1.12f + 0.05f, 0.55f, 0.94f);
                        return Color.HSVToRGB(h, s, val);
                    }

                    // Transform dull medieval browns/greys into regal Highborne colors
                    if (s >= 0.15f && val > 0.18f)
                    {
                        if (mode == PaletteMode.ElfSunwell)      { h = val > 0.52f ? 0.12f : 0.98f; s = Mathf.Max(s, 0.56f); } // Gold & Sunwell Crimson
                        else if (mode == PaletteMode.ElfSilvermoon) { h = val > 0.55f ? 0.55f : 0.60f; s = Mathf.Max(s, 0.48f); val = Mathf.Min(0.92f, val * 1.12f); } // Arcane Silver & Azure
                        else if (mode == PaletteMode.ElfFarstrider) { h = val > 0.50f ? 0.11f : 0.36f; s = Mathf.Max(s, 0.48f); } // Gold & Ranger Emerald
                    }
                    else if (s < 0.15f && val > 0.30f)
                    {
                        // Gleaming silver-mithril armor
                        h = 0.58f; s = 0.10f; val = Mathf.Clamp(val * 1.16f, 0.38f, 0.92f);
                    }
                    return Color.HSVToRGB(h, s, val);
                }

                // -------------------- DWARVES (Ruddy Khaz Modan Skin + Bronzed Iron & Clan Colors) --------------------
                case PaletteMode.DwarfClan:
                case PaletteMode.DwarfGold:
                case PaletteMode.DwarfFrost:
                {
                    if (isWarmFleshOrFur && val > 0.36f && s < 0.52f)
                    {
                        // Warm ruddy mountain-clan complexion
                        h = 0.032f;
                        s = Mathf.Clamp(s * 1.12f, 0.24f, 0.46f);
                        val = Mathf.Clamp(val * 1.03f, 0.38f, 0.88f);
                        return Color.HSVToRGB(h, s, val);
                    }
                    if (s < 0.16f && val > 0.22f)
                    {
                        // Forged Dwarven bronze-iron steel
                        h = mode == PaletteMode.DwarfFrost ? 0.58f : 0.09f;
                        s = mode == PaletteMode.DwarfGold ? 0.34f : 0.16f;
                        val = Mathf.Clamp(val * 1.08f, 0.28f, 0.86f);
                    }
                    else if (s >= 0.20f)
                    {
                        if (mode == PaletteMode.DwarfClan) h = Mathf.Lerp(h, 0.02f, 0.45f);
                        else if (mode == PaletteMode.DwarfFrost) { h = 0.57f; s = Mathf.Max(s, 0.46f); }
                    }
                    return Color.HSVToRGB(h, s, val);
                }

                // -------------------- UNDEAD / FORSAKEN (Hand-Painted Bone + Dark Gothic Armor) --------------------
                case PaletteMode.UndeadWarrior:
                case PaletteMode.UndeadPaladin:
                case PaletteMode.UndeadMage:
                case PaletteMode.UndeadRogue:
                case PaletteMode.UndeadGhoul:
                {
                    if (isAtlas)
                    {
                        // Skeleton atlas: high-val warm/neutral cells are bone; darker/saturated cells are cloth/armor/leather
                        bool isBoneCell = val > 0.62f && s < 0.32f;
                        if (isBoneCell)
                        {
                            float boneShade = Fbm(u * 22f, v * 48f, 3);
                            if (mode == PaletteMode.UndeadGhoul)
                                return Color.Lerp(new Color(0.42f, 0.54f, 0.38f), new Color(0.68f, 0.76f, 0.58f), boneShade);
                            return Color.Lerp(new Color(0.66f, 0.64f, 0.56f), new Color(0.88f, 0.85f, 0.76f), boneShade);
                        }

                        // Armor & tattered cloak cells per Undead class
                        if (mode == PaletteMode.UndeadPaladin)
                        {
                            h = val > 0.42f ? 0.12f : 0.77f; // Twilight Gold trim & Forgotten-Shadow Royal Violet plate/cape
                            s = Mathf.Clamp(s + 0.28f, 0.25f, 0.62f);
                            val = Mathf.Clamp(val * 1.08f + 0.06f, 0.20f, 0.82f);
                        }
                        else if (mode == PaletteMode.UndeadRogue)
                        {
                            h = s > 0.25f ? 0.35f : 0.68f; // Plague-emerald & Deathstalker charcoal
                            s = Mathf.Clamp(s * 0.85f + 0.12f, 0.16f, 0.48f);
                            val = Mathf.Clamp(val * 0.88f + 0.04f, 0.16f, 0.62f);
                        }
                        else if (mode == PaletteMode.UndeadMage)
                        {
                            h = 0.78f; // Dark Necromancer / Cultist violet-crimson
                            s = Mathf.Clamp(s + 0.22f, 0.24f, 0.60f);
                            val = Mathf.Clamp(val * 0.96f + 0.05f, 0.18f, 0.72f);
                        }
                        else if (mode == PaletteMode.UndeadGhoul)
                        {
                            h = 0.30f; // Rotting crypt moss/plague hide
                            s = 0.42f;
                            val = Mathf.Clamp(val * 0.85f + 0.05f, 0.16f, 0.56f);
                        }
                        else
                        {
                            // Undead Warrior: Dread-iron & dark Forsaken crimson
                            if (s > 0.25f) { h = 0.98f; s = 0.52f; }
                            else { h = 0.62f; s = 0.14f; }
                        }
                        return Color.HSVToRGB(h, s, val);
                    }

                    // Humanoid Forsaken fallback: ashen blue-slate undead skin
                    if (isWarmFleshOrFur)
                    {
                        return Color.HSVToRGB(0.58f, 0.18f, Mathf.Clamp(val * 0.88f, 0.30f, 0.72f));
                    }
                    return Color.HSVToRGB(h, s, val);
                }

                // -------------------- ZONE CREATURES (Troggs & Mana Sprites) --------------------
                case PaletteMode.CreatureTrogg:
                {
                    // Craggy subterranean frost-granite stone hide with earthen leather wraps
                    if (val > 0.22f)
                    {
                        h = 0.56f;
                        s = 0.18f;
                        val = Mathf.Clamp(val * 0.96f + 0.08f, 0.26f, 0.76f);
                    }
                    return Color.HSVToRGB(h, s, val);
                }

                case PaletteMode.CreatureManaSprite:
                {
                    // Luminous cyan-violet arcane ley elemental
                    h = Mathf.Lerp(0.53f, 0.74f, Mathf.Clamp01(1f - val));
                    s = 0.68f;
                    val = Mathf.Clamp(val * 1.25f + 0.18f, 0.38f, 0.96f);
                    return Color.HSVToRGB(h, s, val);
                }

                // -------------------- HUMANS (Cohesive Alliance / Scarlet / Shadow Palettes) --------------------
                case PaletteMode.HumanCrimson:
                    if (!isWarmFleshOrFur && s > 0.20f) { h = 0.99f; s = Mathf.Max(s, 0.58f); }
                    return Color.HSVToRGB(h, s, val);

                case PaletteMode.HumanShadow:
                    if (!isWarmFleshOrFur && s > 0.18f) { h = 0.78f; s = Mathf.Max(s, 0.50f); val *= 0.90f; }
                    return Color.HSVToRGB(h, s, val);

                case PaletteMode.HumanArcane:
                    if (!isWarmFleshOrFur && s > 0.22f) { h = Mathf.Lerp(h, 0.60f, 0.55f); s = Mathf.Max(s, 0.48f); }
                    return Color.HSVToRGB(h, s, val);

                default:
                    // HumanAlliance: lift dark charcoal steel armor into gleaming Alliance silver-steel & royal blue trim
                    if (!isWarmFleshOrFur && s < 0.24f && val > 0.08f && val < 0.58f)
                    {
                        h = 0.58f;
                        s = 0.14f;
                        val = Mathf.Clamp(val * 1.38f + 0.12f, 0.28f, 0.86f);
                        return Color.HSVToRGB(h, s, val);
                    }
                    return c;
            }
        }

        // =========================================================================
        // 2. SCULPTED 3D RACIAL & CLASS FEATURE MESHES (SAVED AS PERSISTENT ASSETS)
        // =========================================================================
        static void EnsureRacialMeshes(bool force)
        {
            CreateMeshAssetIfMissing("Mesh_ElfEar_L.asset", force, () => BuildSweptEarMesh(isLeft: true, isGoblin: false));
            CreateMeshAssetIfMissing("Mesh_ElfEar_R.asset", force, () => BuildSweptEarMesh(isLeft: false, isGoblin: false));
            CreateMeshAssetIfMissing("Mesh_GoblinEar_L.asset", force, () => BuildSweptEarMesh(isLeft: true, isGoblin: true));
            CreateMeshAssetIfMissing("Mesh_GoblinEar_R.asset", force, () => BuildSweptEarMesh(isLeft: false, isGoblin: true));
            CreateMeshAssetIfMissing("Mesh_GoblinNose.asset", force, BuildGoblinNoseMesh);
            CreateMeshAssetIfMissing("Mesh_OrcTusk_L.asset", force, () => BuildCurvedTuskMesh(isLeft: true));
            CreateMeshAssetIfMissing("Mesh_OrcTusk_R.asset", force, () => BuildCurvedTuskMesh(isLeft: false));
            CreateMeshAssetIfMissing("Mesh_DwarfBeard_Full.asset", force, BuildDwarvenBraidedBeardMesh);
            CreateMeshAssetIfMissing("Mesh_DwarfPlaits_Female.asset", force, BuildDwarvenSidePlaitsMesh);
            CreateMeshAssetIfMissing("Mesh_SpikedPauldron.asset", force, () => BuildPauldronMesh(spiked: true));
            CreateMeshAssetIfMissing("Mesh_RegalPauldron.asset", force, () => BuildPauldronMesh(spiked: false));
            CreateMeshAssetIfMissing("Mesh_SunwellCirclet.asset", force, BuildSunwellCircletMesh);
            CreateMeshAssetIfMissing("Mesh_TinkerGoggles.asset", force, BuildTinkerGogglesMesh);
            CreateMeshAssetIfMissing("Mesh_ArcaneCrystal.asset", force, BuildArcaneCrystalClusterMesh);
        }

        static Mesh LoadRacialMesh(string fileName) => AssetDatabase.LoadAssetAtPath<Mesh>(RacialMeshDir + fileName);

        static void CreateMeshAssetIfMissing(string fileName, bool force, Func<Mesh> builder)
        {
            string path = RacialMeshDir + fileName;
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null && !force) return;

            var mesh = builder();
            mesh.name = Path.GetFileNameWithoutExtension(fileName);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                EditorUtility.SetDirty(existing);
                UnityEngine.Object.DestroyImmediate(mesh);
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, path);
            }
        }

        /// <summary>
        /// Builds a sculpted 5-ring tapered 3D fantasy ear (Quel'dorei upward-swept long ear or Bilgewater wide bat-wing ear).
        /// Origin (0,0,0) sits inside the temple attachment root; extends outward along -X (left) / +X (right).
        /// </summary>
        static Mesh BuildSweptEarMesh(bool isLeft, bool isGoblin)
        {
            float sign = isLeft ? -1f : 1f;
            int rings = 5;
            int segs = 6;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings;
                float radius = Mathf.Lerp(isGoblin ? 0.036f : 0.028f, 0.0025f, Mathf.Pow(t, 0.85f));
                Vector3 spine = isGoblin
                    ? new Vector3(sign * (t * 0.22f), t * 0.055f - t * t * 0.02f, -t * 0.075f)
                    : new Vector3(sign * (t * 0.095f), t * 0.21f, -t * 0.115f);

                for (int s = 0; s < segs; s++)
                {
                    float ang = (s / (float)segs) * Mathf.PI * 2f;
                    // Flatten front-to-back into an authentic ear blade cross-section
                    Vector3 offset = new Vector3(
                        0f,
                        Mathf.Sin(ang) * radius * (isGoblin ? 1.25f : 0.95f),
                        Mathf.Cos(ang) * radius * 0.48f);
                    verts.Add(spine + offset);
                    uvs.Add(new Vector2(s / (float)segs, t));
                }
            }

            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segs; s++)
                {
                    int sn = (s + 1) % segs;
                    int a = r * segs + s;
                    int b = r * segs + sn;
                    int c = (r + 1) * segs + s;
                    int d = (r + 1) * segs + sn;
                    if (isLeft)
                    {
                        tris.Add(a); tris.Add(c); tris.Add(b);
                        tris.Add(b); tris.Add(c); tris.Add(d);
                    }
                    else
                    {
                        tris.Add(a); tris.Add(b); tris.Add(c);
                        tris.Add(b); tris.Add(d); tris.Add(c);
                    }
                }
            }

            return new Mesh { vertices = verts.ToArray(), uv = uvs.ToArray(), triangles = tris.ToArray() };
        }

        static Mesh BuildGoblinNoseMesh()
        {
            var mb = new MeshBuilder();
            // Hooked pointed Goblin nose extending +Z forward and slightly downward at tip
            mb.AddTaperedTube(new[]
            {
                (new Vector3(0f, 0.015f, -0.015f), new Vector2(0.024f, 0.026f)),
                (new Vector3(0f, 0.004f,  0.035f), new Vector2(0.016f, 0.018f)),
                (new Vector3(0f, -0.012f, 0.078f), new Vector2(0.004f, 0.005f)),
            }, 6);
            return mb.ToMesh();
        }

        static Mesh BuildCurvedTuskMesh(bool isLeft)
        {
            float sx = isLeft ? -1f : 1f;
            var mb = new MeshBuilder();
            mb.AddTaperedTube(new[]
            {
                (new Vector3(0f, 0f, -0.015f),            new Vector2(0.017f, 0.017f)),
                (new Vector3(sx * 0.008f, 0.022f, 0.028f), new Vector2(0.014f, 0.014f)),
                (new Vector3(sx * 0.014f, 0.062f, 0.046f), new Vector2(0.009f, 0.009f)),
                (new Vector3(sx * 0.010f, 0.102f, 0.038f), new Vector2(0.002f, 0.002f)),
            }, 6);
            return mb.ToMesh();
        }

        static Mesh BuildDwarvenBraidedBeardMesh()
        {
            var mb = new MeshBuilder();

            // 1. Thick sweeping Dwarven Mustache across upper lip (+Z front)
            for (int side = -1; side <= 1; side += 2)
            {
                mb.AddTaperedTube(new[]
                {
                    (new Vector3(0f,           0.032f, 0.022f), new Vector2(0.028f, 0.024f)),
                    (new Vector3(side * 0.055f, 0.022f, 0.016f), new Vector2(0.026f, 0.022f)),
                    (new Vector3(side * 0.105f, -0.012f, -0.005f), new Vector2(0.012f, 0.012f)),
                }, 6);
            }

            // 2. Central Full Chin Beard Mass
            mb.AddTaperedTube(new[]
            {
                (new Vector3(0f,  0.010f, -0.010f), new Vector2(0.088f, 0.055f)),
                (new Vector3(0f, -0.075f,  0.015f), new Vector2(0.095f, 0.062f)),
                (new Vector3(0f, -0.175f,  0.022f), new Vector2(0.072f, 0.048f)),
                (new Vector3(0f, -0.265f,  0.018f), new Vector2(0.032f, 0.026f)),
            }, 8);

            // 3. Twin Forked Clan Braids flanking the central beard
            for (int side = -1; side <= 1; side += 2)
            {
                mb.AddTaperedTube(new[]
                {
                    (new Vector3(side * 0.052f, -0.040f, 0.005f), new Vector2(0.038f, 0.035f)),
                    (new Vector3(side * 0.066f, -0.145f, 0.020f), new Vector2(0.034f, 0.032f)),
                    (new Vector3(side * 0.058f, -0.255f, 0.024f), new Vector2(0.025f, 0.024f)),
                    (new Vector3(side * 0.048f, -0.335f, 0.020f), new Vector2(0.010f, 0.010f)),
                }, 6);
            }

            return mb.ToMesh();
        }

        static Mesh BuildDwarvenSidePlaitsMesh()
        {
            var mb = new MeshBuilder();
            for (int side = -1; side <= 1; side += 2)
            {
                mb.AddTaperedTube(new[]
                {
                    (new Vector3(side * 0.085f,  0.03f, -0.01f), new Vector2(0.028f, 0.028f)),
                    (new Vector3(side * 0.095f, -0.08f,  0.02f), new Vector2(0.025f, 0.025f)),
                    (new Vector3(side * 0.088f, -0.20f,  0.03f), new Vector2(0.018f, 0.018f)),
                    (new Vector3(side * 0.080f, -0.28f,  0.025f), new Vector2(0.008f, 0.008f)),
                }, 6);
            }
            return mb.ToMesh();
        }

        static Mesh BuildPauldronMesh(bool spiked)
        {
            var mb = new MeshBuilder();
            // Curved armored shoulder dome centered over shoulder joint (local +X = outward)
            mb.AddDome(Vector3.zero, new Vector3(0.16f, 0.12f, 0.16f), 8, 4);
            // Lower flared trim plate
            mb.AddDome(new Vector3(0.02f, -0.035f, 0f), new Vector3(0.175f, 0.075f, 0.175f), 8, 3);

            if (spiked)
            {
                // 3 iconic Warcraft Horde war-spikes
                mb.AddCone(new Vector3(0.03f, 0.10f, 0f),     new Vector3(0.45f, 1f, 0f).normalized,  0.026f, 0.125f, 6);
                mb.AddCone(new Vector3(0.05f, 0.06f, 0.065f), new Vector3(0.55f, 0.8f, 0.35f).normalized, 0.020f, 0.095f, 6);
                mb.AddCone(new Vector3(0.05f, 0.06f, -0.065f),new Vector3(0.55f, 0.8f, -0.35f).normalized, 0.020f, 0.095f, 6);
            }
            return mb.ToMesh();
        }

        static Mesh BuildSunwellCircletMesh()
        {
            var mb = new MeshBuilder();
            int steps = 10;
            var pts = new (Vector3 p, Vector2 r)[steps + 1];
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.Lerp(-Mathf.PI * 0.62f, Mathf.PI * 0.62f, i / (float)steps);
                pts[i] = (new Vector3(Mathf.Sin(a) * 0.105f, Mathf.Cos(a) * 0.012f, Mathf.Cos(a) * 0.108f), new Vector2(0.012f, 0.016f));
            }
            mb.AddTaperedTube(pts, 5);
            // Central rising phoenix/arcane crest fin
            mb.AddCone(new Vector3(0f, 0.018f, 0.112f), new Vector3(0f, 0.95f, 0.25f).normalized, 0.022f, 0.085f, 4);
            return mb.ToMesh();
        }

        static Mesh BuildTinkerGogglesMesh()
        {
            var mb = new MeshBuilder();
            // Brow strap + two thick brass goggle barrels facing +Z
            for (int side = -1; side <= 1; side += 2)
            {
                mb.AddTaperedTube(new[]
                {
                    (new Vector3(side * 0.040f, 0f, -0.010f), new Vector2(0.032f, 0.032f)),
                    (new Vector3(side * 0.040f, 0f,  0.032f), new Vector2(0.030f, 0.030f)),
                }, 8);
            }
            // Bridge bar
            mb.AddTaperedTube(new[]
            {
                (new Vector3(-0.085f, 0f, -0.005f), new Vector2(0.010f, 0.012f)),
                (new Vector3( 0.085f, 0f, -0.005f), new Vector2(0.010f, 0.012f)),
            }, 4);
            return mb.ToMesh();
        }

        // =========================================================================
        // 3. SHARED STYLIZED URP MATERIALS FOR RACIAL FEATURES & CREATURES
        // =========================================================================
        public static Material GetOrCreateSurfaceMaterial(string matName, string stylizedTexName, Color baseColor, float smoothness = 0.15f, float metallic = 0.0f, bool unlit = false)
        {
            Directory.CreateDirectory(StylizedMatDir);
            string path = StylizedMatDir + matName + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = unlit
                ? (Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit"))
                : Shader.Find("Universal Render Pipeline/Lit");

            if (!mat)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader && shader != null)
            {
                mat.shader = shader;
            }

            if (!string.IsNullOrEmpty(stylizedTexName))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(StylizedTexDir + stylizedTexName);
                if (tex)
                {
                    mat.SetTexture("_BaseMap", tex);
                    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                }
            }

            mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
            if (!unlit)
            {
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
                if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", null);
                mat.DisableKeyword("_NORMALMAP");
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        // =========================================================================
        // 4. UNIFIED MODEL BUILDER + T-POSE VERTEX-ACCURATE RACIAL RIGGING
        // =========================================================================
        public static GameObject BuildStyledRacialVisual(
            ActorRacialSpec spec,
            RuntimeAnimatorController controller,
            Transform parent,
            string visualObjectName = "RacialModelVisual")
        {
            EnsureUnifiedAssetsReady(false);

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(spec.fbxPath);
            if (!source)
            {
                Debug.LogWarning($"[StylizedRacialModelSystem] Missing FBX '{spec.fbxPath}' for {spec.race} {spec.archetype}");
                return null;
            }

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(source);
            visual.name = visualObjectName;
            // Unpack so we can attach racial 3D bone accessories cleanly without nested-prefab restrictions
            if (PrefabUtility.IsAnyPrefabInstanceRoot(visual))
                PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            // Skeleton FBXs face -Z; normalize before measuring forward-facing anatomy.
            bool isSkeleton = spec.fbxPath.StartsWith(SkeletonsDir, StringComparison.OrdinalIgnoreCase);
            var facing = isSkeleton ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
            visual.transform.SetParent(null, false);
            visual.transform.SetPositionAndRotation(Vector3.zero, facing);
            visual.transform.localScale = spec.bodyScale;

            // 1. Apply Unified Stylized Hand-Painted Materials
            if (isSkeleton)
                ApplyUnifiedSkeletonMaterials(visual, spec.palette);
            else
                ApplyUnifiedHumanoidMaterials(visual, Path.GetFileNameWithoutExtension(spec.fbxPath), spec.palette);

            // 2. Pre-scale to approximate target height in T-Pose so world-space racial attachments match final proportions 1:1
            var anim = visual.GetComponentInChildren<Animator>();
            if (!anim) anim = visual.AddComponent<Animator>();
            anim.runtimeAnimatorController = null;

            var tPoseBounds = ComputeWorldBounds(visual);
            if (tPoseBounds.size.y > 0.05f)
            {
                visual.transform.localScale *= (spec.targetHeight / tPoseBounds.size.y);
            }

            // 3. Rig 3D Racial Anatomy (Ears, Tusks, Beards, Glowing Eyes) & Class Gear/Weapons in exact T-Pose!
            AttachRacialAndClassAnatomyInTPose(visual, anim, spec);

            // 4. Bind rig-matched AnimatorController, sample Idle pose, refine exact standing height & ground boots flush to y=0
            if (controller) anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.Rebind();
            if (controller && anim.HasState(0, Animator.StringToHash("Idle")))
                anim.Play("Idle", 0, 0f);
            anim.Update(0f);

            var idleBounds = ComputeWorldBounds(visual);
            if (idleBounds.size.y > 0.05f)
            {
                // Measure body height excluding tall vertical staff/crest tips so staffs don't shrink the character!
                var bodyOnlyBounds = ComputeCharacterBodyBounds(visual);
                float measuredH = bodyOnlyBounds.size.y > 0.25f ? bodyOnlyBounds.size.y : idleBounds.size.y;
                float refine = Mathf.Clamp(spec.targetHeight / measuredH, 0.72f, 1.38f);
                visual.transform.localScale *= refine;

                anim.Rebind();
                if (controller && anim.HasState(0, Animator.StringToHash("Idle")))
                    anim.Play("Idle", 0, 0f);
                anim.Update(0f);

                bodyOnlyBounds = ComputeCharacterBodyBounds(visual);
                float minFootY = bodyOnlyBounds.size.y > 0.25f ? bodyOnlyBounds.min.y : ComputeWorldBounds(visual).min.y;
                visual.transform.position = new Vector3(0f, -minFootY, 0f);
                anim.Update(0f);
            }

            foreach (var smr in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                smr.updateWhenOffscreen = true;
            foreach (var col in visual.GetComponentsInChildren<Collider>(true))
                col.enabled = false;

            if (parent != null)
            {
                Vector3 localPos = visual.transform.position;
                Vector3 localScale = visual.transform.localScale;
                visual.transform.SetParent(parent, false);
                visual.transform.localPosition = localPos;
                visual.transform.localRotation = facing;
                visual.transform.localScale = localScale;
            }

            return visual;
        }

        static void ApplyUnifiedHumanoidMaterials(GameObject instance, string modelKey, PaletteMode palette)
        {
            string texDir = $"Assets/RPG/Art/NPCs_WoW/Textures/{modelKey}";
            if (!Directory.Exists(texDir) && modelKey == "Brute") texDir = "Assets/RPG/Art/Brute/Textures";

            var allTexPaths = Directory.Exists(texDir)
                ? Directory.GetFiles(texDir).Where(f => !f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)).Select(f => f.Replace('\\', '/')).ToArray()
                : Array.Empty<string>();

            var diffusePaths = allTexPaths.Where(p =>
            {
                string n = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
                return !n.Contains("normal") && !n.Contains("_nrm") && !n.Contains("_nm") &&
                       !n.Contains("spec") && !n.Contains("gloss") && !n.Contains("rough") &&
                       !n.Contains("metal") && !n.Contains("stdnm");
            }).ToArray();

            string primaryDiffusePath = diffusePaths
                .OrderByDescending(p => p.IndexOf("diffuse", StringComparison.OrdinalIgnoreCase) >= 0 || p.IndexOf("dif", StringComparison.OrdinalIgnoreCase) >= 0)
                .FirstOrDefault();

            foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
            {
                var origMats = r.sharedMaterials;
                var newMats = new Material[origMats.Length];
                for (int i = 0; i < origMats.Length; i++)
                {
                    var orig = origMats[i];
                    string slotName = orig ? orig.name : $"Slot{i}";
                    string lower = slotName.ToLowerInvariant();
                    bool alphaClip = lower.Contains("lash") || lower.Contains("hair") || lower.Contains("mous");

                    string chosenTexPath = ResolveDiffusePathForSlot(lower, diffusePaths, primaryDiffusePath);
                    Texture2D stylizedTex = GetOrBakeStylizedDiffuse(chosenTexPath, palette, alphaClip);

                    string safeSlot = new string(slotName.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
                    string matPath = $"{StylizedMatDir}Mat_Stylized_{modelKey}_{safeSlot}_{palette}.mat";

                    var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (!mat)
                    {
                        mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        AssetDatabase.CreateAsset(mat, matPath);
                    }

                    if (stylizedTex)
                    {
                        mat.SetTexture("_BaseMap", stylizedTex);
                        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", stylizedTex);
                    }

                    mat.SetColor("_BaseColor", Color.white);
                    mat.SetFloat("_Metallic", (lower.Contains("armor") || lower.Contains("knight") || lower.Contains("paladin") || lower.Contains("guard")) ? 0.18f : 0.0f);
                    mat.SetFloat("_Smoothness", lower.Contains("eyespec") ? 0.30f : 0.15f);
                    mat.SetTexture("_BumpMap", null);
                    mat.DisableKeyword("_NORMALMAP");

                    if (alphaClip)
                    {
                        mat.SetFloat("_AlphaClip", 1f);
                        mat.SetFloat("_Cutoff", 0.35f);
                        mat.EnableKeyword("_ALPHATEST_ON");
                        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                    }
                    else
                    {
                        mat.SetFloat("_AlphaClip", 0f);
                        mat.DisableKeyword("_ALPHATEST_ON");
                        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                    }

                    EditorUtility.SetDirty(mat);
                    newMats[i] = mat;
                }
                r.sharedMaterials = newMats;
            }
        }

        static string ResolveDiffusePathForSlot(string lowerSlot, string[] candidates, string fallback)
        {
            if (candidates.Length > 1)
            {
                if (lowerSlot.Contains("lash") || lowerSlot.Contains("hair"))
                {
                    var m = candidates.FirstOrDefault(p => p.IndexOf("lash", StringComparison.OrdinalIgnoreCase) >= 0 || p.IndexOf("hair", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (m != null) return m;
                }
                if (lowerSlot.Contains("body") || lowerSlot.Contains("eyespec") || lowerSlot.Contains("skin"))
                {
                    var m = candidates.FirstOrDefault(p => p.IndexOf("body", StringComparison.OrdinalIgnoreCase) >= 0 || p.IndexOf("skin", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (m != null) return m;
                }
                if (lowerSlot.Contains("akai") || lowerSlot.Contains("cloth") || lowerSlot.Contains("armor") || lowerSlot.Contains("bottom"))
                {
                    var m = candidates.FirstOrDefault(p => p.IndexOf("clothes", StringComparison.OrdinalIgnoreCase) >= 0 || p.IndexOf("armor", StringComparison.OrdinalIgnoreCase) >= 0 || p.IndexOf("bottom", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (m != null) return m;
                }
                if (lowerSlot.Contains("axe"))
                {
                    var m = candidates.FirstOrDefault(p => p.IndexOf("axe", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (m != null) return m;
                }
            }
            return fallback;
        }

        static void ApplyUnifiedSkeletonMaterials(GameObject visual, PaletteMode palette)
        {
            Texture2D stylizedSkel = GetOrBakeStylizedDiffuse(SkeletonsDir + "skeleton_texture.png", palette, false);
            var bodyMat = GetOrCreateSurfaceMaterial($"Mat_Stylized_Skeleton_{palette}", null, Color.white, 0.16f, 0.08f, false);
            if (stylizedSkel)
            {
                bodyMat.SetTexture("_BaseMap", stylizedSkel);
                if (bodyMat.HasProperty("_MainTex")) bodyMat.SetTexture("_MainTex", stylizedSkel);
                EditorUtility.SetDirty(bodyMat);
            }

            Color eyeColor = palette == PaletteMode.UndeadPaladin ? new Color(1.0f, 0.82f, 0.28f)
                           : palette == PaletteMode.UndeadMage    ? new Color(0.75f, 0.35f, 1.0f)
                           : new Color(0.28f, 1.0f, 0.48f);
            var glowMat = GetOrCreateSurfaceMaterial($"Mat_Stylized_UndeadGlow_{palette}", null, eyeColor, 0.1f, 0f, unlit: true);

            foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
            {
                var origMats = r.sharedMaterials;
                var newMats = new Material[origMats.Length];
                for (int i = 0; i < origMats.Length; i++)
                {
                    string slot = origMats[i] ? origMats[i].name.ToLowerInvariant() : "";
                    newMats[i] = slot.Contains("glow") ? glowMat : bodyMat;
                }
                r.sharedMaterials = newMats;
            }
        }

        // =========================================================================
        // 5. EXACT T-POSE VERTEX-MEASURED RACIAL ANATOMY & CLASS GEAR ATTACHMENT
        // =========================================================================
        struct HeadMetrics
        {
            public Transform bone;
            public float minY;
            public float maxY;
            public float halfWidth;
            public float frontZ;
            public float centerZ;
            public float height => Mathf.Max(0.14f, maxY - minY);
        }

        static bool TryMeasureHeadInTPose(GameObject visual, Animator anim, out HeadMetrics hm)
        {
            hm = default;
            Transform head = (anim && anim.isHuman) ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
            if (!head)
            {
                head = visual.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => string.Equals(t.name, "head", StringComparison.OrdinalIgnoreCase) ||
                                         t.name.EndsWith(":Head", StringComparison.OrdinalIgnoreCase) ||
                                         t.name.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0 && !t.name.Contains("Top") && !t.name.Contains("End"));
            }
            if (!head) return false;

            hm.bone = head;
            float baseMinY = head.position.y - 0.02f;
            float maxY = head.position.y + 0.24f;
            float maxHalfW = 0.085f;
            float maxFrontZ = head.position.z + 0.10f;
            float minBackZ = head.position.z - 0.10f;
            bool foundVerts = false;

            var tempMesh = new Mesh();
            foreach (var smr in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!smr.sharedMesh) continue;
                smr.BakeMesh(tempMesh, true);
                var verts = tempMesh.vertices;
                Transform tr = smr.transform;
                for (int i = 0; i < verts.Length; i++)
                {
                    Vector3 wp = tr.position + tr.rotation * verts[i];
                    if (wp.y < baseMinY || Mathf.Abs(wp.x - head.position.x) > 0.28f) continue;
                    if (!foundVerts)
                    {
                        maxY = wp.y; maxHalfW = Mathf.Abs(wp.x - head.position.x); maxFrontZ = wp.z; minBackZ = wp.z;
                        foundVerts = true;
                    }
                    else
                    {
                        if (wp.y > maxY) maxY = wp.y;
                        if (wp.y < baseMinY + 0.24f)
                        {
                            float ax = Mathf.Abs(wp.x - head.position.x);
                            if (ax < 0.19f && ax > maxHalfW) maxHalfW = ax;
                            if (wp.z < head.position.z + 0.22f && wp.z > maxFrontZ) maxFrontZ = wp.z;
                            if (wp.z > head.position.z - 0.22f && wp.z < minBackZ) minBackZ = wp.z;
                        }
                    }
                }
            }
            UnityEngine.Object.DestroyImmediate(tempMesh);

            hm.minY = head.position.y;
            hm.maxY = Mathf.Clamp(maxY, head.position.y + 0.16f, head.position.y + 0.34f);
            hm.halfWidth = Mathf.Clamp(maxHalfW, 0.075f, 0.135f);
            hm.frontZ = Mathf.Clamp(maxFrontZ, head.position.z + 0.075f, head.position.z + 0.155f);
            hm.centerZ = (hm.frontZ + minBackZ) * 0.5f;
            return true;
        }

        static void AttachRacialAndClassAnatomyInTPose(GameObject visual, Animator anim, ActorRacialSpec spec)
        {
            if (TryMeasureHeadInTPose(visual, anim, out var hm))
            {
                float eyeY = Mathf.Lerp(hm.minY, hm.maxY, 0.48f);
                float earY = Mathf.Lerp(hm.minY, hm.maxY, 0.46f);
                float jawY = Mathf.Lerp(hm.minY, hm.maxY, 0.16f);

                // -------------------- DWARF: Braided Beard / Plaits + Brow --------------------
                if (spec.race == "Dwarf")
                {
                    Color beardCol = spec.palette == PaletteMode.DwarfGold  ? new Color(0.92f, 0.90f, 0.86f) // Venerable Silver-White Paladin beard
                                   : spec.palette == PaletteMode.DwarfFrost ? new Color(0.68f, 0.74f, 0.82f) // Frost-Grey Mage beard
                                   : spec.archetype == "Rogue" || spec.archetype == "Smith" ? new Color(0.32f, 0.20f, 0.12f) // Dark Iron-Brown
                                   : new Color(0.76f, 0.30f, 0.12f); // Fiery Khaz Modan Red-Auburn!

                    var beardMat = GetOrCreateSurfaceMaterial($"Mat_Racial_DwarfHair_{spec.palette}_{spec.archetype}", "Tex_Stylized_HairBeard.png", beardCol, 0.12f, 0f);
                    if (spec.isFemale)
                    {
                        SpawnMeshAttachment("Dwarf_BraidedPlaits", LoadRacialMesh("Mesh_DwarfPlaits_Female.asset"), beardMat,
                            new Vector3(0f, earY, hm.centerZ), Quaternion.identity, Vector3.one, hm.bone);
                    }
                    else
                    {
                        SpawnMeshAttachment("Dwarf_BraidedBeard", LoadRacialMesh("Mesh_DwarfBeard_Full.asset"), beardMat,
                            new Vector3(0f, jawY + 0.01f, hm.frontZ - 0.015f), Quaternion.identity, Vector3.one * 1.05f, hm.bone);
                    }
                }

                // -------------------- HIGH ELF: Swept Pointed Ears + Glowing Sunwell/Arcane Eyes + Circlet --------------------
                else if (spec.race == "Elf")
                {
                    var earMat = GetOrCreateSurfaceMaterial("Mat_Racial_ElfSkin", "Tex_Stylized_SkinGrain.png", new Color(0.98f, 0.84f, 0.76f), 0.14f, 0f);
                    SpawnMeshAttachment("Elf_Ear_L", LoadRacialMesh("Mesh_ElfEar_L.asset"), earMat,
                        new Vector3(-hm.halfWidth * 0.88f, earY, hm.centerZ), Quaternion.identity, Vector3.one, hm.bone);
                    SpawnMeshAttachment("Elf_Ear_R", LoadRacialMesh("Mesh_ElfEar_R.asset"), earMat,
                        new Vector3( hm.halfWidth * 0.88f, earY, hm.centerZ), Quaternion.identity, Vector3.one, hm.bone);

                    Color eyeCol = spec.palette == PaletteMode.ElfSunwell ? new Color(0.35f, 0.96f, 0.65f) : new Color(0.32f, 0.88f, 1.0f);
                    AttachGlowingEyes(hm, eyeY, eyeCol, "ElfArcaneEyes");

                    if (spec.attachHeadgear)
                    {
                        var goldMat = GetOrCreateSurfaceMaterial("Mat_Racial_SunwellGold", "Tex_Stylized_ForgedMetal.png", new Color(0.96f, 0.78f, 0.28f), 0.45f, 0.35f);
                        SpawnMeshAttachment("Elf_SunwellCirclet", LoadRacialMesh("Mesh_SunwellCirclet.asset"), goldMat,
                            new Vector3(0f, Mathf.Lerp(hm.minY, hm.maxY, 0.68f), hm.centerZ + 0.01f), Quaternion.identity, Vector3.one, hm.bone);
                    }
                }

                // -------------------- ORC: Curved Ivory Lower-Jaw Tusks + Fierce Glowing Amber/Crimson Eyes --------------------
                else if (spec.race == "Orc")
                {
                    var tuskMat = GetOrCreateSurfaceMaterial("Mat_Racial_OrcTusk", "Tex_Stylized_BoneHorn.png", new Color(0.95f, 0.90f, 0.76f), 0.20f, 0f);
                    float tuskScale = spec.isFemale ? 0.72f : (spec.targetHeight > 2.14f ? 1.22f : 1.0f);
                    SpawnMeshAttachment("Orc_Tusk_L", LoadRacialMesh("Mesh_OrcTusk_L.asset"), tuskMat,
                        new Vector3(-0.036f * tuskScale, jawY + 0.005f, hm.frontZ - 0.018f), Quaternion.identity, Vector3.one * tuskScale, hm.bone);
                    SpawnMeshAttachment("Orc_Tusk_R", LoadRacialMesh("Mesh_OrcTusk_R.asset"), tuskMat,
                        new Vector3( 0.036f * tuskScale, jawY + 0.005f, hm.frontZ - 0.018f), Quaternion.identity, Vector3.one * tuskScale, hm.bone);

                    Color orcEye = spec.palette == PaletteMode.OrcShadowmoon ? new Color(0.45f, 0.85f, 1.0f) : new Color(1.0f, 0.42f, 0.12f);
                    AttachGlowingEyes(hm, eyeY, orcEye, "OrcBattleEyes");
                }

                // -------------------- GOBLIN: Wide Swept Bat-Ears + Hooked Nose + Tinker Goggles --------------------
                else if (spec.race == "Goblin")
                {
                    Color gobSkin = spec.palette == PaletteMode.GoblinScavenger ? new Color(0.46f, 0.68f, 0.26f) : new Color(0.38f, 0.76f, 0.28f);
                    var gobMat = GetOrCreateSurfaceMaterial($"Mat_Racial_GoblinSkin_{spec.palette}", "Tex_Stylized_SkinGrain.png", gobSkin, 0.15f, 0f);

                    SpawnMeshAttachment("Goblin_Ear_L", LoadRacialMesh("Mesh_GoblinEar_L.asset"), gobMat,
                        new Vector3(-hm.halfWidth * 0.84f, earY, hm.centerZ), Quaternion.identity, Vector3.one, hm.bone);
                    SpawnMeshAttachment("Goblin_Ear_R", LoadRacialMesh("Mesh_GoblinEar_R.asset"), gobMat,
                        new Vector3( hm.halfWidth * 0.84f, earY, hm.centerZ), Quaternion.identity, Vector3.one, hm.bone);
                    SpawnMeshAttachment("Goblin_Nose", LoadRacialMesh("Mesh_GoblinNose.asset"), gobMat,
                        new Vector3(0f, eyeY - 0.018f, hm.frontZ - 0.01f), Quaternion.identity, Vector3.one, hm.bone);

                    if (spec.attachHeadgear)
                    {
                        var brassMat = GetOrCreateSurfaceMaterial("Mat_Racial_GoblinBrass", "Tex_Stylized_ForgedMetal.png", new Color(0.88f, 0.66f, 0.22f), 0.45f, 0.35f);
                        SpawnMeshAttachment("Goblin_TinkerGoggles", LoadRacialMesh("Mesh_TinkerGoggles.asset"), brassMat,
                            new Vector3(0f, Mathf.Lerp(hm.minY, hm.maxY, 0.66f), hm.frontZ - 0.01f), Quaternion.Euler(-12f, 0f, 0f), Vector3.one, hm.bone);
                    }
                }

                // -------------------- TROGG & MANA SPRITE HEAD FEATURES --------------------
                else if (spec.race == "Trogg")
                {
                    var hornMat = GetOrCreateSurfaceMaterial("Mat_Creature_TroggHorn", "Tex_Stylized_BoneHorn.png", new Color(0.62f, 0.66f, 0.72f), 0.18f, 0f);
                    SpawnMeshAttachment("Trogg_Horn_L", LoadRacialMesh("Mesh_OrcTusk_L.asset"), hornMat,
                        new Vector3(-0.05f, Mathf.Lerp(hm.minY, hm.maxY, 0.68f), hm.frontZ - 0.02f), Quaternion.Euler(-25f, 0f, 18f), Vector3.one * 1.15f, hm.bone);
                    SpawnMeshAttachment("Trogg_Horn_R", LoadRacialMesh("Mesh_OrcTusk_R.asset"), hornMat,
                        new Vector3( 0.05f, Mathf.Lerp(hm.minY, hm.maxY, 0.68f), hm.frontZ - 0.02f), Quaternion.Euler(-25f, 0f, -18f), Vector3.one * 1.15f, hm.bone);
                }
                else if (spec.race == "ManaSprite")
                {
                    AttachGlowingEyes(hm, eyeY, new Color(0.45f, 0.95f, 1.0f), "ManaSpriteEyes");
                    var crysMat = GetOrCreateSurfaceMaterial("Mat_Creature_ManaCrystal", "Tex_Stylized_ArcaneCrystal.png", new Color(0.55f, 0.92f, 1.0f), 0.35f, 0f, unlit: true);
                    SpawnMeshAttachment("Mana_Crest", LoadRacialMesh("Mesh_SunwellCirclet.asset"), crysMat,
                        new Vector3(0f, hm.maxY + 0.04f, hm.centerZ), Quaternion.identity, Vector3.one * 0.9f, hm.bone);
                }
            }

            // -------------------- SHOULDER PAULDRONS (ORC, DWARF, PALADIN/WARRIOR, UNDEAD, NOBLES) --------------------
            if (spec.attachPauldrons)
            {
                AttachShoulderPauldrons(visual, anim, spec);
            }

            // -------------------- HAND WEAPONS (AXES, SWORDS, STAVES, DUAL DAGGERS) --------------------
            if (!string.IsNullOrEmpty(spec.weaponRightFbx))
                AttachWeaponToHand(visual, anim, isRightHand: true, spec.weaponRightFbx, spec.palette);
            if (!string.IsNullOrEmpty(spec.weaponLeftFbx))
                AttachWeaponToHand(visual, anim, isRightHand: false, spec.weaponLeftFbx, spec.palette);
        }

        static void AttachGlowingEyes(HeadMetrics hm, float eyeY, Color glowColor, string matKey)
        {
            var eyeMat = GetOrCreateSurfaceMaterial($"Mat_Glow_{matKey}", null, glowColor, 0.1f, 0f, unlit: true);
            for (int side = -1; side <= 1; side += 2)
            {
                var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eye.name = side < 0 ? "Racial_EyeGlow_L" : "Racial_EyeGlow_R";
                UnityEngine.Object.DestroyImmediate(eye.GetComponent<Collider>());
                eye.GetComponent<Renderer>().sharedMaterial = eyeMat;
                eye.transform.position = new Vector3(side * 0.032f, eyeY, hm.frontZ - 0.012f);
                eye.transform.localScale = new Vector3(0.018f, 0.012f, 0.014f);
                eye.transform.SetParent(hm.bone, true);
            }
        }

        static void AttachShoulderPauldrons(GameObject visual, Animator anim, ActorRacialSpec spec)
        {
            Transform leftArm = (anim && anim.isHuman) ? (anim.GetBoneTransform(HumanBodyBones.LeftUpperArm) ?? anim.GetBoneTransform(HumanBodyBones.LeftShoulder)) : null;
            Transform rightArm = (anim && anim.isHuman) ? (anim.GetBoneTransform(HumanBodyBones.RightUpperArm) ?? anim.GetBoneTransform(HumanBodyBones.RightShoulder)) : null;

            if (!leftArm || !rightArm)
            {
                var all = visual.GetComponentsInChildren<Transform>(true);
                if (!leftArm) leftArm = all.FirstOrDefault(t => t.name.IndexOf("upperarm.l", StringComparison.OrdinalIgnoreCase) >= 0 || t.name.IndexOf("LeftArm", StringComparison.OrdinalIgnoreCase) >= 0);
                if (!rightArm) rightArm = all.FirstOrDefault(t => t.name.IndexOf("upperarm.r", StringComparison.OrdinalIgnoreCase) >= 0 || t.name.IndexOf("RightArm", StringComparison.OrdinalIgnoreCase) >= 0);
            }
            if (!leftArm || !rightArm) return;

            bool spiked = spec.race == "Orc" || spec.race == "Undead" || spec.race == "Trogg" || spec.palette == PaletteMode.GoblinBruiser;
            Mesh mesh = LoadRacialMesh(spiked ? "Mesh_SpikedPauldron.asset" : "Mesh_RegalPauldron.asset");

            Color metalCol = spec.palette == PaletteMode.OrcWarsong      ? new Color(0.68f, 0.20f, 0.18f)
                           : spec.palette == PaletteMode.OrcKorkron || spec.palette == PaletteMode.ElfSunwell || spec.palette == PaletteMode.DwarfGold || spec.palette == PaletteMode.GoblinCartelGold
                                                                         ? new Color(0.92f, 0.74f, 0.28f)
                           : spec.palette == PaletteMode.ElfSilvermoon   ? new Color(0.82f, 0.88f, 0.96f)
                           : spec.palette == PaletteMode.UndeadPaladin   ? new Color(0.58f, 0.46f, 0.78f)
                           : spec.race == "Dwarf"                        ? new Color(0.64f, 0.50f, 0.36f)
                           : new Color(0.46f, 0.48f, 0.52f);

            var pauldronMat = GetOrCreateSurfaceMaterial($"Mat_Pauldron_{spec.race}_{spec.palette}", "Tex_Stylized_ForgedMetal.png", metalCol, 0.42f, 0.30f);
            float scale = spec.race == "Orc" ? 1.18f : spec.race == "Dwarf" ? 1.05f : spec.race == "Goblin" ? 0.72f : 0.95f;

            SpawnMeshAttachment("Pauldron_R", mesh, pauldronMat,
                rightArm.position + new Vector3(0.03f, 0.045f, 0f), Quaternion.identity, Vector3.one * scale, rightArm);
            SpawnMeshAttachment("Pauldron_L", mesh, pauldronMat,
                leftArm.position + new Vector3(-0.03f, 0.045f, 0f), Quaternion.Euler(0f, 180f, 0f), Vector3.one * scale, leftArm);
        }

        static void AttachWeaponToHand(GameObject visual, Animator anim, bool isRightHand, string weaponFbxPath, PaletteMode palette)
        {
            string slotName = isRightHand ? "handslot.r" : "handslot.l";
            Transform hand = visual.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => string.Equals(t.name, slotName, StringComparison.OrdinalIgnoreCase));
            if (!hand && anim && anim.isHuman)
                hand = anim.GetBoneTransform(isRightHand ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            if (!hand) return;

            // Avoid duplicate weapon if Skeleton already attached one
            string wpnKey = Path.GetFileNameWithoutExtension(weaponFbxPath);
            if (hand.Find(wpnKey) != null) return;

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(weaponFbxPath);
            if (!asset) return;

            var wpn = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            wpn.name = wpnKey;
            if (PrefabUtility.IsAnyPrefabInstanceRoot(wpn))
                PrefabUtility.UnpackPrefabInstance(wpn, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            var wpnAnim = wpn.GetComponent<Animator>();
            if (wpnAnim) UnityEngine.Object.DestroyImmediate(wpnAnim);
            foreach (var col in wpn.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(col);

            // Apply unified stylized weapon material
            var skelTex = GetOrBakeStylizedDiffuse(SkeletonsDir + "skeleton_texture.png", PaletteMode.UndeadWarrior, false);
            var metalMat = GetOrCreateSurfaceMaterial("Mat_Stylized_WeaponSteel", "Tex_Stylized_ForgedMetal.png", new Color(0.84f, 0.86f, 0.90f), 0.45f, 0.35f);
            var skelWpnMat = GetOrCreateSurfaceMaterial("Mat_Stylized_SkeletonWeapon", null, Color.white, 0.25f, 0.15f);
            if (skelTex) { skelWpnMat.SetTexture("_BaseMap", skelTex); EditorUtility.SetDirty(skelWpnMat); }

            bool usesSkelAtlas = weaponFbxPath.StartsWith(SkeletonsDir, StringComparison.OrdinalIgnoreCase);
            foreach (var r in wpn.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterials = Enumerable.Repeat(usesSkelAtlas ? skelWpnMat : metalMat, r.sharedMaterials.Length).ToArray();

            // Size cleanly in world space before parenting to hand bone so FBX 0.01 scale differences never blow up or shrink weapons!
            wpn.transform.SetParent(null, false);
            wpn.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            wpn.transform.localScale = Vector3.one;
            var wb = ComputeWorldBounds(wpn);
            float desiredLength = wpnKey.Contains("Staff") ? 1.55f : wpnKey.Contains("Blade") ? 0.68f : 1.05f;
            float maxDim = Mathf.Max(wb.size.x, Mathf.Max(wb.size.y, wb.size.z));
            if (maxDim > 0.01f)
                wpn.transform.localScale = Vector3.one * (desiredLength / maxDim);

            bool isHumanoidHand = hand.name != slotName;
            wpn.transform.position = hand.position;
            wpn.transform.rotation = isHumanoidHand
                ? hand.rotation * Quaternion.Euler(90f, 0f, 0f)
                : hand.rotation;
            wpn.transform.SetParent(hand, true);
        }

        static GameObject SpawnMeshAttachment(string name, Mesh mesh, Material mat, Vector3 worldPos, Quaternion worldRot, Vector3 worldScale, Transform parentBone)
        {
            if (!mesh || !parentBone) return null;
            var go = new GameObject(name);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            go.transform.SetPositionAndRotation(worldPos, worldRot);
            go.transform.localScale = worldScale;
            go.transform.SetParent(parentBone, true);
            return go;
        }

        static Bounds ComputeWorldBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        static Bounds ComputeCharacterBodyBounds(GameObject go)
        {
            var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(false);
            if (smrs.Length == 0) return ComputeWorldBounds(go);
            var b = smrs[0].bounds;
            for (int i = 1; i < smrs.Length; i++) b.Encapsulate(smrs[i].bounds);
            return b;
        }

        // =========================================================================
        // 6. PLAYABLE 24 RACE x CLASS SPECS
        // =========================================================================
        public static ActorRacialSpec GetPlayableSpec(string race, string characterClass)
        {
            var profile = CharacterVisualFactory.GetProfile(race);
            var spec = new ActorRacialSpec
            {
                race = race,
                archetype = characterClass,
                isFemale = false,
                targetHeight = profile.height,
                bodyScale = profile.bodyScale,
                attachPauldrons = characterClass == "Warrior" || characterClass == "Paladin"
            };

            switch (race)
            {
                case "Orc":
                    // ALL 4 Orc classes use authentic muscular Warcraft Orc anatomy (Crag_Orc) so Orc Mages/Rogues never turn into skinny Humans!
                    spec.fbxPath = ArtWowDir + "Crag_Orc.fbx";
                    spec.attachPauldrons = characterClass != "Rogue";
                    if (characterClass == "Warrior")      { spec.palette = PaletteMode.OrcWarsong;       spec.weaponRightFbx = SkeletonsDir + "Skeleton_Axe.fbx"; }
                    else if (characterClass == "Paladin") { spec.palette = PaletteMode.OrcKorkron;       spec.weaponRightFbx = "Assets/RPG/Art/NPCs/KnightSword.fbx"; }
                    else if (characterClass == "Mage")    { spec.palette = PaletteMode.OrcShadowmoon;    spec.weaponRightFbx = SkeletonsDir + "Skeleton_Staff.fbx"; }
                    else                                  { spec.palette = PaletteMode.OrcShatteredHand; spec.weaponRightFbx = SkeletonsDir + "Skeleton_Blade.fbx"; spec.weaponLeftFbx = SkeletonsDir + "Skeleton_Blade.fbx"; }
                    break;

                case "Goblin":
                    spec.fbxPath = ArtWowDir + "Forest_Goblin.fbx";
                    spec.attachHeadgear = characterClass == "Mage";
                    spec.attachPauldrons = characterClass == "Warrior" || characterClass == "Paladin";
                    if (characterClass == "Warrior")      { spec.palette = PaletteMode.GoblinBruiser;    spec.weaponRightFbx = SkeletonsDir + "Skeleton_Axe.fbx"; }
                    else if (characterClass == "Paladin") { spec.palette = PaletteMode.GoblinCartelGold; spec.weaponRightFbx = "Assets/RPG/Art/NPCs/KnightSword.fbx"; }
                    else if (characterClass == "Mage")    { spec.palette = PaletteMode.GoblinTinker;     spec.weaponRightFbx = SkeletonsDir + "Skeleton_Staff.fbx"; }
                    else                                  { spec.palette = PaletteMode.GoblinCutthroat;  spec.weaponRightFbx = SkeletonsDir + "Skeleton_Blade.fbx"; spec.weaponLeftFbx = SkeletonsDir + "Skeleton_Blade.fbx"; }
                    break;

                case "Undead":
                    // ALL 4 Undead classes (including Undead_Paladin!) use authentic Forsaken Undead skeletal rigs with hand-painted bone & dark armor!
                    if (characterClass == "Warrior")      { spec.fbxPath = SkeletonsDir + "Skeleton_Warrior.fbx"; spec.palette = PaletteMode.UndeadWarrior; spec.weaponRightFbx = SkeletonsDir + "Skeleton_Axe.fbx"; }
                    else if (characterClass == "Paladin") { spec.fbxPath = SkeletonsDir + "Skeleton_Warrior.fbx"; spec.palette = PaletteMode.UndeadPaladin; spec.weaponRightFbx = "Assets/RPG/Art/NPCs/KnightSword.fbx"; }
                    else if (characterClass == "Mage")    { spec.fbxPath = SkeletonsDir + "Skeleton_Mage.fbx";    spec.palette = PaletteMode.UndeadMage;    spec.weaponRightFbx = SkeletonsDir + "Skeleton_Staff.fbx"; }
                    else                                  { spec.fbxPath = SkeletonsDir + "Skeleton_Rogue.fbx";   spec.palette = PaletteMode.UndeadRogue;   spec.weaponRightFbx = SkeletonsDir + "Skeleton_Blade.fbx"; }
                    break;

                case "Dwarf":
                    spec.fbxPath = characterClass == "Warrior" ? ArtWowDir + "Blacksmith_Man.fbx"
                                 : characterClass == "Paladin" ? ArtWowDir + "Paladin_Guard.fbx"
                                 : characterClass == "Mage"    ? ArtWowDir + "Arcanist_Mage.fbx"
                                 : ArtWowDir + "Ranger_Scout.fbx";
                    spec.palette = characterClass == "Paladin" ? PaletteMode.DwarfGold
                                 : characterClass == "Mage"    ? PaletteMode.DwarfFrost
                                 : PaletteMode.DwarfClan;
                    if (characterClass == "Warrior") spec.weaponRightFbx = SkeletonsDir + "Skeleton_Axe.fbx";
                    else if (characterClass == "Mage") spec.weaponRightFbx = SkeletonsDir + "Skeleton_Staff.fbx";
                    break;

                case "Elf":
                    spec.fbxPath = characterClass == "Paladin" ? ArtWowDir + "Paladin_Guard.fbx"
                                 : characterClass == "Mage"    ? ArtWowDir + "Arcanist_Mage.fbx"
                                 : characterClass == "Rogue"   ? ArtWowDir + "Ranger_Scout.fbx"
                                 : ArtWowDir + "Knight_Arms.fbx";
                    spec.palette = characterClass == "Mage"  ? PaletteMode.ElfSilvermoon
                                 : characterClass == "Rogue" ? PaletteMode.ElfFarstrider
                                 : PaletteMode.ElfSunwell;
                    spec.attachHeadgear = characterClass == "Mage" || characterClass == "Paladin";
                    if (characterClass == "Mage") spec.weaponRightFbx = SkeletonsDir + "Skeleton_Staff.fbx";
                    break;

                default: // Human
                    spec.fbxPath = characterClass == "Paladin" ? ArtWowDir + "Paladin_Guard.fbx"
                                 : characterClass == "Mage"    ? ArtWowDir + "Arcanist_Mage.fbx"
                                 : characterClass == "Rogue"   ? ArtWowDir + "Ranger_Scout.fbx"
                                 : ArtWowDir + "Knight_Arms.fbx";
                    spec.palette = characterClass == "Mage" ? PaletteMode.HumanArcane : PaletteMode.HumanAlliance;
                    if (characterClass == "Mage") spec.weaponRightFbx = SkeletonsDir + "Skeleton_Staff.fbx";
                    break;
            }

            return spec;
        }

        // =========================================================================
        // 7. SCENE-WIDE RACIAL NPC, ENEMY, ANIMAL, PET & MOUNT UPGRADER
        // =========================================================================
        public static void UpgradeAllSceneModelsAndMaterials(StringBuilder report = null)
        {
            EnsureUnifiedAssetsReady(false);

            var ctrlMelee  = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimDir + "Player Humanoid Melee.controller")
                          ?? AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimDir + "WoW_Humanoid_Melee.controller");
            var ctrlCaster = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimDir + "Player Humanoid Caster.controller")
                          ?? AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimDir + "WoW_Humanoid_Caster.controller");
            var ctrlRogue  = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimDir + "Player Humanoid Rogue.controller")
                          ?? ctrlMelee;
            var ctrlSkelW  = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimDir + "Player Undead Warrior.controller")
                          ?? AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimDir + "Skeleton Warrior.controller");
            var ctrlSkelM  = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimDir + "Player Undead Mage.controller")
                          ?? AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimDir + "Skeleton Mage.controller");
            var ctrlSkelR  = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimDir + "Player Undead Rogue.controller")
                          ?? AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimDir + "Skeleton Rogue.controller");

            int npcCount = 0, enemyCount = 0, animalMatCount = 0;

            // A. Upgrade every TownNPC & FlightMaster to authentic Racial Visuals + Unified Stylized Textures
            foreach (var npc in UnityEngine.Object.FindObjectsByType<TownNPC>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var spec = ResolveTownNPCSpec(npc.npcName, npc.roleTag);
                var ctrl = SelectControllerForSpec(spec, ctrlMelee, ctrlCaster, ctrlRogue, ctrlSkelW, ctrlSkelM, ctrlSkelR);
                ReplaceActorVisualCleanly(npc.gameObject, spec, ctrl);
                npc.nameplateHeight = spec.nameplateHeight;
                EditorUtility.SetDirty(npc);
                npcCount++;
            }

            // B. Upgrade Rowan (Merchant) & Elder Abe (QuestGiver) with Unified Stylized Textures
            foreach (var merch in UnityEngine.Object.FindObjectsByType<Merchant>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (merch.GetComponent<TownNPC>()) continue;
                var spec = new ActorRacialSpec
                {
                    race = "Human", archetype = "Smith", targetHeight = 1.86f, bodyScale = Vector3.one,
                    palette = PaletteMode.HumanAlliance, fbxPath = ArtWowDir + "Blacksmith_Man.fbx", nameplateHeight = 2.18f
                };
                ReplaceActorVisualCleanly(merch.gameObject, spec, ctrlMelee, preserveNameChild: "Merchant name");
            }

            foreach (var qg in UnityEngine.Object.FindObjectsByType<QuestGiver>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var abeTex = GetOrBakeStylizedDiffuse("Assets/RPG/Art/Abe/Textures/Ch39_1001_Diffuse.png", PaletteMode.HumanAlliance, false);
                var abeMat = GetOrCreateSurfaceMaterial("Mat_Stylized_ElderAbe", null, Color.white, 0.14f, 0f);
                if (abeTex) { abeMat.SetTexture("_BaseMap", abeTex); EditorUtility.SetDirty(abeMat); }
                foreach (var r in qg.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    r.sharedMaterials = Enumerable.Repeat(abeMat, r.sharedMaterials.Length).ToArray();
            }

            // C. Upgrade World & Dungeon Enemies to Racial/Creature-Accurate Models & Unified Stylized Textures
            foreach (var enemy in UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (enemy.trainingDummy) continue;
                if (TryUpgradeEnemyVisual(enemy, ctrlMelee, ctrlCaster, ctrlRogue, ctrlSkelW, ctrlSkelM, ctrlSkelR))
                    enemyCount++;
            }

            // D. Unify All Animal, Pet & Mount Materials with Stylized Painted Fur Texture
            animalMatCount = UnifyAllAnimalMountAndPetMaterials();

            // D2. Unify All World Nature, Architecture, Water & Prop Materials with Real PBR/Stylized Textures
            int worldMatCount = UnifyAllWorldEnvironmentAndNatureMaterials();

            // E. Refresh Active Scene Player Avatar so Play Mode starts with the newly rebuilt Racial Model immediately!
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (player)
            {
                CharacterVisualFactory.ApplyToPlayer(player, player.CharacterRace, player.CharacterClass);
            }

            MarkTextureStampComplete();
            AssetDatabase.SaveAssets();
            report?.AppendLine($"MODELS & TEXTURES: Upgraded {npcCount} racial Town/Flight NPCs, {enemyCount} zone enemies, {animalMatCount} mount/wildlife materials, and {worldMatCount} world/nature/architecture materials.");
        }

        static ActorRacialSpec ResolveTownNPCSpec(string npcName, string roleTag)
        {
            string n = (npcName ?? "").Trim();

            // ---------------- DWARF NPCS (Frostpeak Foothills) ----------------
            if (n.Contains("Baelor") || n.Contains("Thane"))
                return new ActorRacialSpec { race = "Dwarf", archetype = "Paladin", targetHeight = 1.46f, bodyScale = new Vector3(1.32f, 0.76f, 1.30f), palette = PaletteMode.DwarfGold, fbxPath = ArtWowDir + "Paladin_Guard.fbx", attachPauldrons = true, weaponRightFbx = SkeletonsDir + "Skeleton_Axe.fbx", nameplateHeight = 1.80f };
            if (n.Contains("Gundrak"))
                return new ActorRacialSpec { race = "Dwarf", archetype = "Smith", targetHeight = 1.44f, bodyScale = new Vector3(1.32f, 0.76f, 1.30f), palette = PaletteMode.DwarfClan, fbxPath = ArtWowDir + "Blacksmith_Man.fbx", attachPauldrons = true, weaponRightFbx = SkeletonsDir + "Skeleton_Axe.fbx", nameplateHeight = 1.76f };
            if (n.Contains("Olga"))
                return new ActorRacialSpec { race = "Dwarf", archetype = "Innkeeper", isFemale = true, targetHeight = 1.38f, bodyScale = new Vector3(1.25f, 0.75f, 1.24f), palette = PaletteMode.DwarfClan, fbxPath = ArtWowDir + "Tavern_Maiden.fbx", nameplateHeight = 1.70f };

            // ---------------- HIGH ELF NPCS (Silverglen Sanctum & Wyrmcrest) ----------------
            if (n.Contains("Thalorien"))
                return new ActorRacialSpec { race = "Elf", archetype = "Warrior", targetHeight = 1.94f, bodyScale = new Vector3(0.88f, 1.05f, 0.88f), palette = PaletteMode.ElfSunwell, fbxPath = ArtWowDir + "Paladin_Guard.fbx", attachPauldrons = true, attachHeadgear = true, nameplateHeight = 2.26f };
            if (n.Contains("Kaelen"))
                return new ActorRacialSpec { race = "Elf", archetype = "Mage", targetHeight = 1.92f, bodyScale = new Vector3(0.88f, 1.05f, 0.88f), palette = PaletteMode.ElfSilvermoon, fbxPath = ArtWowDir + "Arcanist_Mage.fbx", attachHeadgear = true, weaponRightFbx = SkeletonsDir + "Skeleton_Staff.fbx", nameplateHeight = 2.24f };
            if (n.Contains("Lyrielle") || n.Contains("Wyrmcrest Skywarden"))
                return new ActorRacialSpec { race = "Elf", archetype = "Innkeeper", isFemale = true, targetHeight = 1.89f, bodyScale = new Vector3(0.88f, 1.04f, 0.88f), palette = PaletteMode.ElfFarstrider, fbxPath = n.Contains("Skywarden") ? ArtWowDir + "Ranger_Scout.fbx" : ArtWowDir + "Tavern_Maiden.fbx", attachHeadgear = true, nameplateHeight = 2.20f };

            // ---------------- UNDEAD FORSAKEN NPCS (Deathknell Hollow) ----------------
            if (n.Contains("Morzan"))
                return new ActorRacialSpec { race = "Undead", archetype = "Rogue", targetHeight = 1.82f, bodyScale = new Vector3(0.95f, 0.98f, 0.95f), palette = PaletteMode.UndeadRogue, fbxPath = SkeletonsDir + "Skeleton_Rogue.fbx", attachPauldrons = true, weaponRightFbx = SkeletonsDir + "Skeleton_Blade.fbx", nameplateHeight = 2.14f };
            if (n.Contains("Alyssa"))
                return new ActorRacialSpec { race = "Undead", archetype = "Mage", targetHeight = 1.80f, bodyScale = new Vector3(0.94f, 0.98f, 0.94f), palette = PaletteMode.UndeadPaladin, fbxPath = SkeletonsDir + "Skeleton_Mage.fbx", weaponRightFbx = SkeletonsDir + "Skeleton_Staff.fbx", nameplateHeight = 2.12f };
            if (n.Contains("Cain"))
                return new ActorRacialSpec { race = "Undead", archetype = "Mage", targetHeight = 1.78f, bodyScale = new Vector3(0.94f, 0.98f, 0.94f), palette = PaletteMode.UndeadMage, fbxPath = SkeletonsDir + "Skeleton_Mage.fbx", nameplateHeight = 2.10f };

            // ---------------- GOBLIN NPCS (Rustwater Scrapworks & Gor'Kresh Exchange) ----------------
            if (n.Contains("Griznak"))
                return new ActorRacialSpec { race = "Goblin", archetype = "Paladin", targetHeight = 1.38f, bodyScale = new Vector3(1.08f, 0.74f, 1.06f), palette = PaletteMode.GoblinCartelGold, fbxPath = ArtWowDir + "Forest_Goblin.fbx", attachPauldrons = true, nameplateHeight = 1.70f };
            if (n.Contains("Fizzlebang"))
                return new ActorRacialSpec { race = "Goblin", archetype = "Mage", targetHeight = 1.33f, bodyScale = new Vector3(1.04f, 0.72f, 1.02f), palette = PaletteMode.GoblinTinker, fbxPath = ArtWowDir + "Forest_Goblin.fbx", attachHeadgear = true, weaponRightFbx = SkeletonsDir + "Skeleton_Staff.fbx", nameplateHeight = 1.65f };
            if (n.Contains("Drazzle"))
                return new ActorRacialSpec { race = "Goblin", archetype = "Noble", targetHeight = 1.35f, bodyScale = new Vector3(1.05f, 0.72f, 1.04f), palette = PaletteMode.GoblinCartelGold, fbxPath = ArtWowDir + "Forest_Goblin.fbx", attachHeadgear = true, nameplateHeight = 1.67f };
            if (n.Contains("Slinky"))
                return new ActorRacialSpec { race = "Goblin", archetype = "Innkeeper", isFemale = true, targetHeight = 1.28f, bodyScale = new Vector3(1.0f, 0.70f, 1.0f), palette = PaletteMode.GoblinBruiser, fbxPath = ArtWowDir + "Tavern_Maiden.fbx", nameplateHeight = 1.60f };

            // ---------------- ORC NPCS (Camp Bloodrock & Gor'Kresh Stronghold) ----------------
            if (n.Contains("Mor'gok"))
                return new ActorRacialSpec { race = "Orc", archetype = "Warrior", targetHeight = 2.22f, bodyScale = new Vector3(1.28f, 1.10f, 1.25f), palette = PaletteMode.OrcKorkron, fbxPath = ArtWowDir + "Crag_Orc.fbx", attachPauldrons = true, weaponRightFbx = SkeletonsDir + "Skeleton_Axe.fbx", nameplateHeight = 2.55f };
            if (n.Contains("Gorgash") || n.Contains("Korgath"))
                return new ActorRacialSpec { race = "Orc", archetype = "Warrior", targetHeight = 2.15f, bodyScale = new Vector3(1.24f, 1.08f, 1.22f), palette = PaletteMode.OrcWarsong, fbxPath = ArtWowDir + "Crag_Orc.fbx", attachPauldrons = true, weaponRightFbx = SkeletonsDir + "Skeleton_Axe.fbx", nameplateHeight = 2.46f };
            if (n.Contains("Garok") || n == "Thrak")
                return new ActorRacialSpec { race = "Orc", archetype = "Smith", targetHeight = 2.06f, bodyScale = new Vector3(1.22f, 1.06f, 1.20f), palette = PaletteMode.OrcWarsong, fbxPath = ArtWowDir + "Crag_Orc.fbx", attachPauldrons = true, weaponRightFbx = SkeletonsDir + "Skeleton_Axe.fbx", nameplateHeight = 2.36f };
            if (n.Contains("Shaman Kelsey"))
                return new ActorRacialSpec { race = "Orc", archetype = "Mage", targetHeight = 2.02f, bodyScale = new Vector3(1.18f, 1.04f, 1.16f), palette = PaletteMode.OrcShadowmoon, fbxPath = ArtWowDir + "Crag_Orc.fbx", attachPauldrons = true, weaponRightFbx = SkeletonsDir + "Skeleton_Staff.fbx", nameplateHeight = 2.32f };
            if (n.Contains("Grimjaw") || n.Contains("Bloodrock Outrider") || n.Contains("Wind Rider Vark"))
                return new ActorRacialSpec { race = "Orc", archetype = "Rogue", targetHeight = 2.04f, bodyScale = new Vector3(1.18f, 1.05f, 1.16f), palette = PaletteMode.OrcShatteredHand, fbxPath = ArtWowDir + "Crag_Orc.fbx", weaponRightFbx = SkeletonsDir + "Skeleton_Blade.fbx", nameplateHeight = 2.34f };
            if (n.Contains("Borga") || n.Contains("Gashna"))
                return new ActorRacialSpec { race = "Orc", archetype = "Innkeeper", isFemale = true, targetHeight = 1.94f, bodyScale = new Vector3(1.14f, 1.02f, 1.12f), palette = PaletteMode.OrcWarsong, fbxPath = ArtWowDir + "Tavern_Maiden.fbx", nameplateHeight = 2.24f };

            // ---------------- HUMAN / ALLIANCE NPCS ----------------
            if (n.Contains("Grand Duke") || n.Contains("Halford") || n.Contains("Darien") || n.Contains("Ryan"))
                return new ActorRacialSpec { race = "Human", archetype = "Paladin", targetHeight = n.Contains("Grand Duke") ? 1.96f : 1.90f, bodyScale = Vector3.one, palette = PaletteMode.HumanAlliance, fbxPath = ArtWowDir + "Paladin_Guard.fbx", attachPauldrons = true, nameplateHeight = 2.25f };
            if (n.Contains("Lucian"))
                return new ActorRacialSpec { race = "Human", archetype = "Mage", targetHeight = 1.90f, bodyScale = Vector3.one, palette = PaletteMode.HumanCrimson, fbxPath = ArtWowDir + "Arcanist_Mage.fbx", weaponRightFbx = SkeletonsDir + "Skeleton_Staff.fbx", nameplateHeight = 2.22f };
            if (n.Contains("Karen") || n.Contains("Aldous") || n.Contains("Skywatcher"))
                return new ActorRacialSpec { race = "Human", archetype = "Mage", targetHeight = 1.88f, bodyScale = Vector3.one, palette = PaletteMode.HumanArcane, fbxPath = ArtWowDir + "Arcanist_Mage.fbx", weaponRightFbx = SkeletonsDir + "Skeleton_Staff.fbx", nameplateHeight = 2.20f };
            if (n.Contains("Cheryl"))
                return new ActorRacialSpec { race = "Human", archetype = "Innkeeper", isFemale = true, targetHeight = 1.78f, bodyScale = Vector3.one, palette = PaletteMode.HumanAlliance, fbxPath = ArtWowDir + "Tavern_Maiden.fbx", nameplateHeight = 2.10f };
            if (n.Contains("Sylvan") || n.Contains("Lyra"))
                return new ActorRacialSpec { race = "Human", archetype = "Rogue", isFemale = true, targetHeight = 1.82f, bodyScale = Vector3.one, palette = PaletteMode.HumanAlliance, fbxPath = ArtWowDir + "Ranger_Scout.fbx", nameplateHeight = 2.14f };
            if (n.Contains("Torvald"))
                return new ActorRacialSpec { race = "Human", archetype = "Smith", targetHeight = 1.88f, bodyScale = new Vector3(1.06f, 1.0f, 1.05f), palette = PaletteMode.HumanAlliance, fbxPath = ArtWowDir + "Blacksmith_Man.fbx", nameplateHeight = 2.20f };
            if (n.Contains("Brian") || n.Contains("Vaelen"))
                return new ActorRacialSpec { race = "Human", archetype = "Warrior", targetHeight = 1.90f, bodyScale = Vector3.one, palette = PaletteMode.HumanAlliance, fbxPath = ArtWowDir + "Knight_Arms.fbx", attachPauldrons = true, nameplateHeight = 2.22f };

            return new ActorRacialSpec { race = "Human", archetype = "Warrior", targetHeight = 1.88f, bodyScale = Vector3.one, palette = PaletteMode.HumanAlliance, fbxPath = ArtWowDir + "Castle_Guard.fbx", nameplateHeight = 2.20f };
        }

        static bool TryUpgradeEnemyVisual(
            EnemyController enemy,
            RuntimeAnimatorController ctrlMelee,
            RuntimeAnimatorController ctrlCaster,
            RuntimeAnimatorController ctrlRogue,
            RuntimeAnimatorController ctrlSkelW,
            RuntimeAnimatorController ctrlSkelM,
            RuntimeAnimatorController ctrlSkelR)
        {
            string id = (enemy.enemyId ?? "").ToLowerInvariant();
            string objName = enemy.name;

            // Leave special procedural dragon/boss hierarchies intact if custom-built, but unify their materials if humanoid/skeleton
            if (id.Contains("vermithrax") || objName.Contains("Vermithrax"))
                return false;

            ActorRacialSpec spec;

            if (id.StartsWith("deathknell-ghoul"))
            {
                // True hunched Necrotic Undead Ghoul instead of Crag_Orc!
                spec = new ActorRacialSpec
                {
                    race = "Ghoul", archetype = "Warrior", targetHeight = 1.62f,
                    bodyScale = new Vector3(1.18f, 0.86f, 1.18f), palette = PaletteMode.UndeadGhoul,
                    fbxPath = SkeletonsDir + "Skeleton_Minion.fbx", weaponRightFbx = SkeletonsDir + "Skeleton_Blade.fbx"
                };
            }
            else if (id.StartsWith("frostpeak-trogg"))
            {
                spec = new ActorRacialSpec
                {
                    race = "Trogg", archetype = "Warrior", targetHeight = 1.58f,
                    bodyScale = new Vector3(1.38f, 0.78f, 1.35f), palette = PaletteMode.CreatureTrogg,
                    fbxPath = ArtWowDir + "Crag_Orc.fbx", attachPauldrons = true, weaponRightFbx = SkeletonsDir + "Skeleton_Axe.fbx"
                };
            }
            else if (id.StartsWith("silverglen-mana-sprite"))
            {
                spec = new ActorRacialSpec
                {
                    race = "ManaSprite", archetype = "Mage", targetHeight = 1.18f,
                    bodyScale = new Vector3(0.92f, 0.68f, 0.92f), palette = PaletteMode.CreatureManaSprite,
                    fbxPath = ArtWowDir + "Forest_Goblin.fbx"
                };
            }
            else if (id.StartsWith("rustwater-scavenger"))
            {
                spec = new ActorRacialSpec
                {
                    race = "Goblin", archetype = "Rogue", targetHeight = 1.34f,
                    bodyScale = new Vector3(1.04f, 0.72f, 1.02f), palette = PaletteMode.GoblinScavenger,
                    fbxPath = ArtWowDir + "Forest_Goblin.fbx", weaponRightFbx = SkeletonsDir + "Skeleton_Blade.fbx"
                };
            }
            else if (id.StartsWith("redridge-orc") || id.Contains("outcast"))
            {
                bool boss = enemy.isBoss || id.Contains("4") || objName.Contains("Chieftain");
                spec = new ActorRacialSpec
                {
                    race = "Orc", archetype = "Warrior", targetHeight = boss ? 2.28f : 2.06f,
                    bodyScale = new Vector3(1.24f, 1.06f, 1.22f), palette = PaletteMode.OrcOutcast,
                    fbxPath = ArtWowDir + "Crag_Orc.fbx", attachPauldrons = true, weaponRightFbx = SkeletonsDir + "Skeleton_Axe.fbx"
                };
            }
            else if (id.Contains("gorgonax"))
            {
                spec = new ActorRacialSpec
                {
                    race = "Orc", archetype = "Warrior", targetHeight = 3.45f,
                    bodyScale = new Vector3(1.36f, 1.12f, 1.34f), palette = PaletteMode.OrcOutcast,
                    fbxPath = ArtWowDir + "Crag_Orc.fbx", attachPauldrons = true, weaponRightFbx = SkeletonsDir + "Skeleton_Axe.fbx"
                };
            }
            else if (id.Contains("crypt") || id.Contains("scarlet") || enemy.transform.position.y < -15f)
            {
                // Subterranean Scarlet Crypts undead & inquisitors: upgrade materials in place so dungeon layout stays untouched
                foreach (var r in enemy.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.sharedMaterial && r.sharedMaterial.name.IndexOf("Skeleton", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        ApplyUnifiedSkeletonMaterials(enemy.gameObject, enemy.isBoss ? PaletteMode.UndeadPaladin : PaletteMode.UndeadWarrior);
                        break;
                    }
                }
                return true;
            }
            else if (enemy.isBoss)
            {
                spec = new ActorRacialSpec
                {
                    race = "Human", archetype = "Warrior", targetHeight = 2.28f,
                    bodyScale = new Vector3(1.12f, 1.05f, 1.12f), palette = PaletteMode.HumanCrimson,
                    fbxPath = ArtWowDir + "Knight_Arms.fbx", attachPauldrons = true
                };
            }
            else if (enemy.ranged)
            {
                spec = new ActorRacialSpec
                {
                    race = "Human", archetype = "Mage", targetHeight = 1.85f,
                    bodyScale = Vector3.one, palette = PaletteMode.HumanShadow,
                    fbxPath = ArtWowDir + "Arcanist_Mage.fbx", weaponRightFbx = SkeletonsDir + "Skeleton_Staff.fbx"
                };
            }
            else if (enemy.campGuard || enemy.heavyAttacker)
            {
                spec = new ActorRacialSpec
                {
                    race = "Orc", archetype = "Warrior", targetHeight = 2.06f,
                    bodyScale = new Vector3(1.22f, 1.06f, 1.20f), palette = PaletteMode.OrcOutcast,
                    fbxPath = ArtWowDir + "Crag_Orc.fbx", attachPauldrons = true, weaponRightFbx = SkeletonsDir + "Skeleton_Axe.fbx"
                };
            }
            else
            {
                spec = new ActorRacialSpec
                {
                    race = "Goblin", archetype = "Rogue", targetHeight = 1.36f,
                    bodyScale = new Vector3(1.04f, 0.72f, 1.02f), palette = PaletteMode.GoblinScavenger,
                    fbxPath = ArtWowDir + "Forest_Goblin.fbx", weaponRightFbx = SkeletonsDir + "Skeleton_Blade.fbx"
                };
            }

            var ctrl = SelectControllerForSpec(spec, ctrlMelee, ctrlCaster, ctrlRogue, ctrlSkelW, ctrlSkelM, ctrlSkelR);
            var visual = ReplaceActorVisualCleanly(enemy.gameObject, spec, ctrl);
            if (!visual) return false;

            var anim = visual.GetComponentInChildren<Animator>();
            enemy.animator = anim;
            enemy.nativeDeathAnimation = true;

            var motor = enemy.GetComponent<CharacterController>();
            if (motor)
            {
                motor.height = Mathf.Max(1.15f, spec.targetHeight);
                motor.radius = enemy.isBoss ? 0.48f : 0.35f;
                motor.center = Vector3.up * (motor.height * 0.5f);
                EditorUtility.SetDirty(motor);
            }

            var grounding = enemy.GetComponent<GroundedVisual>();
            if (grounding)
            {
                grounding.BindAnimator(anim);
                EditorUtility.SetDirty(grounding);
            }

            EditorUtility.SetDirty(enemy);
            return true;
        }

        static RuntimeAnimatorController SelectControllerForSpec(
            ActorRacialSpec spec,
            RuntimeAnimatorController ctrlMelee,
            RuntimeAnimatorController ctrlCaster,
            RuntimeAnimatorController ctrlRogue,
            RuntimeAnimatorController ctrlSkelW,
            RuntimeAnimatorController ctrlSkelM,
            RuntimeAnimatorController ctrlSkelR)
        {
            if (spec.fbxPath.StartsWith(SkeletonsDir, StringComparison.OrdinalIgnoreCase))
            {
                if (spec.archetype == "Mage") return ctrlSkelM;
                if (spec.archetype == "Rogue") return ctrlSkelR;
                return ctrlSkelW;
            }
            if (spec.archetype == "Mage") return ctrlCaster;
            if (spec.archetype == "Rogue") return ctrlRogue;
            return ctrlMelee;
        }

        static GameObject ReplaceActorVisualCleanly(GameObject actorGo, ActorRacialSpec spec, RuntimeAnimatorController ctrl, string preserveNameChild = null)
        {
            if (PrefabUtility.IsAnyPrefabInstanceRoot(actorGo))
                PrefabUtility.UnpackPrefabInstance(actorGo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            // If actorGo itself had a mesh/animator directly on root (rare), strip only visual child hierarchies
            var toRemove = new List<GameObject>();
            foreach (Transform child in actorGo.transform)
            {
                if (!string.IsNullOrEmpty(preserveNameChild) && child.name == preserveNameChild) continue;
                if (child.GetComponent<TextMesh>() || child.name.Contains("Nameplate") || child.name.Contains("Bark") || child.name.Contains("Quest"))
                    continue;

                if (child.name == "RacialModelVisual" || child.name == "ClassicWoWVisual" || child.name == "Model")
                    toRemove.Add(child.gameObject);
                else if (child.GetComponentInChildren<Renderer>(true) || child.GetComponentInChildren<Animator>(true))
                    child.gameObject.SetActive(false);
            }

            foreach (var old in toRemove)
                UnityEngine.Object.DestroyImmediate(old);

            var rootAnim = actorGo.GetComponent<Animator>();
            if (rootAnim) rootAnim.enabled = false;

            actorGo.transform.localScale = Vector3.one;
            var newVis = BuildStyledRacialVisual(spec, ctrl, actorGo.transform, "RacialModelVisual");
            EditorUtility.SetDirty(actorGo);
            return newVis;
        }

        /// <summary>
        /// Upgrades all Animal / Mount / Pet materials in Assets/RPG/Materials/ so horses, zebras, gryphons,
        /// pugs, sheep, pigs, cows, and llamas use the unified painted fur texture & matte-satin URP Lit shading.
        /// </summary>
        public static int UnifyAllAnimalMountAndPetMaterials()
        {
            var furTex = AssetDatabase.LoadAssetAtPath<Texture2D>(StylizedTexDir + "Tex_Stylized_Fur.png");
            if (!furTex) return 0;

            int count = 0;
            string matFolder = "Assets/RPG/Materials";
            if (!Directory.Exists(matFolder)) return 0;

            foreach (string file in Directory.GetFiles(matFolder, "Animal *.mat"))
            {
                string path = file.Replace('\\', '/');
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!mat) continue;

                var lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit && mat.shader != lit) mat.shader = lit;

                Color c = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.color;
                // Normalize animal palette into warm cohesive RPG tones
                Color.RGBToHSV(c, out float h, out float s, out float v);
                v = Mathf.Clamp(v, 0.16f, 0.92f);
                s = Mathf.Clamp(s, 0.04f, 0.68f);
                Color unified = Color.HSVToRGB(h, s, v);

                mat.SetTexture("_BaseMap", furTex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", furTex);
                mat.SetTextureScale("_BaseMap", new Vector2(3f, 3f));
                mat.SetColor("_BaseColor", unified);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.12f);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.0f);
                EditorUtility.SetDirty(mat);
                count++;
            }

            return count;
        }

        /// <summary>
        /// Assigns high-resolution PBR & stylized hand-painted textures + normal maps to all World,
        /// Nature (trees, bushes, flowers, rocks), Architecture (Foundation, Dark timber, Warm plaster,
        /// banners, gold, benches, anvils), Road, and Water materials so zero materials have _BaseMap=null.
        /// </summary>
        public static int UnifyAllWorldEnvironmentAndNatureMaterials()
        {
            EnsureProceduralSurfaceTextures(false);
            int count = 0;

            void ConfigureMat(string matPath, string baseTexPath, string normalTexPath, Color baseColor, Vector2 tiling, float smoothness, float metallic = 0f, Color? emission = null)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (!mat) return;

                var lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit && mat.shader != lit && !mat.shader.name.Contains("Simple Lit"))
                    mat.shader = lit;

                var tex = !string.IsNullOrEmpty(baseTexPath) ? AssetDatabase.LoadAssetAtPath<Texture2D>(baseTexPath) : null;
                if (tex)
                {
                    mat.SetTexture("_BaseMap", tex);
                    mat.SetTextureScale("_BaseMap", tiling);
                    if (mat.HasProperty("_MainTex"))
                    {
                        mat.SetTexture("_MainTex", tex);
                        mat.SetTextureScale("_MainTex", tiling);
                    }
                }

                var nrm = !string.IsNullOrEmpty(normalTexPath) ? AssetDatabase.LoadAssetAtPath<Texture2D>(normalTexPath) : null;
                if (nrm && mat.HasProperty("_BumpMap"))
                {
                    mat.SetTexture("_BumpMap", nrm);
                    mat.SetTextureScale("_BumpMap", tiling);
                    if (mat.HasProperty("_BumpScale")) mat.SetFloat("_BumpScale", 0.85f);
                    mat.EnableKeyword("_NORMALMAP");
                }

                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);

                if (emission.HasValue && mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", emission.Value);
                    if (tex && mat.HasProperty("_EmissionMap")) mat.SetTexture("_EmissionMap", tex);
                }

                EditorUtility.SetDirty(mat);
                count++;
            }

            // 1. Nature Materials (Trees, Bushes, Flowers, Rocks)
            ConfigureMat("Assets/RPG/Materials/Nature_NormalTree_Bark.mat", StylizedTexDir + "Tex_Stylized_TreeBark.png", null,
                new Color(0.58f, 0.42f, 0.28f, 1f), new Vector2(2f, 4f), 0.10f);
            ConfigureMat("Assets/RPG/Materials/Nature_PineTree_Bark.mat", StylizedTexDir + "Tex_Stylized_TreeBark.png", null,
                new Color(0.50f, 0.35f, 0.23f, 1f), new Vector2(2f, 4f), 0.10f);
            ConfigureMat("Assets/RPG/Materials/Nature_BirchTree_Bark.mat", StylizedTexDir + "Tex_Stylized_BirchBark.png", null,
                new Color(0.94f, 0.91f, 0.85f, 1f), new Vector2(2f, 4f), 0.12f);

            ConfigureMat("Assets/RPG/Materials/Nature_NormalTree_Leaves.mat", StylizedTexDir + "Tex_Stylized_LeafCanopy.png", null,
                new Color(0.36f, 0.72f, 0.28f, 1f), new Vector2(3f, 3f), 0.12f);
            ConfigureMat("Assets/RPG/Materials/Nature_PineTree_Leaves.mat", StylizedTexDir + "Tex_Stylized_PineNeedles.mat".Replace(".mat", ".png"), null,
                new Color(0.24f, 0.58f, 0.32f, 1f), new Vector2(3f, 3f), 0.12f);
            ConfigureMat("Assets/RPG/Materials/Nature_BirchTree_Leaves.mat", StylizedTexDir + "Tex_Stylized_BirchLeaves.png", null,
                new Color(0.94f, 0.76f, 0.26f, 1f), new Vector2(3f, 3f), 0.12f);
            ConfigureMat("Assets/RPG/Materials/Nature_Bush_Leaves.mat", StylizedTexDir + "Tex_Stylized_LeafCanopy.png", null,
                new Color(0.32f, 0.68f, 0.26f, 1f), new Vector2(3f, 3f), 0.12f);
            ConfigureMat("Assets/RPG/Materials/Nature_Flowers.mat", StylizedTexDir + "Tex_Stylized_FlowerPetals.png", null,
                new Color(0.96f, 0.76f, 0.88f, 1f), new Vector2(3f, 3f), 0.14f);
            ConfigureMat("Assets/RPG/Materials/Nature_Rock.mat",
                "Assets/RPG/Textures/TerrainPBR/Rock_Albedo.jpg", "Assets/RPG/Textures/TerrainPBR/Rock_Normal.exr",
                new Color(0.72f, 0.70f, 0.67f, 1f), new Vector2(2f, 2f), 0.14f);

            // 2. Architecture, Village & Prop Materials
            ConfigureMat("Assets/RPG/Materials/Foundation.mat",
                "Assets/RPG/Art/Village/Textures/T_RockTrim_BaseColor.png", "Assets/RPG/Art/Village/Textures/Normals Godot-Unity/T_RockTrim_Normal.png",
                new Color(0.78f, 0.76f, 0.72f, 1f), new Vector2(2f, 2f), 0.16f);
            ConfigureMat("Assets/RPG/Materials/Dark timber.mat",
                "Assets/RPG/Art/Village/Textures/T_WoodTrim_BaseColor.png", "Assets/RPG/Art/Village/Textures/Normals Godot-Unity/T_WoodTrim_Normal.png",
                new Color(0.64f, 0.46f, 0.32f, 1f), new Vector2(2f, 2f), 0.18f);
            ConfigureMat("Assets/RPG/Materials/Warm plaster.mat",
                "Assets/RPG/Art/Village/Textures/T_Plaster_BaseColor.png", "Assets/RPG/Art/Village/Textures/Normals Godot-Unity/T_Plaster_Normal.png",
                new Color(0.95f, 0.90f, 0.80f, 1f), new Vector2(2f, 2f), 0.14f);
            ConfigureMat("Assets/RPG/Materials/Faction crimson.mat",
                "Assets/RPG/Art/FantasyProps/Textures/T_Trim_Cloth_BaseColor.png", "Assets/RPG/Art/FantasyProps/Textures/Normals Godot-Unity/T_Trim_Cloth_Normal.png",
                new Color(0.84f, 0.18f, 0.18f, 1f), new Vector2(2f, 2f), 0.18f);
            ConfigureMat("Assets/RPG/Materials/Mat_Banner_AllianceBlue.mat",
                "Assets/RPG/Art/FantasyProps/Textures/T_Trim_Cloth_BaseColor.png", "Assets/RPG/Art/FantasyProps/Textures/Normals Godot-Unity/T_Trim_Cloth_Normal.png",
                new Color(0.18f, 0.44f, 0.88f, 1f), new Vector2(2f, 2f), 0.18f);
            ConfigureMat("Assets/RPG/Materials/Beacon gold.mat",
                "Assets/RPG/Art/FantasyProps/Textures/T_Trim_Metal_BaseColor.png", "Assets/RPG/Art/FantasyProps/Textures/Normals Godot-Unity/T_Trim_Metal_Normal.png",
                new Color(0.96f, 0.78f, 0.28f, 1f), new Vector2(2f, 2f), 0.48f, 0.45f, new Color(0.35f, 0.25f, 0.05f));
            ConfigureMat("Assets/RPG/Materials/Mat_Medieval_Anvil.mat",
                "Assets/RPG/Art/FantasyProps/Textures/T_Trim_Metal_BaseColor.png", "Assets/RPG/Art/FantasyProps/Textures/Normals Godot-Unity/T_Trim_Metal_Normal.png",
                new Color(0.52f, 0.54f, 0.58f, 1f), new Vector2(1.5f, 1.5f), 0.42f, 0.55f);
            ConfigureMat("Assets/RPG/Materials/Mat_Medieval_Bench.mat",
                "Assets/RPG/Art/FantasyProps/Textures/T_Trim_Furniture_BaseColor.png", "Assets/RPG/Art/FantasyProps/Textures/Normals Godot-Unity/T_Trim_Furniture_Normal.png",
                new Color(0.74f, 0.56f, 0.38f, 1f), new Vector2(1.5f, 1.5f), 0.20f);
            ConfigureMat("Assets/RPG/Materials/MedievalProps/Mat_Medieval_Anvil.mat",
                "Assets/RPG/Art/FantasyProps/Textures/T_Trim_Metal_BaseColor.png", "Assets/RPG/Art/FantasyProps/Textures/Normals Godot-Unity/T_Trim_Metal_Normal.png",
                new Color(0.52f, 0.54f, 0.58f, 1f), new Vector2(1.5f, 1.5f), 0.42f, 0.55f);
            ConfigureMat("Assets/RPG/Materials/MedievalProps/Mat_Medieval_Bench.mat",
                "Assets/RPG/Art/FantasyProps/Textures/T_Trim_Furniture_BaseColor.png", "Assets/RPG/Art/FantasyProps/Textures/Normals Godot-Unity/T_Trim_Furniture_Normal.png",
                new Color(0.74f, 0.56f, 0.38f, 1f), new Vector2(1.5f, 1.5f), 0.20f);

            // 3. Roads, Distant Hills & Water Surfaces
            ConfigureMat("Assets/RPG/Materials/Road.mat",
                "Assets/RPG/Textures/TerrainPBR/Dirt_Albedo.jpg", "Assets/RPG/Textures/TerrainPBR/Dirt_Normal.exr",
                new Color(0.68f, 0.56f, 0.43f, 1f), new Vector2(4f, 4f), 0.10f);
            ConfigureMat("Assets/RPG/Materials/Distant hills.mat",
                "Assets/RPG/Textures/TerrainPBR/Rock_Albedo.jpg", "Assets/RPG/Textures/TerrainPBR/Rock_Normal.exr",
                new Color(0.54f, 0.60f, 0.56f, 1f), new Vector2(6f, 6f), 0.08f);

            ConfigureMat("Assets/RPG/Materials/Willow brook water.mat", StylizedTexDir + "Tex_Stylized_WaterRipples.png", null,
                new Color(0.20f, 0.60f, 0.80f, 0.88f), new Vector2(4f, 6f), 0.80f);
            ConfigureMat("Assets/RPG/Materials/Mat_Medieval_WellWater.mat", StylizedTexDir + "Tex_Stylized_WaterRipples.png", null,
                new Color(0.22f, 0.64f, 0.84f, 0.92f), new Vector2(2f, 2f), 0.82f);
            ConfigureMat("Assets/RPG/Materials/MedievalProps/Mat_Medieval_WellWater.mat", StylizedTexDir + "Tex_Stylized_WaterRipples.png", null,
                new Color(0.22f, 0.64f, 0.84f, 0.92f), new Vector2(2f, 2f), 0.82f);
            ConfigureMat("Assets/RPG/Terrain/Flowing brook.mat", StylizedTexDir + "Tex_Stylized_WaterRipples.png", null,
                new Color(0.20f, 0.58f, 0.78f, 0.88f), new Vector2(3f, 8f), 0.80f);

            // 4. Emissive Crystals, Portals, Braziers & Lanterns
            ConfigureMat("Assets/RPG/Materials/Runestone glow.mat", StylizedTexDir + "Tex_Stylized_ArcaneCrystal.png", null,
                new Color(0.38f, 0.82f, 1.0f, 1f), new Vector2(2f, 2f), 0.65f, 0.1f, new Color(0.22f, 0.62f, 0.98f) * 1.8f);
            ConfigureMat("Assets/RPG/Materials/Mat_Portal_Arcane.mat", StylizedTexDir + "Tex_Stylized_ArcaneCrystal.png", null,
                new Color(0.32f, 0.76f, 1.0f, 0.92f), new Vector2(2f, 2f), 0.70f, 0.1f, new Color(0.24f, 0.66f, 1.0f) * 2.0f);
            ConfigureMat("Assets/RPG/Materials/Mat_Portal_Crimson.mat", StylizedTexDir + "Tex_Stylized_ArcaneCrystal.png", null,
                new Color(1.0f, 0.24f, 0.30f, 0.92f), new Vector2(2f, 2f), 0.70f, 0.1f, new Color(0.95f, 0.16f, 0.22f) * 2.0f);
            ConfigureMat("Assets/RPG/Materials/Camp embers.mat", StylizedTexDir + "Tex_Stylized_ArcaneCrystal.png", null,
                new Color(1.0f, 0.52f, 0.14f, 1f), new Vector2(2f, 2f), 0.35f, 0f, new Color(1.0f, 0.42f, 0.10f) * 2.2f);
            ConfigureMat("Assets/RPG/Materials/Mat_Horde_WarFlame.mat", StylizedTexDir + "Tex_Stylized_ArcaneCrystal.png", null,
                new Color(1.0f, 0.56f, 0.16f, 1f), new Vector2(2f, 2f), 0.40f, 0f, new Color(1.0f, 0.46f, 0.12f) * 2.2f);
            ConfigureMat("Assets/RPG/Materials/Mat_Lantern_WarmGold.mat", StylizedTexDir + "Tex_Stylized_ArcaneCrystal.png", null,
                new Color(1.0f, 0.84f, 0.42f, 1f), new Vector2(1f, 1f), 0.50f, 0f, new Color(1.0f, 0.76f, 0.32f) * 2.0f);

            return count;
        }

        static Mesh BuildArcaneCrystalClusterMesh()
        {
            var mb = new MeshBuilder();
            // Central tall crystal prism + 4 angled side shards
            mb.AddTaperedTube(new[]
            {
                (new Vector3(0f, 0f, 0f), new Vector2(0.18f, 0.16f)),
                (new Vector3(0f, 0.55f, 0f), new Vector2(0.22f, 0.20f)),
                (new Vector3(0f, 0.95f, 0f), new Vector2(0.01f, 0.01f)),
            }, 6);

            (Vector3 dir, float h, float r)[] shards =
            {
                (new Vector3(0.24f, 0.52f, 0.12f), 1.0f, 0.12f),
                (new Vector3(-0.22f, 0.48f, 0.16f), 1.0f, 0.11f),
                (new Vector3(0.14f, 0.44f, -0.24f), 1.0f, 0.11f),
                (new Vector3(-0.18f, 0.40f, -0.20f), 1.0f, 0.10f),
            };
            foreach (var sh in shards)
            {
                mb.AddTaperedTube(new[]
                {
                    (new Vector3(sh.dir.x * 0.2f, 0f, sh.dir.z * 0.2f), new Vector2(sh.r, sh.r)),
                    (sh.dir * 0.65f, new Vector2(sh.r * 1.1f, sh.r * 1.1f)),
                    (sh.dir * sh.h, new Vector2(0.008f, 0.008f)),
                }, 5);
            }
            return mb.ToMesh();
        }

        // =========================================================================
        // 8. COMPACT PROCEDURAL MESH & NOISE HELPERS
        // =========================================================================
        static float Hash2D(int x, int y)
        {
            int n = x * 374761393 + y * 668265263;
            n = (n ^ (n >> 13)) * 1274126177;
            return ((n ^ (n >> 16)) & 0x7fffffff) / (float)0x7fffffff;
        }

        static float ValueNoise(float x, float y)
        {
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash2D(ix, iy), b = Hash2D(ix + 1, iy);
            float c = Hash2D(ix, iy + 1), d = Hash2D(ix + 1, iy + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Fbm(float x, float y, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += ValueNoise(x, y) * amp;
                norm += amp;
                x *= 2.03f; y *= 2.01f; amp *= 0.5f;
            }
            return sum / Mathf.Max(0.0001f, norm);
        }

        static float QuantizeSoft(float val, float bands, float strength)
        {
            float q = Mathf.Round(val * bands) / bands;
            return Mathf.Lerp(val, q, strength);
        }

        class MeshBuilder
        {
            readonly List<Vector3> verts = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();
            readonly List<int> tris = new List<int>();

            public void AddTaperedTube((Vector3 pos, Vector2 radii)[] spine, int segs)
            {
                int baseIdx = verts.Count;
                int rings = spine.Length;
                for (int r = 0; r < rings; r++)
                {
                    Vector3 fwd = r < rings - 1 ? (spine[r + 1].pos - spine[r].pos) : (spine[r].pos - spine[r - 1].pos);
                    if (fwd.sqrMagnitude < 0.000001f) fwd = Vector3.up;
                    fwd.Normalize();
                    Vector3 upRef = Mathf.Abs(Vector3.Dot(fwd, Vector3.forward)) > 0.9f ? Vector3.up : Vector3.forward;
                    Vector3 right = Vector3.Cross(upRef, fwd).normalized;
                    Vector3 up = Vector3.Cross(fwd, right).normalized;

                    float vCoord = r / (float)Mathf.Max(1, rings - 1);
                    for (int s = 0; s < segs; s++)
                    {
                        float a = (s / (float)segs) * Mathf.PI * 2f;
                        Vector3 pt = spine[r].pos + right * (Mathf.Cos(a) * spine[r].radii.x) + up * (Mathf.Sin(a) * spine[r].radii.y);
                        verts.Add(pt);
                        uvs.Add(new Vector2(s / (float)segs, vCoord));
                    }
                }

                for (int r = 0; r < rings - 1; r++)
                {
                    for (int s = 0; s < segs; s++)
                    {
                        int sn = (s + 1) % segs;
                        int a = baseIdx + r * segs + s;
                        int b = baseIdx + r * segs + sn;
                        int c = baseIdx + (r + 1) * segs + s;
                        int d = baseIdx + (r + 1) * segs + sn;
                        tris.Add(a); tris.Add(c); tris.Add(b);
                        tris.Add(b); tris.Add(c); tris.Add(d);
                    }
                }

                // Cap ends
                int startCap = verts.Count;
                verts.Add(spine[0].pos); uvs.Add(new Vector2(0.5f, 0f));
                for (int s = 0; s < segs; s++)
                {
                    tris.Add(startCap);
                    tris.Add(baseIdx + s);
                    tris.Add(baseIdx + ((s + 1) % segs));
                }

                int endRing = baseIdx + (rings - 1) * segs;
                int endCap = verts.Count;
                verts.Add(spine[rings - 1].pos); uvs.Add(new Vector2(0.5f, 1f));
                for (int s = 0; s < segs; s++)
                {
                    tris.Add(endCap);
                    tris.Add(endRing + ((s + 1) % segs));
                    tris.Add(endRing + s);
                }
            }

            public void AddDome(Vector3 center, Vector3 radii, int lonSegs, int latRings)
            {
                int baseIdx = verts.Count;
                for (int lat = 0; lat <= latRings; lat++)
                {
                    float t = lat / (float)latRings;
                    float phi = t * Mathf.PI * 0.52f;
                    float y = Mathf.Sin(phi) * radii.y;
                    float ringR = Mathf.Cos(phi);
                    for (int lon = 0; lon < lonSegs; lon++)
                    {
                        float theta = (lon / (float)lonSegs) * Mathf.PI * 2f;
                        verts.Add(center + new Vector3(Mathf.Cos(theta) * ringR * radii.x, y - radii.y * 0.15f, Mathf.Sin(theta) * ringR * radii.z));
                        uvs.Add(new Vector2(lon / (float)lonSegs, t));
                    }
                }

                for (int lat = 0; lat < latRings; lat++)
                {
                    for (int lon = 0; lon < lonSegs; lon++)
                    {
                        int ln = (lon + 1) % lonSegs;
                        int a = baseIdx + lat * lonSegs + lon;
                        int b = baseIdx + lat * lonSegs + ln;
                        int c = baseIdx + (lat + 1) * lonSegs + lon;
                        int d = baseIdx + (lat + 1) * lonSegs + ln;
                        tris.Add(a); tris.Add(c); tris.Add(b);
                        tris.Add(b); tris.Add(c); tris.Add(d);
                    }
                }
            }

            public void AddCone(Vector3 basePos, Vector3 dir, float radius, float length, int segs)
            {
                AddTaperedTube(new[]
                {
                    (basePos, new Vector2(radius, radius)),
                    (basePos + dir * (length * 0.5f), new Vector2(radius * 0.52f, radius * 0.52f)),
                    (basePos + dir * length, new Vector2(0.002f, 0.002f)),
                }, segs);
            }

            public Mesh ToMesh() => new Mesh { vertices = verts.ToArray(), uv = uvs.ToArray(), triangles = tris.ToArray() };
        }
    }
}
