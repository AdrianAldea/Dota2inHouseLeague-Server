using SteamKit2;
using SteamKit2.GC;
using SteamKit2.GC.Dota;
using SteamKit2.GC.Dota.Internal;
using SteamKit2.Internal;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static SteamKit2.Internal.CMsgRemoteClientBroadcastStatus;

namespace IHLDota2Bot
{
    public class DotaLobbyManager
    {
        private SteamClient steamClient;
        private CallbackManager callbackManager;
        private SteamUser steamUser;
        private SteamFriends steamFriends;
        private SteamGameCoordinator gameCoordinator;
        private uint dotaAppId = 570;


        private string username = "dota2inhouseleague";
        private string password = "Romania90.";

        private bool isRunning = false;
        private bool isReady = false;

        private List<ulong> pendingInvites = new();

        public async Task ConnectAsync()
        {
            DebugLog.AddListener(new MyListener());
            steamClient = new SteamClient();
            callbackManager = new CallbackManager(steamClient);
            steamUser = steamClient.GetHandler<SteamUser>();
            steamFriends = steamClient.GetHandler<SteamFriends>();
            gameCoordinator = steamClient.GetHandler<SteamGameCoordinator>();

            callbackManager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
            callbackManager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
            callbackManager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
            callbackManager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);
            callbackManager.Subscribe<SteamGameCoordinator.MessageCallback>(OnGCMessage);

            isRunning = true;

            steamClient.Connect();

            while (!isReady)
            {
                callbackManager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
                await Task.Delay(500);
            }
        }

        class MyListener : IDebugListener
        {
            public void WriteLine(string category, string msg)
            {
                // this function will be called when internal steamkit components write to the debuglog

                // for this example, we'll print the output to the console
                Console.WriteLine("MyListener - {0}: {1}", category, msg);
            }
        }

        public async Task CreateLobbyAsync(string name, string password)
        {
            if (!isReady)
            {
                Console.WriteLine("GC nu este pregătit încă.");
                return;
            }

            var createLobby = new ClientGCMsgProtobuf<CMsgPracticeLobbyCreate>((uint)EDOTAGCMsg.k_EMsgGCPracticeLobbyCreate);

            //createLobby.Body.search_key = name;
            createLobby.Body.lobby_details = new CMsgPracticeLobbySetDetails();
            //createLobby.Body.lobby_details.pass_key = password;
            //createLobby.Body.lobby_details.game_mode = (uint)DOTA_GameMode.DOTA_GAMEMODE_1V1MID;
            createLobby.Body.lobby_details.game_name = name;
            createLobby.Body.lobby_details.visibility = DOTALobbyVisibility.DOTALobbyVisibility_Public;
            //createLobby.Body.lobby_details.game_version = DOTAGameVersion.GAME_VERSION_CURRENT;
            //createLobby.Body.lobby_details.server_region = 2;
            //createLobby.Body.lobby_details.custom_max_players = 2;
            //createLobby.Body.lobby_details.intro_mode = false;
            //createLobby.Body.lobby_details.lan = false;

            gameCoordinator.Send(createLobby, dotaAppId);

            Console.WriteLine($"Lobby creat: {name} cu parola: {password}");
            await Task.Delay(1000);

            foreach (var steamId in pendingInvites)
            {
                InviteToLobby(steamId);
                await Task.Delay(500);
            }
        }

        public void SetPendingInvites(List<string> steamIds)
        {
            pendingInvites.Clear();
            foreach (var id in steamIds)
            {
                if (ulong.TryParse(id, out var steamId))
                {
                    pendingInvites.Add(steamId);
                }
            }
        }

        private void InviteToLobby(ulong steamId)
        {
            var inviteMsg = new ClientGCMsgProtobuf<CMsgInviteToLobby>((uint)EDOTAGCMsg.k_EMsgGCPracticeLobbyCreate);
            inviteMsg.Body.steam_id = steamId;
            gameCoordinator.Send(inviteMsg, dotaAppId);
            Console.WriteLine($"Trimis invite către: {steamId}");
        }

        private void LeaveLobby()
        {
            var leaveLobby = new ClientGCMsgProtobuf<CMsgPracticeLobbyLeave>((uint)EDOTAGCMsg.k_EMsgGCPracticeLobbyLeave);
            gameCoordinator.Send(leaveLobby, dotaAppId);
        }

        private void OnConnected(SteamClient.ConnectedCallback callback)
        {
            Console.WriteLine("Conectat la Steam, autentificare in curs... '{0}'...", username);

            steamUser.LogOn(new SteamUser.LogOnDetails
            {
                Username = username,
                Password = password
            });
        }

        private void OnDisconnected(SteamClient.DisconnectedCallback callback)
        {
            Console.WriteLine("Deconectat de la Steam");
            isRunning = false;
        }

        private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            if (callback.Result == EResult.OK)
            {
                Console.WriteLine("Autentificare Steam reușită! Lansare Dota 2...");

                if (callback.Result == EResult.AccountLogonDenied)
                {
                    // if we recieve AccountLogonDenied or one of it's flavors (AccountLogonDeniedNoMailSent, etc)
                    // then the account we're logging into is SteamGuard protected
                    // see sample 5 for how SteamGuard can be handled

                    Console.WriteLine("Unable to logon to Steam: This account is SteamGuard protected.");

                    isRunning = false;
                    return;
                }

                isReady = true;
            }
            else
            {
                Console.WriteLine("Unable to logon to Steam: {0} / {1}", callback.Result, callback.ExtendedResult);

                isRunning = false;
                return;
            }
            // at this point, we'd be able to perform actions on Steam

            // for this sample we'll just log off
            //steamUser.LogOff();
        }

        private void OnLoggedOff(SteamUser.LoggedOffCallback callback)
        {
            Console.WriteLine("Delogat de la Steam");
            isRunning = false;
        }

        private void OnGCMessage(SteamGameCoordinator.MessageCallback callback)
        {
            if (callback.EMsg == (uint)EMsg.ClientHello)
            {
                Console.WriteLine("Conectat la Game Coordinator Dota 2!");
                isReady = true;
            }
            else
            {
                Console.WriteLine($"[GC MSG] {callback.EMsg}");
            }
        }
    }
}
