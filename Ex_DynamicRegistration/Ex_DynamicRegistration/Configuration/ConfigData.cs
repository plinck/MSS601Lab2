//-----------------------------------------------------------------------
// <copyright file="ConfigData.cs" company="Crestron">
//     Copyright (c) Crestron Electronics. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crestron.SimplSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Ex_DynamicRegistration.Configuration
{
    /// <summary>
    /// Class used to deserialize JSON to a usable configuration
    /// Used in ControlSystem.cs to start the system config
    /// </summary>
    public class ConfigData
    {
        /// <summary>
        /// All the sources in this config
        /// </summary>
        public class SourcesItem
        {
            /// <summary>
            /// Gets or sets the ID of this source
            /// </summary>
            [JsonProperty("id")]
            public uint Id { get; set; }

            /// <summary>
            /// Gets or sets the type of this source. Can be "appletv" or " bluray" in this example
            /// </summary>
            [JsonProperty("type")]
            public string Type { get; set; }

            /// <summary>
            /// Gets or sets the label that is shown on the touchpanel interface
            /// </summary>
            [JsonProperty("label")]
            public string Label { get; set; }

            /// <summary>
            /// Gets or sets the icon number of this source. Can be found in Icons_State_ID.pdf
            /// </summary>
            [JsonProperty("icon")]
            public ushort Icon { get; set; }
        }

        /// <summary>
        /// All the destinations in this config
        /// </summary>
        public class DestinationsItem
        {
            /// <summary>
            /// Gets or sets the ID of this source
            /// </summary>
            [JsonProperty("id")]
            public uint Id { get; set; }

            /// <summary>
            /// Gets or sets the type of this source. Can be "tv" or " projector" in this example
            /// </summary>
            [JsonProperty("type")]
            public string Type { get; set; }

            /// <summary>
            /// Gets or sets the label that is shown on the touchpanel interface
            /// </summary>
            [JsonProperty("label")]
            public string Label { get; set; }

            /// <summary>
            /// Gets or sets the icon number of this source. Can be found in Icons_State_ID.pdf
            /// </summary>
            [JsonProperty("icon")]
            public ushort Icon { get; set; }
        }

        /// <summary>
        /// All the touchpanels in this config
        /// </summary>
        public class TouchpanelsItem
        {
            /// <summary>
            /// Gets or sets the ID of this source
            /// </summary>
            [JsonProperty("id")]
            public uint Id { get; set; }

            /// <summary>
            /// Gets or sets the type of this source. Can be "tsw760" or " xpanel" in this example
            /// </summary>
            [JsonProperty("type")]
            public string Type { get; set; }

            /// <summary>
            /// Gets or sets the label that is shown on the touchpanel interface
            /// </summary>
            [JsonProperty("label")]
            public string Label { get; set; }
        }

        /// <summary>
        /// All the NVX's in this config
        /// </summary>
        public class NvxItems
        {
            /// <summary>
            /// Gets or sets the type of this NVX unit. Can be "350, 350C, 351 or 351C"
            /// </summary>
            [JsonProperty("type")]
            public string Type { get; set; }

            /// <summary>
            /// Gets or sets the IPID of this NVX
            /// </summary>
            [JsonProperty("id")]
            public uint Id { get; set; }

            /// <summary>
            /// Gets or sets the endpoint name as well as the label in the IPTable
            /// </summary>
            [JsonProperty("name")]
            public string Name { get; set; }

            /// <summary>
            /// Gets or sets the multicast address of this NVX
            /// </summary>
            [JsonProperty("multicast")]
            public string Multicast { get; set; }
        }

        /// <summary>
        /// Configuration object
        /// </summary>
        public class Configuration
        {
            /// <summary>
            /// Gets or sets the List of sources
            /// </summary>
            [JsonProperty("sources")]
            public SourcesItem[] Sources { get; set; }

            /// <summary>
            /// Gets or sets the List of destinations
            /// </summary>
            [JsonProperty("destinations")]
            public DestinationsItem[] Destinations { get; set; }

            /// <summary>
            /// Gets or sets the List of touchpanels
            /// </summary>
            [JsonProperty("touchpanels")]
            public TouchpanelsItem[] Touchpanels { get; set; }

            /// <summary>
            /// Gets or sets the time the config file was last updated
            /// </summary>
            [JsonProperty("lastupdate")]
            public string LastUpdate { get; set; }
        }
    }
}