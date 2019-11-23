using System;
using System.Linq;
using System.Windows.Forms;
using GlassTL.Telegram;
using GlassTL.Telegram.MTProto;
using Newtonsoft.Json.Linq;

namespace GlassTL.Examples
{
    public partial class MainForm : Form
    {
        private TelegramClient botClient = null;

        public MainForm()
        {
            InitializeComponent();

            Logger.LoggerHandlerManager
                .AddHandler(new ConsoleLoggerHandler());

            botClient = new TelegramClient();

            botClient.PhoneNumberRequestedEvent += BotClient_PhoneNumberRequestedEventHandler;
            botClient.AuthCodeRequestedEvent += BotClient_AuthCodeRequestedEventHandler;
            botClient.CloudPasswordRequestedEvent += BotClient_CloudPasswordRequestedEventHandler;
            botClient.NameRequestedEvent += BotClient_FirstNameRequestedEventHandler;
            botClient.TermsOfServiceRequestedEvent += BotClient_TermsOfServiceRequestedEventHandler;
            botClient.UpdateUserEvent += BotClient_UpdateUserEventHandler;
            botClient.ClientLoggedOutEvent += BotClient_ClientLoggedOutEventHandler;
            botClient.NewMessageEvent += BotClient_NewMessageEventHandler;

            botClient.Start();
        }
        private void MainForm_Load(object sender, EventArgs e)
        {

        }

        private async void BotClient_NewMessageEventHandler(object sender, TLObjectEventArgs e)
        {
            try
            {
                if (RawUpdatesCheckbox.Checked)
                {
                    AddToLog(e.TLObject.ToString());
                }

                var FromUser = e.TLObject["from_user"];
                var ToPeer = e.TLObject["to_peer"];

                var FromName = "";
                var ToName = "";
                var FromId = 0;
                var ToId = 0;

                if (ToPeer.Value<string>("_") == "channel")
                {
                    ToName = ToPeer.Value<string>("title");
                }
                else
                {
                    ToName = $"{ToPeer.Value<string>("first_name")} {ToPeer.Value<string>("last_name")}".Trim();
                }

                FromName = FromUser.Type != JTokenType.Null
                    ? $"{FromUser.Value<string>("first_name")} {FromUser.Value<string>("last_name")}".Trim()
                    : default;
                if (string.IsNullOrEmpty(FromName)) FromName = "No-Name";
                if (string.IsNullOrEmpty(ToName)) ToName = "No-Name";
                FromId = FromUser.Type != JTokenType.Null
                    ? FromUser.Value<int>("id")
                    : default;
                ToId = ToPeer.Value<int>("id");

                AddToLog($"{FromName} ({FromId}) -> {ToName} ({ToId}) >>> {e.TLObject["message"].Value<string>("message")}");

                if (FromUser.Type == JTokenType.Null) return;
                if ((bool)e.TLObject["message"]["out"]) return;
                if (!(new long[] { 876650892, 960462, 295152997, 976906477 }).Contains((long)FromUser["id"])) return;
                //if (!(new long[] { 848427085, 234480941, 313742192, 537790376 }).Contains(FromId)) return;

                TLObject ShouldReplyTo;
                if ((int)botClient.CurrentUser["id"] == (int)ToPeer["id"])
                {
                    ShouldReplyTo = new TLObject(FromUser);
                }
                else
                {
                    ShouldReplyTo = new TLObject(ToPeer);
                }

                switch ((string)e.TLObject["message"]["message"])
                {
                    case ";ping":
                        var first = DateTime.Now;
                        var UpdateEventArgs = await botClient.SendMessage(ShouldReplyTo, "<b>Status</b>\n\n<code>✅ Online</code>\n\n<i>(calculating lag...)</i>");

                        double lag = Math.Round((DateTime.Now - first).TotalMilliseconds, 2);

                        UpdateEventArgs = await botClient.EditMessage(ShouldReplyTo, UpdateEventArgs, $"<b>Status</b>\n\n<code>✅ Online</code>\n\n(<i>reply took {lag}ms</i>)");

                        break;
                    case ";code":
                        await botClient.SendMessage(ShouldReplyTo, $"<code>{e.TLObject["message"].ToString()}</code>");
                        break;
                    case ";whois":
                        //long UserId = -335698371; //-10010356929597;

                        //UpdateEventArgs u = await botClient.GetChat(UserId);

                        //await botClient.SendMessage(ShouldReplyTo, u.RawUpdate.ToString());
                        break;
                    default:

                        break;
                }
            }
            catch (Exception ex)
            {

            }
        }

        private void BotClient_PhoneNumberRequestedEventHandler(object sender, TLObjectEventArgs e)
        {
            phoneNumberTextBox.Text = "";
            phoneNumberTextBox.Enabled = true;
            loginButton.Enabled = true;
        }
        private void BotClient_AuthCodeRequestedEventHandler(object sender, TLObjectEventArgs e)
        {
            phoneNumberTextBox.Enabled = false;
            loginButton.Enabled = false;

            authCodeTextBox.Text = "";
            authCodeTextBox.Enabled = true;
            verifyAuthCodeButton.Enabled = true;
            changeMethodLinkLabel.Enabled = true;

            AddToLog(e.ToString());
            MessageBox.Show((string)e.TLObject["type"]["_"]);
        }
        private void BotClient_FirstNameRequestedEventHandler(object sender, TLObjectEventArgs e)
        {
            authCodeTextBox.Enabled = false;
            verifyAuthCodeButton.Enabled = false;
            changeMethodLinkLabel.Enabled = false;

            firstNameTextBox.Enabled = true;
            lastNameTextBox.Enabled = true;
            verifyNameButton.Enabled = true;
        }
        private void BotClient_CloudPasswordRequestedEventHandler(object sender, TLObjectEventArgs e)
        {
            authCodeTextBox.Enabled = false;
            verifyAuthCodeButton.Enabled = false;
            changeMethodLinkLabel.Enabled = false;

            passwordTextBox.Text = "";
            passwordTextBox.Enabled = true;
            passwordTextBox.Tag = (e.TLObject["hint"] != null && ((string)e.TLObject["hint"]).Length > 0) ? $"A Cloud Password is set on the account:\n\n{(string)e.TLObject["hint"]}" : "";
            verifyPasswordButton.Enabled = true;
            viewHintLinkLabel.Enabled = true;

            ViewHintLinkLabel_LinkClicked(sender, null);
        }
        private void BotClient_TermsOfServiceRequestedEventHandler(object sender, TLObjectEventArgs e)
        {
            //if (MessageBox.Show(e.TermsOfServiceText, "GlassTL Userbot", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
            //{
            //    return;
            //}

            //botClient.SendAcceptTOS(e.TermsOfServiceId);
        }
        private void BotClient_UpdateUserEventHandler(object sender, TLObjectEventArgs e)
        {
            authCodeTextBox.Enabled = false;
            verifyAuthCodeButton.Enabled = false;
            changeMethodLinkLabel.Enabled = false;

            firstNameTextBox.Enabled = false;
            lastNameTextBox.Enabled = false;
            verifyNameButton.Enabled = false;

            passwordTextBox.Enabled = false;
            verifyPasswordButton.Enabled = false;
            viewHintLinkLabel.Enabled = false;

            phoneNumberTextBox.Text = (string)e.TLObject["phone_number"];
            firstNameTextBox.Text = (string)e.TLObject["first_name"];
            lastNameTextBox.Text = (string)(e.TLObject["last_name"] ?? "");

            Text = (string)e.TLObject["first_name"] + ((((string)e.TLObject["last_name"] ?? "").Length > 0) ? $" {(string)e.TLObject["last_name"]}" : "") + " - " + e.TLObject["id"];
            //AddToLog(e.User.ToString());
        }
        private void BotClient_ClientLoggedOutEventHandler(object sender, TLObjectEventArgs e)
        {
            MessageBox.Show("You have been logged out.", "GlassTL Userbot", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async void ChangeMethodLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (await botClient.ReSendAuthCode())
            {
                AddToLog("Switched to next Auth Code Method");
            }
            else
            {
                AddToLog("There is no other way to deliver the Auth Code");
            }
        }
        private void ViewHintLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (passwordTextBox.Tag is null || !(passwordTextBox.Tag is string) || (passwordTextBox.Tag as string).Length == 0)
            {
                MessageBox.Show("A password is required but there is no hint supplied.");
            }
            else
            {
                MessageBox.Show(passwordTextBox.Tag as string, "GlassTL Userbot", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private async void LoginButton_Click(object sender, EventArgs e)
        {
            if (await botClient.SetPhoneNumber(phoneNumberTextBox.Text))
            {
                AddToLog("Phone number sent successfully");
            }
            else
            {
                AddToLog("Check your phone number and try again");
            }
        }
        private async void VerifyAuthCodeButton_Click(object sender, EventArgs e)
        {
            if (await botClient.SendAuthCode(authCodeTextBox.Text))
            {
                AddToLog("Auth Code sent successfully");
            }
            else
            {
                AddToLog("Check the Auth Code and try again");
            }
        }
        private async void VerifyNameButton_Click(object sender, EventArgs e)
        {
            if (await botClient.CreateAccount(firstNameTextBox.Text, lastNameTextBox.Text))
            {
                AddToLog("Account Name set successfully");
            }
            else
            {
                AddToLog("Unable to set Account Name");
            }
        }
        private async void VerifyPasswordButton_Click(object sender, EventArgs e)
        {
            if (await botClient.MakeAuthWithPasswordAsync(passwordTextBox.Text))
            {
                AddToLog("Password sent successfully");
            }
            else
            {
                AddToLog("Please check your password and try again");
            }
        }

        private void DeleteAccountButton_Click(object sender, EventArgs e)
        {
            //if (MessageBox.Show("Are you sure you want to delete your account?", "GlassTL Userbot", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
            //{
            //    return;
            //}

            //botClient.DeleteAccount();
        }

        private void AddToLog(string entry, bool ForceScroll = false)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => AddToLog(entry, ForceScroll)));
                return;
            }

            logListBox.Items.AddRange(entry.Split('\n'));

            if (ActiveControl != logListBox || logListBox.TopIndex >= logListBox.Items.Count + entry.Split('\n').Length - (logListBox.ClientSize.Height / logListBox.ItemHeight) - 1)
            {
                ForceScroll = true;
            }

            if (ForceScroll)
            {
                logListBox.TopIndex = logListBox.Items.Count - 1;
            }
        }


    }
}
