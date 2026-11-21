using ServiceBusConsole;
using ServiceBusConsole.Azsb;
using ServiceBusConsole.BundledPlugins;

return await TerminalApp.RunAsync(args, new AppIdentity("azsb", Strings.Program_Description), AzureTransport.Broker, Bundled.Plugins);
