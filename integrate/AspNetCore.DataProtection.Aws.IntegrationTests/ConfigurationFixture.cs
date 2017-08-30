// Copyright(c) 2017 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using System.IO;
using Microsoft.Extensions.Configuration;

namespace AspNetCore.DataProtection.Aws.IntegrationTests
{
    public class ConfigurationFixture
    {
        public ConfigurationFixture()
        {
            var builder = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory())
                                                    .AddJsonFile("config.json");

            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }
    }
}
