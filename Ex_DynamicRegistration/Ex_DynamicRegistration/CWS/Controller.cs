﻿//-----------------------------------------------------------------------
// <copyright file="Controller.cs" company="Crestron">
//     Copyright (c) Crestron Electronics. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Crestron.SimplSharp.CrestronIO;
using Crestron.SimplSharp.WebScripting;
using Crestron.SimplSharpPro.DeviceSupport;
using Crestron.SimplSharpPro.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

 namespace Ex_DynamicRegistration.CWS
 {
    /* Instructors notes
     */

    /// <summary>
    /// CWS controller class that handles all CWS communication back and forth
    /// </summary>
    public class Controller
    {
        /// <summary>
        /// Used for logging information to error log
        /// </summary>
        private const string LogHeader = "[PAULLINCK2 CWS] ";

        /// <summary>
        /// Optional CWS path to be added
        /// Base path for this exercise is http://[ipaddres]/VirtualControl/Rooms/[roomname]/cws/
        /// Where [ipaddress] is the ip address or hostname of the VC-4 server
        /// And [roomname] is the name used when creating a room instance of this program
        /// </summary>
        //private string cwsPath = "http://ec2-52-15-101-154.us-east-2.compute.amazonaws.com/VirtualControl/Rooms/PAULLINCK2/cws/";
        private string cwsPath;

        /// <summary>
        /// Locking object for CWS Server
        /// </summary>
        private CCriticalSection cwsServerLock = new CCriticalSection();

        /// <summary>
        /// The HTTP CWS server
        /// </summary>
        private HttpCwsServer cwsServer;

        /// <summary>
        /// XpanelForSmartGraphics object used to send feedback to user
        /// </summary>
        private XpanelForSmartGraphics tp;
        
        // Object type for the request
        public class Request
        {
            public string text { get; set; }
        }
        public class SliderRequest
        {
            public ushort value { get; set; }
        }

        public class ButtonStatus
        {
            public Boolean button { get; set; }
            public ButtonStatus(Boolean state)
            {
                this.button = state;
            }
        }
        public class InterlockResponse
        {
            public List<ButtonStatus> status { get; set; }
            
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="Controller" /> class.
        /// </summary>
        /// <param name="tp">The XpanelForSmartGraphics object</param>
        /// <param name="cwsPath">Additional CWS path. May be empty</param>
        public Controller(XpanelForSmartGraphics tp, string cwsPath)
        {
            ErrorLog.Notice(string.Format($"{LogHeader} Running CWS Controller Constructor"));
            
            this.cwsPath = cwsPath;
            ErrorLog.Notice(string.Format($"{LogHeader} CWS.Controller path : {cwsPath}"));


            this.StartServer();

            this.tp = tp;
        }

        /// <summary>
        /// Start the CWS server with the previously set path
        /// </summary>
        public void StartServer()
        {
            ErrorLog.Notice(string.Format($"{LogHeader} Starting CWS API Server"));

            try
            {
                this.cwsServerLock.Enter();
                if (this.cwsServer == null)
                {
                    ErrorLog.Notice(string.Format($"{LogHeader} Starting CWS API Server"));
                    this.cwsServer = new HttpCwsServer(this.cwsPath);

                    this.cwsServer.ReceivedRequestEvent += new HttpCwsRequestEventHandler(this.ReceivedRequestEvent);

                    // GET config file route
                    this.cwsServer.Routes.Add(new HttpCwsRoute("config") {Name = "config"});
 
                    // register the server
                    this.cwsServer.Register();
                    ErrorLog.Notice(string.Format($"{LogHeader} Started CWS API Server"));
                }
                else
                {
                    throw new InvalidOperationException("CWS API Server is already running");
                }
            }
            catch (Exception e)
            {
                ErrorLog.Error(LogHeader + "Exception in StartServer(): {0}", e.Message);
            }
            finally
            {
                this.cwsServerLock.Leave();
            }
        }

        /// <summary>
        /// The received request handler for the CWS server
        /// </summary>
        /// <param name="sender">optional sender object</param>
        /// <param name="args">The HttpCwsRequestEventArgs arguments containing information about this request like the HTTP method</param>
        public void ReceivedRequestEvent(object sender, HttpCwsRequestEventArgs args)
        {
            ErrorLog.Notice($"{LogHeader} ReceivedRequestEvent running ...");
            
            try
            {
                if (args.Context.Request.RouteData == null)
                {
                    args.Context.Response.StatusCode = 200;
                    args.Context.Response.ContentType = "text/html";
                    switch (args.Context.Request.Path.ToUpper())
                    {
                        // not used, for demo/temp purposes
                        case "/WHATEVER":
                            break;
                        default:
                            args.Context.Response.StatusCode = 204;
                            args.Context.Response.Write(
                                JsonConvert.SerializeObject(
                                new Response
                                {
                                    Status = "Error",
                                    Message = this.GetApiHelp()
                                }, 
                                Formatting.Indented), 
                                true);
                            break;
                    }
                }
                else
                {
                    args.Context.Response.StatusCode = 200;
                    args.Context.Response.ContentType = "application/json";

                    // When we get a "GET" request
                    if (args.Context.Request.HttpMethod == "GET")
                    {
                        switch (args.Context.Request.RouteData.Route.Name.ToUpper())
                        {
                            case "CONFIG":
                                // Level 3
                                ErrorLog.Notice($"{LogHeader} GET Request CONFIG running ...");
                                
                                string JSONResponseString = FileControl.ReadFile($"{Directory.GetApplicationRootDirectory()}/User/config.json");
                                ErrorLog.Notice($"{LogHeader} returning interlock status {JSONResponseString}");
                                args.Context.Response.Write(JSONResponseString, true);
                                
                                break;
    
                            default:
                                break;
                        }
                    }

                    // When we get a "POST" request, we receive information from the frontend
                    if (args.Context.Request.HttpMethod == "POST")
                    {
                        string contents;

                        using (Crestron.SimplSharp.CrestronIO.Stream inputStream = args.Context.Request.InputStream)
                        {
                            using (StreamReader readStream = new StreamReader(inputStream, Encoding.UTF8))
                            {
                                contents = readStream.ReadToEnd();
                            }
                        }

                        switch (args.Context.Request.RouteData.Route.Name.ToUpper())
                        {
                            case "HOLAMUNDO":
                                ErrorLog.Notice($"{LogHeader} ReceivedRequestEvent HOLAMUNDO running ...");
                                break;

                            default:
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                args.Context.Response.ContentType = "application/json";
                args.Context.Response.StatusCode = 401;
                args.Context.Response.Write(
                    JsonConvert.SerializeObject(
                    new Response
                    {
                        Status = "Error",
                        Message = this.GetApiError(ex)
                    }, 
                    Formatting.Indented), 
                    true);
            }
        }

        /// <summary>
        /// Stop the CWS server
        /// </summary>
        public void StopServer()
        {
            ErrorLog.Notice($"{LogHeader} StopServer() running ...");
            
            try
            {
                this.cwsServerLock.Enter();
                ErrorLog.Notice(LogHeader + "Stopping CWS API Server");
                if (this.cwsServer != null)
                {
                    this.cwsServer.Unregister();
                    this.cwsServer = null;
                    ErrorLog.Notice(LogHeader + "Stopped CWS API Server");
                }
                else
                {
                    ErrorLog.Error(LogHeader + "CWS API Server was not running!");
                }
            }
            finally
            {
                this.cwsServerLock.Leave();
            }
        }

        /// <summary>
        /// Returns the API Help
        /// </summary>
        /// <returns>List of possible commands</returns>
        private List<string> GetApiHelp()
        {
            var apiCommands = new List<string>();

            apiCommands.Add("[GET] Here you can put information regarding GET routes");
            apiCommands.Add("[POST] Here you can put information regarding POST routes\n");
            return apiCommands;
        }

        /// <summary>
        /// Returns any exception that occured to the user
        /// </summary>
        /// <param name="e">Exception message / stacktrace</param>
        /// <returns>List with the exception to be written back to the user</returns>
        private List<string> GetApiError(Exception e)
        {
            var apiError = new List<string>();
            apiError.Add(string.Format("Message: {0} \n", e.Message));
            apiError.Add(string.Format("Trace: {0}", e.StackTrace));
            return apiError;
        }
    }
}