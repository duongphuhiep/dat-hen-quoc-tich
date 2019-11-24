using Serilog;
using System;
using ToolsPack.Config;
using Xunit;
using Xunit.Abstractions;

namespace nghe.tests
{
    public class ViberAlertSenderTests
    {
        public ViberAlertSenderTests(ITestOutputHelper output)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.Xunit(output, outputTemplate: "[{Timestamp:HH:mm:ss,fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")  //write log to xunit output
                .CreateLogger();
        }

        [Fact]
        public async void SendTest()
        {
            const string ViberAuthToken = "secret";
            var sender = new ViberAlertSender(ViberAuthToken);
            var receivers = new[] { "2hKqr/HncUsqJplpD6qJKw==" };
            var resp = await sender.Send("Hi from Unit test", receivers);
            Log.Information(resp);
        }
    }
}
