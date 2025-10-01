using System;

namespace Scalerize.InfiniteGpu.Desktop.Constants
{
    /// <summary>
    /// Centralized URL constants for the desktop application.
    /// </summary>
    public static class UrlConstants
    {
#if DEBUG
        /// <summary>
        /// Base URI for the backend API in development mode.
        /// </summary>
        public static readonly Uri BackendBaseUri = new("http://localhost:5116/");

        /// <summary>
        /// URI for the frontend application in development mode.
        /// </summary>
        public static readonly Uri FrontendUri = new("http://localhost:5173");
#else
        /// <summary>
        /// Base URI for the backend API in production mode.
        /// </summary>
        public static readonly Uri BackendBaseUri = new("https://infinite-gpu-backend-bvh8a7c3fdgxd7c5.canadacentral-01.azurewebsites.net/");

        /// <summary>
        /// URI for the frontend application in production mode.
        /// </summary>
        public static readonly Uri FrontendUri = new("https://salmon-island-06e155e0f.2.azurestaticapps.net");
#endif
    }
}