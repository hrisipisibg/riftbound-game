using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    [Serializable]
    public class AuctionListing
    {
        public string id;
        public string itemName;
        public string sellerName;
        public int buyoutPrice;
        public string category; // "Weapon", "Armor", "Consumable", "Trade Goods"
        public string rarity;   // "Common", "Uncommon", "Rare", "Epic"
        public string itemId;
        public Color rarityColor;
    }

    [Serializable]
    public class MailMessage
    {
        public string id;
        public string sender;
        public string subject;
        public string body;
        public int goldAttachment;
        public string itemAttachmentId;
        public bool isRead;
        public bool isClaimed;
    }

    public class AuctionHouseSystem : MonoBehaviour
    {
        public static AuctionHouseSystem Instance { get; private set; }

        public static bool AuctionOpen { get; set; } = false;
        public static bool MailboxOpen { get; set; } = false;

        public List<AuctionListing> listings = new List<AuctionListing>();
        public List<MailMessage> mailbox = new List<MailMessage>();

        int selectedTab = 0; // 0 = Browse, 1 = Bids, 2 = Auctions (Sell)
        string categoryFilter = "All";
        string searchFilter = "";
        Vector2 browseScroll, mailScroll;
        AuctionListing selectedListing;
        MailMessage selectedMail;

        PlayerController player;

        GUIStyle winTitle, tabStyle, itemRowStyle, headerStyle, priceStyle, mailSubjectStyle, bodyStyle;
        Texture2D bgTex, borderTex, headerTex, goldTex, unreadMailTex;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying) Destroy(this); else DestroyImmediate(this);
                return;
            }
            Instance = this;
            player = GetComponent<PlayerController>();
            PopulateInitialAuctions();
            PopulateStarterMail();
        }

        void PopulateInitialAuctions()
        {
            listings.Clear();
            listings.Add(new AuctionListing
            {
                id = "auc-1",
                itemName = "Shadowfang Stiletto",
                sellerName = "Rogue_Vax",
                buyoutPrice = 45,
                category = "Weapon",
                rarity = "Epic",
                itemId = "duskwood-shadow-dagger",
                rarityColor = new Color(0.64f, 0.21f, 0.93f)
            });
            listings.Add(new AuctionListing
            {
                id = "auc-2",
                itemName = "Aegis of the Scarlet Dawn",
                sellerName = "Paladin_Brian",
                buyoutPrice = 60,
                category = "Armor",
                rarity = "Epic",
                itemId = "scarlet-aegis",
                rarityColor = new Color(0.64f, 0.21f, 0.93f)
            });
            listings.Add(new AuctionListing
            {
                id = "auc-3",
                itemName = "Peacebloom (Stack of 5)",
                sellerName = "Herbalist_Rose",
                buyoutPrice = 6,
                category = "Trade Goods",
                rarity = "Common",
                itemId = "Peacebloom",
                rarityColor = Color.white
            });
            listings.Add(new AuctionListing
            {
                id = "auc-4",
                itemName = "Tin Ore (Stack of 5)",
                sellerName = "Miner_Baelor",
                buyoutPrice = 10,
                category = "Trade Goods",
                rarity = "Common",
                itemId = "Tin Ore",
                rarityColor = Color.white
            });
            listings.Add(new AuctionListing
            {
                id = "auc-5",
                itemName = "Elixir of Lion's Strength",
                sellerName = "Alchemist_Karen",
                buyoutPrice = 12,
                category = "Consumable",
                rarity = "Uncommon",
                itemId = "buff-strength",
                rarityColor = new Color(0.12f, 1.0f, 0.0f)
            });
            listings.Add(new AuctionListing
            {
                id = "auc-6",
                itemName = "Inquisitor's Sunblade",
                sellerName = "Templar_Alden",
                buyoutPrice = 55,
                category = "Weapon",
                rarity = "Epic",
                itemId = "scarlet-blade",
                rarityColor = new Color(0.64f, 0.21f, 0.93f)
            });
        }

        void PopulateStarterMail()
        {
            mailbox.Clear();
            mailbox.Add(new MailMessage
            {
                id = "mail-1",
                sender = "Grand Duke Ronald",
                subject = "Royal Alliance Treasury Stipend",
                body = "Hero of Highspire,\n\nYour valor across the Amber Vale and beyond has secured our northern borders. Accept this gold stipend from the Alliance treasury to fund your campaign.\n\nFor the Alliance!\nâ€” Grand Duke Ronald",
                goldAttachment = 30,
                isRead = false,
                isClaimed = false
            });
            mailbox.Add(new MailMessage
            {
                id = "mail-2",
                sender = "Bilgewater Cartel",
                subject = "Quarterly Trade Dividend",
                body = "Time is money, associate!\n\nYour participation in frontier trade has paid substantial dividends. Keep spending gold in our outposts and you'll go far!\n\nâ€” Boss Griznak, Foreman",
                goldAttachment = 20,
                isRead = false,
                isClaimed = false
            });
        }

        public bool HasUnreadMail()
        {
            foreach (var m in mailbox)
                if (!m.isRead || (!m.isClaimed && m.goldAttachment > 0)) return true;
            return false;
        }

        public void BuyoutListing(AuctionListing listing)
        {
            if (listing == null || !player) return;

            if (player.Gold < listing.buyoutPrice)
            {
                ChatSystem.LogSystem("You do not have enough gold for this buyout!");
                CombatTextManager.Spawn(transform.position + Vector3.up * 1.5f, "Not Enough Gold!", new Color(1f, 0.3f, 0.3f), false);
                return;
            }

            player.SpendGoldDirect(listing.buyoutPrice);
            listings.Remove(listing);

            // Grant purchased item
            if (listing.category == "Trade Goods")
            {
                if (ProfessionSystem.Instance) ProfessionSystem.Instance.AddReagent(listing.itemId, 5);
            }
            else if (listing.itemId == "potion")
            {
                player.AddPotionsDirect(2);
            }
            else
            {
                player.GrantItem(listing.itemId);
            }

            AudioEffects.PlayLootCoin();
            CombatTextManager.Spawn(transform.position + Vector3.up * 1.8f, $"Purchased {listing.itemName}!", listing.rarityColor, false);
            ChatSystem.LogLoot($"Auction House: Won [{listing.itemName}] for {listing.buyoutPrice} Gold!");
            selectedListing = null;
        }

        public void ClaimMailAttachment(MailMessage mail)
        {
            if (mail == null || mail.isClaimed || !player) return;

            if (mail.goldAttachment > 0)
            {
                player.AddGoldDirect(mail.goldAttachment);
                CombatTextManager.Spawn(transform.position + Vector3.up * 1.8f, $"+{mail.goldAttachment} Gold (Mail)", new Color(1f, 0.85f, 0.25f), false);
                ChatSystem.LogLoot($"Mailbox: Collected {mail.goldAttachment} Gold from {mail.sender}.");
                mail.goldAttachment = 0;
            }
            mail.isClaimed = true;
            mail.isRead = true;
            AudioEffects.PlayLootCoin();
        }

        void InitTextures()
        {
            if (bgTex != null) return;
            bgTex = MakeTex(new Color(0.04f, 0.05f, 0.07f, 0.96f));
            borderTex = MakeTex(new Color(0.58f, 0.46f, 0.28f, 0.95f));
            headerTex = MakeTex(new Color(0.09f, 0.12f, 0.17f, 0.95f));
            goldTex = MakeTex(new Color(1f, 0.85f, 0.25f, 1f));
            unreadMailTex = MakeTex(new Color(1.0f, 0.82f, 0.15f, 0.95f));

            winTitle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            winTitle.normal.textColor = new Color(1f, 0.85f, 0.3f);

            tabStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            itemRowStyle = new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(8, 8, 4, 4) };

            headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
            headerStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

            priceStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
            priceStyle.normal.textColor = new Color(1f, 0.85f, 0.25f);

            mailSubjectStyle = new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };

            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            bodyStyle.normal.textColor = new Color(0.88f, 0.88f, 0.88f);
        }

        Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        public void DrawAuctionGUI(float screenW, float screenH)
        {
            if (!AuctionOpen || !player) return;
            InitTextures();

            float winW = 820f;
            float winH = 500f;
            float x = (screenW - winW) * 0.5f;
            float y = (screenH - winH) * 0.5f;

            // Frame
            GUI.DrawTexture(new Rect(x - 3, y - 3, winW + 6, winH + 6), borderTex);
            GUI.DrawTexture(new Rect(x, y, winW, winH), bgTex);

            // Header
            GUI.DrawTexture(new Rect(x, y, winW, 46), headerTex);
            GUI.Label(new Rect(x + 16, y + 8, 400, 30), "CAPITAL CITY AUCTION HOUSE", winTitle);

            // Player Gold in Header
            GUI.Label(new Rect(x + winW - 220, y + 12, 160, 24), $"Your Gold: {player.Gold} ðŸª™", priceStyle);

            if (GUI.Button(new Rect(x + winW - 38, y + 8, 28, 28), "X", tabStyle)) AuctionOpen = false;

            // Tabs
            string[] tabs = { "BROWSE", "BIDS", "AUCTIONS (SELL)" };
            float tabW = 140f;
            for (int t = 0; t < tabs.Length; t++)
            {
                bool isSel = selectedTab == t;
                GUI.backgroundColor = isSel ? new Color(0.95f, 0.78f, 0.25f) : new Color(0.35f, 0.40f, 0.45f);
                if (GUI.Button(new Rect(x + 16 + t * (tabW + 6), y + 52, tabW, 30), tabs[t], tabStyle)) selectedTab = t;
            }
            GUI.backgroundColor = Color.white;

            // Category filter pills
            string[] cats = { "All", "Weapon", "Armor", "Consumable", "Trade Goods" };
            float filterX = x + 16 + tabs.Length * (tabW + 6) + 12;
            for (int c = 0; c < cats.Length; c++)
            {
                bool isCat = categoryFilter == cats[c];
                GUI.backgroundColor = isCat ? new Color(0.35f, 0.75f, 0.45f) : new Color(0.25f, 0.28f, 0.32f);
                if (GUI.Button(new Rect(filterX + c * 72, y + 54, 68, 26), cats[c], tabStyle)) categoryFilter = cats[c];
            }
            GUI.backgroundColor = Color.white;

            // Search filter input
            float searchX = filterX + cats.Length * 72 + 16;
            GUI.Label(new Rect(searchX, y + 56, 52, 22), "Search:", headerStyle);
            searchFilter = GUI.TextField(new Rect(searchX + 56, y + 55, 140, 24), searchFilter);

            // Content Area
            float listY = y + 92;
            float listH = winH - (listY - y) - 62;

            // Column Headers
            GUI.DrawTexture(new Rect(x + 16, listY, winW - 32, 26), MakeTex(new Color(0.12f, 0.15f, 0.20f)));
            GUI.Label(new Rect(x + 24, listY + 4, 260, 20), "ITEM NAME", headerStyle);
            GUI.Label(new Rect(x + 300, listY + 4, 120, 20), "SELLER", headerStyle);
            GUI.Label(new Rect(x + 440, listY + 4, 120, 20), "CATEGORY", headerStyle);
            GUI.Label(new Rect(x + 580, listY + 4, 180, 20), "BUYOUT PRICE", headerStyle);

            var filtered = listings.FindAll(l => (categoryFilter == "All" || l.category == categoryFilter) && (string.IsNullOrEmpty(searchFilter) || l.itemName.IndexOf(searchFilter, StringComparison.OrdinalIgnoreCase) >= 0));
            browseScroll = GUI.BeginScrollView(new Rect(x + 16, listY + 28, winW - 32, listH - 28), browseScroll, new Rect(0, 0, winW - 56, filtered.Count * 42));

            for (int i = 0; i < filtered.Count; i++)
            {
                var l = filtered[i];
                bool isSel = selectedListing == l;
                GUI.backgroundColor = isSel ? new Color(0.85f, 0.70f, 0.25f) : (i % 2 == 0 ? new Color(0.18f, 0.22f, 0.26f) : new Color(0.14f, 0.17f, 0.21f));

                if (GUI.Button(new Rect(0, i * 42, winW - 56, 38), "", itemRowStyle)) selectedListing = l;

                // Item Name
                var nameStyle = new GUIStyle(itemRowStyle) { alignment = TextAnchor.MiddleLeft };
                nameStyle.normal.textColor = l.rarityColor;
                GUI.Label(new Rect(8, i * 42 + 9, 270, 22), l.itemName, nameStyle);

                // Seller
                GUI.Label(new Rect(284, i * 42 + 9, 130, 22), l.sellerName, headerStyle);

                // Category
                GUI.Label(new Rect(424, i * 42 + 9, 130, 22), l.category, headerStyle);

                // Price
                GUI.Label(new Rect(564, i * 42 + 9, 180, 22), $"{l.buyoutPrice} Gold ðŸª™", priceStyle);
            }
            GUI.EndScrollView();
            GUI.backgroundColor = Color.white;

            // Bottom Action Bar
            float bottomY = y + winH - 52;
            GUI.DrawTexture(new Rect(x + 16, bottomY, winW - 32, 1), borderTex);

            if (selectedListing != null)
            {
                bool canAfford = player.Gold >= selectedListing.buyoutPrice;
                GUI.backgroundColor = canAfford ? new Color(0.95f, 0.75f, 0.2f) : new Color(0.35f, 0.35f, 0.35f);
                GUI.enabled = canAfford;
                if (GUI.Button(new Rect(x + winW - 220, bottomY + 8, 204, 38), $"BUYOUT ({selectedListing.buyoutPrice}g)", tabStyle))
                {
                    BuyoutListing(selectedListing);
                }
                GUI.enabled = true;
                GUI.backgroundColor = Color.white;
            }
            else
            {
                GUI.Label(new Rect(x + 24, bottomY + 14, 400, 24), "Select a listing to place a bid or buyout.", headerStyle);
            }
        }

        public void DrawMailboxGUI(float screenW, float screenH)
        {
            if (!MailboxOpen || !player) return;
            InitTextures();

            float winW = 680f;
            float winH = 460f;
            float x = (screenW - winW) * 0.5f;
            float y = (screenH - winH) * 0.5f;

            // Frame
            GUI.DrawTexture(new Rect(x - 3, y - 3, winW + 6, winH + 6), borderTex);
            GUI.DrawTexture(new Rect(x, y, winW, winH), bgTex);

            // Header
            GUI.DrawTexture(new Rect(x, y, winW, 44), headerTex);
            GUI.Label(new Rect(x + 16, y + 8, 300, 28), "POSTAL MAILBOX", winTitle);
            if (GUI.Button(new Rect(x + winW - 36, y + 8, 26, 26), "X", tabStyle)) MailboxOpen = false;

            // Left Pane (Inbox List - 260w)
            float leftW = 260f;
            float contentY = y + 52;
            float contentH = winH - 68;

            GUI.DrawTexture(new Rect(x + 14, contentY, leftW, contentH), MakeTex(new Color(0.06f, 0.08f, 0.11f)));
            GUI.DrawTexture(new Rect(x + 14, contentY, leftW, 1), borderTex);

            mailScroll = GUI.BeginScrollView(new Rect(x + 16, contentY + 4, leftW - 4, contentH - 8), mailScroll, new Rect(0, 0, leftW - 24, mailbox.Count * 56));

            for (int m = 0; m < mailbox.Count; m++)
            {
                var mail = mailbox[m];
                bool isSel = selectedMail == mail;
                GUI.backgroundColor = isSel ? new Color(0.85f, 0.70f, 0.25f) : (mail.isRead ? new Color(0.2f, 0.23f, 0.28f) : new Color(0.35f, 0.42f, 0.32f));

                string prefix = (!mail.isClaimed && mail.goldAttachment > 0) ? "ðŸª™ " : (mail.isRead ? "âœ‰ " : "â˜… ");
                if (GUI.Button(new Rect(2, m * 56, leftW - 26, 50), $"{prefix}{mail.subject}\nFrom: {mail.sender}", mailSubjectStyle))
                {
                    selectedMail = mail;
                    mail.isRead = true;
                }
            }
            GUI.EndScrollView();
            GUI.backgroundColor = Color.white;

            // Right Pane (Reading Letter - 370w)
            float rightX = x + 14 + leftW + 12;
            float rightW = winW - (rightX - x) - 14;

            GUI.DrawTexture(new Rect(rightX, contentY, rightW, contentH), MakeTex(new Color(0.07f, 0.09f, 0.12f)));
            GUI.DrawTexture(new Rect(rightX, contentY, rightW, 1), borderTex);

            if (selectedMail != null)
            {
                float dy = contentY + 12;
                GUI.Label(new Rect(rightX + 12, dy, rightW - 24, 22), selectedMail.subject, winTitle);
                dy += 26;
                GUI.Label(new Rect(rightX + 12, dy, rightW - 24, 18), $"From: {selectedMail.sender}", headerStyle);
                dy += 26;

                // Separator
                GUI.DrawTexture(new Rect(rightX + 12, dy, rightW - 24, 1), borderTex);
                dy += 10;

                // Letter Body
                GUI.Label(new Rect(rightX + 12, dy, rightW - 24, 180), selectedMail.body, bodyStyle);
                dy += 190;

                // Attached Money / Items
                if (selectedMail.goldAttachment > 0 && !selectedMail.isClaimed)
                {
                    GUI.DrawTexture(new Rect(rightX + 12, dy, rightW - 24, 46), MakeTex(new Color(0.15f, 0.22f, 0.18f)));
                    GUI.Label(new Rect(rightX + 22, dy + 12, 180, 24), $"Attached: {selectedMail.goldAttachment} Gold ðŸª™", priceStyle);

                    GUI.backgroundColor = new Color(0.95f, 0.78f, 0.25f);
                    if (GUI.Button(new Rect(rightX + rightW - 130, dy + 8, 118, 30), "TAKE GOLD", tabStyle))
                    {
                        ClaimMailAttachment(selectedMail);
                    }
                    GUI.backgroundColor = Color.white;
                }
                else if (selectedMail.isClaimed)
                {
                    GUI.Label(new Rect(rightX + 12, dy + 12, 200, 24), "(All attachments claimed)", headerStyle);
                }
            }
            else
            {
                GUI.Label(new Rect(rightX + 20, contentY + 40, rightW - 40, 30), "Select a letter from the inbox to read its contents.", headerStyle);
            }
        }
    }
}
