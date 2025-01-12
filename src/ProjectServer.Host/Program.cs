using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using ProjectServer.Host.Services;

namespace ProjectServer.Host
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
            var builder = WebApplication.CreateBuilder(args);

            // TODO: Parse command-line arguments, and configure Kestrel to listen on either a Unix domain socket or a named pipe (whose name was passed on the command line).

            builder.Services.AddGrpc();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            app.MapGrpcService<ProjectServerService>();
            app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

            app.Run();
        }
    }
}
