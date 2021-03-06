﻿using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.Documents.Client.TransientFaultHandling;
using Microsoft.DataTransfer.Basics;
using Microsoft.DataTransfer.DocumentDb.Client;
using Microsoft.DataTransfer.Extensibility;
using Microsoft.Practices.EnterpriseLibrary.TransientFaultHandling;
using System;
using System.Globalization;
using System.Reflection;

namespace Microsoft.DataTransfer.DocumentDb.Shared
{
    abstract class DocumentDbAdapterFactoryBase
    {
        protected static void ValidateBaseConfiguration(IDocumentDbAdapterConfiguration configuration)
        {
            Guard.NotNull("configuration", configuration);

            if (String.IsNullOrEmpty(configuration.ConnectionString))
                throw Errors.ConnectionStringMissing();

            if (String.IsNullOrEmpty(configuration.Collection))
                throw Errors.CollectionNameMissing();
        }

        protected static DocumentDbClient CreateClient(IDocumentDbAdapterConfiguration configuration, IDataTransferContext context)
        {
            Guard.NotNull("configuration", configuration);

            var connectionSettings = ParseConnectionString(configuration.ConnectionString);
            return new DocumentDbClient(
                CreateRawClient(connectionSettings, configuration.ConnectionMode, context)
                    .AsReliable(new FixedInterval(
                        null,
                        GetValueOrDefault(configuration.Retries, Defaults.Current.NumberOfRetries, Errors.InvalidNumberOfRetries),
                        GetValueOrDefault(configuration.RetryInterval, Defaults.Current.RetryInterval, Errors.InvalidRetryInterval),
                        false)),
                connectionSettings.Database
            );
        }

        private static DocumentClient CreateRawClient(IDocumentDbConnectionSettings connectionSettings, DocumentDbConnectionMode? connectionMode, IDataTransferContext context)
        {
            Guard.NotNull("connectionSettings", connectionSettings);

            return new DocumentClient(
                new Uri(connectionSettings.AccountEndpoint),
                connectionSettings.AccountKey,
                CreateConnectionPolicy(connectionMode, context)
            );
        }

        private static ConnectionPolicy CreateConnectionPolicy(DocumentDbConnectionMode? connectionMode, IDataTransferContext context)
        {
            return DocumentDbClientHelper.ApplyConnectionMode(new ConnectionPolicy
                {
                    UserAgentSuffix = String.Format(CultureInfo.InvariantCulture, Resources.CustomUserAgentSuffixFormat,
                        Assembly.GetExecutingAssembly().GetName().Version, context.SourceName, context.SinkName)
                }, connectionMode);
        }

        private static IDocumentDbConnectionSettings ParseConnectionString(string connectionString)
        {
            var connectionSettings = DocumentDbConnectionStringBuilder.Parse(connectionString);

            if (String.IsNullOrEmpty(connectionSettings.AccountEndpoint))
                throw Errors.AccountEndpointMissing();

            if (String.IsNullOrEmpty(connectionSettings.AccountKey))
                throw Errors.AccountKeyMissing();

            if (String.IsNullOrEmpty(connectionSettings.Database))
                throw Errors.DatabaseNameMissing();

            return connectionSettings;
        }

        protected static int GetValueOrDefault(int? value, int defaultValue, Func<Exception> errorProvider)
        {
            return GetValueOrDefault(value, defaultValue, v => v > 0, errorProvider);
        }

        protected static TimeSpan GetValueOrDefault(TimeSpan? value, TimeSpan defaultValue, Func<Exception> errorProvider)
        {
            return GetValueOrDefault(value, defaultValue, v => v >= TimeSpan.Zero, errorProvider);
        }

        protected static T GetValueOrDefault<T>(T? value, T defaultValue)
            where T : struct
        {
            return value.HasValue ? value.Value : defaultValue;
        }

        protected static T GetValueOrDefault<T>(T? value, T defaultValue, Func<T, bool> isValidValue, Func<Exception> errorProvider)
            where T : struct
        {
            if (!value.HasValue)
                return defaultValue;

            if (!isValidValue(value.Value))
                throw errorProvider();

            return value.Value;
        }
    }
}
