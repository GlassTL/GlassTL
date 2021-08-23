using System;
using System.Collections.Generic;
using System.Text;
using System.Security;

namespace GlassTL.Telegram.Utils
{
    public static class SmartConsole
    {
        public static string ReadLine(string ask)
        {
            Console.WriteLine(ask);
            return Console.ReadLine();
        }
        public static SecureString ReadPassword(string ask)
        {
            Console.WriteLine(ask);
            var pwd = new SecureString();
            while (true)
            {
                var i = Console.ReadKey(true);
                if (i.Key == ConsoleKey.Enter) break;

                if (i.Key == ConsoleKey.Backspace)
                {
                    if (pwd.Length <= 0) continue;
                    pwd.RemoveAt(pwd.Length - 1);
                    Console.Write("\b \b");
                }
                else if (i.KeyChar != '\u0000') // KeyChar == '\u0000' if the key pressed does not correspond to a printable character, e.g. F1, Pause-Break, etc
                {
                    pwd.AppendChar(i.KeyChar);
                    Console.Write("*");
                }
            }
            return pwd;
        }
    }
}
