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
        [ExportGroup("Chat Window")]
        [Export] private LineEdit chatMessage;
        [Export] private TextEdit chatField;

        public bool IsHost;

        uint currentMembers;

        float m_PlatformTickTimer = 0f;
        float m_ForcePeerConnectionTickTimer = 0f;

        public override void _PhysicsProcess(double delta)
        {
            if (!Visible) return;
            if (EOSManager.Instance == null) return;
            if (EOSManager.Instance.lobbyDetails == null) return;

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

                GetLobbyMembers();
                ReadChatMessage();
            }
        }

        void GetLobbyMembers()
        {
            var info = EOSManager.Instance.GetLobbyDetailsInfo(EOSManager.Instance.lobbyDetails);
            IsHost = EOSManager.Instance.userID == info.Value.LobbyOwnerUserId;

            var lb_MemberCount = new LobbyDetailsGetMemberCountOptions();
            currentMembers = EOSManager.Instance.lobbyDetails.GetMemberCount(ref lb_MemberCount);

            lobbyName.Text = info.Value.BucketId;
            lobbyMemberCount.Text = $"{currentMembers}/{info.Value.MaxMembers} Members";

            lobbyAttributes.Text = "Attributes\n" + 
                $"Mode: {EOSManager.Instance.GetLobbyAttribute(ref EOSManager.Instance.lobbyDetails, "GameMode").AsUtf8}\n" +
                $"Stocks: {EOSManager.Instance.GetLobbyAttribute(ref EOSManager.Instance.lobbyDetails, "Stocks").AsInt64}\n" +
                $"Use Items: {EOSManager.Instance.GetLobbyAttribute(ref EOSManager.Instance.lobbyDetails, "allowItems").AsBool}";

            for (int i = 0; i < lobbyMemberButtons.Length; i++)
            {
                lobbyMemberButtons[i].Clear();
                if (i >= currentMembers) continue;

                var playerOptions = new LobbyDetailsGetMemberByIndexOptions();
                playerOptions.MemberIndex = (uint)i;
                var player = EOSManager.Instance.lobbyDetails.GetMemberByIndex(ref playerOptions);
                if (player == null) return;

                EOSManager.Instance.lobbyMembers[i] = player;
                EOSManager.Instance.RequestConnection(player);

                bool? isReady = EOSManager.Instance.GetLobbyMemberAttribute(ref EOSManager.Instance.lobbyDetails, player, "Ready").AsBool;
                string playerName = EOSManager.Instance.GetLobbyMemberAttribute(ref EOSManager.Instance.lobbyDetails, player, "PlayerName").AsUtf8;
                string charName = EOSManager.Instance.GetLobbyMemberAttribute(ref EOSManager.Instance.lobbyDetails, player, "CharacterName").AsUtf8;

                lobbyMemberButtons[i].Set(
                    player == info.Value.LobbyOwnerUserId, isReady == null ? false : isReady.Value, player == EOSManager.Instance.userID, 
                    playerName == null ? "" : playerName, charName == null ? "" : charName
                );
            }
        }

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
            }
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
    }
}