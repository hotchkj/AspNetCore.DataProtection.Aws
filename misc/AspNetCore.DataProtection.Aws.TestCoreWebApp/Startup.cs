// Copyright(c) 2017 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using Amazon;
using Amazon.KeyManagementService;
using Amazon.S3;
using AspNetCore.DataProtection.Aws.Kms;
using AspNetCore.DataProtection.Aws.S3;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AspNetCore.DataProtection.Aws.TestCoreWebApp
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Expectation that local SDK has been configured correctly, whether via VS Tools or user config files
            services.AddSingleton<IAmazonS3>(new AmazonS3Client(RegionEndpoint.EUWest1));
            services.AddSingleton<IAmazonKeyManagementService>(new AmazonKeyManagementServiceClient(RegionEndpoint.EUWest1));
            services.AddDataProtection()
                    .ProtectKeysWithAwsKms(Configuration.GetSection("KmsProtection"))
                    .PersistKeysToAwsS3(Configuration.GetSection("S3Persistence"));
            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    "default",
                    "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
