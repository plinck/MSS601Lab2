//-----------------------------------------------------------------------
// <copyright file="SystemManager.cs" company="Crestron">
//     Copyright (c) Crestron Electronics. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Crestron.SimplSharp;                       // For Basic SIMPL# Classes
using Crestron.SimplSharp.CrestronIO;            // For Directory
using Crestron.SimplSharpPro;                    // For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;     // For Threading
using Crestron.SimplSharpPro.DeviceSupport;      // For Generic Device Support
using Crestron.SimplSharpPro.Diagnostics;        // For System Monitor Access
using Crestron.SimplSharpPro.DM.Streaming;       // For DM-NVX
using Crestron.SimplSharpPro.UI;

using RabbitMQ.Client;

namespace Ex_DynamicRegistration
{
    /// <summary>
    /// Used to manage all the different subsystems
    /// </summary>
    public class SystemManager
    {
        /// <summary>
        /// Used for logging information to error log
        /// </summary>
        private const string LogHeader = "[MSS601Room1SM] ";

        /// <summary>
        /// You can use this string when defining your exchange when publishing a message.
        /// Keep in mind that this exchange needs to be similar for both producer as well as consumer
        /// </summary>
        private const string NvxExchange = "nvxroute";

        /// <summary>
        /// Keeps track of all the touchpanels that are registered
        /// </summary>
        private Dictionary<string, UI.TouchpanelUI> touchpanels = new Dictionary<string, UI.TouchpanelUI>();
        
        /// <summary>
        /// We're not using a Dictionary in this example, but we are syncing data through RabbitMQ
        /// </summary>
        private Video.DmNvx nvxUnits;

        /// <summary>
        /// ConnectionFactory used for RabbitMQ
        /// Main entry point to the client API
        /// </summary>
        private ConnectionFactory factory;

        /// <summary>
        /// Create a connection to a specified endpoint
        /// </summary>
        private IConnection connection;

        /// <summary>
        /// Represents a channel and provides most of the operations (protocol methods)
        /// </summary>
        private IModel channel;

        /// <summary>
        /// TouchpanelUI object to use for registration
        /// </summary>
        // PAUL TODO - Revisit this
        // private UI.TouchpanelUI tp;

        /// <summary>
        /// Initializes a new instance of the SystemManager class
        /// </summary>
        /// <param name="config">full config data</param>
        /// <param name="cs">CrestronControlSystem</param>
        public SystemManager(Configuration.ConfigData.Configuration config, CrestronControlSystem cs)
        {
            try
            {
                // Added for Exercise 2.
                this.ConfigurePublisher();
            }
            catch (Exception e)
            {
                ErrorLog.Error(LogHeader + "Unable to configure publisher: {0}", e.Message);
            }

            if (config.Touchpanels != null)
            {                
                // TODO: Level1. Implement touchPanel + sources + destinations
                foreach (var touchpanel in config.Touchpanels)
                {
                    this.touchpanels[touchpanel.Label] = new UI.TouchpanelUI(touchpanel.Type, touchpanel.Id, touchpanel.Label, cs);

                    // TODO: Level1. Create new instance of TouchpanelUI
                    if (this.touchpanels[touchpanel.Label].Register())
                    {
                        try
                        {
                            // Set up touchpanel basics based on configuration
                            this.touchpanels[touchpanel.Label].UserInterface.SmartObjects[1].UShortInput["Set Number of Items"].UShortValue = (ushort)config.Sources.Length;
                            this.touchpanels[touchpanel.Label].UserInterface.SmartObjects[2].UShortInput[4].UShortValue = (ushort)config.Destinations.Length;

                            // set the source labels and dynamic icons
                            foreach (var source in config.Sources)
                            {
                                this.touchpanels[touchpanel.Label].UserInterface.SmartObjects[1].StringInput[$"Set Item {source.Id} Text"].StringValue = source.Label;
                                this.touchpanels[touchpanel.Label].UserInterface.SmartObjects[1].UShortInput["Set Item " + source.Id + " Icon Analog"].UShortValue = source.Icon;
                            }

                            foreach (var destination in config.Destinations)
                            {
                                this.touchpanels[touchpanel.Label].UserInterface.SmartObjects[2].StringInput[10 + destination.Id].StringValue = destination.Label;
                                this.touchpanels[touchpanel.Label].UserInterface.SmartObjects[2].UShortInput[10 + destination.Id].UShortValue = destination.Icon;
                            }

                            // Register event handler
                            this.touchpanels[touchpanel.Label].UserInterface.SigChange += this.UserInterface_SigChange;

                            // Register event handlers for Smart Objects
                            foreach (KeyValuePair<uint, SmartObject> smartObject in this.touchpanels[touchpanel.Label].UserInterface.SmartObjects)
                            {
                                smartObject.Value.SigChange += this.SO_Value_SigChange;
                            }
                        }
                        catch (Exception e)
                        {
                            ErrorLog.Error(LogHeader + "Exception trying to set up touchpanel: {0} - {1}", touchpanel.Label, e.Message);
                        }
                    }
                }
            }

            if (config.Nvx != null)
            {
                foreach (var nvx in config.Nvx)
                {
                    try
                    {
                        this.nvxUnits = new Video.DmNvx(nvx.Type, nvx.Id, nvx.Name, nvx.Multicast, NvxExchange, cs);

                        if (this.nvxUnits.Register())
                        {
                            // Register some events, etc
                            ErrorLog.Notice(LogHeader + "Registered NVX {0} successfully!", nvx.Name);
                        }
                    }
                    catch (Exception e)
                    {
                        ErrorLog.Error(LogHeader + "Error in registering NVX units: {0}", e.Message);
                    }
                }
            }
        }
                /// <summary>
        /// Configures everything necessary for RabbitMQ
        /// </summary>
        private void ConfigurePublisher()
        {
            Task.Run(() => this.InitConnectionFactory());
        }

        /// <summary>
        /// Initializes everything for this ConnectionFactory
        /// We're connecting to localhost (RabbitMQ is running on your VC-4 instance) with default username/pass
        /// For this exercise we are using the "FanOut" method of exchange (ExchangeType.Fanout)
        /// </summary>
        /// <returns>unused object</returns>
        private object InitConnectionFactory()
        {
            try
            {
                // The ConnectionFactory is a convenience class to facilitate opening a connection to an AMQP Broker
                this.factory = new ConnectionFactory()
                {
                    HostName = "localhost",                        // Host of the Broker. In this case, same as VC4
                    UserName = ConnectionFactory.DefaultUser,      // User. In this case, we're using the default user 
                    Password = ConnectionFactory.DefaultPass,      // Password. In this case, we're using the default password
                    Port = AmqpTcpEndpoint.UseDefaultPort,         // Port. The TCP Port that is being used. Again, we're selecting the default
                    VirtualHost = ConnectionFactory.DefaultVHost   // The Virtual Host being used. Getting boring, but using the default again!
                };

                // Create the connection based on the information above.
                // If we cannot connect, or the VHost is not valid, we will get an exception
                this.connection = this.factory.CreateConnection();

                // create the channel and model we need
                this.channel = this.connection.CreateModel();

                // Declare the exchange. This needs to match what we have used when setting up the producer
                // Hence using a property
                this.channel.ExchangeDeclare(
                                             NvxExchange,
                                             ExchangeType.Fanout);      // The type of Exchange. Fanout: broadcasts to all subscribers
                                                                        // Direct: allows a pub to define a routing key on send,
                                                                        // and subscribers can define routing key(s) to receive
                                                                        // Topics: where routing key matches all, or a portion
                                                                        // Header: Similar to Topics, but order doesn't matter
            }
            catch (Exception e)
            {
                ErrorLog.Error(LogHeader + "Factory Init Error: {0}", e.Message);
            }

            return null;
        }

        /// <summary>
        /// Specific event handler for Smart Objects
        /// </summary>
        /// <param name="currentDevice">The device that triggered the event</param>
        /// <param name="args">Contains args.Sig.Type, args.Sig.Name, args.SmartObjectArgs.ID and more</param>
        private void SO_Value_SigChange(GenericBase currentDevice, SmartObjectEventArgs args)
        {
            try
            {
                // TODO: Level1. Construct a message to publish on your channel
                // Hint: You can use channel.BasicPublish()
                // If ExchangeType is FanOut, th routing key can be ""
                // The basicProperties can be null in this example
            }
            catch (Exception e)
            {
                ErrorLog.Error(LogHeader + "Unable to publish message: {0}", e.Message);
            }
        }

        /// <summary>
        /// Eventhandler for boolean/ushort/string sigs
        /// This event handler is not being used for this exercise
        /// </summary>
        /// <param name="currentDevice">The device that triggered the event</param>
        /// <param name="args">Contains the SigType, Sig.Number and Sig.Value and more</param>
        private void UserInterface_SigChange(BasicTriList currentDevice, SigEventArgs args)
        {
        }
    }//class
}
