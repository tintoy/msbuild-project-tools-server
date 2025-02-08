using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;

namespace ProjectServer.Host.Controllers
{
    [Route("api/v1/configuration")]
    public class ConfigurationController
        : Controller
    {
        readonly ILogger _logger;

        public ConfigurationController(ILogger<ConfigurationController> logger)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            _logger = logger;
        }
    }
}
