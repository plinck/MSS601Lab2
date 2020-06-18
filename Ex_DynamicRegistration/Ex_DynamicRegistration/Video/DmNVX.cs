//-----------------------------------------------------------------------
// <copyright file="DmNvx.cs" company="Crestron">
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
using Crestron.SimplSharp.Reflection;
using Crestron.SimplSharpPro;                    // For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;     // For Threading
using Crestron.SimplSharpPro.DeviceSupport;      // For Generic Device Support
using Crestron.SimplSharpPro.Diagnostics;        // For System Monitor Access
using Crestron.SimplSharpPro.DM.Streaming;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Ex_DynamicRegistration.Video
{
    /// <summary>
    /// Allows us to instantiate and register an NVX unit dynamically
    /// </summary>
    public class DmNvx
    {
        /// <summary>
        /// Hardware interface for Nvx
        /// </summary>
        public DmNvxBaseClass Nvx;

        /// <summary>
        /// Used by reflection to load Crestron.SimplSharpPro.DM.dll
        /// </summary>
        public Assembly NvxAssembly;

        /// <summary>
        /// CrestronControlSystem
        /// </summary>
        public CrestronControlSystem Cs;

        /// <summary>
        /// Used for logging information to error log
        /// </summary>
        private const string LogHeader = "[NVX] ";

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
        /// A consumer implementation built around event handlers.
        /// </summary>
        private EventingBasicConsumer consumer;

        /// <summary>
        /// Initializes a new instance of the DmNvx class
        /// </summary>
        /// <param name="type">NVX type (ie. 350, 351)</param>
        /// <param name="id">IPID</param>
        /// <param name="name">Label you want to show up in the IPTable + Name of endpoint</param>
        /// <param name="multicast">Multicast address of this NVX unit</param>
        /// <param name="nvxExchange">Needs to match an existing exchange</param>
        /// <param name="cs">CrestronControlSystem</param> 
        public DmNvx(string type, uint id, string name, string multicast, string nvxExchange, CrestronControlSystem cs)
        {
            this.Type = type;

            this.Id = id;

            this.Name = name;

            this.Multicast = multicast;

            this.NvxExchange = nvxExchange;

            this.Cs = cs;

            CrestronEnvironment.ProgramStatusEventHandler += this.CrestronEnvironment_ProgramStatusEventHandler;
        }
        
        /// <summary>
        /// Gets or sets type of NVX. Can be "350", "351C", etc
        /// Keep in mind that this will not be checked before trying to load
        /// Yes, that can be improved ;)
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets IPID of the nvx unit
        /// </summary>
        public uint Id { get; set; }

        /// <summary>
        /// Gets or sets the endpoint name and the label you want to show in IPTable
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the multicast address
        /// </summary>
        public string Multicast { get; set; }

        /// <summary>
        /// Gets or sets the exchange name for this consumer
        /// </summary>
        public string NvxExchange { get; set; }

        /// <summary>
        /// Register the NVX using the proper information
        /// </summary>
        /// <returns>true or false, depending on if the registration succeeded</returns>
        public bool Register()
        {
            try
            {
                this.Nvx = this.RetrieveNvxObject(this.Type, this.Id);

                if (this.Nvx == null)
                {
                    return false;
                }

                this.Nvx.Description = this.Name;
                this.Nvx.Control.Name.StringValue = this.Name;
                this.Nvx.Control.MulticastAddress.StringValue = this.Multicast;
                
                if (this.Nvx.Register() != Crestron.SimplSharpPro.eDeviceRegistrationUnRegistrationResponse.Success)
                {
                    ErrorLog.Error(LogHeader + "Error registering NVX {0}", this.Name);
                    return false;
                }
                else
                {
                    ErrorLog.Error("Setting up Consumer");
                    this.ConfigureConsumer();
                    return true;
                }
            }
            catch (Exception e)
            {
                ErrorLog.Error(LogHeader + "Excepting when trying to register UI {0}: {1}", this.Name, e.Message);
                return false;
            }
        }

        /// <summary>
        /// Instantiates the NVX unit properly
        /// </summary>
        /// <param name="nvxType">Type of NVX (350,350C,351,351C)</param>
        /// <param name="deviceId">IPID of NVX unit</param>
        /// <returns>DmNvxBaseClass to be used genericly</returns>
        public DmNvxBaseClass RetrieveNvxObject(string nvxType, uint deviceId)
        {
            try
            {
                // always load from UI assembly
                this.NvxAssembly = Assembly.LoadFrom(Path.Combine(Directory.GetApplicationDirectory(), "Crestron.SimplSharpPro.DM.dll"));

                // add the correct device type that we want to reflect into
                string assembly = string.Format("Crestron.SimplSharpPro.DM.Streaming.DmNvx{0}", nvxType);
                CType cswitcher = this.NvxAssembly.GetType(assembly);

                // get the correct constructor for this type
                CType[] constructorTypes = new CType[] { typeof(uint), typeof(CrestronControlSystem) };

                // get info for the previously found constructor
                ConstructorInfo cinfo = cswitcher.GetConstructor(constructorTypes);

                // create the object with all the information
                return (DmNvxBaseClass)cinfo.Invoke(new object[] { deviceId, this.Cs });
            }
            catch (MissingMethodException e)
            {
                ErrorLog.Error(LogHeader + "Unable to create NVX. No constructor: {0}", e.Message);
            }
            catch (ArgumentException e)
            {
                ErrorLog.Error(LogHeader + "Unable to create NVX. No type: {0}", e.Message);
            }
            catch (NullReferenceException e)
            {
                ErrorLog.Error(LogHeader + "Unable to create NVX. No match: {0}", e.Message);
            }

            return null;
        }

        /// <summary>
        /// Configure everything consumer related
        /// </summary>
        private void ConfigureConsumer()
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
                // Hence use using a property
                this.channel.ExchangeDeclare(
                                             this.NvxExchange,
                                             ExchangeType.Fanout);      // The type of Exchange. Fanout: broadcasts to all subscribers
                                                                        // Direct: allows a pub to define a routing key on send,
                                                                        // and subscribers can define routing key(s) to receive
                                                                        // Topics: where routing key matches all, or a portion
                                                                        // Header: Similar to Topics, but order doesn't matter
                try
                {
                    string queueName = this.channel.QueueDeclare().QueueName;   // The specific queue used for this subscriber

                    this.channel.QueueBind(
                                            queueName,                          // Bind the Queue to the exchange.
                                            this.NvxExchange,                   // Name of the Exchange, needs to match again
                                            string.Empty);                      // Routing Key, used on Direct Connection

                    this.consumer = new EventingBasicConsumer(this.channel);    // Create Consumer

                    this.consumer.Received += this.ConsumerOnReceived;          // Register for Event Handler

                    this.channel.BasicConsume(
                                                queueName,                      // Tell the consumer to start receiving on this Queue
                                                false,                          // Not Required to acknowledge receipt
                                                this.Name,                      // Give the consumer a name 
                                                this.consumer);                 // What is the consumer that will be used 
                }
                catch (Exception e)
                {
                    ErrorLog.Error(LogHeader + "Queuename Init error: {0}", e.Message);
                }
            }
            catch (Exception e)
            {
                ErrorLog.Error(LogHeader + "Constumer Init Error: {0}", e.Message);
            }
        }

        /// <summary>
        /// Triggered when we receive a message for this consumer
        /// </summary>
        /// <param name="sender">not used in this exercise</param>
        /// <param name="basicDeliverEventArgs">contains the Body, among others</param>
        private void ConsumerOnReceived(object sender, BasicDeliverEventArgs basicDeliverEventArgs)
        {
            try
            {
                // TODO: Level1. Message is received, parse it and use it
                // Hint: Use basicDeliverEventArgs.Body
            }
            catch (Exception e)
            {
                ErrorLog.Error(LogHeader + "ConsumerOnReceived Error: {0}", e.Message);
            }
        }

        /// <summary>
        /// Event Handler for Programmatic events: Stop, Pause, Resume.
        /// Use this event to clean up when a program is stopping, pausing, and resuming.
        /// This event only applies to this SIMPL#Pro program, it doesn't receive events
        /// for other programs stopping
        /// </summary>
        /// <param name="programEventType">Stop, resume or pause</param>
        private void CrestronEnvironment_ProgramStatusEventHandler(eProgramStatusEventType programEventType)
        {
            switch (programEventType)
            {
                case eProgramStatusEventType.Paused:
                    // The program has been paused.  Pause all user threads/timers as needed.
                    break;
                case eProgramStatusEventType.Resumed:
                    // The program has been resumed. Resume all the user threads/timers as needed.
                    break;
                case eProgramStatusEventType.Stopping:
                    ErrorLog.Notice("Stopping program!");
                    this.channel.BasicCancel(this.consumer.ConsumerTag);
                    break;
            }
        }
    }
}