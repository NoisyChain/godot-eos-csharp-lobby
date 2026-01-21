using Godot;
using System;

namespace LobbySystem
{
    public partial class LobbyMember : Button
    {
        //[Export] private Button body;
        [Export] private Control portrait;
        [Export] private Label playerName;
        [Export] private Label characterName;
        [Export] private Control ownerIndicator;
        [Export] private Control hostIndicator;
        [Export] private Control readyIndicator;

        public void Clear()
        {
            Disabled = true;
            portrait.Visible = false;
            playerName.Text = "";
            playerName.Visible = false;
            characterName.Text = "";
            characterName.Visible = false;
            ownerIndicator.Visible = false;
            hostIndicator.Visible = false;
            readyIndicator.Visible = false;
        }

        public void Set(bool isHost, bool isReady, bool isMe, string player, string character)
        {
            Disabled = false;
            portrait.Visible = true;
            playerName.Text = player == null ? "" : player;
            playerName.Visible = true;
            characterName.Text = character == null ? "" : character;
            characterName.Visible = true;
            ownerIndicator.Visible = isMe;
            hostIndicator.Visible = isHost;
            readyIndicator.Visible = isReady;
        }
    }
}
