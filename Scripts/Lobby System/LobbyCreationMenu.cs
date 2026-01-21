using Godot;
using System;

namespace LobbySystem
{
    public partial class LobbyCreationMenu : Control
    {
        [Export] private OptionButton characterSelect;
        [ExportGroup("Create a lobby")]
        [Export] private LineEdit lobbyName;
        [Export] private OptionButton lobbyMaxMembers;
        [Export] private CheckButton isPrivate;
        [Export] private CheckBox canInvite;
        [Export] private CheckBox canCrossplay;

        [Export] private OptionButton gameMode;
        [Export] private OptionButton stocks;
        [Export] private CheckBox allowItems;
        [ExportGroup("Join a lobby")]
        [Export] private LineEdit lobbyId;

        public void _on_lobby_create_pressed()
        {
            CreateLobby();
        }

        public void _on_lobby_join_pressed()
        {
            JoinLobby(lobbyId.Text);
        }

        public void CreateLobby()
        {
            AddPlayerAttributes();
            AddLobbyAttributes();
            EOSManager.Instance.CreateNewLobby(lobbyName.Text, lobbyMaxMembers.Selected + 2, !isPrivate.ButtonPressed, canInvite.ButtonPressed, !canCrossplay.ButtonPressed);
        }

        public void JoinLobby(string lobbyID)
        {
            AddPlayerAttributes();
            EOSManager.Instance.JoinLobby(lobbyID);
        }

        private void AddPlayerAttributes()
        {
            EOSManager.Instance.playerAttributes.Clear();
            EOSManager.Instance.playerAttributes.Add(new EOSManager.LobbyAttributeStorage{ Key = "PlayerName", Type = 0, ValueString = EOSManager.Instance.playerName });
            EOSManager.Instance.playerAttributes.Add(new EOSManager.LobbyAttributeStorage{ Key = "CharacterName", Type = 0, ValueString = characterSelect.Text });
            EOSManager.Instance.playerAttributes.Add(new EOSManager.LobbyAttributeStorage{ Key = "Ready", Type = 1, ValueBool = false });
        }

        private void AddLobbyAttributes()
        {
            EOSManager.Instance.lobbyAttributes.Clear();
            EOSManager.Instance.lobbyAttributes.Add(new EOSManager.LobbyAttributeStorage{ Key = "Searchable", Type = 1, ValueBool = !isPrivate.ButtonPressed });
            EOSManager.Instance.lobbyAttributes.Add(new EOSManager.LobbyAttributeStorage{ Key = "GameMode", Type = 0, ValueString = gameMode.Text });
            EOSManager.Instance.lobbyAttributes.Add(new EOSManager.LobbyAttributeStorage{ Key = "Stocks", Type = 2, ValueLong = stocks.Selected + 1 });
            EOSManager.Instance.lobbyAttributes.Add(new EOSManager.LobbyAttributeStorage{ Key = "AllowItems", Type = 1, ValueBool = allowItems.ButtonPressed });
        }
    }
}
