using Godot;
using System;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;

namespace LobbySystem
{
    public partial class LobbyScreen : Control
    {
        [Export] private LobbyMember[] lobbyMemberButtons;
        [Export] private Label lobbyName;
        [Export] private Label lobbyMemberCount;
        [Export] private Label lobbyAttributes;
        [Export] private LineEdit lobbyIdSpace;
        [Export] private VBoxContainer playerContextMenu;
        [ExportGroup("Chat Window")]
        [Export] private LineEdit chatMessage;
        [Export] private TextEdit chatField;

        LobbyDetailsInfo? info;

        public bool IsHost;
        public ProductUserId HostID;

        uint currentMembers;

        float m_PlatformTickTimer = 0f;
        float m_ForcePeerConnectionTickTimer = 0f;
        private ProductUserId playerContextInfo;

        public override void _PhysicsProcess(double delta)
        {
            if (!Visible) return;
            if (EOSManager.Instance == null) return;
            if (EOSManager.Instance.lobbyDetails == null) return;

            info = EOSManager.Instance.GetLobbyDetailsInfo(EOSManager.Instance.lobbyDetails);

            if (Input.IsActionJustPressed("send"))
            {
                _on_chat_send_message_pressed();
            }
            
            m_PlatformTickTimer += (float)delta;
            if (m_PlatformTickTimer >= 0.1f)
            {
                m_PlatformTickTimer = 0f;
                if (lobbyIdSpace.Text != EOSManager.Instance.lobbyID)
                    lobbyIdSpace.Text = EOSManager.Instance.lobbyID;

                GetLobbyInfo();
                UpdateLobbyMembers();
                //CheckKicked();
                ReadChatMessage();
            }

            if (!IsHost) GD.Print(currentMembers);
        }

        void GetLobbyInfo()
        {
            IsHost = EOSManager.Instance.userID == info.Value.LobbyOwnerUserId;
            HostID = info.Value.LobbyOwnerUserId;

            var lb_MemberCount = new LobbyDetailsGetMemberCountOptions();
            currentMembers = EOSManager.Instance.lobbyDetails.GetMemberCount(ref lb_MemberCount);

            lobbyName.Text = info.Value.BucketId;
            lobbyMemberCount.Text = $"{currentMembers}/{info.Value.MaxMembers} Members";

            lobbyAttributes.Text = "Attributes\n" + 
                $"Mode: {EOSManager.Instance.GetLobbyAttribute(ref EOSManager.Instance.lobbyDetails, "GameMode").AsUtf8}\n" +
                $"Stocks: {EOSManager.Instance.GetLobbyAttribute(ref EOSManager.Instance.lobbyDetails, "Stocks").AsInt64}\n" +
                $"Use Items: {EOSManager.Instance.GetLobbyAttribute(ref EOSManager.Instance.lobbyDetails, "allowItems").AsBool}";
            
            for (int i = 0; i < EOSManager.MAX_LOBBY_MEMBERS; i++)
            {
                if (i >= currentMembers)
                    EOSManager.Instance.lobbyMembers[i] = null;
                else
                {
                    var playerOptions = new LobbyDetailsGetMemberByIndexOptions();
                    playerOptions.MemberIndex = (uint)i;
                    var player = EOSManager.Instance.lobbyDetails.GetMemberByIndex(ref playerOptions);
                    EOSManager.Instance.lobbyMembers[i] = player;
                }
            }
        }

        void UpdateLobbyMembers()
        {
            for (int i = 0; i < EOSManager.Instance.lobbyMembers.Length; i++)
            {
                if (EOSManager.Instance.lobbyMembers[i] == null)
                {
                    lobbyMemberButtons[i].Clear();
                    continue;
                }

                EOSManager.Instance.RequestConnection(EOSManager.Instance.lobbyMembers[i]);

                bool? isReady = EOSManager.Instance.GetLobbyMemberAttribute(ref EOSManager.Instance.lobbyDetails, EOSManager.Instance.lobbyMembers[i], "Ready").AsBool;
                string playerName = EOSManager.Instance.GetLobbyMemberAttribute(ref EOSManager.Instance.lobbyDetails, EOSManager.Instance.lobbyMembers[i], "PlayerName").AsUtf8;
                string charName = EOSManager.Instance.GetLobbyMemberAttribute(ref EOSManager.Instance.lobbyDetails, EOSManager.Instance.lobbyMembers[i], "CharacterName").AsUtf8;

                lobbyMemberButtons[i].Set(
                    EOSManager.Instance.lobbyMembers[i], // Player ID
                    EOSManager.Instance.lobbyMembers[i] == info.Value.LobbyOwnerUserId, // Is host
                    isReady == null ? false : isReady.Value, // Is ready
                    EOSManager.Instance.lobbyMembers[i] == EOSManager.Instance.userID, // Is me
                    playerName == null ? "" : playerName, // Player name
                    charName == null ? "" : charName // Character name
                );
            }
        }

        public void KickFromLobby(ProductUserId memberToKick)
        {
            if (!IsHost)
            {
                GD.Print("Only the host can kick players");
                playerContextMenu.Visible = false;
                return;
            }

            if (memberToKick == HostID)
            {
                GD.Print("You are the host, you cannot kick yourself from the lobby");
                playerContextMenu.Visible = false;
                return;
            }

            string playerName = EOSManager.Instance.GetLobbyMemberAttribute(ref EOSManager.Instance.lobbyDetails, memberToKick, "PlayerName").AsUtf8;
            EOSManager.Instance.KickFromLobby(EOSManager.Instance.lobbyID, HostID, memberToKick);
            SendChatMessageToAllMembers($"\nPlayer {playerName} has been kicked from the server.");
            playerContextMenu.Visible = false;
        }

        public void Promote(ProductUserId memberToPromote)
        {
            if (!IsHost)
            {
                GD.Print("Only the host can promote players");
                playerContextMenu.Visible = false;
                return;
            }

            if (memberToPromote == HostID)
            {
                GD.Print("You are already the host.");
                playerContextMenu.Visible = false;
                return;
            }

            string playerName = EOSManager.Instance.GetLobbyMemberAttribute(ref EOSManager.Instance.lobbyDetails, memberToPromote, "PlayerName").AsUtf8;
            EOSManager.Instance.PromoteLobbyMember(EOSManager.Instance.lobbyID, HostID, memberToPromote);
            SendChatMessageToAllMembers($"\nPlayer {playerName} has been promoted.");
            playerContextMenu.Visible = false;
        }

        /*public void CheckKicked()
        {
            EOSManager.Instance.CheckGotKickedFromLobby(HostID);
        }*/

        private void SendChatMessageToAllMembers(string message)
        {
            foreach(ProductUserId id in EOSManager.Instance.lobbyMembers)
            {
                if (id == null) continue;

                SendChatMessage(id, message);
            }
        }

        private void SendChatMessage(ProductUserId id, string message)
        {
            byte[] packet = System.Text.Encoding.UTF8.GetBytes(message);
            EOSManager.Instance.SendPacket(id, packet, 2);
        }

        private void ReadChatMessage()
        {
            while(EOSManager.Instance.PacketsStillQueued())
            {
                byte[] packetArray = EOSManager.Instance.ReceivePacket(out ProductUserId senderId);

                if (packetArray == null || senderId == null)
                {
                    GD.PrintErr($"Packet is empty? ID: {senderId}");
                    break;
                }
                string message = System.Text.Encoding.UTF8.GetString(packetArray);

                chatField.Text += message;
                chatField.ScrollVertical = chatField.GetLineCount();

                // Use the message to check if the kicked player should leave the lobby screen
                // This can be done in a way better way but for now it works
                if (message.Contains(EOSManager.Instance.playerName) && message.Contains("kicked"))
                {
                    EOSManager.Instance.GotKickedFromServer();
                }
            }
        }

        public void SetPlayerContextMenu(ProductUserId playerCTX)
        {
            playerContextInfo = playerCTX;
            playerContextMenu.Position = GetViewport().GetMousePosition();
            playerContextMenu.Visible = true;
        }

        public void _on_lobby_quit_pressed()
        {
            SendChatMessageToAllMembers($"\n{EOSManager.Instance.playerName} has left the lobby.");
            EOSManager.Instance.QuitLobby();
        }

        public void _on_lobby_ready_pressed()
        {
            EOSManager.Instance.AddLobbyMemberAttribute("Ready", true);
        }

        public void _on_chat_send_message_pressed()
        {
            string msg = $"\n{EOSManager.Instance.playerName}: {chatMessage.Text}";
            chatMessage.Text = "";
            SendChatMessageToAllMembers(msg);
            
        }

        public void _on_ctx_button_kick_pressed()
        {
            KickFromLobby(playerContextInfo);
        }

        public void _on_ctx_button_promote_pressed()
        {
            Promote(playerContextInfo);
        }
    }
}