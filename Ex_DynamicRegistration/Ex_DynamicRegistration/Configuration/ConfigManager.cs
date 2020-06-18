//-----------------------------------------------------------------------
// <copyright file="ConfigManager.cs" company="Crestron">
//     Copyright (c) Crestron Electronics. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Newtonsoft.Json;

namespace Ex_DynamicRegistration.Configuration
{
    /// <summary>
    /// Reads/Writes data from config.json
    /// </summary>
    public class ConfigManager
    {
        /// <summary>
        /// Configuration object for this system
        /// </summary>
        public ConfigData.Configuration RoomConfig;

        /// <summary>
        /// Used for logging information to error log
        /// </summary>
        private const string LogHeader = "[Configuration] ";

        /// <summary>
        /// Locking object for config
        /// </summary>
        private static CCriticalSection configLock = new CCriticalSection();

        /// <summary>
        /// Was the read succesfull
        /// </summary>
        private bool readSuccess;

        /// <summary>
        /// Can be used to identify individual program.
        /// Mostly usefull on an appliance with multiple programs running
        /// Not used in this exercise
        /// </summary>
        private uint appId;

        /// <summary>
        /// Initializes a new instance of the ConfigManager class
        /// </summary>
        public ConfigManager()
        {
        }

        /// <summary>
        /// Reads a JSON formatted configuration from disc
        /// </summary>
        /// <param name="configFile">Location and name of the config file</param>
        /// <returns>True or False depending on read success</returns>
        public bool ReadConfig(string configFile)
        {
            // string for file contents
            string configData = string.Empty;

            ErrorLog.Notice(LogHeader + "Started loading config file: {0}", configFile);
            if (string.IsNullOrEmpty(configFile))
            {
                this.readSuccess = false;
                ErrorLog.Error(LogHeader + "No File?!?");
            }

            if (!File.Exists(configFile))
            {
                this.readSuccess = false;
                ErrorLog.Error(LogHeader + "Config file doesn't exist");
            }
            else if (File.Exists(configFile))
            {
                configLock.Enter();

                // Open, read and close the file
                // If you don't want an exception to be thrown which would potentially stop the program,
                // you could throw a try/catch around the method which loads the configuration file so that
                // the exception can be caught so that you could show the exception message on screen,
                // rather than leaving it be thrown (unhandled)
                try
                {
                    using (StreamReader file = new StreamReader(configFile))
                    {
                        configData = file.ReadToEnd();
                        file.Close();
                    }

                    // Try to deserialize into a Room object. If this fails, the JSON file is probably malformed
                    this.RoomConfig = JsonConvert.DeserializeObject<ConfigData.Configuration>(configData);
                    ErrorLog.Notice(LogHeader + "Config file loaded!");
                    this.readSuccess = true;
                }
                catch (Exception e)
                {
                    this.readSuccess = false;
                    ErrorLog.Error(LogHeader + "Exception in reading config file: {0}", e.Message);
                }
                finally
                {
                    configLock.Leave();
                }
            }

            return this.readSuccess;
        }

        /// <summary>
        /// Update a running configuration
        /// Most likely to happen through the API
        /// </summary>
        /// <param name="roomConfig">New config file location and file name</param>
        public void UpdateConfiguration(Configuration.ConfigData.Configuration roomConfig)
        {
            this.appId = InitialParametersClass.ApplicationNumber;

            string filePath = string.Empty;

            // Add current date and time to config file
            // Not used at this point, for future use
            roomConfig.LastUpdate = DateTime.Now.ToString();

            string json = JsonConvert.SerializeObject(roomConfig, Formatting.Indented);

            // check which platfrom we are running on
            if (CrestronEnvironment.DevicePlatform == eDevicePlatform.Appliance)
            {
                filePath = string.Format(@"\User\App{0:D2}\config.json", this.appId);
            }
            else if (CrestronEnvironment.DevicePlatform == eDevicePlatform.Server)
            {
                filePath = string.Format(@"{0}/User/config.json", Directory.GetApplicationRootDirectory());
            }

            using (var streamToWrite = new FileStream(filePath, FileMode.OpenOrCreate))
            {
                using (var writer = new StreamWriter(streamToWrite))
                {
                    writer.Write(json);
                }
            }
        }
    }
}