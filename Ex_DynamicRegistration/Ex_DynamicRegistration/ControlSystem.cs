//-----------------------------------------------------------------------
// <copyright file="ControlSystem.cs" company="Crestron">
//     Copyright (c) Crestron Electronics. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Crestron.SimplSharp;                       // For Basic SIMPL# Classes
using Crestron.SimplSharp.CrestronIO;            // For Directory
using Crestron.SimplSharpPro;                    // For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;     // For Threading
using Crestron.SimplSharpPro.DeviceSupport;      // For Generic Device Support
using Crestron.SimplSharpPro.Diagnostics;        // For System Monitor Access
using Crestron.SimplSharpPro.UI;

namespace Ex_DynamicRegistration
{
    /* Instructors notes
     * 
     */

    /// <summary>
    /// ControlSystem class that inherits from CrestronControlSystem
    /// </summary>
    public class ControlSystem : CrestronControlSystem
    {
        /// <summary>
        /// Used for logging information to error log
        /// </summary>
        private const string LogHeader = "[MSS601Room1CS] ";

        /// <summary>
        /// Can be used to identify individual program.
        /// Mostly usefull on an appliance with multiple programs running
        /// See below how we use it
        /// </summary>
        private uint appId;

        /// <summary>
        /// Used to read/write config files
        /// </summary>
        private Configuration.ConfigManager config;

        /// <summary>
        /// Used to manage all the different subsystems
        /// </summary>
        private SystemManager manager;

        
        /// <summary>
        /// These are temp classes I used to get started and using for CWS
        /// <summary>
        private readonly XpanelForSmartGraphics tpForCWS;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="ControlSystem" /> class.
        /// <summary>
        /// Second touchpanel used throughout this exercise
        /// Could also be a Tsw or any other SmartGraphics enabled touchpanel
        /// </summary>

        /// <summary>
        /// The CWS controller used for Lab 2
        /// </summary>
        private CWS.Controller controller;

        /// Use the constructor to:
        /// * Initialize the maximum number of threads (max = 400)
        /// * Register devices
        /// * Register event handlers
        /// * Add Console Commands
        /// Please be aware that the constructor needs to exit quickly; if it doesn't
        /// exit in time, the SIMPL#Pro program will exit.
        /// You cannot send / receive data in the constructor
        /// </summary>
        ///
        public ControlSystem()
            : base()
        {
            try
            {
                Thread.MaxNumberOfUserThreads = 20;

                // Subscribe to the controller events (System, Program, and Ethernet)
                CrestronEnvironment.SystemEventHandler += new SystemEventHandler(this.ControlSystem_ControllerSystemEventHandler);
                CrestronEnvironment.ProgramStatusEventHandler += new ProgramStatusEventHandler(this.ControlSystem_ControllerProgramEventHandler);
                CrestronEnvironment.EthernetEventHandler += new EthernetEventHandler(this.ControlSystem_ControllerEthernetEventHandler);
                
                this.appId = InitialParametersClass.ApplicationNumber;

                if (this.SupportsEthernet)
                {
                    this.tpForCWS = new XpanelForSmartGraphics(0x90, this);
                    this.tpForCWS.SigChange += new SigEventHandler(this.Xpanel_SigChange);
                    this.tpForCWS.OnlineStatusChange += this.Xpanel_OnlineStatusChange;

                    string sgdPath = string.Format($"{Directory.GetApplicationDirectory()}/XPanel_v1.sgd");
                    if (this.tpForCWS.Register() != eDeviceRegistrationUnRegistrationResponse.Success)
                    {
                        ErrorLog.Error(string.Format(
                            $"{LogHeader} Error registering XPanel: {this.tpForCWS.RegistrationFailureReason}"));
                    }
                    else
                    {
                        this.tpForCWS.LoadSmartObjects(sgdPath);
                        ErrorLog.Error($"{LogHeader} Loaded SmartObjects: {this.tpForCWS.SmartObjects.Count}");
                        foreach (KeyValuePair<uint, SmartObject> smartObject in this.tpForCWS.SmartObjects)
                        {
                            smartObject.Value.SigChange += new SmartObjectSigChangeEventHandler(this.Xpanel_SO_SigChange);
                        }
                        // e.g. /Rooms/MSS601Room1/cws/api/config
                        this.controller = new CWS.Controller(this.tpForCWS, "api");
                        ErrorLog.Notice(string.Format(LogHeader + "CWS.Controller started"));
                    }
                }
                
                // Potential way to make your program more dynamic
                // Not being used in either Lab1 or Lab2
                this.appId = InitialParametersClass.ApplicationNumber;
            }
            catch (Exception e)
            {
                ErrorLog.Error(string.Format(LogHeader + "Error in the constructor: {0}", e.Message));
            }
        }

        /// <summary>
        /// InitializeSystem - this method gets called after the constructor 
        /// has finished. 
        /// Use InitializeSystem to:
        /// * Start threads
        /// * Configure ports, such as serial and verisports
        /// * Start and initialize socket connections
        /// Send initial device configurations
        /// Please be aware that InitializeSystem needs to exit quickly also; 
        /// if it doesn't exit in time, the SIMPL#Pro program will exit.
        /// </summary>
        public override void InitializeSystem()
        {
            Task.Run(() => this.SystemSetup());
        }
        
        
        /// <summary>
        /// Event Handler for Ethernet events: Link Up and Link Down. 
        /// Use these events to close / re-open sockets, etc. 
        /// </summary>
        /// <param name="ethernetEventArgs">This parameter holds the values 
        /// such as whether it's a Link Up or Link Down event. It will also indicate 
        /// which Ethernet adapter this event belongs to.
        /// </param>
        public void ControlSystem_ControllerEthernetEventHandler(EthernetEventArgs ethernetEventArgs)
        {
            switch (ethernetEventArgs.EthernetEventType)
            {
                // Determine the event type Link Up or Link Down
                case eEthernetEventType.LinkDown:
                    // Next need to determine which adapter the event is for. 
                    // LAN is the adapter is the port connected to external networks.
                    if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter)
                    {
                    }

                    break;
                case eEthernetEventType.LinkUp:
                    if (ethernetEventArgs.EthernetAdapter == EthernetAdapterType.EthernetLANAdapter)
                    {
                    }

                    break;
            }
        }
                /// <summary>
        /// Event Handler for Programmatic events: Stop, Pause, Resume.
        /// Use this event to clean up when a program is stopping, pausing, and resuming.
        /// This event only applies to this SIMPL#Pro program, it doesn't receive events
        /// for other programs stopping
        /// </summary>
        /// <param name="programStatusEventType">Stop, resume or pause</param>
        public void ControlSystem_ControllerProgramEventHandler(eProgramStatusEventType programStatusEventType)
        {
            switch (programStatusEventType)
            {
                case eProgramStatusEventType.Paused:
                    // ErrorLog.Notice(string.Format("Program Paused"));
                    break;
                case eProgramStatusEventType.Resumed:
                    // ErrorLog.Notice(string.Format("Program Resumed"));
                    break;
                case eProgramStatusEventType.Stopping:
                    // ErrorLog.Notice(string.Format("Program Stopping"));
                    break;
            }
        }

        /// <summary>
        /// Event Handler for system events, Disk Inserted/Ejected, and Reboot
        /// Use this event to clean up when someone types in reboot, or when your SD /USB
        /// removable media is ejected / re-inserted.
        /// </summary>
        /// <param name="systemEventType">Inserted, Removed, Rebooting</param>
        public void ControlSystem_ControllerSystemEventHandler(eSystemEventType systemEventType)
        {
            switch (systemEventType)
            {
                case eSystemEventType.DiskInserted:
                    // Removable media was detected on the system
                    break;
                case eSystemEventType.DiskRemoved:
                    // Removable media was detached from the system
                    break;
                case eSystemEventType.Rebooting:
                    // The system is rebooting. 
                    // Very limited time to preform clean up and save any settings to disk.
                    break;
            }
        }

        /// <summary>
        /// Thread to create all the necessary logic and devices
        /// </summary>
        /// <returns>unused object</returns>
        private object SystemSetup()
        {
            this.config = new Configuration.ConfigManager();

            // Potential check you can do to make your program more dynamic
            // Any "box" processor is an appliance.
            // VC-4 is a server
            // We're not using this for either Lab1 or Lab2
            if (CrestronEnvironment.DevicePlatform == eDevicePlatform.Appliance)
            {
            }
            else if (CrestronEnvironment.DevicePlatform == eDevicePlatform.Server)
            {
            }

            var configFile = $"{Directory.GetApplicationRootDirectory()}/User/config.json";
            ErrorLog.Notice($"{LogHeader} trying to read config file: {configFile} in dir {Directory.GetApplicationRootDirectory()}");
            if (this.config.ReadConfig(configFile))
			{
                this.manager = new SystemManager(this.config.RoomConfig, this);
            }
            else
            {
                ErrorLog.Error(string.Format(LogHeader + "Unable to read config!"));
            }

            return null;
        }

        
        /// <summary>
        /// Eventhandler for boolean/ushort/string sigs
        /// </summary>
        /// <param name="currentDevice">The device that triggered the event</param>
        /// <param name="args">Contains the SigType, Sig.Number and Sig.Value and more</param>
        public void Xpanel_SigChange(BasicTriList currentDevice, SigEventArgs args)
        {
            switch (args.Sig.Type)
            {
                case eSigType.Bool:
                    ErrorLog.Notice(string.Format(LogHeader + "Boolean Received from Touch Panel: {0}, {1}", args.Sig.Number, args.Sig.BoolValue));
                    switch (args.Sig.Number)
                    {
                        // YS: Level 1, exercise 2
                        // Hello world button
                        case 12:
                            if (args.Sig.BoolValue == true)
                            {
                                currentDevice.StringInput[11].StringValue = "Hello World!";
                            }
                            else
                            {
                                currentDevice.StringInput[11].StringValue = string.Empty;
                            }

                            break;

                        // YS: Level 2, exercise 1
                        // toggle button
                        case 21:
                            if (args.Sig.BoolValue == true)
                            {
                                // toggle it, easy way
                                currentDevice.BooleanInput[21].BoolValue = !currentDevice.BooleanInput[21].BoolValue;
                                if (currentDevice.BooleanInput[21].BoolValue == true)
                                {
                                    currentDevice.StringInput[21].StringValue = "Hello World!";
                                }
                                else
                                {
                                    currentDevice.StringInput[21].StringValue = string.Empty;
                                }
                            }

                            break;

                        // YS: Level 2, exercise 2
                        // interlock
                        case 22:
                        case 23:
                        case 24:
                            if (args.Sig.BoolValue == true)
                            {
                                // Loop through the possible interlocked buttons
                                for (ushort i = 22; i <= 24; i++)
                                {
                                    currentDevice.BooleanInput[i].BoolValue = false;
                                }

                                // Set only the pressed button feedback to high
                                currentDevice.BooleanInput[args.Sig.Number].BoolValue = true;

                                // Set the correct text
                                if (currentDevice.BooleanInput[22].BoolValue == true)
                                {
                                    currentDevice.StringInput[21].StringValue = "Hello World!";
                                }
                                else if (currentDevice.BooleanInput[23].BoolValue == true)
                                {
                                    currentDevice.StringInput[21].StringValue = "Hallo Wereld!";
                                }
                                else if (currentDevice.BooleanInput[24].BoolValue == true)
                                {
                                    currentDevice.StringInput[21].StringValue = "Hola Mundo!";
                                }
                            }

                            break;
                        case 25:
                            if (args.Sig.BoolValue == true)
                            {
                                // Loop through the possible interlocked buttons
                                for (ushort i = 22; i <= 24; i++)
                                {
                                    currentDevice.BooleanInput[i].BoolValue = false;
                                }

                                // Clear text field
                                currentDevice.StringInput[21].StringValue = string.Empty;
                            }

                            break;

                    }

                    break;
                case eSigType.UShort:
                    // ErrorLog.Error(string.Format(LogHeader + "Ushort Received from Touch Panel: {0}, {1}", args.Sig.Number, args.Sig.UShortValue));
                    // YS: Level 3, exercise 1
                    if (args.Sig.Number == 31)
                    {
                        ushort percentage = Convert.ToUInt16(args.Sig.UShortValue * 100 / 65535);

                        // send it right back to analog join 32 after converting 0->65535 to 0->100
                        currentDevice.UShortInput[32].UShortValue = percentage;

                        currentDevice.UShortInput[31].UShortValue = args.Sig.UShortValue;

                        if (percentage == 0)
                        {
                            currentDevice.UShortInput[33].UShortValue = 0;
                        }
                        else if (percentage > 0 && percentage <= 33)
                        {
                            currentDevice.UShortInput[33].UShortValue = 1;
                        }
                        else if (percentage > 33 && percentage <= 66)
                        {
                            currentDevice.UShortInput[33].UShortValue = 2;
                        }
                        else if (percentage > 66 && percentage <= 100)
                        {
                            currentDevice.UShortInput[33].UShortValue = 3;
                        }
                    }

                    break;
                case eSigType.String:
                    // ErrorLog.Notice(string.Format(LogHeader + "String Received from Touch Panel: {0}, {1}", args.Sig.Number, args.Sig.StringValue));
                    break;
            }
        }

        /// <summary>
        /// Online/Ofline event handler for Xpanel
        /// </summary>
        /// <param name="currentDevice">The device that triggered the event</param>
        /// <param name="args">Contains DeviceOnline for status feedback</param>
        public void Xpanel_OnlineStatusChange(GenericBase currentDevice, OnlineOfflineEventArgs args)
        {
            if (args.DeviceOnLine)
            {
                // if it was tpForCWS that triggered the event
                if (currentDevice == this.tpForCWS)
                {
                    ErrorLog.Notice(string.Format(LogHeader + "{0} is online", tpForCWS.Type));
                    this.tpForCWS.BooleanInput[11].BoolValue = args.DeviceOnLine;
                }
            }
            else
            {
                // ErrorLog.Notice(string.Format(LogHeader + "{0} is offline", currentDevice.Description));
            }
        }

        /// <summary>
        /// Specific event handler for Smart Objects (not used in this exercise)
        /// </summary>
        /// <param name="currentDevice">The device that triggered the event</param>
        /// <param name="args">Contains args.Sig.Type, args.Sig.Name, args.SmartObjectArgs.ID and more</param>
        public void Xpanel_SO_SigChange(GenericBase currentDevice, SmartObjectEventArgs args)
        {
            // ErrorLog.Notice(string.Format(LogHeader + "Event Type: {0}, Signal: {1}, from SmartObject: {2}", args.Sig.Type, args.Sig.Name, args.SmartObjectArgs.ID));
        }

    } // Class
} // Namespace
