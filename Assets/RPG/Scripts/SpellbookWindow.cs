using UnityEngine;

namespace Riftbound
{
    public partial class PrototypeHUD
    {
        int spellbookTab;
        Vector2 spellbookScroll;
        static readonly string[] BookTabs = { "Class abilities", "Travel & utility", "Learned talents" };

        void DrawSpellbook(float width, float height)
        {
            var window = new Rect(width / 2f - 376f, height / 2f - 276f, 752f, 552f);
            Panel(window);
            GUI.Label(new Rect(window.x + 22f, window.y + 12f, 370f, 36f), "SPELLBOOK", title);
            GUI.Label(new Rect(window.x + 24f, window.y + 49f, 520f, 23f),
                $"Level {player.Level}  ·  {player.CharacterRace} {player.CharacterClass}", small);
            if (GUI.Button(new Rect(window.xMax - 114f, window.y + 20f, 90f, 28f), "Close [P]", microBtnStyle))
                player.SpellbookOpen = false;

            for (int i = 0; i < BookTabs.Length; i++)
            {
                var tab = new Rect(window.x + 22f + i * 237f, window.y + 81f, 229f, 32f);
                if (GUI.Button(tab, BookTabs[i], questButton) && spellbookTab != i)
                {
                    spellbookTab = i;
                    spellbookScroll = Vector2.zero;
                }
                if (spellbookTab == i) Fill(new Rect(tab.x, tab.yMax - 2f, tab.width, 2f), new Color(.92f, .71f, .34f));
            }

            GUILayout.BeginArea(new Rect(window.x + 22f, window.y + 126f, 708f, 373f));
            spellbookScroll = GUILayout.BeginScrollView(spellbookScroll);
            if (spellbookTab == 0)
            {
                for (int slot = 1; slot <= 3; slot++)
                {
                    var ability = ClassSystem.GetAbilityInfo(player.CharacterClass, slot);
                    DrawBookCard(ability.name, ability.desc, $"Key {slot}", RiftboundAbilityArt.ForAction(player.CharacterClass, slot.ToString()));
                }
                DrawBookCard(player.InterruptSpellName, "Interrupt a visible enemy casting a spell.", "Key F", 14);
            }
            else if (spellbookTab == 1)
            {
                DrawBookCard("Health potion", "Drink one potion to restore up to 70 health.", "Key 4", 1);
                DrawBookCard("Guard", "Hold your defensive stance.", "Hold Q", 27);
                DrawBookCard("Summon mount", "Summon or dismiss your selected mount.", "Key Z", 30);
                DrawBookCard("Hearthstone", "Return to your bound home settlement.", "Key H", 31);
                DrawBookCard("World atlas", "Open the map and place a travel waypoint.", "Key M", 32);
                int racialArt = player.CharacterRace switch
                {
                    "Dwarf" => 38, "Undead" => 39, "Human" => 40, "Elf" or "High Elf" => 43, _ => 41
                };
                DrawBookCard($"{player.CharacterRace} heritage", ClassSystem.GetRaceDescription(player.CharacterRace), "Passive", racialArt);
            }
            else
            {
                bool any = false;
                bool canBind = player.MilestoneTalentsAvailable && !player.InCombat();
                foreach (var talent in MilestoneTalents.All)
                {
                    if (!player.HasMilestone(talent.Id)) continue;
                    any = true;
                    DrawBookCard(talent.Name, talent.Description,
                        talent.Active ? $"Instant · {talent.Cooldown:0}s cooldown" : "Passive", talent.Icon, talent, canBind);
                }
                if (!any)
                    GUILayout.Label(player.MilestoneTalentsAvailable
                        ? "Your first talent unlocks at level 5. Learn it in Milestone Talents."
                        : "Milestone talents are available in solo play.", label);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            if (spellbookTab == 2)
            {
                GUI.Label(new Rect(window.x + 24f, window.yMax - 44f, 490f, 31f),
                    "Assign active talents to slots 5 and 6 outside combat.", small);
                if (GUI.Button(new Rect(window.xMax - 218f, window.yMax - 44f, 194f, 28f), "Milestone Talents [N]", microBtnStyle))
                {
                    showSpecializations = false;
                    player.SpellbookOpen = false;
                    player.TalentsOpen = true;
                }
            }
        }

        void DrawBookCard(string name, string description, string detail, int artwork,
            MilestoneTalents.Talent talent = null, bool canBind = false)
        {
            var card = GUILayoutUtility.GetRect(1, 85f, GUILayout.ExpandWidth(true));
            Fill(card, new Color(.061f, .088f, .117f, .98f));
            var image = new Rect(card.x + 10f, card.y + 10f, 62f, 62f);
            Fill(image, new Color(.095f, .127f, .165f));
            RiftboundAbilityArt.Draw(image, artwork);
            var bookHeading = new GUIStyle(label) { fontStyle = FontStyle.Bold, fontSize = 16 };
            bookHeading.normal.textColor = new Color(1f, .91f, .72f);
            var bookDetail = new GUIStyle(small) { fontSize = 12 };
            bookDetail.normal.textColor = new Color(.62f, .81f, .91f);
            float textWidth = card.width - (talent?.Active == true ? 230f : 95f);
            GUI.Label(new Rect(card.x + 84f, card.y + 5f, textWidth, 24f), name, bookHeading);
            GUI.Label(new Rect(card.x + 84f, card.y + 29f, textWidth, 35f), description, small);
            GUI.Label(new Rect(card.x + 84f, card.y + 63f, textWidth, 19f), detail, bookDetail);
            if (talent?.Active == true)
            {
                bool enabled = GUI.enabled;
                for (int slot = 0; slot < 2; slot++)
                {
                    bool equipped = player.MilestoneInSlot(slot) == talent;
                    GUI.enabled = enabled && canBind && !equipped;
                    if (GUI.Button(new Rect(card.xMax - 136f, card.y + 15f + slot * 28f, 125f, 24f),
                            equipped ? $"Equipped [ {5 + slot} ]" : $"Equip to [ {5 + slot} ]", microBtnStyle) &&
                        player.BindMilestone(talent.Id, slot)) AudioEffects.PlayClick();
                }
                GUI.enabled = enabled;
            }
            GUILayout.Space(7f);
        }
    }
}
