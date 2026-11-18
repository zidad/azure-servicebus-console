// The brokers azsb talks to, on free ports, for the integration tests (DistributedApplicationTestingBuilder).
// `EmulatorConfig` picks the emulator's entities; the repository's sample ones by default.
var builder = DistributedApplication.CreateBuilder(args);

var emulatorConfig = Path.GetFullPath(builder.Configuration["EmulatorConfig"]
    ?? Path.Combine(builder.AppHostDirectory, "..", "..", "emulator", "Config.json"));

builder.AddAzureServiceBus("servicebus")
    .RunAsEmulator(emulator => emulator.WithConfigurationFile(emulatorConfig));

builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin();

builder.Build().Run();
