using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Verteco.Shared
{
    public sealed class MessageType
    {
        private char value;

        public static readonly MessageType Debug = new MessageType('D');
        public static readonly MessageType Information = new MessageType('I');
        public static readonly MessageType Warning = new MessageType('W');
        public static readonly MessageType Error = new MessageType('E');

        private MessageType(char v)
        {
            value = v;
        }

        public override string ToString()
        {
            return value.ToString();
        }
        public char ToChar()
        {
            return value;
        }

    }

    /// <summary>
    /// Very simple console logger - could do with lots of work!
    /// </summary>
    public static class Logger
    {
        static public bool ShowDebugMesssage { get; set; }
        static Logger()
        {
            ShowDebugMesssage = false;
        }

        public static void Init(string connectionString)
        {
            
        }

        static public void LogMessage(MessageType messageType, string message)
        {
            
            
            string appName;
            string time;

            // read the app name from the environment
            appName = Environment.GetCommandLineArgs()[0].Substring(Environment.GetCommandLineArgs()[0].LastIndexOf('\\') + 1);

            time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            
            object[] arrayOfParams = new object[3];
            arrayOfParams[0] = time;
            arrayOfParams[1] = messageType;
            arrayOfParams[2] = message;

            // E,W,I messages are always shown.  Debug message are only shown if enabled
            if (!messageType.Equals(MessageType.Debug) || (messageType.Equals(MessageType.Debug) && ShowDebugMesssage) )
            {
                // Write to Console
                Console.Error.WriteLine("[{0}]-[{1}]::{2}", arrayOfParams);
            }
        }

    }
}
