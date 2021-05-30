﻿using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using Certes.Cli.Settings;
using NLog;

namespace Certes.Cli.Commands
{
    internal class OrderFinalizeCommand : CommandBase, ICliCommand
    {
        private const string CommandText = "finalize";
        private const string OrderIdParam = "--order-id";
        private const string DnOption = "--dn";
        private const string PrivateKeyOption = "--private-key";
        private const string KeyAlgorithmOption = "--key-algorithm";
        private static readonly ILogger logger = LogManager.GetLogger(nameof(OrderFinalizeCommand));
        private readonly IEnvironmentVariables environment;

        public CommandGroup Group { get; } = CommandGroup.Order;

        public OrderFinalizeCommand(
            IUserSettings userSettings,
            AcmeContextFactory contextFactory,
            IFileUtil fileUtil,
            IEnvironmentVariables environment)
            : base(userSettings, contextFactory, fileUtil)
        {
            this.environment = environment;
        }

        public Command Define()
        {
            var cmd = new Command(CommandText, Strings.HelpCommandOrderFinalize)
            {
                new Option(new[]{ "--server", "-s" }, Strings.HelpServer),
                new Option(new[]{ "--key-path", "--key", "-k" }, Strings.HelpKey),
                new Option<string>(DnOption, Strings.HelpDn),
                new Option<string>(new [] { "--out-path", "--out" }, Strings.HelpKeyOut),
                new Option<string>(PrivateKeyOption, Strings.HelpPrivateKey),
                new Option<KeyAlgorithm>(KeyAlgorithmOption, () => KeyAlgorithm.ES256, Strings.HelpKeyAlgorithm),
                new Option<Uri>(OrderIdParam, Strings.HelpOrderId) { IsRequired = true },
            };

            cmd.Handler = CommandHandler.Create(async (
                Uri orderId,
                KeyAlgorithm keyAlgorithm,
                string privateKey,
                string outPath,
                string dn,
                Uri server,
                string keyPath,
                IConsole console) =>
            {
                var (serverUri, key) = await ReadAccountKey(server, keyPath, true, false);
                var providedKey = await ReadKey(privateKey, "CERTES_CERT_KEY", File, environment);
                var privKey = providedKey ?? KeyFactory.NewKey(keyAlgorithm);

                logger.Debug("Finalizing order from '{0}'.", serverUri);

                var acme = ContextFactory.Invoke(serverUri, key);
                var orderCtx = acme.Order(orderId);

                var csr = await orderCtx.CreateCsr(privKey);
                if (!string.IsNullOrWhiteSpace(dn))
                {
                    csr.AddName(dn);
                }

                var order = await orderCtx.Finalize(csr.Generate());

                // output private key only if it is generated and not being saved
                if (string.IsNullOrWhiteSpace(outPath) && providedKey == null)
                {
                    var output = new
                    {
                        location = orderCtx.Location,
                        privateKey = privKey.ToDer(),
                        resource = order,
                    };

                    console.WriteAsJson(output);
                }
                else
                {
                    if (providedKey == null)
                    {
                        await File.WriteAllText(outPath, privKey.ToPem());
                    }

                    var output = new
                    {
                        location = orderCtx.Location,
                        resource = order,
                    };

                    console.WriteAsJson(output);
                }
            });

            return cmd;
        }
    }
}
