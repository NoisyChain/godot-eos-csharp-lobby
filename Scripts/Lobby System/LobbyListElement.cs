using Godot;
using System;

namespace LobbySystem
{
    public partial class LobbyListElement : Button
    {
        [Export] private Label lobbyName;
        [Export] private Label gameMode;
        [Export] private Label members;
        [Export] private LobbyCreationMenu menu;
        private string lobbyID;

        public void Set(string name, string mode, uint memberCount, uint maxMembers, string id, LobbyCreationMenu lobbyMenu)
        {
            menu = lobbyMenu;
            lobbyID = id;
            lobbyName.Text = name;
            gameMode.Text = mode;
            members.Text = $"{memberCount}/{maxMembers} members";
        }

        public void _on_pressed()
        {
            menu.JoinLobby(lobbyID);
        }
    }
}
