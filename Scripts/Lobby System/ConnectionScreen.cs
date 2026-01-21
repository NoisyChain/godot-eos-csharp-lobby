using Godot;
using System;

namespace LobbySystem
{
    public partial class ConnectionScreen : Control
    {
        [Export] private LineEdit playerName;
        [Export] private OptionButton characterSelect;

        public void _on_login_direct_pressed()
        {
            EOSManager.Instance.playerName = playerName.Text;
            EOSManager.Instance.DirectConnection();
        }

        public void _on_login_epic_pressed()
        {
            EOSManager.Instance.LoginEpicGames();
        }
    }
}
