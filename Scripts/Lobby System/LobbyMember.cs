using Godot;
using Epic.OnlineServices;

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

        private ProductUserId ID;
        [Export] private LobbyScreen parent;

        public void Clear()
        {
            ID = null;
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

        public void Set(ProductUserId id, bool isHost, bool isReady, bool isMe, string player, string character)
        {
            ID = id;
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

        public void _on_button_down()
        {
            if (parent == null) return;

            parent.SetPlayerContextMenu(ID);
        }
    }
}
