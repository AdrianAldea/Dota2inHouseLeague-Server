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
    class DotaLobbyManager
    {
        private SteamClient steamClient;
        private CallbackManager callbackManager;
        private SteamUser steamUser;
        private SteamFriends steamFriends;
        private SteamGameCoordinator steamGameCoordinator;

        private bool isRunning;
        private string username;
        private string password;
        private string authCode, twoFactorAuth; // Pentru Steam Guard

        private const uint DOTA_APP_ID = 570; // AppID pentru Dota 2

        public DotaLobbyManager(string user, string pass)
        {
            username = user;
            password = pass;

            steamClient = new SteamClient();
            callbackManager = new CallbackManager(steamClient);

            steamUser = steamClient.GetHandler<SteamUser>();
            steamFriends = steamClient.GetHandler<SteamFriends>();
            steamGameCoordinator = steamClient.GetHandler<SteamGameCoordinator>();

            // Subscrie la callback-uri
            callbackManager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
            callbackManager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
            callbackManager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
            callbackManager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);
            callbackManager.Subscribe<SteamUser.AccountInfoCallback>(OnAccountInfo);
            //callbackManager.Subscribe<SteamUser.UpdateMachineAuthCallback>(OnMachineAuth);
            callbackManager.Subscribe<SteamGameCoordinator.MessageCallback>(OnGCMessage);
        }

        public void Connect()
        {
            isRunning = true;
            Console.WriteLine($"Conectare la Steam ca {username}...");
            steamClient.Connect();

            // Bucla principală pentru callback-uri
            while (isRunning)
            {
                callbackManager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
            }
        }

        private void OnConnected(SteamClient.ConnectedCallback callback)
        {
            Console.WriteLine("Conectat la Steam! Se încearcă autentificarea...");
            // Pregătește fișierul sentry dacă există (pentru Steam Guard)
            //byte[] sentryHash = null;
            //if (File.Exists("sentry.bin"))
            //{
            //    sentryHash = File.ReadAllBytes("sentry.bin");
            //}

            steamUser.LogOn(new SteamUser.LogOnDetails
            {
                Username = username,
                Password = password,
                //AuthCode = authCode, // Se completează dacă este cerut (email)
                //TwoFactorCode = twoFactorAuth, // Se completează dacă este cerut (authenticator app)
                //SentryFileHash = sentryHash, // Pentru autentificarea mașinii
                //ShouldRememberPassword = true,
            });
        }

        private void OnDisconnected(SteamClient.DisconnectedCallback callback)
        {
            Console.WriteLine("Deconectat de la Steam.");
            isRunning = false;
            // Poți adăuga logică de reconectare aici
            Thread.Sleep(TimeSpan.FromSeconds(5));
            // steamClient.Connect(); // Exemplu de reconectare
        }

        private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
        {
            if (callback.Result == EResult.OK)
            {
                Console.WriteLine("Autentificare reușită!");
                steamFriends.SetPersonaState(EPersonaState.Online); // Setează statusul ca Online

                // "Lansează" Dota 2 pentru a putea interacționa cu GC-ul Dota 2
                var gamePlayed = new ClientMsgProtobuf<CMsgClientGamesPlayed>(EMsg.ClientGamesPlayed);
                gamePlayed.Body.games_played.Add(new CMsgClientGamesPlayed.GamePlayed
                {
                    game_id = DOTA_APP_ID, // Poate fi necesar să folosești game_id long (ulong)
                    // game_ip_address = 0, // Opțional
                    // game_port = 0, // Opțional
                });
                steamClient.Send(gamePlayed);
                Console.WriteLine("Am notificat Steam că 'jucăm' Dota 2.");

                // Așteaptă puțin pentru ca GC-ul să fie gata
                Thread.Sleep(5000);

                // Acum poți încerca să creezi un lobby
                CreateDotaLobby();
            }
            else if (callback.Result == EResult.AccountLogonDenied)
            {
                Console.WriteLine("Autentificare eșuată: Steam Guard. Introduceți codul din email/aplicație.");
                Console.Write("Auth Code (lăsați gol dacă folosiți 2FA): ");
                authCode = Console.ReadLine();
                Console.Write("Two-Factor Code (lăsați gol dacă folosiți email): ");
                twoFactorAuth = Console.ReadLine();
            }
            else if (callback.Result == EResult.InvalidPassword)
            {
                Console.WriteLine("Autentificare eșuată: Parolă invalidă.");
                isRunning = false;
            }
            else
            {
                Console.WriteLine($"Autentificare eșuată: {callback.Result} / {callback.ExtendedResult}");
                isRunning = false;
            }
        }

        private void OnLoggedOff(SteamUser.LoggedOffCallback callback)
        {
            Console.WriteLine($"Deconectat de la cont: {callback.Result}");
        }

        private void OnAccountInfo(SteamUser.AccountInfoCallback callback)
        {
            Console.WriteLine($"Nume cont: {callback.PersonaName}");
            // Poți seta aici și clan tag, etc.
        }


        //private void OnMachineAuth(SteamUser.UpdateMachineAuthCallback callback)
        //{
        //    Console.WriteLine("Se actualizează fișierul sentry...");
        //    // Scrie noul hash sentry pe disc pentru autentificări viitoare
        //    File.WriteAllBytes("sentry.bin", callback.Data);
        //    steamUser.SendMachineAuthResponse(new SteamUser.MachineAuthDetails
        //    {
        //        JobID = callback.JobID,
        //        FileName = callback.FileName,
        //        BytesWritten = callback.BytesToWrite,
        //        FileSize = callback.Data.Length,
        //        Offset = callback.Offset,
        //        Result = EResult.OK,
        //        LastError = 0,
        //        OneTimePassword = callback.OneTimePassword, // Poate fi necesar pentru unele cazuri
        //        SentryFileHash = callback.SentryFileHash,
        //    });
        //    Console.WriteLine("Fișier sentry actualizat.");
        //}

        private void CreateDotaLobby()
        {
            Console.WriteLine("Se încearcă trimiterea mesajului de creare lobby către GC-ul Dota 2...");

            // ACEASTA ESTE PARTEA CRITICĂ ȘI COMPLEXĂ
            // Vei avea nevoie de definițiile protobuf corecte pentru Dota 2.
            // CMsgPracticeLobbyCreate este un exemplu, dar structura sa exactă
            // și ID-ul mesajului (EMsg) trebuie verificate din surse actualizate.

            // Presupunând că ai o clasă generată `CMsgPracticeLobbyCreate`
            // și `EDOTAGCMsg.k_EMsgGCPracticeLobbyCreate`

            var createLobbyMsg = new ClientGCMsgProtobuf<CMsgPracticeLobbyCreate>((uint)EDOTAGCMsg.k_EMsgGCPracticeLobbyCreate); // ID-ul mesajului GC

            // Setează detaliile lobby-ului
            // Aceste câmpuri sunt ipotetice și trebuie verificate cu definiția protobuf reală!
            createLobbyMsg.Body.lobby_details = new CMsgPracticeLobbySetDetails();
            //createLobby.Body.lobby_details.pass_key = password;
            createLobbyMsg.Body.lobby_details.game_mode = (uint)DOTA_GameMode.DOTA_GAMEMODE_1V1MID;
            createLobbyMsg.Body.lobby_details.game_name = "test123";

            createLobbyMsg.Body.client_version = 0;
            createLobbyMsg.Body.lobby_details.visibility = DOTALobbyVisibility.DOTALobbyVisibility_Friends;
            createLobbyMsg.Body.lobby_details.game_mode = (uint)DOTA_GameMode.DOTA_GAMEMODE_AP; // All Pick, exemplu
            createLobbyMsg.Body.pass_key = "parolasecreta"; // Parola lobby-ului
                                                            // ... alte setări: server region, fill with bots, cheats, etc.
            createLobbyMsg.Body.lobby_details.server_region = 2;

            Console.WriteLine($"Se trimite CMsgPracticeLobbyCreate pentru lobby: {createLobbyMsg.Body.lobby_details.game_name}");
            steamGameCoordinator.Send(createLobbyMsg, DOTA_APP_ID);


            // ---- DECOMENTEAZĂ ȘI ADAPTEAZĂ SECȚIUNEA DE MAI SUS ----
            Console.WriteLine("Funcționalitatea de creare lobby (CMsgPracticeLobbyCreate) este comentată.");
            Console.WriteLine("Trebuie să obții definițiile protobuf corecte pentru Dota 2 și să le implementezi.");
            Console.WriteLine("Caută pe GitHub 'SteamDatabase/GameTracking-Dota2' pentru protobuf-uri.");
        }

        private void OnGCMessage(SteamGameCoordinator.MessageCallback callback)
        {
            Console.WriteLine($"Mesaj GC primit: ID Mesaj = {callback.EMsg}, AppID = {callback.AppID}");

            // Verifică dacă mesajul este pentru Dota 2
            if (callback.AppID != DOTA_APP_ID)
                return;

            // Aici vei procesa răspunsurile de la GC-ul Dota 2
            // De exemplu, răspunsul la crearea lobby-ului (CMsgPracticeLobbyCreateResponse sau similar)
            // Vei avea nevoie de un switch pe callback.EMsg pentru a identifica tipul mesajului
            // și apoi să deserializezi `callback.Data` în obiectul protobuf corespunzător.


            if (callback.EMsg == (uint)EDOTAGCMsg.k_EMsgGCPracticeLobbyCreate) // ID-ul mesajului de răspuns
            {
                //var response = callback.GetDataAs<CMsgPracticeLobbyCreateResponse>(); // Presupunând că ai această clasă generată
                //if (response != null && response.result == DOTALobbyReadyState.DOTALobbyReadyState_ACCEPTED) // Verifică enum-ul corect
                //{
                //    Console.WriteLine($"Lobby creat cu succes! Lobby ID: {response.lobby_id}");
                //    // Poți salva lobby_id pentru acțiuni viitoare (invitații, start joc etc.)
                //}
                //else
                //{
                //    Console.WriteLine($"Crearea lobby-ului a eșuat. Rezultat: {response?.result}");
                //}
            }
            // ... alte cazuri pentru diferite mesaje GC


            // ---- DECOMENTEAZĂ ȘI ADAPTEAZĂ SECȚIUNEA DE MAI SUS ----
            Console.WriteLine("Procesarea mesajelor GC (răspunsuri) este comentată.");

            // Pentru debug, poți scrie datele brute ale mesajului
            // File.WriteAllBytes($"gc_msg_{callback.EMsg}.bin", callback.Data.ToArray());
        }

        public void Disconnect()
        {
            Console.WriteLine("Se deconectează...");
            steamUser.LogOff();
            // isRunning va fi setat pe false în OnDisconnected sau OnLoggedOff
        }

        //static async Task Main(string[] args)
        //{
        //    Console.WriteLine("Bot SteamKit2 pentru Lobby Dota 2");

        //    // ATENȚIE: Nu stoca niciodată credențialele direct în cod pentru aplicații reale!
        //    // Folosește variabile de mediu, fișiere de configurare securizate etc.
        //    Console.Write("Nume utilizator Steam: ");
        //    string user = Console.ReadLine();
        //    Console.Write("Parolă Steam: ");
        //    string pass = Console.ReadLine(); // Ideal ar fi să maschezi input-ul parolei

        //    if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
        //    {
        //        Console.WriteLine("Numele de utilizator și parola nu pot fi goale.");
        //        return;
        //    }

        //    SteamBot bot = new SteamBot(user, pass);
        //    bot.Connect(); // Aceasta va bloca până la deconectare

        //    Console.WriteLine("Bot oprit. Apasă orice tastă pentru a ieși.");
        //    Console.ReadKey();
        //}
    }
}
