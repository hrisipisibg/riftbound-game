using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public class BarberShopSystem : MonoBehaviour
    {
        public static BarberShopSystem Instance { get; private set; }

        [Serializable]
        public struct AppearanceOption
        {
            public string name;
            public Color colorValue;
            public string icon;

            public AppearanceOption(string name, Color color, string icon = "")
            {
                this.name = name;
                this.colorValue = color;
                this.icon = icon;
            }
        }

        [Serializable]
        public class CharacterAppearance
        {
            public int hairStyleIndex = 0;
            public int hairColorIndex = 0;
            public int facialHairIndex = 0;
            public int skinToneIndex = 0;
            public int warpaintIndex = 0;

            public CharacterAppearance Clone()
            {
                return new CharacterAppearance
                {
                    hairStyleIndex = this.hairStyleIndex,
                    hairColorIndex = this.hairColorIndex,
                    facialHairIndex = this.facialHairIndex,
                    skinToneIndex = this.skinToneIndex,
                    warpaintIndex = this.warpaintIndex
                };
            }
        }

        public static readonly string[] HairStyles = new[]
        {
            "Short Spikes",
            "Long Braids",
            "Savage Mohawk",
            "High Ponytail",
            "Dreadlocks",
            "Flowing Mane",
            "Shaved Bald"
        };

        public static readonly AppearanceOption[] HairColors = new[]
        {
            new AppearanceOption("Onyx Black", new Color(0.12f, 0.12f, 0.14f)),
            new AppearanceOption("Golden Blonde", new Color(0.92f, 0.78f, 0.35f)),
            new AppearanceOption("Chestnut Brown", new Color(0.42f, 0.28f, 0.16f)),
            new AppearanceOption("Fiery Crimson", new Color(0.78f, 0.20f, 0.15f)),
            new AppearanceOption("Pure Silver", new Color(0.85f, 0.88f, 0.92f)),
            new AppearanceOption("Amethyst Purple", new Color(0.55f, 0.25f, 0.75f)),
            new AppearanceOption("Emerald Moss", new Color(0.20f, 0.65f, 0.35f))
        };

        public static readonly AppearanceOption[] SkinTones = new[]
        {
            new AppearanceOption("Fair Ivory", new Color(0.98f, 0.88f, 0.80f)),
            new AppearanceOption("Sun Tanned", new Color(0.85f, 0.70f, 0.55f)),
            new AppearanceOption("Olive Ochre", new Color(0.72f, 0.60f, 0.45f)),
            new AppearanceOption("Night Elf Lilac", new Color(0.70f, 0.65f, 0.85f)),
            new AppearanceOption("Orcish Viridian", new Color(0.45f, 0.65f, 0.40f)),
            new AppearanceOption("Undead Ashen", new Color(0.68f, 0.72f, 0.70f))
        };

        public static readonly string[] FacialHairs = new[]
        {
            "Clean Shaven",
            "Rugged Stubble",
            "Pointed Goatee",
            "Braided Dwarf Beard",
            "Full Mountain Beard",
            "Imperial Moustache"
        };

        public static readonly AppearanceOption[] Warpaints = new[]
        {
            new AppearanceOption("None", Color.clear),
            new AppearanceOption("Crimson Warpath", new Color(0.85f, 0.15f, 0.15f)),
            new AppearanceOption("Arcane Runes", new Color(0.25f, 0.75f, 1.0f)),
            new AppearanceOption("Shadow Sigil", new Color(0.45f, 0.15f, 0.65f)),
            new AppearanceOption("Ancestral White", new Color(0.95f, 0.95f, 0.95f))
        };

        public const int CutCostGold = 10;
        public CharacterAppearance current = new CharacterAppearance();
        public CharacterAppearance preview = new CharacterAppearance();
        public bool IsWindowOpen { get; set; } = false;

        public enum BarberTab { Hair, Face, Skin, Warpaint }
        public BarberTab activeTab = BarberTab.Hair;

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
        }

        public void OpenBarber()
        {
            preview = current.Clone();
            IsWindowOpen = true;
            AudioEffects.PlayClick();
        }

        public void CloseBarber()
        {
            preview = current.Clone(); // Discard unapplied
            IsWindowOpen = false;
            AudioEffects.PlayClick();
        }

        public void ToggleWindow()
        {
            if (IsWindowOpen) CloseBarber();
            else OpenBarber();
        }

        public bool ApplyMakeover(out string feedback)
        {
            if (!player) player = FindFirstObjectByType<PlayerController>();

            if (player && player.Gold < CutCostGold)
            {
                feedback = $"Insufficient gold! The Barber charges {CutCostGold} Gold.";
                AudioEffects.PlayClick();
                return false;
            }

            if (player)
            {
                player.SpendGoldDirect(CutCostGold);
            }

            current = preview.Clone();
            ApplyAppearanceToModel(current);

            feedback = $"New hairstyle and makeover applied! Paid {CutCostGold} Gold.";
            ChatSystem.LogSystem($"<color=#ffd700>✂ <b>[Barber Shop]</b> {feedback}</color>");
            ZoneBanner.Show("NEW LOOK APPLIED!", $"{HairStyles[current.hairStyleIndex]} · {HairColors[current.hairColorIndex].name}");
            AudioEffects.PlayQuestComplete();
            return true;
        }

        public void RevertAppearance()
        {
            preview = current.Clone();
            ApplyAppearanceToModel(current);
            AudioEffects.PlayClick();
        }

        public void ApplyAppearanceToModel(CharacterAppearance app)
        {
            if (!player) player = FindFirstObjectByType<PlayerController>();
            if (!player) return;

            // Apply skin tint to player renderers
            var renderers = player.GetComponentsInChildren<SkinnedMeshRenderer>();
            var skinCol = SkinTones[Mathf.Clamp(app.skinToneIndex, 0, SkinTones.Length - 1)].colorValue;
            foreach (var smr in renderers)
            {
                if (smr.material != null && smr.material.HasProperty("_BaseColor"))
                {
                    smr.material.SetColor("_BaseColor", skinCol);
                }
                else if (smr.material != null && smr.material.HasProperty("_Color"))
                {
                    smr.material.color = skinCol;
                }
            }
        }

        // GUI Rendering
        public void DrawBarberShopGUI(float screenWidth, float screenHeight)
        {
            if (!IsWindowOpen) return;

            float winW = 660f;
            float winH = 470f;
            float winX = (screenWidth - winW) * 0.5f;
            float winY = (screenHeight - winH) * 0.5f;

            // Background & Gothic Border
            DrawRect(new Rect(winX - 3, winY - 3, winW + 6, winH + 6), new Color(0.85f, 0.70f, 0.28f, 0.95f)); // Gold trim
            DrawRect(new Rect(winX, winY, winW, winH), new Color(0.06f, 0.08f, 0.12f, 0.98f));

            // Header Banner
            DrawRect(new Rect(winX, winY, winW, 38f), new Color(0.12f, 0.15f, 0.22f, 1f));
            var headStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            headStyle.normal.textColor = new Color(1f, 0.85f, 0.35f);
            GUI.Label(new Rect(winX + 16, winY + 6, winW - 60, 26), "✂ REALM BARBER SHOP & MAKEUP STUDIO", headStyle);

            if (GUI.Button(new Rect(winX + winW - 32, winY + 6, 24, 24), "✕"))
            {
                CloseBarber();
            }

            // Category Navigation Tabs
            float tabY = winY + 44;
            float tabW = 145f;
            float tabH = 30f;
            var tabs = new[] { BarberTab.Hair, BarberTab.Face, BarberTab.Skin, BarberTab.Warpaint };
            var tabNames = new[] { "💇 Hair Style & Color", "🧔 Facial Hair", "🎨 Skin Complexion", "⚡ Warpaint / Runes" };

            for (int t = 0; t < tabs.Length; t++)
            {
                bool isAct = (activeTab == tabs[t]);
                Color tabCol = isAct ? new Color(0.20f, 0.32f, 0.48f, 1f) : new Color(0.08f, 0.10f, 0.14f, 0.85f);
                DrawRect(new Rect(winX + 16 + (t * (tabW + 8)), tabY, tabW, tabH), tabCol);

                var tabTxtStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = isAct ? FontStyle.Bold : FontStyle.Normal, alignment = TextAnchor.MiddleCenter };
                tabTxtStyle.normal.textColor = isAct ? Color.white : new Color(0.75f, 0.75f, 0.75f);
                GUI.Label(new Rect(winX + 16 + (t * (tabW + 8)), tabY, tabW, tabH), tabNames[t], tabTxtStyle);

                if (GUI.Button(new Rect(winX + 16 + (t * (tabW + 8)), tabY, tabW, tabH), GUIContent.none, GUIStyle.none))
                {
                    activeTab = tabs[t];
                    AudioEffects.PlayClick();
                }
            }

            // Tab Content Pane
            float paneY = winY + 82;
            float paneW = winW - 32;
            float paneH = winH - 146;
            DrawRect(new Rect(winX + 16, paneY, paneW, paneH), new Color(0.04f, 0.05f, 0.07f, 0.85f));

            switch (activeTab)
            {
                case BarberTab.Hair:
                    DrawHairTab(winX + 24, paneY + 12, paneW - 16);
                    break;
                case BarberTab.Face:
                    DrawFacialHairTab(winX + 24, paneY + 12, paneW - 16);
                    break;
                case BarberTab.Skin:
                    DrawSkinTab(winX + 24, paneY + 12, paneW - 16);
                    break;
                case BarberTab.Warpaint:
                    DrawWarpaintTab(winX + 24, paneY + 12, paneW - 16);
                    break;
            }

            // Bottom Confirmation & Fee Bar
            float barY = winY + winH - 54;
            DrawRect(new Rect(winX + 16, barY, paneW, 44), new Color(0.08f, 0.10f, 0.14f, 0.9f));

            int playerGold = player ? player.Gold : 0;
            var feeStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleLeft };
            feeStyle.normal.textColor = (playerGold >= CutCostGold) ? new Color(1f, 0.85f, 0.35f) : new Color(1f, 0.3f, 0.3f);
            GUI.Label(new Rect(winX + 28, barY + 8, 300, 26), $"✂ Makeover Fee: <b>{CutCostGold} Gold</b> (Your Gold: {playerGold} 🪙)", feeStyle);

            if (GUI.Button(new Rect(winX + paneW - 240, barY + 7, 110, 30), "Revert"))
            {
                RevertAppearance();
            }

            bool canAfford = (playerGold >= CutCostGold);
            GUI.enabled = canAfford;
            if (GUI.Button(new Rect(winX + paneW - 120, barY + 7, 120, 30), "✂ Trim & Apply"))
            {
                ApplyMakeover(out _);
            }
            GUI.enabled = true;
        }

        void DrawHairTab(float x, float y, float w)
        {
            var headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            headerStyle.normal.textColor = new Color(1f, 0.85f, 0.35f);
            GUI.Label(new Rect(x, y, w, 20), "CHOOSE HAIRSTYLE:", headerStyle);

            float rowW = 180f;
            float rowH = 34f;
            for (int i = 0; i < HairStyles.Length; i++)
            {
                float colX = x + ((i % 3) * (rowW + 14));
                float rowY = y + 26 + ((i / 3) * (rowH + 8));

                bool isSel = (preview.hairStyleIndex == i);
                DrawRect(new Rect(colX, rowY, rowW, rowH), isSel ? new Color(0.25f, 0.40f, 0.60f, 0.9f) : new Color(0.10f, 0.12f, 0.16f, 0.8f));

                var lblStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
                lblStyle.normal.textColor = isSel ? Color.white : new Color(0.85f, 0.85f, 0.85f);
                GUI.Label(new Rect(colX, rowY, rowW, rowH), HairStyles[i], lblStyle);

                if (GUI.Button(new Rect(colX, rowY, rowW, rowH), GUIContent.none, GUIStyle.none))
                {
                    preview.hairStyleIndex = i;
                    AudioEffects.PlayClick();
                }
            }

            float colorY = y + 120;
            GUI.Label(new Rect(x, colorY, w, 20), "CHOOSE HAIR DYE COLOR:", headerStyle);

            for (int c = 0; c < HairColors.Length; c++)
            {
                var colOpt = HairColors[c];
                float colX = x + (c * 82f);
                float colY = colorY + 26;

                bool isSel = (preview.hairColorIndex == c);
                DrawRect(new Rect(colX, colY, 74f, 40f), isSel ? new Color(1f, 0.85f, 0.2f, 1f) : new Color(0.2f, 0.2f, 0.2f, 0.8f));
                DrawRect(new Rect(colX + 3, colY + 3, 68f, 34f), colOpt.colorValue);

                var nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 9, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                nameStyle.normal.textColor = (colOpt.colorValue.grayscale > 0.5f) ? Color.black : Color.white;
                GUI.Label(new Rect(colX + 3, colY + 3, 68f, 34f), colOpt.name.Replace(" ", "\n"), nameStyle);

                if (GUI.Button(new Rect(colX, colY, 74f, 40f), GUIContent.none, GUIStyle.none))
                {
                    preview.hairColorIndex = c;
                    AudioEffects.PlayClick();
                }
            }
        }

        void DrawFacialHairTab(float x, float y, float w)
        {
            var headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            headerStyle.normal.textColor = new Color(1f, 0.85f, 0.35f);
            GUI.Label(new Rect(x, y, w, 20), "FACIAL HAIR & BEARD GROOMING:", headerStyle);

            float rowW = 220f;
            float rowH = 40f;
            for (int i = 0; i < FacialHairs.Length; i++)
            {
                float colX = x + ((i % 2) * (rowW + 24));
                float rowY = y + 30 + ((i / 2) * (rowH + 12));

                bool isSel = (preview.facialHairIndex == i);
                DrawRect(new Rect(colX, rowY, rowW, rowH), isSel ? new Color(0.25f, 0.40f, 0.60f, 0.9f) : new Color(0.10f, 0.12f, 0.16f, 0.8f));

                var lblStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
                lblStyle.normal.textColor = isSel ? Color.white : new Color(0.85f, 0.85f, 0.85f);
                GUI.Label(new Rect(colX, rowY, rowW, rowH), FacialHairs[i], lblStyle);

                if (GUI.Button(new Rect(colX, rowY, rowW, rowH), GUIContent.none, GUIStyle.none))
                {
                    preview.facialHairIndex = i;
                    AudioEffects.PlayClick();
                }
            }
        }

        void DrawSkinTab(float x, float y, float w)
        {
            var headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            headerStyle.normal.textColor = new Color(1f, 0.85f, 0.35f);
            GUI.Label(new Rect(x, y, w, 20), "RACIAL SKIN COMPLEXION & TINT:", headerStyle);

            float rowW = 180f;
            float rowH = 55f;
            for (int i = 0; i < SkinTones.Length; i++)
            {
                var tone = SkinTones[i];
                float colX = x + ((i % 3) * (rowW + 16));
                float rowY = y + 30 + ((i / 3) * (rowH + 14));

                bool isSel = (preview.skinToneIndex == i);
                DrawRect(new Rect(colX, rowY, rowW, rowH), isSel ? new Color(1f, 0.85f, 0.2f, 1f) : new Color(0.15f, 0.17f, 0.22f, 0.9f));
                DrawRect(new Rect(colX + 3, rowY + 3, 50, rowH - 6), tone.colorValue);

                var lblStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = isSel ? FontStyle.Bold : FontStyle.Normal };
                lblStyle.normal.textColor = isSel ? Color.white : new Color(0.85f, 0.85f, 0.85f);
                GUI.Label(new Rect(colX + 60, rowY + 12, rowW - 65, 30), tone.name, lblStyle);

                if (GUI.Button(new Rect(colX, rowY, rowW, rowH), GUIContent.none, GUIStyle.none))
                {
                    preview.skinToneIndex = i;
                    AudioEffects.PlayClick();
                }
            }
        }

        void DrawWarpaintTab(float x, float y, float w)
        {
            var headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
            headerStyle.normal.textColor = new Color(1f, 0.85f, 0.35f);
            GUI.Label(new Rect(x, y, w, 20), "TRIBAL WARPAINT & MYSTIC RUNES:", headerStyle);

            float rowW = 260f;
            float rowH = 45f;
            for (int i = 0; i < Warpaints.Length; i++)
            {
                var wp = Warpaints[i];
                float colX = x + ((i % 2) * (rowW + 20));
                float rowY = y + 30 + ((i / 2) * (rowH + 10));

                bool isSel = (preview.warpaintIndex == i);
                DrawRect(new Rect(colX, rowY, rowW, rowH), isSel ? new Color(0.25f, 0.40f, 0.60f, 0.9f) : new Color(0.10f, 0.12f, 0.16f, 0.8f));

                if (wp.colorValue != Color.clear)
                {
                    DrawRect(new Rect(colX + 10, rowY + 12, 22, 22), wp.colorValue);
                }

                var lblStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
                lblStyle.normal.textColor = isSel ? Color.white : new Color(0.85f, 0.85f, 0.85f);
                GUI.Label(new Rect(colX + 42, rowY + 12, rowW - 50, 22), wp.name, lblStyle);

                if (GUI.Button(new Rect(colX, rowY, rowW, rowH), GUIContent.none, GUIStyle.none))
                {
                    preview.warpaintIndex = i;
                    AudioEffects.PlayClick();
                }
            }
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
