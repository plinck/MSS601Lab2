﻿//-----------------------------------------------------------------------
// <copyright file="FileControl.cs" company="Crestron">
//     Copyright (c) Crestron Electronics. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Crestron.SimplSharp;                      // For Basic SIMPL# Classes
using Crestron.SimplSharp.CrestronIO;           // For Directory
using Crestron.SimplSharpPro;                   // For Basic SIMPL#Pro classes
using Crestron.SimplSharpPro.CrestronThread;    // For Threading
using Newtonsoft.Json;

 namespace Ex_DynamicRegistration
{
    /// <summary>
    /// Handles everything related to reading / writing files
    /// </summary>
    public class FileControl
    {
        /// <summary>
        /// Used for logging information to error log
        /// </summary>
        private const string LogHeader = "[File] ";

        /// <summary>
        /// Initializes a new instance of the <see cref="FileControl" /> class.
        /// </summary>
        public FileControl()
        {
        }

        /// <summary>
        /// Appends data to an existing file.
        /// If the file doesn't exist, it creates it
        /// </summary>
        /// <param name="data">The data to append to the file</param>
        /// <param name="pathAndFilename">Path and filename. Example: "/User/logfile.txt Is always relative to the root directory of the program</param>
        public static void WriteWithAppend(string data, string pathAndFilename)
        {
            // TODO: Level1. You can use this static method to create your log writing code
            StreamWriter writer = File.AppendText( pathAndFilename );
            writer.WriteLine(data);
            writer.Close();
            writer.Dispose();

        }
        public static string ReadFile(string pathAndFilename)
        {
            // TODO: Level3
            if (!File.Exists(pathAndFilename))
            {
                ErrorLog.Error($"{LogHeader} log file {pathAndFilename} doesn't exist");
                return String.Empty;
                ;
            }
            FileStream stream = File.OpenRead(pathAndFilename);
            StreamReader reader = new StreamReader(stream);
            String contents = reader.ReadToEnd();
            reader.Close();
            reader.Dispose();
            return contents;
        }
    }
}
