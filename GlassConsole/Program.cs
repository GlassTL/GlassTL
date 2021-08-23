using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GlassTL.Telegram;

namespace GlassConsole
{
    using GlassTL.EventArgs;

    class Program
    {
        /// <summary>
        /// Allows quick and easy access to Telegram's API
        /// </summary>
        private static readonly TelegramClient botClient = new TelegramClient(null, null, true);

        /// <summary>
        /// The commands and actions to run for each
        /// </summary>
        private static readonly Dictionary<string, Action<string[]>> BotCommands = new Dictionary<string, Action<string[]>>()
        {
            { "msg", async (a) => await botClient.SendMessage(a[0], a[1]) }
        };

        private static long LastNumber = 0;
        private static long LastUserID = 0;

        static async Task Main(string[] args)
        {
            // Enable logging to the Console
            Logger.LoggerHandlerManager
                .AddHandler(new DebugConsoleLoggerHandler());

            // Capture CTRL+C to gracefully handle forceful closing
            Console.CancelKeyPress += new ConsoleCancelEventHandler(SigIntHandler);

            // Subscribe to New Message events
            botClient.NewMessageEvent += BotClient_NewMessageEvent;

            // Start the client
            // This will connect to the servers and prepare to log in.
            // Since we did not define a handler for phone number/auth
            // code/password/etc, these will be auto-handled by the client.
            // The client will prefer subscribed events over auto-handling
            await botClient.Start();

            // Define an array to hold the individual parts of the console command
            var ConsoleCommands = Array.Empty<string>();
            // Determines if we should keep looping or not
            var ExitRequested = false;

            // Loop de loop
            while (!ExitRequested)
            {
                // Read the next command
                ConsoleCommands = Console.ReadLine().Split(' ');

                // The first item should be the command
                switch (ConsoleCommands[0].ToLower())
                {
                    case "quit":
                        ExitRequested = true;
                        continue;
                    default:
                        // Determine if the command is in the set of predetermined actions
                        if (BotCommands.ContainsKey(ConsoleCommands[0].ToLower()))
                        {
                            // Invoke the command
                            BotCommands[ConsoleCommands[0].ToLower()](new string[] { ConsoleCommands[1].ToLower(), string.Join(' ', ConsoleCommands.Skip(2)) });
                            continue;
                        }

                        // If unable to handle above, there's nothing else we can do.
                        Console.WriteLine($"Unknown Command: {ConsoleCommands[0]}");
                        continue;
                }
            }
        }

        private static async void BotClient_NewMessageEvent(object sender, TLObjectEventArgs e)
        {
            try
            {
                if (e.TLObject["to_peer"]["id"].ToString() != "1330858993") return;

                Console.WriteLine($"{e.TLObject["from_user"]["first_name"]} ({e.TLObject["from_user"]["id"]}) -> {e.TLObject["message"]["message"]}");

                if (!long.TryParse(e.TLObject["from_user"]["id"].ToString(), out long userUD)
                    || !long.TryParse(e.TLObject["message"]["message"].ToString(), out long messageLong)
                    || userUD == LastUserID
                    || LastNumber + 1 != messageLong)
                {
                    await botClient.DeleteMessage(new GlassTL.Telegram.MTProto.TLObject(e.TLObject["from_user"]), new GlassTL.Telegram.MTProto.TLObject(e.TLObject["message"]));
                    return;
                }

                LastUserID = userUD;
                LastNumber = messageLong;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static void SigIntHandler(object sender, ConsoleCancelEventArgs e)
        {
            // Are you sure that you are sure???
            Console.WriteLine("Are you sure you want to abort? (Y/n)");
            e.Cancel = (new string[] { "n", "no" }).Contains(Console.ReadLine().ToLower());
        }
    }
}
