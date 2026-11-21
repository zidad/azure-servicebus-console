using ServiceBusConsole;
using ServiceBusConsole.BundledPlugins;
using ServiceBusConsole.Rmqsb;

return await TerminalApp.RunAsync(args, new AppIdentity("rmqsb", Strings.Program_Description), RabbitMqTransport.Broker, Bundled.Plugins);
