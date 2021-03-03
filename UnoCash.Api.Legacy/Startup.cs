using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: FunctionsStartup(typeof(UnoCash.Api.Startup))]

namespace UnoCash.Api
{
    class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder) => 
            builder.Services.AddLogging(lb => lb.SetMinimumLevel(LogLevel.Debug));
    }
}