﻿using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Certes.Cli.Settings;
using NLog;

namespace Certes.Cli.Commands
{
    internal class CertificatePfxCommand : CertificateCommandBase, ICliCommand
    {
        private const string CommandText = "pfx";
        private const string FriendlyNameOption = "--friendly-name";
        private const string PasswordParam = "--password";
        private const string PrivateKeyOption = "--private-key";
        private const string IssuerOption = "--issuer";

        private readonly ILogger logger = LogManager.GetLogger(nameof(CertificatePfxCommand));
        private readonly IEnvironmentVariables environment;

        public CertificatePfxCommand(
            IUserSettings userSettings,
            AcmeContextFactory contextFactory, 
            IFileUtil fileUtil,
            IEnvironmentVariables environment)
            : base(userSettings, contextFactory, fileUtil)
        {
            this.environment = environment;
        }

        public override Command Define()
        {
            var cmd = new Command(CommandText, Strings.HelpCommandCertificatePem)
            {
                new Option(new[]{ "--server", "-s" }, Strings.HelpServer),
                new Option(new[]{ "--key-path", "--key", "-k" }, Strings.HelpKey),
                new Option(new [] { "--out-path", "--out" }, Strings.HelpCertificateOut),
                new Option(PrivateKeyOption, Strings.HelpPrivateKey),
                new Option(FriendlyNameOption, Strings.HelpFriendlyName),
                new Option(IssuerOption, Strings.HelpCertificateIssuer),
                new Option(PreferredChainOption, Strings.HelpPreferredChain),
                new Option<Uri>(OrderIdOption, Strings.HelpOrderId) { IsRequired = true },
                new Option<Uri>(PasswordParam, Strings.HelpPfxPassword) { IsRequired = true },
            };

            cmd.Handler = CommandHandler.Create(async (
                Uri orderId,
                string privateKey,
                string friendlyName,
                string issuer,
                string password,
                string preferredChain,
                string outPath,
                Uri server,
                string keyPath,
                IConsole console) =>
            {
                var (location, cert) = await DownloadCertificate(orderId, preferredChain, server, keyPath);

                var privKey = await ReadKey(privateKey, "CERTES_CERT_KEY", File, environment);
                if (privKey == null)
                {
                    throw new CertesCliException(Strings.ErrorNoPrivateKey);
                }

                var pfxName = string.Format(CultureInfo.InvariantCulture, "[certes] {0:yyyyMMddhhmmss}", DateTime.UtcNow);
                if (!string.IsNullOrWhiteSpace(friendlyName))
                {
                    pfxName = string.Concat(friendlyName, " ", pfxName);
                }

                var pfxBuilder = cert.ToPfx(privKey);
                if (!string.IsNullOrWhiteSpace(issuer))
                {
                    var issuerPem = await File.ReadAllText(issuer);
                    pfxBuilder.AddIssuers(Encoding.UTF8.GetBytes(issuerPem));
                }

                var pfx = pfxBuilder.Build(pfxName, password);

                if (string.IsNullOrWhiteSpace(outPath))
                {
                    var output = new
                    {
                        location,
                        pfx,
                    };

                    console.WriteAsJson(output);
                }
                else
                {
                    logger.Debug("Saving certificate to '{0}'.", outPath);
                    await File.WriteAllBytes(outPath, pfx);

                    var output = new
                    {
                        location,
                    };

                    console.WriteAsJson(output);
                }
            });

            return cmd;
        }
    }
}
