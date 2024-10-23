using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using ABB.Robotics.Controllers;
using ABB.Robotics.Controllers.Discovery;
using ABB.Robotics.Controllers.EventLogDomain;

namespace ABBLogger
{
    internal class Program
    {
        private static NetworkScanner scanner = new NetworkScanner();
        private static Dictionary<string, Controller> controllers = new Dictionary<string, Controller>();
        private static string logFolder = null;
        private static string logFile = null;

        static int Main(string[] args)
        {
            string msg = string.Format("{0};{1}", Timestamp(DateTime.Now), "Logger;Started");
            if (args.Length > 0)
            {
                logFolder = args[0];
                if (!Directory.Exists(logFolder))
                    try { Directory.CreateDirectory(logFolder); } catch { }
                if (!Directory.Exists(logFolder))
                {
                    Console.WriteLine("Can not create log folder in '{0}", logFolder);
                    return -1;
                }
                if (!Write(msg))
                {
                    Console.WriteLine("Can not write to log file in '{0}", logFile);
                    return -2;
                }
            }
            Console.WriteLine(msg);

            while (true)
            {
                scanner.Scan();
                foreach (ControllerInfo info in scanner.Controllers)
                {
                    string name = info.SystemName;
                    switch (info.Availability)
                    {
                        case Availability.Available:
                            if (!controllers.ContainsKey(name))
                            {
                                Log(name, info.Availability.ToString(), info.IPAddress.ToString());
                                try
                                {
                                    Controller controller = Controller.Connect(info, ConnectionType.Standalone);
                                    controller.Logon(UserInfo.DefaultUser);
                                    Log(name, "Connected");
                                    controller.ConnectionChanged += OnConnectionChanged;
                                    controller.OperatingModeChanged += OnOperatingModeChanged;
                                    controller.StateChanged += OnStateChanged;
                                    controller.EventLog.MessageWritten += OnMessageWritten;
                                    controllers.Add(name, controller);
                                }
                                catch (Exception ex)
                                {
                                    Log(name, "Error", ex.ToString());
                                    controllers.Add(name, null);
                                }
                            }
                            break;
                        default:
                            if (controllers.ContainsKey(name))
                            {
                                Controller controller = controllers[name];
                                controllers.Remove(name);
                                try { controller.Dispose(); } catch { }
                                Log(name, info.Availability.ToString());
                            }
                            break;
                    }
                    Thread.Sleep(1000);
                }
            }
        }

        private static void OnMessageWritten(object sender, MessageWrittenEventArgs e)
        {
            Controller c = (sender as EventLog).Controller;
            EventLogMessage msg = e.Message;
            EventLogCategory cat = c.EventLog.GetCategory(msg.CategoryId);
            Log(msg.Timestamp, c.SystemName, msg.Type.ToString(), cat.Name, msg.Number.ToString(), "#" + msg.SequenceNumber.ToString(), "\"" + msg.Title + "\"", "\"" + msg.Body + "\"");
        }

        private static void OnConnectionChanged(object sender, ConnectionChangedEventArgs e)
        {
            Log((sender as Controller).SystemName, e.Connected ? "Connected" : "Disconnected");
        }

        private static void OnOperatingModeChanged(object sender, OperatingModeChangeEventArgs e)
        {
            Log((sender as Controller).SystemName, "Mode", e.NewMode.ToString());
        }

        private static void OnStateChanged(object sender, StateChangedEventArgs e)
        {
            Log((sender as Controller).SystemName, "State", e.NewState.ToString());
        }

        static string Timestamp(DateTime dt) { return dt.ToString("yyyy-MM-dd;hh:mm:ss.fff"); }

        static bool Log(params string[] pars)
        {
            return Log(DateTime.Now, pars);
        }
        static bool Log(DateTime dt, params string[] pars)
        {
            string msg = string.Format("{0};{1}", Timestamp(dt), string.Join(";", pars));
            Console.WriteLine(msg);
            return Write(msg);
        }
        static bool Write(string msg)
        {
            if (string.IsNullOrEmpty(logFolder)) return true;
            DateTime now = DateTime.Now;
            string yearLogFolder = Path.Combine(logFolder, now.ToString("yyyy", CultureInfo.InvariantCulture));
            if (!Directory.Exists(yearLogFolder)) try { Directory.CreateDirectory(yearLogFolder); } catch { }
            if (!Directory.Exists(yearLogFolder)) return false;
            logFile = Path.Combine(yearLogFolder,now.ToString("yyyyMMdd")+".csv");
            try
            {
                File.AppendAllText(logFile, msg+"\r\n");
                return true;
            }
            catch { }
            return false;
        }
    }
}
