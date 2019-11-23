using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using GlassTL.Telegram.MTProto;
using GlassTL.Telegram.Network;
using GlassTL.Telegram.Exceptions;
using GlassTL.Telegram;
using GlassTL.Telegram.Utils;
using System.Text;
using System.Security.Cryptography;
using GlassTL.Telegram.MTProto.Crypto;
using static GlassTL.Telegram.Utils.PeerManager;

namespace GlassTL.Telegram
{
    public sealed class TelegramClient
    {
        #region Private-Members
        /// <summary>
        /// Wrapper around the Mobile Transport Protocol
        /// </summary>
        private MTProtoSender Sender { get; }

        /// <summary>
        /// The default production Data Center
        /// </summary>
        private DataCenter DefaultDataCenter => new DataCenter("149.154.175.55", 443, false, 1);
        /// <summary>
        /// The default test Data Center
        /// </summary>
        private DataCenter DefaultTestDataCenter => new DataCenter("149.154.175.10", 443, true, 1);

        /// <summary>
        /// The API ID used when communicating with the server
        /// </summary>
        private int ApiId { get; } = 0;
        /// <summary>
        /// The API Hash used when communicating with the server
        /// </summary>
        private string ApiHash { get; } = "";

        /// <summary>
        /// The underlying connection used with communicating with the server
        /// </summary>
        private Connection Connection { get; set; }
        /// <summary>
        /// The session object which contains information like the current DC, server auth info, signed in user, and additional settings
        /// </summary>
        private Session Session { get; }

        /// <summary>
        /// Contains the possible DCs we can use.  This is populated when the initial connection is made and is used when needing to migrate to a different DC.
        /// </summary>
        private DataCenter[] DcOptions { get; set; }

        /// <summary>
        /// Allows access to the current schema by means of a dynamic object
        /// </summary>
        private dynamic Schema { get; } = new TLSchema();

        private string PhoneNumber { get; set; } = "";
        private string AuthCode { get; set; } = "";

        //private int pts = 0, date = 0, qts = 0;
        #endregion

        #region Public-Members
        /// <summary>
        /// Gets a value indicating whether or not we are connected to a Test DC or not.
        /// </summary>
        public bool UseTestDC => Session?.DataCenter?.TestDC ?? false;
        /// <summary>
        /// Gets the currently logged in user (if applicable) or null if no user is signed in
        /// </summary>
        public TLObject CurrentUser { get; private set; } = null;
        /// <summary>
        /// Gets information about the auth code that was sent (if aplpicable) or null if no auth has been sent
        /// </summary>
        public TLObject AuthCodeInfo { get; private set; } = null;
        /// <summary>
        /// Gets information about the Cloud Password (if it's been fetched from the server) or null
        /// </summary>
        public TLObject CloudPasswordInfo { get; private set; } = null;
        #endregion

        #region Contructors-and-Factories
        /// <summary>
        /// Creates a new connection to Telegram and provides easy access to the API
        /// </summary>
        /// <param name="apiId">The API ID used when communicating with the server.</param>
        /// <param name="apiHash">The API Hash used when communicating with the server.</param>
        /// <param name="useTestDC">True if a connection should be made to the test servers.  Otherwise, false.</param>
        /// <param name="sessionUserId">The name of the session if applicable.</param>
        public TelegramClient(int? apiId = null, string apiHash = null, bool useTestDC = false, string sessionUserId = "session")
        {
            Logger.Log(Logger.Level.Info, "Initializing TelegramClient");

            // These must be provided.  Default API info is from the official android client
            if (apiId == null || apiHash == null)
            {
                Logger.Log(Logger.Level.Debug, "API info not supplied or partial. Using default");
                ApiId = 6;
                ApiHash = "eb06d4abfb49dc3eeb1aeb98ae0f581e";
            }
            else
            {
                Logger.Log(Logger.Level.Debug, "Using API info supplied by the user.");
                ApiId = (int)apiId;
                ApiHash = apiHash;
            }

            // Load the session if it exists or create a new one
            Session = Session.LoadOrCreate(sessionUserId);

            // If the DC isn't defined (new session maybe) or it's not a test or production
            // DC (as requested by the user), assign a new DC to the session
            if (Session.DataCenter == null || Session.DataCenter.TestDC != useTestDC)
            {
                Session.DataCenter = useTestDC ? DefaultTestDataCenter : DefaultDataCenter;
            }

            // Create a connection to the provided DC
            Connection = new TCPFull(Session.DataCenter);

            // Create a new sender.
            // Note: We are passing a reference to the helper so that any changes
            // made during runtime will still be in the session for when we save it
            Sender = new MTProtoSender(ref Session.Helper);

            Sender.UpdateReceivedEvent += (sender, e) =>
            {
                OnUpdate(e);
            };

            Logger.Log(Logger.Level.Info, "Created TelegramClient");
        }
        #endregion

        #region Connections
        /// <summary>
        /// This should be one of the first things you do.  It negotiates a secure connection with the server and prepares to accept login requests
        /// </summary>
        public async Task Connect()
        {
            try
            {
                // Connect to and authorize the connection with the server.
                await Sender.Connect(Connection);

                // Now that we are connected, start the whole process by
                // sending data about ourselves, the layer, and request
                // a list of available DCs while we are at it
                var config = await Sender.Send(Schema.invokeWithLayer(new
                {
                    layer = Schema.Layer,
                    query = Schema.initConnection(new
                    {
                        api_id = ApiId,
                        device_model = "Userbot",
                        system_version = "Windows",
                        app_version = "1.0.1",
                        system_lang_code = "en",
                        lang_pack = "", // For official clients only
                        lang_code = "en",
                        query = Schema.help.getConfig
                    })
                }));

                // For now, we will only support a limited number of DCs.
                DcOptions = ((JArray)config["dc_options"])
                    .Where(x => (bool)x["static"])
                    .Select(x => new DataCenter((string)x["ip_address"], (int)x["port"], UseTestDC, (int)x["id"])).ToArray();


                // Save the session in case something changed
                Session.Save();
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, $"Failed to connect to Telegram.\n\n{ex.Message}");
                throw;
            }
        }
        /// <summary>
        /// Disconnects from the current DC and reconnects to a new one
        /// </summary>
        /// <param name="dcId">The new DC to connect to</param>
        private async Task MigrateToDC(int dcId)
        {
            try
            {
                // This list should have been populated during the initial connection.
                if (DcOptions == null || !DcOptions.Any())
                    throw new InvalidOperationException($"Can't connect to DC{dcId}. Are we connected in the first place?");

                // Attempt to get the DC from the list
                var dataCenterOptions = DcOptions.ToList().Where(d => d.DataCenterId == dcId);

                // Maybe the ID isn't in the list
                if (!dataCenterOptions.Any())
                    throw new InvalidOperationException($"Unable to find information on DC{dcId}. Are we connected in the first place?");

                // Save the DC
                var dataCenter = dataCenterOptions.ToArray()[0];

                // Prevent loops by not allowing the DC to be the same
                if (dataCenter.DataCenterId == Session.DataCenter.DataCenterId)
                    throw new InvalidOperationException($"Already connected to DC{dcId}.");

                // If we are logged in, we can export the session for use at the new DC
                TLObject exported = null;
                if (Session.TLUser != null)
                {
                    exported = await Sender.Send(Schema.auth.exportAuthorization(new { dc_id = dcId }));
                }

                // Disconnect from the server.
                // Note: This should trickle down to all the
                // different wrappers -- unsubscribing from
                // all events, terminating the connection,
                // stopping all loops, and requeueing messages
                // we haven't received yet (not necessarily
                // in that order)
                Sender.Disconnect();
                // Force the connection to be re-authenticated
                Sender.ResetServerAuthentication();

                // Create a new connection to the DC
                Connection = new TCPFull(dataCenter);

                // Connect and see what happens
                await Connect();

                // Save the new DC in our session file
                Session.DataCenter = dataCenter;

                // If we were authenticated on the other DC, move the authentication over to the new DC
                if (Session.TLUser != null)
                {
                    var imported = await Sender.Send(Schema.auth.importAuthorization(new { id = exported["Id"], bytes = exported["Bytes"] }));
                    OnUpdateUser(new TLObjectEventArgs(imported["User"]));
                }

                Session.Save();
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, $"An error occurred while switching DCs.\n\n{ex.Message}");
                throw;
            }
        }
        /// <summary>
        /// Queries the server (handling errors if possible) and returns the result
        /// </summary>
        /// <param name="request">The request to send to the server</param>
        private async Task<TLObject> RequestSafe(TLObject request)
        {
            // If we aren't connected, there's no point in continuing
            if (Sender == null || !Sender.CommunicationEstablished)
                throw new InvalidOperationException("Cannot send data over a closed connection.");

            // Create objects to hold the result and whether or not
            // the result should be returned
            TLObject result = null;
            bool Success = false;

            // Loop until the result should be sent back
            while(!Success)
            {
                try
                {
                    // Send the request to the server
                    result = await Sender.Send(request);

                    // If the server did not respond with a typical
                    // message (maybe a  vector), just return the result
                    if (result.InternalType == JTokenType.Array)
                    {
                        Success = true;
                        continue;
                    }
                    if ((string)result["_"] == "rpc_error")
                    {
                        var ErrorMessage = (string)result["error_message"];

                        if (ErrorMessage.StartsWith("FLOOD_WAIT_"))
                        {
                            // Flood waited just gets an exception.  There's nothing we can do.
                            throw new FloodWaitException(int.Parse(Regex.Match(ErrorMessage, @"\d+").Value));
                        }
                        else if (ErrorMessage.StartsWith("PHONE_MIGRATE_"))
                        {
                            // Determine which DC the server wants us to conect to
                            var newDC = int.Parse(Regex.Match(ErrorMessage, @"\d+").Value);

                            // Prevent loops by not allowing the DC to be the same
                            if (newDC == Session.DataCenter.DataCenterId)
                                throw new InvalidOperationException($"Already connected to DC{newDC}.");

                            // Switch to the DC and try again
                            await MigrateToDC(newDC);
                            continue;
                        }
                        else if (ErrorMessage.StartsWith("FILE_MIGRATE_"))
                        {
                            //var resultString = System.Text.RegularExpressions.Regex.Match(ErrorMessage, @"\d+").Value;
                            //var dcIdx = int.Parse(resultString);
                            //throw new FileMigrationException(dcIdx);
                        }
                        else if (ErrorMessage.StartsWith("USER_MIGRATE_"))
                        {
                            //var resultString = System.Text.RegularExpressions.Regex.Match(ErrorMessage, @"\d+").Value;
                            //var dcIdx = int.Parse(resultString);
                            //throw new UserMigrationException(dcIdx);
                        }
                        else if (ErrorMessage.StartsWith("NETWORK_MIGRATE_"))
                        {
                            //var resultString = System.Text.RegularExpressions.Regex.Match(ErrorMessage, @"\d+").Value;
                            //var dcIdx = int.Parse(resultString);
                            //throw new NetworkMigrationException(dcIdx);
                        }
                        else if (ErrorMessage == "AUTH_KEY_UNREGISTERED")
                        {
                            throw new NotSignedInException("The user is either not signed in or has been manually logged out");
                        }
                        else if (ErrorMessage == "AUTH_RESTART")
                        {
                            throw new AuthRestartException("A login attempt was already started.");
                        }
                        else if (ErrorMessage == "PHONE_CODE_INVALID")
                        {
                            //throw new InvalidPhoneCodeException("The numeric code used to authenticate does not match the numeric code sent by SMS/Telegram");
                        }
                        else if (ErrorMessage == "PHONE_NUMBER_UNOCCUPIED")
                        {
                            throw new PhoneNumberUnoccupiedException("The account does not exist.  Please create one.");
                        }
                        else if (ErrorMessage == "SESSION_PASSWORD_NEEDED")
                        {
                            throw new CloudPasswordNeededException("A password is needed to complete the sign-in process");
                        }
                        else if (ErrorMessage == "SRP_ID_INVALID")
                        {
                            throw new SrpIDInvalidException("The password was not entered in time");
                        }
                        else
                        {
                            throw new InvalidOperationException(ErrorMessage);
                        }
                    }

                    Success = true;
                }
                catch (Exception ex)
                {
                    throw;
                }
            }

            return result;
        }
        #endregion

        #region Events
        /// <summary>
        /// Occurs when the server is ready to accept a phone number to begin the sign-on/sign-up process
        /// </summary>
        public event EventHandler<TLObjectEventArgs> PhoneNumberRequestedEvent;
        /// <summary>
        /// Occurs when the server has sent the auth code and is waiting for the user to send the auth code back
        /// </summary>
        public event EventHandler<TLObjectEventArgs> AuthCodeRequestedEvent;
        /// <summary>
        /// Occurs during a sign-in process when the account is password protected and a requires a password
        /// </summary>
        public event EventHandler<TLObjectEventArgs> CloudPasswordRequestedEvent;
        /// <summary>
        /// Occurs during the sign-up process when the server is waiting for the user's name to create the account
        /// </summary>
        public event EventHandler<TLObjectEventArgs> NameRequestedEvent;
        /// <summary>
        /// Occurs when the the user needs to accept the Terms of Service
        /// </summary>
        public event EventHandler<TLObjectEventArgs> TermsOfServiceRequestedEvent;
        /// <summary>
        /// Occurs when a change to the current user has occurred (sign-on or username/name/number change)
        /// </summary>
        public event EventHandler<TLObjectEventArgs> UpdateUserEvent;
        /// <summary>
        /// Occurs when the currently logged on user has been signed out
        /// </summary>
        public event EventHandler<TLObjectEventArgs> ClientLoggedOutEvent;
        /// <summary>
        /// Occurs when a new update is received from the server.
        /// Note: This is ALL updates.  To get Messages only,
        /// subscribe to <see cref="NewMessageEvent"/>.
        /// </summary>
        public event EventHandler<TLObjectEventArgs> NewUpdateEvent;
        /// <summary>
        /// Occurs when a new message is received.
        /// Note: This is JUST messages.  To get all updates,
        /// subscribe to <see cref="NewUpdateEvent"/>.
        /// </summary>
        public event EventHandler<TLObjectEventArgs> NewMessageEvent;

        private void OnPhoneNumberRequested(TLObjectEventArgs e)
        {
            var args = new object[] { this, e };
            PhoneNumberRequestedEvent.RaiseEventSafe(ref args);
        }
        private void OnAuthCodeRequested(TLObjectEventArgs e)
        {
            var args = new object[] { this, e };
            AuthCodeRequestedEvent.RaiseEventSafe(ref args);
        }
        private void OnCloudPasswordRequested(TLObjectEventArgs e)
        {
            var args = new object[] { this, e };
            CloudPasswordRequestedEvent.RaiseEventSafe(ref args);
        }
        private void OnUpdateUser(TLObjectEventArgs e)
        {
            if ((string)e.TLObject["_"] == "auth.authorization")
            {
                e = new TLObjectEventArgs(new TLObject(e.TLObject["user"]));
            }

            if (e.TLObject["status"]?["expires"] != null)
            {
                Session.SessionExpires = (int)e.TLObject["status"]["expires"];
            }

            Session.TLUser = e.TLObject;

            var args = new object[] { this, e };
            UpdateUserEvent.RaiseEventSafe(ref args);

            Session.Save();
        }
        private void OnNameRequested(TLObjectEventArgs e)
        {
            var args = new object[] { this, e };
            NameRequestedEvent.RaiseEventSafe(ref args);
        }
        private void OnClientLoggedOut(TLObjectEventArgs e)
        {
            // We should clear information when this happens
            CurrentUser = null;
            Session.Reset();

            var args = new object[] { this, e };
            ClientLoggedOutEvent.RaiseEventSafe(ref args);
        }
        private async void OnUpdate(TLObjectEventArgs e)
        {
            Session.KnownPeers.ParsePeers(e.TLObject);

            switch ((string)e.TLObject["_"])
            {
                case "updatesCombined":
                case "updates":
                    foreach (var update in (JArray)e.TLObject["updates"])
                    {
                        ProcessUpdate(new TLObject(update), e.TLObject);
                    }
                    break;
                case "updateShort":
                    ProcessUpdate(new TLObject(e.TLObject["update"]), e.TLObject);
                    break;
                case "updateShortMessage":
                    var msg_id = (int)e.TLObject["id"];
                    var sender_id = (int)(((bool)e.TLObject["out"]) ? Session.TLUser["id"] : e.TLObject["user_id"]);
                    var recipient_id = (int)(((bool)e.TLObject["out"]) ? e.TLObject["user_id"] : Session.TLUser["id"]);

                    TLObject sender_peer;
                    TLObject recipient_peer;

                    if ((bool)e.TLObject["out"])
                    {
                        sender_peer = Session.TLUser;
                        recipient_peer = await GetPeerFromID(recipient_id);
                    }
                    else
                    {
                        sender_peer = await GetPeerFromID(sender_id);
                        recipient_peer = Session.TLUser;
                    }

                    TLObject updateShort = Schema.updateNewMessage(new
                    {
                        message = Schema.message(new
                        {
                            @out = e.TLObject["out"],
                            mentioned = e.TLObject["mentioned"],
                            media_unread = e.TLObject["media_unread"],
                            silent = e.TLObject["silent"],
                            id = msg_id,
                            from_id = sender_id,
                            to_id = Schema.peerUser(new { user_id = recipient_id }),
                            fwd_from = e.TLObject["fwd_from"],
                            via_bot_id = e.TLObject["via_bot_id"],
                            reply_to_msg_id = e.TLObject["reply_to_msg_id"],
                            date = e.TLObject["date"],
                            message = e.TLObject["message"],
                            entities = e.TLObject["entities"]
                        }),
                        pts = e.TLObject["pts"],
                        pts_count = e.TLObject["pts_count"]
                    });

                    e.TLObject["users"] = new JArray()
                    {
                        sender_peer, recipient_peer
                    };

                    ProcessUpdate(new TLObject(updateShort), e.TLObject);
                    break;
                default:
                    // Probably back end.  Don't need it
                    break;
            }
        }
        private void OnNewMessage(TLObjectEventArgs e)
        {
            var args = new object[] { this, e };
            NewMessageEvent.RaiseEventSafe(ref args);
        }
        #endregion

        #region Logging In
        /// <summary>
        /// Formats a US/CA number to be used by telegram
        /// </summary>
        /// <param name="RawNumer">The raw unformatted input</param>
        /// <returns>The formatted number if successful.  Otherwise an empty string.</returns>
        public string FormatNumber(string RawNumer)
        {
            // Cannot work with an empty value
            if (string.IsNullOrEmpty(RawNumer)) return string.Empty;

            // Strip out all the non-numeric characters
            RawNumer = string.Concat(RawNumer.Where(x => char.IsDigit(x)));

            // The string didn't have any digits apparently
            if (string.IsNullOrEmpty(RawNumer)) return string.Empty;

            // Add the country code if applicable
            if (RawNumer[0] != '1') RawNumer = $"1{RawNumer}";

            // At this point, the number should be complete...
            if (RawNumer.Length != 11) return string.Empty;

            return $"+{RawNumer}";
        }

        /// <summary>
        /// Sends the initial sing-up/sing-in requst to the server
        /// </summary>
        /// <param name="PhoneNumber">The phone number to log in with</param>
        public async Task<bool> SetPhoneNumber(string PhoneNumber)
        {
            if (string.IsNullOrEmpty(PhoneNumber = FormatNumber(PhoneNumber)))
                throw new Exception("The phone number provided is not supported.");

            // If the user is sugned in already, what's the point?
            // ToDo: Throw exception?
            if (IsUserAuthorized()) return false;

            try
            {
                // Save the number for future reference
                this.PhoneNumber = PhoneNumber;

                // Send the auth code  and save the information about it
                AuthCodeInfo = await RequestSafe(Schema.auth.sendCode(new
                {
                    phone_number = PhoneNumber,
                    api_id = ApiId,
                    api_hash = ApiHash,
                    settings = Schema.codeSettings(new
                    {
                        allow_flashcall = false,
                        current_number = false, // will be ignored anyway
                        allow_app_hash = false
                    })
                }));

                // Tell the user we are waiting for it
                OnAuthCodeRequested(new TLObjectEventArgs(AuthCodeInfo));

                // Return success
                return true;
            }
            catch (AuthRestartException)
            {
                return await SetPhoneNumber(PhoneNumber);
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, $"Unable to send the auth code using provided phone number.\n\n{ex.Message}");
                return false;
            }
        }
        /// <summary>
        /// Sends a provided auth code to the server
        /// </summary>
        /// <param name="AuthCode">An auth code</param>
        public async Task<bool> SendAuthCode(string AuthCode)
        {
            // If the auth code was never sent or a valid
            // was has not been received, we cannot continue
            if (AuthCodeInfo == null || string.IsNullOrEmpty(AuthCode)) return false;

            try
            {
                // Save the auth code for later use
                this.AuthCode = AuthCode;

                // Attempt to send the auth back to server.
                // We will either be logged in successfully
                // or an error will be received indicating
                // that something was wrong
                CurrentUser = await RequestSafe(Schema.auth.signIn(new
                {
                    phone_number = PhoneNumber,
                    phone_code_hash = (string)AuthCodeInfo["phone_code_hash"],
                    phone_code = AuthCode
                }));

                // Let the user know we got account
                OnUpdateUser(new TLObjectEventArgs(CurrentUser));

                // Success
                return true;
            }
            catch (PhoneNumberUnoccupiedException)
            {
                // In the case the account doesn't exist,
                // tell the user that we need more info
                OnNameRequested(null);

                // Success
                return true;
            }
            catch (CloudPasswordNeededException)
            {
                // In the case the account does exist,
                // but is password protected
                CloudPasswordInfo = await GetPasswordSetting();
                OnCloudPasswordRequested(new TLObjectEventArgs(CloudPasswordInfo));

                // Success
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, $"Unable to verify the auth code.\n\n{ex.Message}");
                return false;
            }
        }
        /// <summary>
        /// If a previous attempt to send an auth code was not received by the user,
        /// this method can be used to resend using another method.
        /// </summary>
        public async Task<bool> ReSendAuthCode()
        {
            // We need a phone number and we need to have sent the auth at least once before
            if (string.IsNullOrEmpty(PhoneNumber) || AuthCodeInfo == null) return false;

            try
            {
                AuthCodeInfo = await RequestSafe(Schema.auth.resendCode(new
                {
                    phone_number = PhoneNumber,
                    phone_code_hash = (string)AuthCodeInfo["phone_code_hash"]
                }));

                // Tell the user we are waiting for it
                OnAuthCodeRequested(new TLObjectEventArgs(AuthCodeInfo));

                return true;
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, $"Failed to resend the auth code.\n\n{ex.Message}");
                return false;
            }
        }
        /// <summary>
        /// Sends name information to the server in order to create a new account
        /// </summary>
        /// <param name="FirstName">The first name (required)</param>
        /// <param name="LastName">The last name (optional)</param>
        public async Task<bool> CreateAccount(string FirstName, string LastName = "")
        {
            // We need each of these in order to continue
            if (string.IsNullOrEmpty(PhoneNumber) || AuthCodeInfo == null || string.IsNullOrEmpty(AuthCode) || string.IsNullOrEmpty(FirstName))
            {
                return false;
            }

            try
            {
                // Attempt to sign up with the given information
                CurrentUser = await RequestSafe(Schema.auth.signUp(new
                {
                    phone_number = PhoneNumber,
                    phone_code_hash = (string)AuthCodeInfo["phone_code_hash"],
                    phone_code = AuthCode,
                    first_name = FirstName,
                    last_name = LastName
                }));

                // Pass the account on to the user
                OnUpdateUser(new TLObjectEventArgs(CurrentUser));

                // Success!
                return true;
            }
            catch (Exception ex)
            {
                Logger.Log(Logger.Level.Error, $"Unable to create an account with the current information.\n\n{ex.Message}");
                return false;
            }
        }
        /// <summary>
        /// Gets Password information like hint, etc
        /// </summary>
        public async Task<TLObject> GetPasswordSetting()
        {
            try
            {
                return await RequestSafe(Schema.account.getPassword);
            }
            catch (Exception ex)
            {
                return null;
            }
        }
        public async Task<bool> MakeAuthWithPasswordAsync(string password_str)
        {
            try
            {
                CurrentUser = await RequestSafe(Schema.auth.checkPassword(new
                {
                    password = CloudPasswordHelper.ComputePasswordCheck(CloudPasswordInfo, password_str)
                }));

                // Let the user know we got account
                OnUpdateUser(new TLObjectEventArgs(CurrentUser));

                return true;
            }
            catch (SrpIDInvalidException)
            {
                try
                {
                    // Most likely, the user took too long to enter the password.
                    CloudPasswordInfo = await GetPasswordSetting();
                    CurrentUser = await RequestSafe(Schema.auth.checkPassword(new
                    {
                        password = CloudPasswordHelper.ComputePasswordCheck(CloudPasswordInfo, password_str)
                    }));

                    // Let the user know we got account
                    OnUpdateUser(new TLObjectEventArgs(CurrentUser));

                    return true;
                }
                catch (Exception ex)
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        public bool IsUserAuthorized()
        {
            return Session.TLUser != null;
        }
        #endregion
        #region Helpers
        private async Task<TLObject> GetPeerFromID(int ID)
        {
            PeerInfo? peer = null;
            TLObject LastPeer = null;
            TLObject LastMessage = null;
            TLObject LastResult = null;

            try
            {
                // Loop until we find the peer
                while ((peer = Session.KnownPeers.GetPeer(ID)) == null)
                {
                    // If we've already done this before and we didn't get a
                    // slice (meaning we've reached the end) we can reasonably
                    // return peer
                    if (LastResult != null && (string)LastResult["_"] != "messages.dialogsSlice") break;

                    // Get the dialogs
                    LastResult = await RequestSafe(Schema.messages.getDialogs(new
                    {
                        exclude_pinned = false,
                        folder_id = ChatFolderFolder.Normal,
                        offset_date = LastMessage?["date"] ?? 0,
                        offset_id = LastMessage?["id"] ?? 0,
                        offset_peer = LastPeer?["peer"] ?? Schema.inputPeerEmpty,
                        limit = 5,
                        hash = 0
                    }));

                    // ToDo: These will throw errors if the user has no dialogs, etc
                    LastPeer = new TLObject(((JArray)LastResult["dialogs"]).Reverse().First(x => x["top_message"] != null));
                    LastMessage = new TLObject(((JArray)LastResult["messages"]).First(x => (int)x["id"] == (int)LastPeer["top_message"]));
                    
                    Session.KnownPeers.ParsePeers(LastResult);
                }

                if (peer != null)
                {
                    //if (peer.Value.Type == "user")
                    //{
                    //    TLObject tmp = await RequestSafe(Schema.users.getUsers(new
                    //    {
                    //        id = new TLObject[]
                    //        { 
                    //            Schema.inputUser(new
                    //            {
                    //                user_id = peer.Value.ID,
                    //                access_hash = peer.Value.AccessHash
                    //            })
                    //        }
                    //    }));

                    //    if (((JArray)tmp).Count > 0) return new TLObject(tmp[0]);
                    //}
                    //else if (peer.Value.Type == "channel")
                    //{
                    //    TLObject tmp = await RequestSafe(Schema.channels.getChannels(new
                    //    {
                    //        id = new TLObject[]
                    //        {
                    //            Schema.inputChannel(new
                    //            {
                    //                channel_id = peer.Value.ID,
                    //                access_hash = peer.Value.AccessHash
                    //            })
                    //        }
                    //    }));

                    //    if (((JArray)tmp["chats"]).Count > 0) return new TLObject(tmp["chats"][0]);
                    //}
                }

                return null;
            }
            catch (Exception ex)
            {
                return null;
            }
        }
        public async Task<TLObject> SendMessage(TLObject Peer, string Message)
        {
            if (!IsUserAuthorized())
                throw new InvalidOperationException("You must be logged in to send a message");

            if ((bool)Peer["min"] == true) Peer = await GetPeerFromID((int)Peer["id"]);

            TLObject rec = null;

            switch ((string)Peer["_"])
            {
                case "user":
                    rec = Schema.inputPeerUser(new
                    {
                        user_id = Peer["id"],
                        access_hash = (long)Peer["access_hash"]
                    });
                    break;
                case "channel":
                    rec = Schema.inputPeerChannel(new
                    {
                        channel_id = Peer["id"],
                        access_hash = (long)Peer["access_hash"]
                    });
                    break;
            }

            (var ParsedMessage, var ParsedEntities) = Helpers.ParseEntities(Message);

            var sentInfo = await RequestSafe(
                Schema.messages.sendMessage(new
                {
                    no_webpage = false,
                    silent = false,
                    background = false,
                    clear_draft = true,
                    peer = rec,
                    reply_to_msg_id = 0,
                    message = ParsedMessage,
                    random_id = Helpers.GenerateRandomLong(),
                    entities = ParsedEntities
                })
            );

            if ((string)sentInfo["_"] == "updateShortSentMessage") return sentInfo;

            var searchPhrase = "";
            if ((string)Peer["_"] == "user")
            {
                searchPhrase = "updateNewMessage";
            }
            else if ((string)Peer["_"] == "channel")
            {
                searchPhrase = "updateNewChannelMessage";
            }

            return new TLObject(((JArray)((TLObject)sentInfo)["updates"]).First(x => (string)x["_"] == searchPhrase)["message"]);
        }
        public async Task<TLObject> EditMessage(TLObject Peer, TLObject Message, string NewMessage)
        {
            if (!IsUserAuthorized())
                throw new InvalidOperationException("You must be logged in to edit a message");

            if ((bool)Peer["min"] == true) Peer = await GetPeerFromID((int)Peer["id"]);

            TLObject rec = null;

            switch ((string)Peer["_"])
            {
                case "user":
                    rec = Schema.inputPeerUser(new
                    {
                        user_id = Peer["id"],
                        access_hash = (long)Peer["access_hash"]
                    });
                    break;
                case "channel":
                    rec = Schema.inputPeerChannel(new
                    {
                        channel_id = Peer["id"],
                        access_hash = (long)Peer["access_hash"]
                    });
                    break;
            }

            (var ParsedMessage, var ParsedEntities) = Helpers.ParseEntities(NewMessage);

            return await RequestSafe(
                Schema.messages.editMessage(new
                {
                    no_webpage = false,
                    peer = rec,
                    id = Message["id"],
                    message = ParsedMessage,
                    entities = ParsedEntities
                })
            );

            return null;
        }
        //public async Task<TLObject> SendReply(TLObject Message, string message)
        //{
        //    if (!IsUserAuthorized())
        //        throw new InvalidOperationException("Authorize user first!");

        //    if ((string)Message["_"] != "Message")
        //        throw new InvalidOperationException("Cannot reply to something that isn't a message.");

        //    switch ((string)peer["_"])
        //    {
        //        case "peerChat":
        //            peer = schema.inputPeerChat(new
        //            {
        //                chat_id = (int)peer["chat_id"]
        //            });
        //            break;
        //        default:

        //            break;
        //    }

        //    return await SendRequestAsync(
        //        schema.messages.sendMessage(new
        //        {
        //            peer,
        //            message,
        //            random_id = Helpers.GenerateRandomLong()
        //        })
        //    );
        //}
        //public async Task<JObject> GetContactsAsync()
        //{
        //    if (!IsUserAuthorized())
        //        throw new InvalidOperationException("Authorize user first!");

        //    JObject req = schema.contacts.getContacts(new { hash = 0 });

        //    return await SendRequestAsync(req);
        //}
        //public async Task<JObject> GetHistoryAsync(JObject peer, int offset_id = 0, int offset_date = 0, int add_offset = 0, int limit = 100, int max_id = 0, int min_id = 0, string hash = "")
        //{
        //    if (!IsUserAuthorized())
        //        throw new InvalidOperationException("Authorize user first!");

        //    var req = schema.messages.getHistory(new
        //    {
        //        peer,
        //        offset_id,
        //        offset_date,
        //        add_offset,
        //        limit,
        //        max_id,
        //        min_id,
        //        hash
        //    });

        //    return await SendRequestAsync(req);
        //}
        //public async Task SendPingAsync()
        //{
        //    await _sender.SendPingAsync();
        //}
        //public async Task<JObject> SendTypingAsync(JObject peer)
        //{
        //    var req = schema.messages.setTyping(new
        //    {
        //        action = schema.sendMessageTypingAction,
        //        peer
        //    });
        //    return await SendRequestAsync(req);
        //}
        //public async Task<JObject> GetUserDialogsAsync(bool exclude_pinned = false, int folder_id = 0, int offset_date = 0, int offset_id = 0, JObject offsetPeer = null, int limit = 100, int hash = 0)
        //{
        //    if (!IsUserAuthorized())
        //        throw new InvalidOperationException("Authorize user first!");

        //    var req = schema.messages.getDialogs(new
        //    {
        //        exclude_pinned,
        //        folder_id,
        //        offset_date,
        //        offset_id,
        //        offset_peer = offsetPeer ?? schema.inputPeerSelf,
        //        limit,
        //        hash
        //    });

        //    return await SendRequestAsync(req);
        //}
        ///// <summary>
        ///// Serch user or chat. API: contacts.search#11f812d8 q:string limit:int = contacts.Found;
        ///// </summary>
        ///// <param name="q">User or chat name</param>
        ///// <param name="limit">Max result count</param>
        ///// <returns></returns>
        //public async Task<JObject> SearchUserAsync(string q, int limit = 10)
        //{
        //    if (!IsUserAuthorized())
        //        throw new InvalidOperationException("Authorize user first!");

        //    JObject req = schema.contacts.search(new
        //    {
        //        q,
        //        limit
        //    });

        //    return await SendRequestAsync(req);
        //}

        //public async Task<TLAbsUpdates> SendUploadedPhoto(TLAbsInputPeer peer, TLAbsInputFile file, string caption)
        //{
        //    return await SendRequestAsync<TLAbsUpdates>(new TLRequestSendMedia()
        //    {
        //        RandomId = Helpers.GenerateRandomLong(),
        //        Background = false,
        //        ClearDraft = false,
        //        Media = new TLInputMediaUploadedPhoto() { File = file, Caption = caption },
        //        Peer = peer
        //    });
        //}

        //public async Task<TLAbsUpdates> SendUploadedDocument(
        //    TLAbsInputPeer peer, TLAbsInputFile file, string caption, string mimeType, TLVector<TLAbsDocumentAttribute> attributes)
        //{
        //    return await SendRequestAsync<TLAbsUpdates>(new TLRequestSendMedia()
        //    {
        //        RandomId = Helpers.GenerateRandomLong(),
        //        Background = false,
        //        ClearDraft = false,
        //        Media = new TLInputMediaUploadedDocument()
        //        {
        //            File = file,
        //            Caption = caption,
        //            MimeType = mimeType,
        //            Attributes = attributes
        //        },
        //        Peer = peer
        //    });
        //}

        //public async Task<TLFile> GetFile(TLAbsInputFileLocation location, int filePartSize, int offset = 0)
        //{
        //    TLFile result = null;
        //    result = await SendRequestAsync<TLFile>(new TLRequestGetFile()
        //    {
        //        Location = location,
        //        Limit = filePartSize,
        //        Offset = offset
        //    });
        //    return result;
        //}
        #endregion
        #region Update Handling
        private void ProcessUpdate(TLObject update, TLObject original)
        {
            var args = new object[] { this, new TLObjectEventArgs(update) };
            NewUpdateEvent.RaiseEventSafe(ref args);

            switch((string)update["_"])
            {
                case "updateNewMessage":
                case "updateNewChannelMessage":
                    //var from = ((JArray)original["users"]).Where(x => (int)x["id"] == (int)update["message"]["from_id"]).First();
                    if (update["message"]["from_id"] != null && update["message"]["from_id"].Type != JTokenType.Null)
                    {
                        update["from_user"] = ((JArray)original["users"]).Where(x => (int)x["id"] == (int)update["message"]["from_id"]).FirstOrDefault();
                    }
                    else
                    {
                        update["from_user"] = null;
                    }
                    

                    switch ((string)update["message"]["to_id"]["_"])
                    {
                        case "peerChannel":
                            update["to_peer"] = ((JArray)original["chats"]).Where(x => (int)x["id"] == (int)update["message"]["to_id"]["channel_id"]).First();
                            break;
                        case "peerUser":
                            update["to_peer"] = ((JArray)original["users"]).Where(x => (int)x["id"] == (int)update["message"]["to_id"]["user_id"]).First();
                            break;
                    }

                    OnNewMessage(new TLObjectEventArgs(update));
                    break;
                case "updateUserName":

                    if ((int)update["user_id"] == (int)Session.TLUser["id"])
                    {
                        ((JToken)update)
                            .Children()
                            .Where(x => x.Type == JTokenType.Property && ((JProperty)x).Name != "_" && ((JProperty)x).Name != "user_id")
                            .ToList()
                            .ForEach(x => Session.TLUser[((JProperty)x).Name] = ((JProperty)x).Value);

                        OnUpdateUser(new TLObjectEventArgs(Session.TLUser));
                    }

                    break;

            }

        }
        //private async Task<JObject> GetUpdateStateAsync()
        //{
        //    if (!IsUserAuthorized())
        //        throw new InvalidOperationException("Authorize user first!");

        //    var req = schema.updates.getState;

        //    await SendRequestAsync(req);

        //    pts = req.Response.pts;
        //    date = req.Response.date;
        //    qts = req.Response.qts;

        //    return req.Response;
        //}
        //private async Task<JObject> GetUpdateDifferenceAsync(int pts_total_limit)
        //{
        //    if (!IsUserAuthorized())
        //        throw new InvalidOperationException("Authorize user first!");

        //    if (pts == 0)
        //        await GetUpdateStateAsync();

        //    JObject req = schema.updates.getDifference(new
        //    {
        //        pts,
        //        pts_total_limit,
        //        date,
        //        qts
        //    });

        //    return await SendRequestAsync(req);
        //}
        //public async Task<TLObject> GetUpdatesAsync(int pts_total_limit = 100)
        //{
        //    if (!IsUserAuthorized())
        //        throw new InvalidOperationException("Authorize user first!");

        //    TLObject res = await GetUpdateDifferenceAsync(pts_total_limit);

        //    if (res.GetMember("state") != null && (string)res.GetMember("state")["_"] == "updates.state")
        //    {
        //        pts = (int)res.GetMember("state")["pts"];
        //        qts = (int)res.GetMember("state")["qts"];
        //        date = (int)res.GetMember("state")["date"];
        //    }

        //    return res;
        //}
        #endregion

        /// <summary>
        /// Starts the client in the background
        /// </summary>
        public void Start()
        {
            Task.Run(async () =>
            {
                try
                {
                    // Attempt to make the initial connection
                    await Connect();

                    // The user was at least authorized when we last started.
                    if (IsUserAuthorized())
                    {
                        try
                        {
                            // We need to send at least one request so that
                            // Telegram will start sending us updates
                            TLObject tmp = await RequestSafe(Schema.users.getUsers(new {
                                id = new TLObject[] { Schema.inputUserSelf }
                            }));

                            // Alert to the fact we are signed in.
                            CurrentUser = new TLObject(tmp[0]);
                            OnUpdateUser(new TLObjectEventArgs(CurrentUser));

                            // Stop so that we don't ask for the phone number
                            return;
                        }
                        catch (NotSignedInException)
                        {
                            // If the user isn't signed in anymore, we can continue, and start the process over.
                            Logger.Log(Logger.Level.Error, $"We were logged in before but are no longer authorized.  Starting the process over.");
                            OnClientLoggedOut(new TLObjectEventArgs(CurrentUser));
                        }
                        catch (Exception ex)
                        {
                            // An unknown error at this point.  Since we don't know what happened, let's fail and
                            // wait for the tickets to be opened...
                            Logger.Log(Logger.Level.Error, $"Failed to start the client.\n\n${ex.Message}");
                            return;
                        }
                    }

                    OnPhoneNumberRequested(null);
                }
                catch (Exception ex)
                {
                    Logger.Log(Logger.Level.Error, $"Failed to start the client.\n\n${ex.Message}");
                }
            });
        }

    }
}
