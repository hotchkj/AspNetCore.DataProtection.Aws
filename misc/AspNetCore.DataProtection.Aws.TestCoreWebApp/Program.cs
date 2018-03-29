// Copyright(c) 2018 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;

namespace AspNetCore.DataProtection.Aws.TestCoreWebApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .Build();
    }
}
