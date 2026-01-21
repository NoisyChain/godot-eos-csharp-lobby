// This code is provided for demonstration purposes and is not intended to represent ideal practices.
using Epic.OnlineServices;
using Epic.OnlineServices.Auth;
using Epic.OnlineServices.Logging;
using Epic.OnlineServices.Platform;
using Epic.OnlineServices.Lobby;
using Epic.OnlineServices.P2P;
using Epic.OnlineServices.UserInfo;
using System;
using Godot;
using System.Collections.Generic;

public partial class EOSManager : Node
{
    public static EOSManager Instance { get; private set; }

    public const int MAX_LOBBY_MEMBERS = 12;
    public const int MAX_LOBBY_SEARCH_RESULTS = 50;

    // Set these values as appropriate. For more information, see the Developer Portal documentation.
    public string m_ProductName = "TestProduct";
    public string m_ProductVersion = "1.0";
    public string m_ProductId = "56952852deb74b39b67fb8d26093fb78";
    public string m_SandboxId = "810c0ec7bcf64e67abe6bb1cfc20dc43";
    public string m_DeploymentId = "229c0501b8044293baeee7755e632681";
    public string m_ClientId = "xyza78911kf0MQN4fBNqGFTKNb9h4HY7";
    public string m_ClientSecret = "CVPiTu3v8MfHX5nhLfEtSNlAa+9QZI1Kx2bEgm9ZU5Q";
    public LoginCredentialType m_LoginCredentialType = LoginCredentialType.AccountPortal;
    public ExternalCredentialType m_ExternalCredentialType = ExternalCredentialType.DeviceidAccessToken;
    // These fields correspond to \<see cref="Credentials.Id" /> and \<see cref="Credentials.Token" />,
    // and their use differs based on the login type. For more information, see \<see cref="Credentials" />
    // and the Auth Interface documentation.
    public string m_LoginCredentialId = null;
    public string m_LoginCredentialToken = null;

    private static PlatformInterface s_PlatformInterface;
    private const float c_PlatformTickInterval = 0.1f;
    private float m_PlatformTickTimer = 0f;

    //Player Info
    public ProductUserId userID;
    public EpicAccountId epicID;

    public string playerName = "Player";
    public string lobbyID = "";
    public LobbyDetails lobbyDetails;
    private SocketId socket;

    public ProductUserId[] lobbyMembers = new ProductUserId[MAX_LOBBY_MEMBERS];

    public LobbyDetails[] foundLobbies = [];

    //Player Attrritbutes
    public List<LobbyAttributeStorage> playerAttributes = new List<LobbyAttributeStorage>();
    public List<LobbyAttributeStorage> lobbyAttributes = new List<LobbyAttributeStorage>();

    public enum ConnectionStatus { Connecting, Succesful, Failed, Lobby, Match }
    public ConnectionStatus status;

#if EOS_EDITOR
    // If we're in editor, we should dynamically load and unload the SDK between play sessions.
    // This allows us to initialize the SDK each time the game is run in editor.
    [DllImport("Kernel32.dll")]
    private static extern IntPtr LoadLibrary(string lpLibFileName);

    [DllImport("Kernel32.dll")]
    private static extern int FreeLibrary(IntPtr hLibModule);

    [DllImport("Kernel32.dll")]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    private IntPtr m_LibraryPointer;
#endif

    public override void _EnterTree()
    {
        Instance = this;
#if EOS_EDITOR
        var libraryPath = "res://addons/EOS/SDK/Bin/" + Config.LibraryName;

        m_LibraryPointer = LoadLibrary(libraryPath);
        if (m_LibraryPointer == IntPtr.Zero)
        {
            throw new Exception("Failed to load library" + libraryPath);
        }

        Bindings.Hook(m_LibraryPointer, GetProcAddress);
#endif
    }

    public override void _ExitTree()
    {
        Instance = null;
#if EOS_EDITOR
        if (s_PlatformInterface != null)
        {
            s_PlatformInterface.Release();
            s_PlatformInterface = null;
            PlatformInterface.Shutdown();
        }

        if (m_LibraryPointer != IntPtr.Zero)
        {
            Bindings.Unhook();

            // Free until the module ref count is 0
            while (FreeLibrary(m_LibraryPointer) != 0) { }
            m_LibraryPointer = IntPtr.Zero;
        }
#endif
    }

    public override void _Ready()
    {
        var initializeOptions = new InitializeOptions()
        {
            ProductName = m_ProductName,
            ProductVersion = m_ProductVersion
        };

        var initializeResult = PlatformInterface.Initialize(ref initializeOptions);
        if (initializeResult != Result.Success)
        {
            throw new Exception("Failed to initialize platform: " + initializeResult);
        }

        // The SDK outputs lots of information that is useful for debugging.
        // Make sure to set up the logging interface as early as possible: after initializing.
        LoggingInterface.SetLogLevel(LogCategory.AllCategories, LogLevel.VeryVerbose);
        LoggingInterface.SetCallback((ref LogMessage logMessage) => GD.Print(logMessage.Message));

        var options = new Options()
        {
            ProductId = m_ProductId,
            SandboxId = m_SandboxId,
            DeploymentId = m_DeploymentId,
            Flags = PlatformFlags.WindowsEnableOverlayOpengl,
            ClientCredentials = new ClientCredentials()
            {
                ClientId = m_ClientId,
                ClientSecret = m_ClientSecret
            }
        };

        s_PlatformInterface = PlatformInterface.Create(ref options);
        
        if (s_PlatformInterface == null)
        {
            throw new Exception("Failed to create platform");
        }
    }

    // Calling tick on a regular interval is required for callbacks to work.
    public override void _PhysicsProcess(double delta)
    {
        if (s_PlatformInterface != null)
        {
            m_PlatformTickTimer += (float)delta;

            if (m_PlatformTickTimer >= c_PlatformTickInterval)
            {
                m_PlatformTickTimer = 0;
                s_PlatformInterface.Tick();
            }
        }
    }

#region Login and Authentication
    public void DirectConnection()
    {
        var deviceIdOptions = new Epic.OnlineServices.Connect.CreateDeviceIdOptions()
        {
            DeviceModel = "Windows PC",
        };

        // Ensure platform tick is called on an interval, or this will not callback.
        s_PlatformInterface.GetConnectInterface().CreateDeviceId(ref deviceIdOptions, null, (ref Epic.OnlineServices.Connect.CreateDeviceIdCallbackInfo deviceIdCallbackInfo) =>
        {
            if (deviceIdCallbackInfo.ResultCode == Result.Success)
            {
                LoginWithDeviceID();
            }
            else if (deviceIdCallbackInfo.ResultCode == Result.DuplicateNotAllowed)
            {
                GD.Print("Device ID code already exists. Proceeding with login.");
                LoginWithDeviceID();
            }
            else if (Common.IsOperationComplete(deviceIdCallbackInfo.ResultCode))
            {
                status = ConnectionStatus.Failed;
                GD.Print("Login failed: " + deviceIdCallbackInfo.ResultCode);
            }
        });
    }

    void LoginWithDeviceID()
    {
        m_ExternalCredentialType = ExternalCredentialType.DeviceidAccessToken;

        var loginOptions = new Epic.OnlineServices.Connect.LoginOptions()
        {
            UserLoginInfo = new Epic.OnlineServices.Connect.UserLoginInfo()
            {
                DisplayName = playerName,
            },
            Credentials = new Epic.OnlineServices.Connect.Credentials()
            {
                Type = m_ExternalCredentialType,
                //Token = deviceIdCallbackInfo.
            },
        };

        s_PlatformInterface.GetConnectInterface().Login(ref loginOptions, null, (ref Epic.OnlineServices.Connect.LoginCallbackInfo loginCallbackInfo) =>
        {
            if (loginCallbackInfo.ResultCode == Result.Success)
            {
                userID = loginCallbackInfo.LocalUserId;
                status = ConnectionStatus.Succesful;
                GD.Print("Connect login succeeded!");
            }
            else if (Common.IsOperationComplete(loginCallbackInfo.ResultCode))
            {
                status = ConnectionStatus.Failed;
                GD.Print("Connect login failed");
            }
        });
    }

    public void LoginEpicGames()
    {
        m_ExternalCredentialType = ExternalCredentialType.EpicIdToken;

        var loginOptions = new LoginOptions()
        {
            Credentials = new Credentials()
            {
                Type = m_LoginCredentialType,
                Id = m_LoginCredentialId,
                Token = m_LoginCredentialToken
            },
            // Change these scopes to match the ones set up on your product on the Developer Portal.
            ScopeFlags = Epic.OnlineServices.Auth.AuthScopeFlags.BasicProfile | Epic.OnlineServices.Auth.AuthScopeFlags.FriendsList | Epic.OnlineServices.Auth.AuthScopeFlags.Presence
        };

        s_PlatformInterface.GetAuthInterface().Login(ref loginOptions, null, (ref LoginCallbackInfo loginCallbackInfo) =>
        {
            if (loginCallbackInfo.ResultCode == Result.Success)
            {
                OnConnectionSuccesful();
                GD.Print("Login succeeded");
            }
            else if (Common.IsOperationComplete(loginCallbackInfo.ResultCode))
            {
                status = ConnectionStatus.Failed;
                GD.Print("Login failed: " + loginCallbackInfo.ResultCode);
            }
        });
    } 

    void OnConnectionSuccesful()
    {
        UserInfoData? data = CopyUserInfo(epicID);
        playerName = data != null ? data.Value.DisplayName : "EpicPlayer";

        ConnectProduct();
    }

    void ConnectProduct()
    {
        var options = new CopyIdTokenOptions()
        {
            AccountId = epicID
        };

        s_PlatformInterface.GetAuthInterface().CopyIdToken(ref options, out IdToken? token);

        var loginOptions = new Epic.OnlineServices.Connect.LoginOptions()
        {
            Credentials = new Epic.OnlineServices.Connect.Credentials()
            {
                Type = m_ExternalCredentialType,
                Token = token.Value.JsonWebToken
            },
        };

        s_PlatformInterface.GetConnectInterface().Login(ref loginOptions, null, (ref Epic.OnlineServices.Connect.LoginCallbackInfo loginCallbackInfo) =>
        {
            if (loginCallbackInfo.ResultCode == Result.Success)
            {
                userID = loginCallbackInfo.LocalUserId;
                status = ConnectionStatus.Succesful;
                GD.Print("Connect login succeeded");
            }
            else if (Common.IsOperationComplete(loginCallbackInfo.ResultCode))
            {
                var loginOptions = new Epic.OnlineServices.Connect.CreateUserOptions()
                {
                    ContinuanceToken = loginCallbackInfo.ContinuanceToken
                };

                s_PlatformInterface.GetConnectInterface().CreateUser(ref loginOptions, null, (ref Epic.OnlineServices.Connect.CreateUserCallbackInfo createUserCallbackInfo) =>
                {
                    if (createUserCallbackInfo.ResultCode == Result.Success)
                    {
                        userID = createUserCallbackInfo.LocalUserId;
                        status = ConnectionStatus.Succesful;
                        GD.Print("User creation succeeded");
                    }
                    else if (Common.IsOperationComplete(createUserCallbackInfo.ResultCode))
                    {
                        status = ConnectionStatus.Failed;
                        GD.Print("User creation failed: " + createUserCallbackInfo.ResultCode);
                    }
                });
            }
        });
    }

    public UserInfoData? CopyUserInfo(EpicAccountId targetID)
    {
        CopyUserInfoOptions copy = new CopyUserInfoOptions();
        copy.TargetUserId = targetID;
        copy.LocalUserId = epicID;

        s_PlatformInterface.GetUserInfoInterface().CopyUserInfo(ref copy, out UserInfoData? info);

        return info;
    }

    public Epic.OnlineServices.Connect.ExternalAccountInfo? CopyProductUserInfo(ProductUserId targetId)
    {
        var options = new Epic.OnlineServices.Connect.CopyProductUserInfoOptions();
        options.TargetUserId = targetId;
        s_PlatformInterface.GetConnectInterface().CopyProductUserInfo(ref options, out Epic.OnlineServices.Connect.ExternalAccountInfo? info);

        return info;
    }
#endregion

#region P2P

    public void RequestConnection(ProductUserId targetID)
    {
        if (targetID == userID) return;

        var options = new AcceptConnectionOptions
        {
            LocalUserId = userID,
            RemoteUserId = targetID,
            SocketId = socket
        };
        var result = s_PlatformInterface.GetP2PInterface().AcceptConnection(ref options);
    }

    public void SendPacket(ProductUserId targetID, byte[] packet, int reliability = 0 /*unreliable by default*/)
    {
        var options = new SendPacketOptions()
        {
            AllowDelayedDelivery = true,
            Reliability = (PacketReliability)reliability,
            LocalUserId = userID,
            RemoteUserId = targetID,
            SocketId = socket,
            Data = new ArraySegment<byte>(packet),
            DisableAutoAcceptConnection = false
        };

        var pkgResult = s_PlatformInterface.GetP2PInterface().SendPacket(ref options);

        if (pkgResult != Result.Success)
        {
            GD.PrintErr("Packet failed to send.");
            return;
        }
        GD.Print($"Packet sent to ID {targetID}. Array size: {packet.Length}");
    }

    public byte[] ReceivePacket(out ProductUserId senderId)
    {
        senderId = null;
        var qOptions = new GetPacketQueueInfoOptions();
        s_PlatformInterface.GetP2PInterface().GetPacketQueueInfo(ref qOptions, out PacketQueueInfo queueInfo);
        uint packetSize = (uint)queueInfo.IncomingPacketQueueCurrentSizeBytes;

        ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[packetSize]);
        var options = new ReceivePacketOptions()
        {
            LocalUserId = userID,
            MaxDataSizeBytes = packetSize
        };

        Result pkgResult = s_PlatformInterface.GetP2PInterface().ReceivePacket(ref options, ref senderId, 
        ref socket, out byte channel, buffer, out uint bytesWritten);

        if (pkgResult != Result.Success) 
        {
            GD.PrintErr("Packet not found.");
            return null;
        }
        GD.Print($"Packet received from ID {senderId}. Array size: {buffer.ToArray().Length}");
        return buffer.ToArray();
    }

    public bool PacketsStillQueued()
    {
        var qOptions = new GetPacketQueueInfoOptions();
        s_PlatformInterface.GetP2PInterface().GetPacketQueueInfo(ref qOptions, out PacketQueueInfo queueInfo);
        return queueInfo.IncomingPacketQueueCurrentPacketCount > 0;
    }
#endregion

#region Lobby
    public void CreateNewLobby(string name, int players, bool isPublic, bool invites, bool crossplay)
    {
        CreateLobbyOptions options = new CreateLobbyOptions();

        options.LocalUserId = userID;
        options.BucketId = name;
        options.AllowInvites = invites;
        options.MaxLobbyMembers = (uint)players;

        if (isPublic)
            options.PermissionLevel = LobbyPermissionLevel.Publicadvertised;
        else
            options.PermissionLevel = LobbyPermissionLevel.Inviteonly;

        options.CrossplayOptOut = crossplay;
        options.DisableHostMigration = false;
        options.EnableJoinById = true;
        options.EnableRTCRoom = false;
        options.RejoinAfterKickRequiresInvite = true;
        options.PresenceEnabled = false;
        
        GD.Print(options.LocalUserId);
        
        s_PlatformInterface.GetLobbyInterface().CreateLobby(ref options, null, (ref CreateLobbyCallbackInfo lobbyCallbackInfo) =>
        {
            if (lobbyCallbackInfo.ResultCode == Result.Success)
            {
                lobbyID = lobbyCallbackInfo.LobbyId;
                lobbyDetails = GetLobbyDetails(lobbyID);
                socket.SocketName = lobbyID;
                //playerData.PlayerPlatform = GetUserPlatform();
                status = ConnectionStatus.Lobby;
                // Set lobby attributes at creation
                foreach(LobbyAttributeStorage lobbyAtt in lobbyAttributes)
                {
                    switch (lobbyAtt.Type)
                    {
                        case 0: //String
                            AddLobbyAttribute(lobbyAtt.Key, lobbyAtt.ValueString);
                            break;
                        case 1: //Bool
                            AddLobbyAttribute(lobbyAtt.Key, lobbyAtt.ValueBool);
                            break;
                        case 2: //Long
                            AddLobbyAttribute(lobbyAtt.Key, lobbyAtt.ValueLong);
                            break;
                        case 3: //Double
                            AddLobbyAttribute(lobbyAtt.Key, lobbyAtt.ValueDouble);
                            break;
                    }
                }
                // Set player attributes
                foreach(LobbyAttributeStorage playerAtt in playerAttributes)
                {
                    switch (playerAtt.Type)
                    {
                        case 0: //String
                            AddLobbyMemberAttribute(playerAtt.Key, playerAtt.ValueString);
                            break;
                        case 1: //Bool
                            AddLobbyMemberAttribute(playerAtt.Key, playerAtt.ValueBool);
                            break;
                        case 2: //Long
                            AddLobbyMemberAttribute(playerAtt.Key, playerAtt.ValueLong);
                            break;
                        case 3: //Double
                            AddLobbyMemberAttribute(playerAtt.Key, playerAtt.ValueDouble);
                            break;
                    }
                }
                GD.Print($"Lobby {name} ({lobbyID}) should be created now...");
            }
            else
            {
                status = ConnectionStatus.Succesful;
                GD.Print("Lobby failed: " + lobbyCallbackInfo.ResultCode);
            }
        });
    }

    public void JoinLobby(string lobbyId)
    {
        var options = new JoinLobbyByIdOptions()
        {
            LocalUserId = userID,
            LobbyId = lobbyId,
            PresenceEnabled = false,
        };

        s_PlatformInterface.GetLobbyInterface().JoinLobbyById(ref options, null, (ref JoinLobbyByIdCallbackInfo lobbyCallbackInfo) =>
        {
            if (lobbyCallbackInfo.ResultCode == Result.Success)
            {
                lobbyID = lobbyCallbackInfo.LobbyId;
                lobbyDetails = GetLobbyDetails(lobbyID);
                socket.SocketName = lobbyID;
                //playerData.PlayerPlatform = GetUserPlatform();
                status = ConnectionStatus.Lobby;
                // Set player attributes
                foreach(LobbyAttributeStorage playerAtt in playerAttributes)
                {
                    switch (playerAtt.Type)
                    {
                        case 0: //String
                            AddLobbyMemberAttribute(playerAtt.Key, playerAtt.ValueString);
                            break;
                        case 1: //Bool
                            AddLobbyMemberAttribute(playerAtt.Key, playerAtt.ValueBool);
                            break;
                        case 2: //Long
                            AddLobbyMemberAttribute(playerAtt.Key, playerAtt.ValueLong);
                            break;
                        case 3: //Double
                            AddLobbyMemberAttribute(playerAtt.Key, playerAtt.ValueDouble);
                            break;
                    }
                }
                GD.Print("Joined lobby successfully");
            }
            else
            {
                status = ConnectionStatus.Succesful;
                GD.Print("Lobby joining failed: " + lobbyCallbackInfo.ResultCode);
            }
        });
    }

    public void QuitLobby()
    {
        var options = new LeaveLobbyOptions()
        {
            LocalUserId = userID,
            LobbyId = lobbyID
        };

        s_PlatformInterface.GetLobbyInterface().LeaveLobby(ref options, null, (ref LeaveLobbyCallbackInfo lobbyCallbackInfo) =>
        {
            if (lobbyCallbackInfo.ResultCode == Result.Success)
            {
                lobbyID = "";
                lobbyDetails = null;
                lobbyMembers = new ProductUserId[MAX_LOBBY_MEMBERS];
                status = ConnectionStatus.Succesful;
                GD.Print("Left lobby successfully");
            }
            else
            {
                status = ConnectionStatus.Lobby;
                GD.Print("Leaving lobby failed: " + lobbyCallbackInfo.ResultCode);
            }
        });
    }

    public void SearchLobbies()
    {
        foundLobbies = [];
        
        var options = new CreateLobbySearchOptions()
        {
            MaxResults = MAX_LOBBY_SEARCH_RESULTS
        };
        s_PlatformInterface.GetLobbyInterface().CreateLobbySearch(ref options, out LobbySearch lobbySearchInfo);

        var searchParameters = new LobbySearchSetParameterOptions
        {
            Parameter = new AttributeData
            {
                Key = "Searchable",
                Value = new AttributeDataValue
                {
                    AsBool = true,  
                },
            },
            ComparisonOp = 0
        };

        var findOptions = new LobbySearchFindOptions();
        findOptions.LocalUserId = userID;

        //lobbySearchInfo.SetLobbyId(ref searchParameters);
        lobbySearchInfo.SetParameter(ref searchParameters);
        lobbySearchInfo.Find(ref findOptions, null, (ref LobbySearchFindCallbackInfo findCallbackInfo) =>
        {
            if (findCallbackInfo.ResultCode == Result.Success)
            {
                var lobbyCount = new LobbySearchGetSearchResultCountOptions();
                uint count = lobbySearchInfo.GetSearchResultCount(ref lobbyCount);
                foundLobbies = new LobbyDetails[count];
                
                for(uint i = 0; i < foundLobbies.Length; i++)
                {
                    var copySearchOptions = new LobbySearchCopySearchResultByIndexOptions
                    {
                        LobbyIndex = i
                    };
                    lobbySearchInfo.CopySearchResultByIndex(ref copySearchOptions, out foundLobbies[i]);
                }
                GD.Print($"Found {count} lobbies");
            }
            else
            {
                GD.Print("Lobby search failed: " + findCallbackInfo.ResultCode);
            }
        });
    }
    public uint GetLobbyMemberPlatform(ProductUserId targetId)
    {
        var copyOptions = new LobbyDetailsCopyMemberInfoOptions();
        copyOptions.TargetUserId = targetId;
        lobbyDetails.CopyMemberInfo(ref copyOptions, out LobbyDetailsMemberInfo? currentPlayerInfo);
        return currentPlayerInfo.Value.Platform;
    }

    public LobbyDetailsInfo? GetLobbyDetailsInfo(LobbyDetails details)
    {
        var lobbyDetailsOptions = new LobbyDetailsCopyInfoOptions();
        details.CopyInfo(ref lobbyDetailsOptions, out LobbyDetailsInfo? lobbyDetailsInfo);
        return lobbyDetailsInfo;
    }

    public LobbyDetails GetLobbyDetails(string lobby)
    {
        LobbyDetails result;
        var options = new CopyLobbyDetailsHandleOptions()
        {
            LobbyId = lobby,
            LocalUserId = userID
        };
        
        s_PlatformInterface.GetLobbyInterface().CopyLobbyDetailsHandle(ref options, out result);
        
        return result;
    }
#endregion

#region Lobby Attributes
    public void AddLobbyAttribute(string key, string valueString, bool isPublic = true)
    {
        var options = new LobbyModificationAddAttributeOptions
        {
            Attribute = new AttributeData
            {
                Key = key,
                Value = new AttributeDataValue
                {
                    AsUtf8 = valueString
                }
            },
            Visibility = isPublic ? LobbyAttributeVisibility.Public : LobbyAttributeVisibility.Private
        };

        UpdateLobbyAdd(ref options);
    }

    public void AddLobbyAttribute(string key, bool valueBool, bool isPublic = true)
    {
        var options = new LobbyModificationAddAttributeOptions
        {
            Attribute = new AttributeData
            {
                Key = key,
                Value = new AttributeDataValue
                {
                    AsBool = valueBool
                }
            },
            Visibility = isPublic ? LobbyAttributeVisibility.Public : LobbyAttributeVisibility.Private
        };

        UpdateLobbyAdd(ref options);
    }

    public void AddLobbyAttribute(string key, long valueInt, bool isPublic = true)
    {
        var options = new LobbyModificationAddAttributeOptions
        {
            Attribute = new AttributeData
            {
                Key = key,
                Value = new AttributeDataValue
                {
                    AsInt64 = valueInt
                }
            },
            Visibility = isPublic ? LobbyAttributeVisibility.Public : LobbyAttributeVisibility.Private
        };

        UpdateLobbyAdd(ref options);
    }

    public void AddLobbyAttribute(string key, double valueFloat, bool isPublic = true)
    {
        var options = new LobbyModificationAddAttributeOptions
        {
            Attribute = new AttributeData
            {
                Key = key,
                Value = new AttributeDataValue
                {
                    AsDouble = valueFloat
                }
            },
            Visibility = isPublic ? LobbyAttributeVisibility.Public : LobbyAttributeVisibility.Private
        };

        UpdateLobbyAdd(ref options);
    }

    private void UpdateLobbyAdd(ref LobbyModificationAddAttributeOptions lobbyAddOptions)
    {
        var lobbyModificationOptions = new UpdateLobbyModificationOptions
        {
            LocalUserId = userID,
            LobbyId = lobbyID
        };

        s_PlatformInterface.GetLobbyInterface().UpdateLobbyModification(ref lobbyModificationOptions, out LobbyModification modificationHandle);
        var lobbyOptions = new UpdateLobbyOptions
        {
            LobbyModificationHandle = modificationHandle
        };
        lobbyOptions.LobbyModificationHandle.AddAttribute(ref lobbyAddOptions);

        //if (result != Result.Success) { GD.Print(result); return; }

        s_PlatformInterface.GetLobbyInterface().UpdateLobby(ref lobbyOptions, null, (ref UpdateLobbyCallbackInfo updateLobbyInfo) =>
        {
            if (updateLobbyInfo.ResultCode == Result.Success)
            {
                GD.Print("lobby data added successfully.");
            }
            else
            {
                GD.Print("lobby data failed to update.");
            }
        });
    }

    public void UpdateLobbyRemove(string key)
    {
        var options = new LobbyModificationRemoveAttributeOptions
        {
            Key = key
        };
        var lobbyModificationOptions = new UpdateLobbyModificationOptions
        {
            LocalUserId = userID,
            LobbyId = lobbyID
        };

        s_PlatformInterface.GetLobbyInterface().UpdateLobbyModification(ref lobbyModificationOptions, out LobbyModification modificationHandle);
        var lobbyOptions = new UpdateLobbyOptions
        {
            LobbyModificationHandle = modificationHandle
        };
        var result = lobbyOptions.LobbyModificationHandle.RemoveAttribute(ref options);

        if (result != Result.Success) return;

        s_PlatformInterface.GetLobbyInterface().UpdateLobby(ref lobbyOptions, null, (ref UpdateLobbyCallbackInfo updateLobbyInfo) =>
        {
            if (updateLobbyInfo.ResultCode == Result.Success)
            {
                GD.Print("lobby data removed successfully.");
            }
            else
            {
                GD.Print("lobby data failed to update.");
            }
        });
    }

    public void AddLobbyMemberAttribute(string key, string valueString, bool isPublic = true)
    {
        var options = new LobbyModificationAddMemberAttributeOptions
        {
            Attribute = new AttributeData
            {
                Key = key,
                Value = new AttributeDataValue
                {
                    AsUtf8 = valueString
                }
            },
            Visibility = isPublic ? LobbyAttributeVisibility.Public : LobbyAttributeVisibility.Private
        };

        UpdateLobbyMemberAdd(ref options);
    }

    public void AddLobbyMemberAttribute(string key, bool valueBool, bool isPublic = true)
    {
        var options = new LobbyModificationAddMemberAttributeOptions
        {
            Attribute = new AttributeData
            {
                Key = key,
                Value = new AttributeDataValue
                {
                    AsBool = valueBool
                }
            },
            Visibility = isPublic ? LobbyAttributeVisibility.Public : LobbyAttributeVisibility.Private
        };

        UpdateLobbyMemberAdd(ref options);
    }

    public void AddLobbyMemberAttribute(string key, long valueInt, bool isPublic = true)
    {
        var options = new LobbyModificationAddMemberAttributeOptions
        {
            Attribute = new AttributeData
            {
                Key = key,
                Value = new AttributeDataValue
                {
                    AsInt64 = valueInt
                }
            },
            Visibility = isPublic ? LobbyAttributeVisibility.Public : LobbyAttributeVisibility.Private
        };

        UpdateLobbyMemberAdd(ref options);
    }

    public void AddLobbyMemberAttribute(string key, double valueFloat, bool isPublic = true)
    {
        var options = new LobbyModificationAddMemberAttributeOptions
        {
            Attribute = new AttributeData
            {
                Key = key,
                Value = new AttributeDataValue
                {
                    AsDouble = valueFloat
                }
            },
            Visibility = isPublic ? LobbyAttributeVisibility.Public : LobbyAttributeVisibility.Private
        };

        UpdateLobbyMemberAdd(ref options);
    }

    private void UpdateLobbyMemberAdd(ref LobbyModificationAddMemberAttributeOptions lobbyMemberAddOptions)
    {
        var lobbyModificationOptions = new UpdateLobbyModificationOptions
        {
            LocalUserId = userID,
            LobbyId = lobbyID
        };

        s_PlatformInterface.GetLobbyInterface().UpdateLobbyModification(ref lobbyModificationOptions, out LobbyModification modificationHandle);

        var lobbyOptions = new UpdateLobbyOptions
        {
            LobbyModificationHandle = modificationHandle
        };
        var result = lobbyOptions.LobbyModificationHandle.AddMemberAttribute(ref lobbyMemberAddOptions);

        if (result != Result.Success) return;

        s_PlatformInterface.GetLobbyInterface().UpdateLobby(ref lobbyOptions, null, (ref UpdateLobbyCallbackInfo updateLobbyInfo) =>
        {
            if (updateLobbyInfo.ResultCode == Result.Success)
            {
                GD.Print("lobby member data added successfully.");
            }
            else
            {
                GD.Print("lobby member data failed to update.");
            }
        });
    }

    public void UpdateLobbyMemberRemove(string key)
    {
        var options = new LobbyModificationRemoveMemberAttributeOptions
        {
            Key = key
        };
        var lobbyModificationOptions = new UpdateLobbyModificationOptions
        {
            LocalUserId = userID,
            LobbyId = lobbyID
        };

        s_PlatformInterface.GetLobbyInterface().UpdateLobbyModification(ref lobbyModificationOptions, out LobbyModification modificationHandle);
        
        var lobbyOptions = new UpdateLobbyOptions
        {
            LobbyModificationHandle = modificationHandle
        };
        var result = lobbyOptions.LobbyModificationHandle.RemoveMemberAttribute(ref options);

        if (result != Result.Success) return;

        s_PlatformInterface.GetLobbyInterface().UpdateLobby(ref lobbyOptions, null, (ref UpdateLobbyCallbackInfo updateLobbyInfo) =>
        {
            if (updateLobbyInfo.ResultCode == Result.Success)
            {
                GD.Print("lobby data removed successfully.");
            }
            else
            {
                GD.Print("lobby data failed to update.");
            }
        });
    }

    public AttributeDataValue GetLobbyAttribute(ref LobbyDetails lobby, string key)
    {
        var options = new LobbyDetailsCopyAttributeByKeyOptions
        {
            AttrKey = key
        };
        lobby.CopyAttributeByKey(ref options, out Epic.OnlineServices.Lobby.Attribute? outAttribute);

        if (outAttribute == null) return new AttributeDataValue{};

        return outAttribute.Value.Data.Value.Value;
    }

    public AttributeDataValue GetLobbyMemberAttribute(ref LobbyDetails lobby, ProductUserId memberId, string key)
    {
        var options = new LobbyDetailsCopyMemberAttributeByKeyOptions
        {
            TargetUserId = memberId,
            AttrKey = key
        };
        lobby.CopyMemberAttributeByKey(ref options, out Epic.OnlineServices.Lobby.Attribute? outAttribute);

        if (outAttribute == null) return new AttributeDataValue{};

        return outAttribute.Value.Data.Value.Value;
    }
#endregion

    public struct LobbyAttributeStorage
    {
        public string Key;
        public byte Type; //0 = string, 1 = bool, 2 = long, 3 = double
        public string ValueString;
        public bool ValueBool;
        public long ValueLong;
        public double ValueDouble;

        public void SetValue(string newKey, string newValue)
        {
            Type = 0;
            Key = newKey;
            ValueString = newValue;
        }

        public void SetValue(string newKey, bool newValue)
        {
            Type = 1;
            Key = newKey;
            ValueBool = newValue;
        }

        public void SetValue(string newKey, long newValue)
        {
            Type = 2;
            Key = newKey;
            ValueLong = newValue;
        }

        public void SetValue(string newKey, double newValue)
        {
            Type = 3;
            Key = newKey;
            ValueDouble = newValue;
        }
    };
}
