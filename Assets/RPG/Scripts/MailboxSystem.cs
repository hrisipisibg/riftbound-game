using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public class MailboxSystem : MonoBehaviour
    {
        static MailboxSystem _instance;
        public static MailboxSystem Instance
        {
            get
            {
                if (!_instance) _instance = FindAnyObjectByType<MailboxSystem>();
                return _instance;
            }
            private set => _instance = value;
        }

        [Serializable]
        public class MailEntry
        {
            public string id;
            public string sender;
            public string subject;
            public string body;
            public int goldAttachment;
            public string itemAttachment;
            public bool isRead;
            public bool goldCollected;
            public bool itemCollected;
            public float timeReceived;
        }

        List<MailEntry> inbox = new List<MailEntry>();
        public IReadOnlyList<MailEntry> Inbox
        {
            get
            {
                if (inbox.Count == 0) SeedInitialMail();
                return inbox;
            }
        }

        public bool IsOpen { get; set; }
        public MailEntry SelectedMail { get; private set; }

        PlayerController player;
        Vector2 scrollPos;
        GUIStyle windowStyle, titleStyle, headerStyle, parchmentStyle, mailBtnStyle, attachBtnStyle;
        Texture2D solidTex;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying) Destroy(this); else DestroyImmediate(this);
                return;
            }
            Instance = this;
            player = GetComponent<PlayerController>();
            SeedInitialMail();
        }

        void Start()
        {
            if (inbox.Count == 0) SeedInitialMail();
        }

        public void SeedInitialMail(bool force = false)
        {
            if (force) inbox.Clear();
            if (inbox.Count >= 3) return;
            if (inbox.Count > 0) inbox.Clear();

            // Letter 1: Grand Duke Ronald
            inbox.Add(new MailEntry
            {
                id = "mail-ronald-1",
                sender = "Grand Duke Ronald",
                subject = "Commendation of Highspire",
                body = "Greetings, Champion of Emberwatch.\n\nYour valiant efforts against the bandits along the road have reached the ears of Highspire Keep. As a token of the Royal Garrison's appreciation, enclosed is a royal bounty. Spend it well to hone your blade for greater trials ahead.\n\nFor the Alliance,\nGrand Duke Ronald",
                goldAttachment = 50,
                itemAttachment = "",
                isRead = false,
                goldCollected = false,
                itemCollected = false,
                timeReceived = Time.time
            });

            // Letter 2: Archmage Karen
            inbox.Add(new MailEntry
            {
                id = "mail-karen-1",
                sender = "Archmage Karen",
                subject = "Arcane Inquiries & Supplies",
                body = "Salutations.\n\nThe ley-lines beneath Duskwood and the Cathedral ruins are stirring with volatile energies. I require competent adventurers to probe the outer rifts. Accept these supplies from the Kirin Tor archives to bolster your mana reserves.\n\nArchmage Karen\nRoyal Arcanist",
                goldAttachment = 25,
                itemAttachment = "crypt-signet",
                isRead = false,
                goldCollected = false,
                itemCollected = false,
                timeReceived = Time.time
            });

            // Letter 3: Warlord Gorgash
            inbox.Add(new MailEntry
            {
                id = "mail-gorgash-1",
                sender = "Warlord Gorgash",
                subject = "Challenge of the Bloodrock",
                body = "Listen well, warrior.\n\nCamp Bloodrock does not welcome cowards, but strength speaks louder than banners. Prove your prowess in the red crags. Enclosed is tribute from the Ashen Pact. Do not waste it.\n\nBlood and Honor,\nWarlord Gorgash",
                goldAttachment = 35,
                itemAttachment = "",
                isRead = false,
                goldCollected = false,
                itemCollected = false,
                timeReceived = Time.time
            });
        }

        public bool HasUnreadMail()
        {
            if (inbox.Count == 0) SeedInitialMail();
            foreach (var m in inbox)
            {
                if (!m.isRead || (!m.goldCollected && m.goldAttachment > 0) || (!m.itemCollected && !string.IsNullOrEmpty(m.itemAttachment)))
                    return true;
            }
            return false;
        }

        public int UnreadCount()
        {
            if (inbox.Count == 0) SeedInitialMail();
            int count = 0;
            foreach (var m in inbox)
            {
                if (!m.isRead) count++;
            }
            return count;
        }

        public void SendMail(string recipient, string subject, string body, int gold = 0, string itemId = "")
        {
            if (inbox.Count == 0) SeedInitialMail();
            var entry = new MailEntry
            {
                id = Guid.NewGuid().ToString(),
                sender = string.IsNullOrEmpty(recipient) ? "Auction House Clerk" : recipient,
                subject = subject,
                body = body,
                goldAttachment = gold,
                itemAttachment = itemId,
                isRead = false,
                goldCollected = false,
                itemCollected = false,
                timeReceived = Time.time
            };

            inbox.Insert(0, entry);
            AudioEffects.PlayLootCoin();
            ChatSystem.LogSystem($"<color=#ffd100>✉ New mail received from {entry.sender}: '{entry.subject}'</color>");
            if (ZoneBanner.Instance) ZoneBanner.Show("NEW MAIL RECEIVED", $"From {entry.sender}");
        }

        public void CollectGold(MailEntry mail)
        {
            if (mail == null || mail.goldCollected || mail.goldAttachment <= 0) return;
            if (!player) player = GetComponent<PlayerController>() ?? FindAnyObjectByType<PlayerController>();

            if (player)
            {
                player.AddGoldDirect(mail.goldAttachment);
                mail.goldCollected = true;
                AudioEffects.PlayLootCoin();
                ChatSystem.LogSystem($"<color=#ffd100>★ Collected {mail.goldAttachment} Gold from mail attachment.</color>");
            }
        }

        public void CollectItem(MailEntry mail)
        {
            if (mail == null || mail.itemCollected || string.IsNullOrEmpty(mail.itemAttachment)) return;
            if (!player) player = GetComponent<PlayerController>() ?? FindAnyObjectByType<PlayerController>();

            if (player)
            {
                player.GrantItem(mail.itemAttachment);
                mail.itemCollected = true;
                AudioEffects.PlayQuestComplete();
                var gear = ItemCatalog.Get(mail.itemAttachment);
                string itemName = gear != null ? gear.name : mail.itemAttachment;
                ChatSystem.LogSystem($"<color=#ffd100>★ Collected [{itemName}] from mail attachment.</color>");
            }
        }

        public void DeleteMail(MailEntry mail)
        {
            if (mail == null) return;
            inbox.Remove(mail);
            if (SelectedMail == mail) SelectedMail = null;
            AudioEffects.PlayClick();
            ChatSystem.LogSystem("<color=#aaaaaa>[Mailbox] Message deleted.</color>");
        }

        public void DrawMailboxGUI(float screenWidth, float screenHeight)
        {
            if (!IsOpen) return;

            InitStyles();

            float winW = 680f, winH = 460f;
            float winX = (screenWidth - winW) * 0.5f, winY = (screenHeight - winH) * 0.5f;

            // Background window
            Fill(new Rect(winX, winY, winW, winH), new Color(0.06f, 0.08f, 0.11f, 0.96f));
            Fill(new Rect(winX, winY, winW, 2.5f), new Color(0.85f, 0.70f, 0.28f, 0.95f));
            Fill(new Rect(winX, winY + winH - 2f, winW, 2f), new Color(0.40f, 0.32f, 0.15f, 0.95f));

            // Header
            GUI.Label(new Rect(winX + 24f, winY + 14f, 400f, 26f), "✉ MAILBOX & POSTAL SERVICE", titleStyle);
            int unread = UnreadCount();
            GUI.Label(new Rect(winX + 440f, winY + 18f, 150f, 20f), $"{inbox.Count} Messages ({unread} Unread)", headerStyle);

            if (GUI.Button(new Rect(winX + winW - 38f, winY + 12f, 26f, 24f), "✕", GUI.skin.button))
            {
                IsOpen = false;
                AudioEffects.PlayClick();
                return;
            }

            // Left Side: Mail List
            float listW = 270f, listH = winH - 85f;
            Fill(new Rect(winX + 20f, winY + 50f, listW, listH), new Color(0.09f, 0.11f, 0.15f, 0.92f));

            scrollPos = GUI.BeginScrollView(new Rect(winX + 20f, winY + 50f, listW, listH), scrollPos, new Rect(0, 0, listW - 20f, inbox.Count * 48f));
            for (int i = 0; i < inbox.Count; i++)
            {
                var mail = inbox[i];
                float my = i * 48f;
                bool isSel = (SelectedMail == mail);

                Color itemBg = isSel ? new Color(0.25f, 0.22f, 0.14f, 0.95f) :
                               mail.isRead ? new Color(0.12f, 0.14f, 0.18f, 0.90f) :
                               new Color(0.18f, 0.22f, 0.28f, 0.95f);

                Fill(new Rect(0, my, listW - 20f, 44f), itemBg);

                string readIcon = mail.isRead ? "✉" : "📬";
                string attachIcon = (mail.goldAttachment > 0 && !mail.goldCollected) || (!string.IsNullOrEmpty(mail.itemAttachment) && !mail.itemCollected) ? "📎" : "";

                if (GUI.Button(new Rect(0, my, listW - 20f, 44f), "", GUI.skin.label))
                {
                    SelectedMail = mail;
                    mail.isRead = true;
                    AudioEffects.PlayClick();
                }

                GUI.Label(new Rect(6f, my + 4f, 24f, 20f), readIcon, headerStyle);
                GUI.Label(new Rect(32f, my + 4f, 190f, 18f), mail.sender, headerStyle);
                if (!string.IsNullOrEmpty(attachIcon))
                    GUI.Label(new Rect(listW - 48f, my + 4f, 20f, 18f), attachIcon, headerStyle);

                GUI.Label(new Rect(32f, my + 22f, listW - 55f, 16f), mail.subject, GUI.skin.label);
            }
            GUI.EndScrollView();

            // Right Side: Letter Reader
            float readX = winX + listW + 36f, readY = winY + 50f;
            float readW = winW - listW - 56f, readH = winH - 85f;

            Fill(new Rect(readX, readY, readW, readH), new Color(0.13f, 0.12f, 0.10f, 0.95f));
            Fill(new Rect(readX, readY, readW, 1.5f), new Color(0.6f, 0.5f, 0.3f, 0.8f));

            if (SelectedMail != null)
            {
                // Letter details
                GUI.Label(new Rect(readX + 16f, readY + 12f, readW - 32f, 20f), $"<b>Subject:</b> {SelectedMail.subject}", headerStyle);
                GUI.Label(new Rect(readX + 16f, readY + 32f, readW - 32f, 18f), $"<b>From:</b> {SelectedMail.sender}", GUI.skin.label);
                Fill(new Rect(readX + 16f, readY + 54f, readW - 32f, 1f), new Color(0.35f, 0.30f, 0.22f, 0.8f));

                // Letter Body
                GUI.Label(new Rect(readX + 16f, readY + 62f, readW - 32f, 220f), SelectedMail.body, parchmentStyle);

                // Attachments Footer
                float attachY = readY + readH - 72f;
                Fill(new Rect(readX + 12f, attachY, readW - 24f, 60f), new Color(0.08f, 0.08f, 0.09f, 0.9f));

                bool hasGold = SelectedMail.goldAttachment > 0;
                bool hasItem = !string.IsNullOrEmpty(SelectedMail.itemAttachment);

                if (hasGold)
                {
                    string goldText = SelectedMail.goldCollected ? $"💰 {SelectedMail.goldAttachment} Gold (Collected)" : $"💰 {SelectedMail.goldAttachment} Gold";
                    GUI.Label(new Rect(readX + 20f, attachY + 8f, 160f, 20f), goldText, headerStyle);

                    if (!SelectedMail.goldCollected && GUI.Button(new Rect(readX + 20f, attachY + 30f, 110f, 22f), "COLLECT GOLD", attachBtnStyle))
                    {
                        CollectGold(SelectedMail);
                    }
                }

                if (hasItem)
                {
                    var gear = ItemCatalog.Get(SelectedMail.itemAttachment);
                    string itemName = gear != null ? gear.name : SelectedMail.itemAttachment;
                    string itemText = SelectedMail.itemCollected ? $"💎 [{itemName}] (Claimed)" : $"💎 [{itemName}]";
                    GUI.Label(new Rect(readX + 170f, attachY + 8f, 180f, 20f), itemText, headerStyle);

                    if (!SelectedMail.itemCollected && GUI.Button(new Rect(readX + 170f, attachY + 30f, 110f, 22f), "CLAIM ITEM", attachBtnStyle))
                    {
                        CollectItem(SelectedMail);
                    }
                }

                if (!hasGold && !hasItem)
                {
                    GUI.Label(new Rect(readX + 20f, attachY + 20f, 200f, 20f), "<i>No attachments enclosed.</i>", GUI.skin.label);
                }

                // Delete Button
                if (GUI.Button(new Rect(readX + readW - 90f, attachY + 30f, 76f, 22f), "DELETE", GUI.skin.button))
                {
                    DeleteMail(SelectedMail);
                }
            }
            else
            {
                GUI.Label(new Rect(readX + 30f, readY + 120f, readW - 60f, 40f), "Select a message from your inbox to read it.", headerStyle);
            }
        }

        void InitStyles()
        {
            if (solidTex == null)
            {
                solidTex = new Texture2D(1, 1);
                solidTex.SetPixel(0, 0, Color.white);
                solidTex.Apply();
            }

            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(1.0f, 0.85f, 0.35f) }
                };
            }

            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.9f, 0.9f, 0.85f) }
                };
            }

            if (parchmentStyle == null)
            {
                parchmentStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    wordWrap = true,
                    normal = { textColor = new Color(0.92f, 0.88f, 0.78f) }
                };
            }

            if (attachBtnStyle == null)
            {
                attachBtnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 10,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }
        }

        void Fill(Rect rect, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, solidTex);
            GUI.color = old;
        }
    }
}
