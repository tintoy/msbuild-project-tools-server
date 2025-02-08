using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace MSBuildProjectTools.ProjectServer.Host
{
    /// <summary>
    ///     Contains the main program entry-point for the project server host.
    /// </summary>
    public class Program
    {
        /// <summary>
        ///     The main program entry-point for the project server host.
        /// </summary>
        /// <param name="args">
        ///     Command-line arguments.
        /// </param>
        public static void Main(string[] args)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

            // TODO: Parse command-line arguments, and configure Kestrel to listen on either a Unix domain socket or a named pipe (whose name was passed on the command line).

            if (OperatingSystem.IsWindows())
            {
                /*
                builder.WebHost.UseKestrel(kestrel =>
                {
                    kestrel.ListenNamedPipe("my-pipe-name");
                });
                builder.WebHost.UseNamedPipes(namedPipes =>
                {
                    namedPipes.CurrentUserOnly = true;
                });
                */
            }
            else if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                //builder.WebHost.UseKestrel(kestrel =>
                //{
                //    kestrel.ListenUnixSocket("/my/socket-path");
                //});
            }

            builder.Services.AddMvc();

            builder.Services.AddProjectServerEngine();

            WebApplication app = builder.Build();

            app.MapControllers();
            app.Run();
        }
    }
}
