using Epic.OnlineServices.Lobby;
using Godot;
using System;
using System.Collections.Generic;

namespace LobbySystem
{
    public partial class LobbySearch : ScrollContainer
    {
        [Export] private PackedScene lobbyListElement;
        [Export] private VBoxContainer contents;

        bool lobbyListUpdated = true;

        List<LobbyListElement> InstantiatedButtons = new List<LobbyListElement>();

        public override void _PhysicsProcess(double delta)
        {
            if (!Visible) return;
            if (EOSManager.Instance.foundLobbies.Length > 0)
                UpdateLobbyList();
        }

        public void _on_lobby_refresh_pressed()
        {
            SearchForLobbies();
        }

        private void SearchForLobbies()
        {
            lobbyListUpdated = false;
            ClearLobbyList();
            EOSManager.Instance.SearchLobbies();
        }

        public void UpdateLobbyList()
        {
            if (lobbyListUpdated) return;
            
            for (int i = 0; i < EOSManager.Instance.foundLobbies.Length; i++)
            {
                var det = EOSManager.Instance.foundLobbies[i];
                Node lobbyInstance = lobbyListElement.Instantiate();
                contents.AddChild(lobbyInstance);
                LobbyListElement lobbyButton = lobbyInstance as LobbyListElement;
                
                var info = EOSManager.Instance.GetLobbyDetailsInfo(det);
                var count = new LobbyDetailsGetMemberCountOptions();
                uint currentMembers = det.GetMemberCount(ref count);
                lobbyButton.Set(
                    info.Value.BucketId,
                    EOSManager.Instance.GetLobbyAttribute(ref det, "GameMode").AsUtf8,
                    currentMembers, info.Value.MaxMembers,
                    info.Value.LobbyId,
                    GetParent() as LobbyCreationMenu
                );
                GD.Print($"Lobby found: ({info.Value.BucketId}, {info.Value.LobbyId})");
                InstantiatedButtons.Add(lobbyButton);
            }

            lobbyListUpdated = true;
        }

        public void ClearLobbyList()
        {
            for (int i = 0; i < InstantiatedButtons.Count; i++)
            {
                InstantiatedButtons[i].QueueFree();
            }
            InstantiatedButtons.Clear();
        }
    }
}
