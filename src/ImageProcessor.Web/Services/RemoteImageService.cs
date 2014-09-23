﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RemoteImageService.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The remote image service.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Web.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Security;
    using System.Threading.Tasks;

    using ImageProcessor.Web.Helpers;

    /// <summary>
    /// The remote image service.
    /// </summary>
    public class RemoteImageService : IImageService
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RemoteImageService"/> class.
        /// </summary>
        public RemoteImageService()
        {
            this.Settings = new Dictionary<string, string>
            {
                { "MaxBytes", "4194304" }, 
                { "Timeout", "30000" }
            };

            this.WhiteList = new Uri[] { };
        }

        /// <summary>
        /// Gets the key for the given implementation.
        /// <remarks>
        /// This value is used as a prefix for any image requests that should use this service.
        /// </remarks>
        /// </summary>
        public string Key
        {
            get
            {
                return "remote.axd";
            }
        }

        /// <summary>
        /// Gets a value indicating whether the image service requests files from
        /// the locally based file system.
        /// </summary>
        public bool IsFileLocalService
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets or sets any additional settings required by the service.
        /// </summary>
        public Dictionary<string, string> Settings { get; set; }

        /// <summary>
        /// Gets or sets the white list of <see cref="System.Uri"/>. 
        /// </summary>
        public Uri[] WhiteList { get; set; }

        /// <summary>
        /// Gets the image using the given identifier.
        /// </summary>
        /// <param name="id">
        /// The value identifying the image to fetch.
        /// </param>
        /// <returns>
        /// The <see cref="System.Byte"/> array containing the image data.
        /// </returns>
        public async Task<byte[]> GetImage(object id)
        {
            Uri uri = new Uri(id.ToString());

            // Check the url is from a whitelisted location.
            this.CheckSafeUrlLocation(uri);

            RemoteFile remoteFile = new RemoteFile(uri)
            {
                MaxDownloadSize = int.Parse(this.Settings["MaxBytes"]),
                TimeoutLength = int.Parse(this.Settings["Timeout"])
            };

            byte[] buffer = { };

            // Prevent response blocking.
            WebResponse webResponse = await remoteFile.GetWebResponseAsync().ConfigureAwait(false);

            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (WebResponse response = webResponse)
                {
                    using (Stream responseStream = response.GetResponseStream())
                    {
                        if (responseStream != null)
                        {
                            responseStream.CopyTo(memoryStream);

                            // Reset the position of the stream to ensure we're reading the correct part.
                            memoryStream.Position = 0;

                            buffer = memoryStream.ToArray();
                        }
                    }
                }
            }

            return buffer;
        }

        /// <summary>
        /// Returns a value indicating whether the current url is in a list of safe download locations.
        /// </summary>
        /// <param name="url">
        /// The <see cref="System.Uri"/> to check against.
        /// </param>
        private void CheckSafeUrlLocation(Uri url)
        {
            string upper = url.Host.ToUpperInvariant();

            // Check for root or sub domain.
            bool validUrl = false;
            foreach (Uri uri in this.WhiteList)
            {
                if (!uri.IsAbsoluteUri)
                {
                    Uri rebaseUri = new Uri("http://" + uri.ToString().TrimStart(new[] { '.', '/' }));
                    validUrl = upper.StartsWith(rebaseUri.Host.ToUpperInvariant()) || upper.EndsWith(rebaseUri.Host.ToUpperInvariant());
                }
                else
                {
                    validUrl = upper.StartsWith(uri.Host.ToUpperInvariant()) || upper.EndsWith(uri.Host.ToUpperInvariant());
                }

                if (validUrl)
                {
                    break;
                }
            }

            if (!validUrl)
            {
                throw new SecurityException("Application is not configured to allow remote file downloads from this domain.");
            }
        }
    }
}