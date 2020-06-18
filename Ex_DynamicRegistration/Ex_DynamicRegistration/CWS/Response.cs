﻿//-----------------------------------------------------------------------
// <copyright file="Response.cs" company="Crestron">
//     Copyright (c) Crestron Electronics. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Newtonsoft.Json;

 namespace Ex_DynamicRegistration
{
    /// <summary>
    /// Class used to serialize object to a JSON string
    /// Used in Controller.cs for writing a response to the user
    /// </summary>
    public class Response
    {
        /// <summary>
        /// Gets or sets the status of the response. Can be used to set as "error" / "warning" / "ok" / etc
        /// </summary>
        [JsonProperty("status")]
        public string Status { get; set; }

        /// <summary>
        /// Gets or sets an additional message in the response object
        /// </summary>
        [JsonProperty("message")]
        public List<string> Message { get; set; }
    }
}