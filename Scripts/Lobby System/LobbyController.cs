using Godot;
using Epic.OnlineServices;

namespace LobbySystem
{
    public partial class LobbyController : Control
    {
        [Export] private Control LoginScreen;
        [Export] private Control LobbyCreationMenu;
        [Export] private Control LobbyScreen;
        
        public override void _PhysicsProcess(double delta)
        {
            if (EOSManager.Instance == null) return;
            
            switch (EOSManager.Instance.status)
            {
                case EOSManager.ConnectionStatus.Succesful:
                    LoginScreen.Visible = false;
                    LobbyCreationMenu.Visible = true;
                    LobbyScreen.Visible = false;
                    break;
                case EOSManager.ConnectionStatus.Lobby:
                    LoginScreen.Visible = false;
                    LobbyCreationMenu.Visible = false;
                    LobbyScreen.Visible = true;
                    break;
                default:
                    LoginScreen.Visible = true;
                    LobbyCreationMenu.Visible = false;
                    LobbyScreen.Visible = false;
                    break;
            }
        }
    }
}
