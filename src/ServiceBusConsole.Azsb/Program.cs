using ServiceBusConsole;
using ServiceBusConsole.Azsb;
using ServiceBusConsole.BundledPlugins;

return await TerminalApp.RunAsync(args, AzureTransport.Broker, Bundled.Plugins);
